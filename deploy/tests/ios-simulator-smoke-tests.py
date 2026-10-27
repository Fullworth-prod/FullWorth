#!/usr/bin/env python3
"""Execute the smoke lifecycle with disposable CLI fixtures, never a real simulator."""
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

SOURCE = Path(__file__).with_name("ios-simulator-smoke.sh").read_text()
BUNDLE = "com.companyname.billwatch"
FAKE_XCRUN = r'''#!/usr/bin/env python3
import json, os, pathlib, sys
args = sys.argv[1:]
root = pathlib.Path(os.environ["SMOKE_FIXTURE"])
scenario = os.environ["SMOKE_SCENARIO"]
with (root / "calls.jsonl").open("a") as stream:
    stream.write(json.dumps(args) + "\n")
if args[:3] == ["simctl", "list", "runtimes"]:
    print(json.dumps({"runtimes":[{"isAvailable":True,
        "identifier":"com.apple.CoreSimulator.SimRuntime.iOS-26-0"}]}))
elif args[:3] == ["simctl", "list", "devices"]:
    print(json.dumps({"devices":{"com.apple.CoreSimulator.SimRuntime.iOS-26-0":
        [{"isAvailable":True,"name":"iPhone Pro","udid":"fixture-device"}]}}))
elif args[:2] == ["simctl", "bootstatus"] and scenario == "boot-failure":
    sys.exit(1)
elif args[:2] == ["simctl", "launch"]:
    print("wrong.bundle: 1" if scenario == "launch-mismatch" else
        "com.companyname.billwatch: 12345")
elif args[:2] == ["simctl", "get_app_container"]:
    counter = root / "container-attempts"
    attempt = int(counter.read_text()) + 1 if counter.exists() else 1
    counter.write_text(str(attempt))
    if scenario == "container-missing" or (scenario == "container-transient" and attempt == 1):
        sys.exit(1)
    if scenario == "container-empty":
        print("")
    else:
        print(root / "FullWorth.app")
elif args[:2] == ["simctl", "io"]:
    pathlib.Path(args[-1]).write_bytes(b"disposable screenshot fixture")
'''
class SmokeLifecycleTests(unittest.TestCase):
    def run_case(self, scenario):
        # All fixture files live inside one newly-created test-only directory.
        with tempfile.TemporaryDirectory(prefix="fullworth-ios-smoke-") as location:
            root = Path(location)
            app = root / "FullWorth.app"
            app.mkdir()
            (app / "Info.plist").write_text("fixture")
            commands = root / "bin"
            commands.mkdir()
            for name, content in {
                "xcrun": FAKE_XCRUN,
                "PlistBuddy": "#!/bin/sh\nprintf '%s\\n' '" + BUNDLE + "'\n",
                "sleep": "#!/bin/sh\nexit 0\n",
            }.items():
                target = commands / name
                target.write_text(content)
                target.chmod(0o700)
            # The sole platform adaptation replaces Apple's unavailable plist CLI.
            # Runtime timeout and actual smoke commands remain unchanged.
            smoke = root / "smoke.sh"
            smoke.write_text(SOURCE.replace("/usr/libexec/PlistBuddy", str(commands / "PlistBuddy")))
            evidence = root / "evidence"
            env = dict(os.environ, PATH=str(commands) + os.pathsep + os.environ["PATH"],
                       SMOKE_FIXTURE=str(root), SMOKE_SCENARIO=scenario)
            result = subprocess.run(["sh", str(smoke), str(app), BUNDLE, str(evidence)],
                                    env=env, capture_output=True, text=True, timeout=30)
            record = json.loads((evidence / "result.json").read_text())
            calls = [json.loads(line) for line in (root / "calls.jsonl").read_text().splitlines()]
            return result, record, calls, (evidence / "hosted-launch.png").exists()

    def test_success_keeps_real_lifecycle_and_records_completion(self):
        result, record, calls, screenshot = self.run_case("success")
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual({"status":"passed","phase":"complete"}, record)
        self.assertTrue(screenshot)
        self.assertEqual(1, sum(c[:2] == ["simctl","install"] for c in calls))
        self.assertEqual(1, sum(c[:2] == ["simctl","launch"] for c in calls))

    def test_transient_lookup_retries_only_the_read(self):
        result, record, calls, screenshot = self.run_case("container-transient")
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("passed", record["status"])
        self.assertTrue(screenshot)
        self.assertEqual(2, sum(c[:2] == ["simctl","get_app_container"] for c in calls))
        self.assertEqual(1, sum(c[:2] == ["simctl","install"] for c in calls))
        self.assertEqual(1, sum(c[:2] == ["simctl","launch"] for c in calls))

    def test_missing_and_empty_containers_fail_closed_with_artifact(self):
        for scenario in ("container-missing", "container-empty"):
            with self.subTest(scenario=scenario):
                result, record, calls, screenshot = self.run_case(scenario)
                self.assertNotEqual(0, result.returncode)
                self.assertEqual({"status":"failed","phase":"app-container"}, record)
                self.assertFalse(screenshot)
                self.assertEqual(3, sum(c[:2] == ["simctl","get_app_container"] for c in calls))
                self.assertEqual(1, sum(c[:2] == ["simctl","launch"] for c in calls))

    def test_boot_failure_has_evidence_and_only_one_boot_retry(self):
        result, record, calls, screenshot = self.run_case("boot-failure")
        self.assertNotEqual(0, result.returncode)
        self.assertEqual({"status":"failed","phase":"boot"}, record)
        self.assertFalse(screenshot)
        self.assertEqual(2, sum(c[:2] == ["simctl","boot"] for c in calls))
        self.assertFalse(any(c[:2] == ["simctl","install"] for c in calls))

    def test_wrong_launch_acknowledgement_does_not_claim_success(self):
        result, record, calls, screenshot = self.run_case("launch-mismatch")
        self.assertNotEqual(0, result.returncode)
        self.assertEqual({"status":"failed","phase":"launch"}, record)
        self.assertFalse(screenshot)
        self.assertFalse(any(c[:2] == ["simctl","get_app_container"] for c in calls))

if __name__ == "__main__":
    unittest.main()
