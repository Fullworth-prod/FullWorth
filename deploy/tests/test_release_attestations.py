"""Policy/transport tests use synthetic receipts, not real signature evidence."""
import copy
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import tarfile
import tempfile
import unittest

spec = importlib.util.spec_from_file_location(
    "release_attestations", Path(__file__).resolve().parents[1] / "verify-release-attestations.py")
verifier = importlib.util.module_from_spec(spec)
spec.loader.exec_module(verifier)
SHA = "a" * 40
RUN = "123"
ATTEMPT = "2"


def receipt(predicate, digest, sbom):
    return [{"verificationResult": {
        "signature": {"certificate": {
            "issuer": "https://token.actions.githubusercontent.com",
            "sourceRepositoryURI": "https://github.com/Fullworth-prod/FullWorth",
            "sourceRepositoryIdentifier": "1344061157",
            "sourceRepositoryOwnerIdentifier": "333422185",
            "sourceRepositoryDigest": SHA, "sourceRepositoryRef": "refs/heads/master",
            "buildSignerURI": verifier.WORKFLOW, "buildSignerDigest": SHA,
            "runnerEnvironment": "github-hosted", "buildTrigger": "push",
            "runInvocationURI": "https://github.com/Fullworth-prod/FullWorth/actions/runs/123/attempts/2",
        }},
        "verifiedTimestamps": [{"type": "tlog"}],
        "statement": {"_type": "https://in-toto.io/Statement/v1",
                      "predicateType": predicate,
                      "subject": [{"digest": {"sha256": digest}}],
                      "predicate": sbom if predicate == verifier.SPDX else {}},
    }}]


class ReleaseAttestationsTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.directory = Path(self.temp.name)
        self.sbom = {"spdxVersion": "SPDX-2.3", "name": "rélease"}
        for image in verifier.IMAGES:
            self.make_image(image)
            (self.directory / f"{image}.spdx.json").write_text(
                json.dumps(self.sbom, ensure_ascii=False), encoding="utf-8")
        self.commands = []

    def make_image(self, image, revision=SHA):
        with tarfile.open(self.directory / f"{image}.tar", "w") as archive:
            for name, value in [
                ("manifest.json", [{"Config": "config.json", "RepoTags": [f"{image}:{SHA}"]}]),
                ("config.json", {"config": {"Labels": {"org.opencontainers.image.revision": revision}}}),
            ]:
                data = json.dumps(value).encode("utf-8")
                info = tarfile.TarInfo(name)
                info.size = len(data)
                archive.addfile(info, io.BytesIO(data))

    def run_gh(self, command, **kwargs):
        self.commands.append(command)
        self.assertTrue(kwargs["check"])
        for flag, value in [
            ("--repo", verifier.REPO), ("--source-digest", SHA),
            ("--source-ref", "refs/heads/master"), ("--signer-digest", SHA),
            ("--signer-workflow", f"{verifier.REPO}/.github/workflows/ci.yml"),
            ("--format", "json"),
        ]:
            self.assertEqual(command[command.index(flag) + 1], value)
        self.assertIn("--deny-self-hosted-runners", command)
        self.assertNotIn("--bundle", command)
        predicate = command[command.index("--predicate-type") + 1]
        data = receipt(predicate, verifier.sha256(Path(command[3])), self.sbom)
        return subprocess.CompletedProcess(command, 0, json.dumps(data).encode("utf-8"))

    def verify(self, runner=None):
        verifier.verify_directory(self.directory, SHA, RUN, ATTEMPT, runner or self.run_gh)

    def test_six_verified_transports_and_utf8_signed_content(self):
        self.verify()
        self.assertEqual(len(self.commands), 6)
        self.assertEqual(sum(verifier.SPDX in cmd for cmd in self.commands), 3)

    def test_signature_failure_is_terminal_without_replay(self):
        def failure(command, **kwargs):
            self.commands.append(command)
            raise subprocess.CalledProcessError(1, command)
        with self.assertRaises(subprocess.CalledProcessError):
            self.verify(failure)
        self.assertEqual(len(self.commands), 1)

    def test_certificate_policy_fields_each_fail_closed(self):
        original = receipt(verifier.SPDX, "digest", self.sbom)
        cert = original[0]["verificationResult"]["signature"]["certificate"]
        for field in cert:
            for replacement in ("wrong", None):
                with self.subTest(field=field, replacement=replacement):
                    changed = copy.deepcopy(original)
                    changed[0]["verificationResult"]["signature"]["certificate"][field] = replacement
                    with self.assertRaises(ValueError):
                        verifier.validate_receipt(changed, verifier.SPDX, "digest", self.sbom,
                                                  SHA, RUN, ATTEMPT)

    def test_wrong_predicate_subject_timestamp_and_sbom_are_rejected(self):
        for field, replacement in [
            ("predicateType", "https://spdx.dev/Document"),
            ("subject", [{"digest": {"sha256": "wrong"}}]),
            ("predicate", {"spdxVersion": "SPDX-2.3", "name": "altered"}),
            ("_type", "untrusted"),
        ]:
            with self.subTest(field=field):
                data = receipt(verifier.SPDX, "digest", self.sbom)
                data[0]["verificationResult"]["statement"][field] = replacement
                with self.assertRaises(ValueError):
                    verifier.validate_receipt(data, verifier.SPDX, "digest", self.sbom, SHA, RUN, ATTEMPT)
        data = receipt(verifier.SPDX, "digest", self.sbom)
        data[0]["verificationResult"]["verifiedTimestamps"] = []
        with self.assertRaises(ValueError):
            verifier.validate_receipt(data, verifier.SPDX, "digest", self.sbom, SHA, RUN, ATTEMPT)

    def test_cannot_combine_certificate_and_content_across_receipts(self):
        data = receipt(verifier.SPDX, "digest", self.sbom)
        other = copy.deepcopy(data[0])
        data[0]["verificationResult"]["statement"]["predicate"] = {}
        other["verificationResult"]["signature"]["certificate"]["sourceRepositoryDigest"] = "b" * 40
        data.append(other)
        with self.assertRaises(ValueError):
            verifier.validate_receipt(data, verifier.SPDX, "digest", self.sbom, SHA, RUN, ATTEMPT)

    def test_modified_supplied_sbom_is_rejected(self):
        (self.directory / "billwatch-api.spdx.json").write_text(
            '{"spdxVersion":"SPDX-2.3","name":"altered"}', encoding="utf-8")
        with self.assertRaises(ValueError):
            self.verify()

    def test_sbom_boolean_cannot_be_replaced_with_numeric_value(self):
        signed = {"spdxVersion": "SPDX-2.3", "filesAnalyzed": False}
        supplied = {"spdxVersion": "SPDX-2.3", "filesAnalyzed": 0}
        with self.assertRaises(ValueError):
            verifier.validate_receipt(receipt(verifier.SPDX, "digest", signed),
                                      verifier.SPDX, "digest", supplied, SHA, RUN, ATTEMPT)

    def test_distinct_decimal_content_cannot_collapse_through_float_parsing(self):
        for signed_number, supplied_number in [
            ("1.00000000000000001", "1.0"), ("1e999", "1e1000"),
        ]:
            with self.subTest(signed=signed_number, supplied=supplied_number):
                signed = verifier.decode_json(('{"n":' + signed_number + '}').encode())
                supplied = verifier.decode_json(('{"n":' + supplied_number + '}').encode())
                with self.assertRaises(ValueError):
                    verifier.validate_receipt(receipt(verifier.SPDX, "digest", signed),
                                              verifier.SPDX, "digest", supplied, SHA, RUN, ATTEMPT)
        self.assertEqual(verifier.json_identity(verifier.decode_json(b'{"n":1.0}')),
                         verifier.json_identity(verifier.decode_json(b'{"n":1}')))

    def test_wrong_image_revision_refused_before_signature_transport(self):
        self.make_image("billwatch-api", "b" * 40)
        with self.assertRaises(ValueError):
            self.verify()
        self.assertEqual(self.commands, [])

    def test_extra_and_missing_artifacts_refused(self):
        extra = self.directory / "unexpected"
        extra.write_text("x")
        with self.assertRaises(ValueError):
            self.verify()
        extra.unlink()
        (self.directory / "billwatch-web.tar").unlink()
        with self.assertRaises(ValueError):
            self.verify()

    def test_json_is_unambiguous_utf8(self):
        for data in (b'{"x":1,"x":2}', b'{"x":NaN}', b'{"x":"\xff"}'):
            with self.subTest(data=data), self.assertRaises((ValueError, UnicodeError)):
                verifier.decode_json(data)

    def test_empty_receipt_and_invalid_release_selection_refused(self):
        with self.assertRaises(ValueError):
            verifier.validate_receipt([], verifier.SPDX, "digest", self.sbom, SHA, RUN, ATTEMPT)
        for sha, run, attempt in [(SHA.upper(), RUN, ATTEMPT), (SHA, "0", ATTEMPT), (SHA, RUN, "../2")]:
            with self.subTest(sha=sha, run=run, attempt=attempt), self.assertRaises(ValueError):
                verifier.verify_directory(self.directory, sha, run, attempt, self.run_gh)


if __name__ == "__main__":
    unittest.main()
