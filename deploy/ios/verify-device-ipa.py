"""Reject simulator/malformed IPAs before offering a personal device build.

This checks package identity and the main executable's platform, not signing,
installability, every embedded framework, runtime behavior, or App Store readiness.
"""
import plistlib
import struct
import sys
import zipfile
from pathlib import PurePosixPath


def verify(path):
    with zipfile.ZipFile(path) as archive:
        names = archive.namelist()
        if len(names) != len(set(names)):
            raise ValueError("Duplicate ZIP entries.")
        for name in names:
            parts = PurePosixPath(name).parts
            if name.startswith("/") or ".." in parts or "\\" in name:
                raise ValueError("Unsafe ZIP entry.")
        apps = {
            PurePosixPath(name).parts[1]
            for name in names
            if len(PurePosixPath(name).parts) >= 2
            and PurePosixPath(name).parts[0] == "Payload"
            and PurePosixPath(name).parts[1].endswith(".app")
        }
        if apps != {"FullWorth.app"}:
            raise ValueError("Expected exactly one Payload/FullWorth.app.")
        root = "Payload/FullWorth.app/"
        info_entry = archive.getinfo(root + "Info.plist")
        if info_entry.file_size > 1024 * 1024:
            raise ValueError("Oversized Info.plist.")
        info = plistlib.loads(archive.read(info_entry))
        if info.get("CFBundleIdentifier") != "org.fullworth.personal":
            raise ValueError("Unexpected personal-test bundle ID.")
        if info.get("DTPlatformName") != "iphoneos":
            raise ValueError("Not an iphoneos bundle.")
        if info.get("CFBundleSupportedPlatforms") != ["iPhoneOS"]:
            raise ValueError("Not an iPhoneOS supported platform.")
        executable = info.get("CFBundleExecutable", "")
        if not isinstance(executable, str) or not executable or "/" in executable or "\\" in executable or executable in (".", ".."):
            raise ValueError("Invalid bundle executable name.")
        with archive.open(root + executable) as binary:
            header = binary.read(32)
            if len(header) != 32:
                raise ValueError("Truncated Mach-O header.")
            magic, cpu, _, filetype, count, size, _, _ = struct.unpack("<8I", header)
            if magic != 0xFEEDFACF or cpu != 0x0100000C or filetype != 2:
                raise ValueError("Expected an arm64 Mach-O executable.")
            if not 1 <= count <= 4096 or not 24 <= size <= 1024 * 1024:
                raise ValueError("Invalid Mach-O load command bounds.")
            commands = binary.read(size)
            if len(commands) != size:
                raise ValueError("Truncated load commands.")
            offset = 0
            platforms = []
            for _ in range(count):
                if offset + 8 > size:
                    raise ValueError("Truncated load command.")
                command, length = struct.unpack_from("<2I", commands, offset)
                if length < 8 or length % 8 != 0 or offset + length > size:
                    raise ValueError("Invalid load command length.")
                if command == 0x32:  # LC_BUILD_VERSION: IOS=2, IOSSIMULATOR=7
                    if length < 24:
                        raise ValueError("Truncated build-version command.")
                    platforms.append(struct.unpack_from("<I", commands, offset + 8)[0])
                offset += length
            if offset != size or platforms != [2]:
                raise ValueError("Main executable was not built exclusively for iOS device.")
    return True


if __name__ == "__main__":
    try:
        if len(sys.argv) != 2:
            raise ValueError("Usage: verify-device-ipa.py package.ipa")
        verify(sys.argv[1])
    except (ValueError, KeyError, OSError, zipfile.BadZipFile, plistlib.InvalidFileException, struct.error):
        sys.exit("Device IPA validation failed; no installation/signing claim is made.")
    print("Verified personal bundle layout and main executable arm64/iOS-device platform. Signing still required.")
