import importlib.util
import io
import plistlib
import struct
import unittest
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("device_ipa", ROOT / "deploy/ios/verify-device-ipa.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class DeviceIpaTests(unittest.TestCase):
    def package(self, platform=2, cpu=0x0100000C, info=None, commands=None, ncmds=1, extra=None):
        metadata = {
            "CFBundleIdentifier": "org.fullworth.personal",
            "CFBundleExecutable": "FullWorth",
            "DTPlatformName": "iphoneos",
            "CFBundleSupportedPlatforms": ["iPhoneOS"],
        }
        metadata.update(info or {})
        if commands is None:
            commands = struct.pack("<6I", 0x32, 24, platform, 0, 0, 0)
        binary = struct.pack("<8I", 0xFEEDFACF, cpu, 0, 2, ncmds, len(commands), 0, 0) + commands
        result = io.BytesIO()
        with zipfile.ZipFile(result, "w") as archive:
            archive.writestr("Payload/FullWorth.app/Info.plist", plistlib.dumps(metadata))
            archive.writestr("Payload/FullWorth.app/FullWorth", binary)
            for name, content in (extra or {}).items():
                archive.writestr(name, content)
        result.seek(0)
        return result

    def test_device_package_is_accepted(self):
        self.assertTrue(module.verify(self.package()))

    def test_arm64_simulator_is_rejected_even_with_device_plist(self):
        with self.assertRaises(ValueError):
            module.verify(self.package(platform=7))

    def test_wrong_cpu_is_rejected(self):
        with self.assertRaises(ValueError):
            module.verify(self.package(cpu=0x01000007))

    def test_simulator_plist_is_rejected(self):
        with self.assertRaises(ValueError):
            module.verify(self.package(info={"DTPlatformName": "iphonesimulator"}))

    def test_supported_platform_is_checked(self):
        with self.assertRaises(ValueError):
            module.verify(self.package(info={"CFBundleSupportedPlatforms": ["iPhoneSimulator"]}))

    def test_placeholder_bundle_is_rejected(self):
        with self.assertRaises(ValueError):
            module.verify(self.package(info={"CFBundleIdentifier": "com.companyname.billwatch"}))

    def test_executable_traversal_is_rejected(self):
        with self.assertRaises(ValueError):
            module.verify(self.package(info={"CFBundleExecutable": "../FullWorth"}))

    def test_zip_traversal_is_rejected(self):
        with self.assertRaises(ValueError):
            module.verify(self.package(extra={"../escape": b"x"}))

    def test_two_apps_are_rejected(self):
        with self.assertRaises(ValueError):
            module.verify(self.package(extra={"Payload/Other.app/Info.plist": b"x"}))

    def test_command_count_must_match(self):
        with self.assertRaises(ValueError):
            module.verify(self.package(ncmds=2))

    def test_short_build_version_is_rejected(self):
        with self.assertRaises(ValueError):
            module.verify(self.package(commands=struct.pack("<2I", 0x32, 8)))

    def test_oversized_command_is_rejected(self):
        with self.assertRaises(ValueError):
            module.verify(self.package(commands=struct.pack("<6I", 0x32, 4096, 2, 0, 0, 0)))

    def test_missing_device_platform_is_rejected(self):
        with self.assertRaises(ValueError):
            module.verify(self.package(commands=struct.pack("<6I", 1, 24, 2, 0, 0, 0)))

    def test_workflow_has_no_signing_or_upload_credentials(self):
        workflow = (ROOT / ".github/workflows/ios-personal-device.yml").read_text()
        self.assertNotIn("secrets.", workflow)
        self.assertNotIn("pull_request_target", workflow)
        self.assertIn("contents: read", workflow)
        self.assertIn("persist-credentials: false", workflow)
        self.assertIn("-p:RuntimeIdentifier=ios-arm64", workflow)
        self.assertIn("-p:EnableCodeSigning=false", workflow)
        self.assertIn("-p:CodesignRequireProvisioningProfile=false", workflow)
        self.assertIn("python3 deploy/ios/verify-device-ipa.py", workflow)
        self.assertIn("retention-days: 7", workflow)
        self.assertIn("shasum -a 256", workflow)
        self.assertIn("workflow_dispatch:", workflow)
        self.assertIn("pull_request:", workflow)
        self.assertIn("Signing: absent", workflow)

    def test_every_external_action_is_immutable(self):
        import re
        workflow = (ROOT / ".github/workflows/ios-personal-device.yml").read_text()
        actions = re.findall(r"uses: (\\S+)", workflow)
        self.assertGreaterEqual(len(actions), 3)
        for action in actions:
            self.assertRegex(action, r"^[^@]+@[a-f0-9]{40}$")


if __name__ == "__main__":
    unittest.main()
