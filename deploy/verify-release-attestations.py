#!/usr/bin/env python3
"""Verify public image signatures and the release-specific signed policy.

Only gh's successful cryptographic verification output is inspected. Raw bundle
JSON is never accepted as a verification receipt. No production access occurs.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import tarfile

REPO = "Fullworth-prod/FullWorth"
REPO_ID = "1344061157"
OWNER_ID = "333422185"
WORKFLOW = f"https://github.com/{REPO}/.github/workflows/ci.yml@refs/heads/master"
PROVENANCE = "https://slsa.dev/provenance/v1"
SPDX = "https://spdx.dev/Document/v2.3"
IMAGES = ("billwatch-api", "billwatch-parser-worker", "billwatch-web")


def require(condition, message):
    if not condition:
        raise ValueError(message)


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, "duplicate JSON key")
        result[key] = value
    return result


def decode_json(data):
    return json.loads(data.decode("utf-8"), object_pairs_hook=unique_object,
                      parse_constant=lambda _: require(False, "non-finite JSON number"))


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def validate_receipt(receipt, predicate_type, digest, sbom, release, run_id, attempt):
    require(isinstance(receipt, list) and receipt, "empty verification receipt")
    invocation = f"https://github.com/{REPO}/actions/runs/{run_id}/attempts/{attempt}"
    expected = {
        "issuer": "https://token.actions.githubusercontent.com",
        "sourceRepositoryURI": f"https://github.com/{REPO}",
        "sourceRepositoryIdentifier": REPO_ID,
        "sourceRepositoryOwnerIdentifier": OWNER_ID,
        "sourceRepositoryDigest": release,
        "sourceRepositoryRef": "refs/heads/master",
        "buildSignerURI": WORKFLOW,
        "buildSignerDigest": release,
        "runnerEnvironment": "github-hosted",
        "buildTrigger": "push",
        "runInvocationURI": invocation,
    }
    matched = False
    for entry in receipt:
        result = entry["verificationResult"]
        cert = result["signature"]["certificate"]
        statement = result["statement"]
        # A matching attestation must independently satisfy every policy field.
        # Never combine one attestation's certificate with another's predicate.
        if not all(cert.get(key) == value for key, value in expected.items()):
            continue
        if not result.get("verifiedTimestamps"):
            continue
        if statement.get("_type") != "https://in-toto.io/Statement/v1":
            continue
        if statement.get("predicateType") != predicate_type:
            continue
        if not any(subject.get("digest", {}).get("sha256") == digest
                   for subject in statement.get("subject", [])):
            continue
        # Python object equality treats False == 0; JSON identity must retain
        # those distinct types. Canonical serialization also ignores key order.
        if predicate_type == SPDX and json.dumps(statement.get("predicate"), sort_keys=True) != json.dumps(sbom, sort_keys=True):
            continue
        matched = True
    require(matched, "no verified attestation satisfies the complete release policy")


def verify_image_revision(path, release, image):
    # Read only the manifest-referenced image config; never extract archive paths.
    with tarfile.open(path, "r:") as archive:
        manifests = archive.getmembers()
        require(sum(member.name == "manifest.json" for member in manifests) == 1,
                "ambiguous Docker manifest")
        member = archive.getmember("manifest.json")
        require(member.isfile() and member.size <= 1024 * 1024, "unsafe Docker manifest")
        manifest = decode_json(archive.extractfile(member).read())
        require(isinstance(manifest, list) and len(manifest) == 1, "unexpected image count")
        require(manifest[0].get("RepoTags") == [f"{image}:{release}"], "wrong image tag")
        config_name = manifest[0]["Config"]
        require(sum(item.name == config_name for item in manifests) == 1,
                "ambiguous image config")
        config = archive.getmember(config_name)
        require(config.isfile() and config.size <= 1024 * 1024, "unsafe image config")
        data = decode_json(archive.extractfile(config).read())
        require(data["config"]["Labels"]["org.opencontainers.image.revision"] == release,
                "wrong embedded image revision")


def verify_directory(directory, release, run_id, attempt, runner=subprocess.run):
    require(re.fullmatch(r"[0-9a-f]{40}", release), "invalid release SHA")
    require(re.fullmatch(r"[1-9][0-9]*", run_id), "invalid run ID")
    require(re.fullmatch(r"[1-9][0-9]*", attempt), "invalid run attempt")
    require(directory.is_dir() and not directory.is_symlink(), "unsafe artifact directory")
    expected = {f"{image}{suffix}" for image in IMAGES for suffix in (".tar", ".spdx.json")}
    require({path.name for path in directory.iterdir()} == expected, "unexpected artifact set")
    for path in directory.iterdir():
        require(path.is_file() and not path.is_symlink(), "unsafe artifact member")
    for image in IMAGES:
        path = directory / f"{image}.tar"
        sbom = decode_json((directory / f"{image}.spdx.json").read_bytes())
        require(isinstance(sbom, dict) and sbom.get("spdxVersion") == "SPDX-2.3",
                "unsupported SBOM version")
        verify_image_revision(path, release, image)
        digest = sha256(path)
        for predicate in (PROVENANCE, SPDX):
            command = [
                "gh", "attestation", "verify", str(path), "--repo", REPO,
                "--predicate-type", predicate, "--source-digest", release,
                "--source-ref", "refs/heads/master",
                "--signer-workflow", f"{REPO}/.github/workflows/ci.yml",
                "--signer-digest", release, "--deny-self-hosted-runners", "--format", "json",
            ]
            result = runner(command, check=True, stdout=subprocess.PIPE)
            validate_receipt(decode_json(result.stdout), predicate, digest, sbom,
                             release, run_id, attempt)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("release")
    parser.add_argument("run_id")
    parser.add_argument("attempt")
    args = parser.parse_args()
    try:
        verify_directory(args.directory, args.release, args.run_id, args.attempt)
    except (ValueError, KeyError, TypeError, OSError, tarfile.TarError,
            subprocess.CalledProcessError) as error:
        # Do not print untrusted JSON fields or raw attestation payloads.
        print(f"Release attestation verification refused ({type(error).__name__}).", file=sys.stderr)
        return 1
    print("All six image signatures, signed SBOM content and exact release policy verified.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
