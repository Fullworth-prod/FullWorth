#!/usr/bin/env python3
"""Validate a private, one-use recovery bundle without printing its contents."""
import configparser
import datetime as dt
import json
import os
from pathlib import Path
import re
import secrets
import stat
import sys

FIELDS = {"version", "createdAtUtc", "repository", "resticPassword",
          "accessKeyId", "secretAccessKey", "snapshotId", "sourceRelease",
          "protectedCopyVerified", "canaryKey"}
REPOSITORY = re.compile(r"s3:https://[a-z0-9]+\.r2\.cloudflarestorage\.com/"
                        r"[a-z0-9][a-z0-9-]*/immutable-recovery/[A-Za-z0-9_-]+/")
HEX40 = re.compile(r"[0-9a-f]{40}")
HEX64 = re.compile(r"[0-9a-f]{64}")

def validate(bundle, now):
    if not isinstance(bundle, dict) or set(bundle) != FIELDS:
        raise ValueError("unexpected recovery bundle fields")
    if type(bundle["version"]) is not int or bundle["version"] != 1:
        raise ValueError("unsupported recovery bundle version")
    if bundle["protectedCopyVerified"] is not True:
        raise ValueError("protected-copy verification is required")
    for key in FIELDS - {"version", "protectedCopyVerified"}:
        if not isinstance(bundle[key], str) or not bundle[key] or len(bundle[key]) > 1024:
            raise ValueError("invalid recovery field")
    created = dt.datetime.fromisoformat(bundle["createdAtUtc"])
    if created.tzinfo is None or not 0 <= (now-created).total_seconds() <= 86400:
        raise ValueError("recovery bundle is stale or future-dated")
    if not REPOSITORY.fullmatch(bundle["repository"]):
        raise ValueError("only frozen R2 recovery copies are accepted")
    if not HEX64.fullmatch(bundle["snapshotId"]) or not HEX40.fullmatch(bundle["sourceRelease"]):
        raise ValueError("invalid source identity")
    if not re.fullmatch(r"[A-Za-z0-9_+./=:@!%-]{24,256}", bundle["resticPassword"]):
        raise ValueError("unsupported protected password encoding")
    if not re.fullmatch(r"[A-Za-z0-9]{16,128}", bundle["accessKeyId"]):
        raise ValueError("invalid recovery access key")
    if not re.fullmatch(r"[A-Za-z0-9/+_=.-]{32,256}", bundle["secretAccessKey"]):
        raise ValueError("invalid recovery secret key")
    if not re.fullmatch(r"recovery-readonly-probe/[0-9a-f]{24}", bundle["canaryKey"]):
        raise ValueError("invalid synthetic read-only canary")
    return bundle

def private_read(path):
    info = path.lstat()
    if not stat.S_ISREG(info.st_mode) or stat.S_IMODE(info.st_mode) != 0o600 or info.st_uid != os.getuid():
        raise ValueError("bundle must be an owned mode-600 regular file")
    if info.st_size > 16384:
        raise ValueError("bundle exceeds safety bound")
    return json.loads(path.read_text())

def prepare(bundle_path, destination, release, now=None):
    if not HEX40.fullmatch(release):
        raise ValueError("invalid runner release")
    bundle = validate(private_read(bundle_path), now or dt.datetime.now(dt.timezone.utc))
    destination.mkdir(mode=0o700, exist_ok=False)
    credentials = configparser.ConfigParser()
    credentials["default"] = {"aws_access_key_id":bundle["accessKeyId"],
                              "aws_secret_access_key":bundle["secretAccessKey"]}
    with (destination/"credentials").open("x") as stream:
        credentials.write(stream)
    (destination/"recovery.env").write_text(
        "BILLWATCH_RELEASE_ID="+release+"\n"
        "BILLWATCH_RECOVERY_DRILL_ALLOW=true\n"
        "BILLWATCH_RECOVERY_IMMUTABLE_COPY=true\n"
        "BILLWATCH_RECOVERY_AWS_CREDENTIALS_FILE="+str(destination/"credentials")+"\n"
        "RESTIC_REPOSITORY="+bundle["repository"]+"\n"
        "RESTIC_PASSWORD="+bundle["resticPassword"]+"\n"
        "BILLWATCH_DATABASE_PASSWORD="+secrets.token_hex(32)+"\n")
    for path in destination.iterdir():
        path.chmod(0o600)
    return bundle

def verify_readonly(bundle):
    # The canary is OUTSIDE the locked prefix: a 409 lock denial does not
    # establish read-only permissions. Only explicit AccessDenied is accepted.
    import boto3
    from botocore.config import Config
    from botocore.exceptions import ClientError
    from urllib.parse import urlsplit
    url = urlsplit(bundle["repository"][3:])
    client = boto3.client("s3", endpoint_url="https://"+url.netloc,
        region_name="auto", aws_access_key_id=bundle["accessKeyId"],
        aws_secret_access_key=bundle["secretAccessKey"],
        config=Config(signature_version="s3v4",
            request_checksum_calculation="when_required",
            response_checksum_validation="when_required",
            s3={"addressing_style":"path"}))
    bucket = url.path.strip("/").split("/")[0]
    body = client.get_object(Bucket=bucket,Key=bundle["canaryKey"])["Body"].read()
    if body != b"FullWorth synthetic read-only recovery permission probe.":
        raise ValueError("unexpected synthetic permission probe")
    for operation in (
        lambda:client.put_object(Bucket=bucket,Key=bundle["canaryKey"],Body=body),
        lambda:client.delete_object(Bucket=bucket,Key=bundle["canaryKey"])):
        try:
            operation()
        except ClientError as error:
            if error.response["ResponseMetadata"]["HTTPStatusCode"] == 403 and error.response["Error"]["Code"] == "AccessDenied":
                continue
            raise ValueError("read-only permission denial was not established") from None
        raise ValueError("recovery credential unexpectedly permits writes")

def main():
    os.umask(0o077)
    if len(sys.argv) != 4:
        raise ValueError("expected bundle, private destination, and runner release")
    bundle = prepare(Path(sys.argv[1]), Path(sys.argv[2]), sys.argv[3])
    verify_readonly(bundle)
    # Metadata only; never output endpoints, keys, passwords, or restored data.
    print(json.dumps({"snapshotId":bundle["snapshotId"],
                      "sourceRelease":bundle["sourceRelease"],
                      "readonlyProviderDenials":"verified"}))

if __name__ == "__main__":
    try:
        main()
    except Exception:
        print("Private recovery preparation failed; no secret contents were published.", file=sys.stderr)
        sys.exit(1)
