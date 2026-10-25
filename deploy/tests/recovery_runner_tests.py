#!/usr/bin/env python3
import contextlib
import datetime as dt
import importlib.util
import io
import json
import os
from pathlib import Path
import tempfile
import types
import unittest
from unittest.mock import patch
import sys

spec=importlib.util.spec_from_file_location("recovery_runner",Path(__file__).parents[1]/"prepare-recovery-runner.py")
module=importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
NOW=dt.datetime(2026,10,25,12,tzinfo=dt.timezone.utc)

def bundle():
    return {"version":1,"createdAtUtc":NOW.isoformat(),
            "repository":"s3:https://abc123.r2.cloudflarestorage.com/example-backup/immutable-recovery/20261025-proof/",
            "resticPassword":"synthetic-password-more-than-24-characters",
            "accessKeyId":"a"*32,"secretAccessKey":"b"*64,
            "snapshotId":"c"*64,"sourceRelease":"d"*40,
            "protectedCopyVerified":True,"canaryKey":"recovery-readonly-probe/"+"e"*24}

class BundleTests(unittest.TestCase):
    def test_valid_frozen_bundle(self):
        self.assertEqual(module.validate(bundle(),NOW)["snapshotId"],"c"*64)

    def test_unknown_fields_and_wrong_types_fail(self):
        for value in ({**bundle(),"productionPassword":"unexpected"},{**bundle(),"version":True},
                      {**bundle(),"accessKeyId":123},{**bundle(),"protectedCopyVerified":"true"}):
            with self.subTest(value=list(value)):
                with self.assertRaises(ValueError):module.validate(value,NOW)

    def test_stale_future_and_naive_timestamps_fail(self):
        for date in (NOW-dt.timedelta(days=2),NOW+dt.timedelta(seconds=1),NOW.replace(tzinfo=None)):
            with self.subTest(date=date):
                with self.assertRaises(ValueError):module.validate({**bundle(),"createdAtUtc":date.isoformat()},NOW)

    def test_mutable_other_backend_and_path_injection_fail(self):
        for repo in ("s3:https://abc123.r2.cloudflarestorage.com/example-backup/",
                     "file:/tmp/recovery",bundle()["repository"]+"../",
                     bundle()["repository"].replace("https://","https://user@"),
                     bundle()["repository"]+"?token=secret",
                     bundle()["repository"].replace("r2.cloudflarestorage.com","attacker.example")):
            with self.subTest(repo=repo):
                with self.assertRaises(ValueError):module.validate({**bundle(),"repository":repo},NOW)

    def test_source_ids_and_canary_are_strict(self):
        for key,value in (("snapshotId","latest"),("sourceRelease","master"),
                          ("canaryKey","snapshots/"+"a"*64),("canaryKey","../probe")):
            with self.subTest(key=key):
                with self.assertRaises(ValueError):module.validate({**bundle(),key:value},NOW)

    def test_compose_injection_and_credential_injection_fail(self):
        for key,value in (("resticPassword","secret\nAWS_SECRET_ACCESS_KEY=oops"),
                          ("resticPassword","a"*24+"$TOKEN"),("secretAccessKey","b"*32+"\n"),
                          ("accessKeyId","a"*32+"\n[extra]")):
            with self.subTest(key=key):
                with self.assertRaises(ValueError):module.validate({**bundle(),key:value},NOW)

    def test_prepare_uses_private_files_without_database_password(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);src=root/"bundle.json";src.write_text(json.dumps(bundle()));src.chmod(0o600)
            out=root/"private"
            stdout=io.StringIO()
            with contextlib.redirect_stdout(stdout):
                module.prepare(src,out,"f"*40,NOW)
            self.assertEqual(stdout.getvalue(),"")
            self.assertEqual(out.stat().st_mode & 0o777,0o700)
            self.assertEqual(set(p.name for p in out.iterdir()),{"credentials","recovery.env"})
            for path in out.iterdir():self.assertEqual(path.stat().st_mode & 0o777,0o600)
            env=(out/"recovery.env").read_text()
            self.assertIn("BILLWATCH_RECOVERY_IMMUTABLE_COPY=true\n",env)
            self.assertNotIn("secretAccessKey",env)
            self.assertNotIn("BILLWATCH_DATABASE_PASSWORD=",env)
            self.assertNotIn("BILLWATCH_RECOVERY_EPHEMERAL_DATABASE_PASSWORD=",env)

    def test_ephemeral_database_password_is_passed_only_to_child(self):
        with patch.object(module.subprocess,"run") as run:
            module.run_proof(Path("/synthetic/recovery.env"))
        args,kwargs=run.call_args
        self.assertEqual(args[0][0],"sh")
        self.assertEqual(args[0][-1],"/synthetic/recovery.env")
        secret=kwargs["env"]["BILLWATCH_RECOVERY_EPHEMERAL_DATABASE_PASSWORD"]
        self.assertRegex(secret,r"^[0-9a-f]{64}$")
        self.assertNotIn(secret," ".join(args[0]))
        self.assertTrue(kwargs["check"])

    def test_public_bundle_symlink_oversize_and_existing_destination_fail(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);src=root/"bundle";src.write_text(json.dumps(bundle()));src.chmod(0o644)
            with self.assertRaises(ValueError):module.private_read(src)
            src.chmod(0o600);link=root/"link";link.symlink_to(src)
            with self.assertRaises(ValueError):module.private_read(link)
            src.write_text("x"*16385)
            with self.assertRaises(ValueError):module.private_read(src)
            src.write_text(json.dumps(bundle()));out=root/"private";out.mkdir()
            with self.assertRaises(FileExistsError):module.prepare(src,out,"f"*40,NOW)

class ProviderError(Exception):
    def __init__(self,status,code):
        self.response={"ResponseMetadata":{"HTTPStatusCode":status},"Error":{"Code":code}}

class ProviderTests(unittest.TestCase):
    def verify(self,status=403,code="AccessDenied",write_success=False,body=None):
        expected=b"FullWorth synthetic read-only recovery permission probe."
        class Client:
            def get_object(self,**kwargs):return {"Body":io.BytesIO(expected if body is None else body)}
            def put_object(self,**kwargs):
                if not write_success:raise ProviderError(status,code)
            def delete_object(self,**kwargs):raise ProviderError(status,code)
        modules={"boto3":types.SimpleNamespace(client=lambda *a,**kw:Client()),
                 "botocore":types.ModuleType("botocore"),
                 "botocore.config":types.SimpleNamespace(Config=lambda **kw:kw),
                 "botocore.exceptions":types.SimpleNamespace(ClientError=ProviderError)}
        with patch.dict(sys.modules,modules):module.verify_readonly(bundle())

    def test_explicit_provider_readonly_denials_pass(self):self.verify()
    def test_authentication_failure_does_not_count_as_readonly(self):
        with self.assertRaises(ValueError):self.verify(code="SignatureDoesNotMatch")
    def test_bucket_lock_denial_does_not_count_as_readonly(self):
        with self.assertRaises(ValueError):self.verify(status=409,code="ObjectLockedByBucketPolicy")
    def test_writable_credentials_fail(self):
        with self.assertRaises(ValueError):self.verify(write_success=True)
    def test_wrong_canary_fails_before_writes(self):
        with self.assertRaises(ValueError):self.verify(body=b"unexpected")

if __name__=="__main__":unittest.main()
