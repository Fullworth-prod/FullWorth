#!/bin/sh

set -eu

app_path=${1:-}
expected_bundle_id=${2:-}
evidence_dir=${3:-}
phase=preflight

record_phase()
{
    phase=$1
    if [ -n "$evidence_dir" ]; then
        printf '%s\n' "$phase" >"$evidence_dir/phase.txt"
    fi
}

fail()
{
    printf '%s\n' "iOS simulator smoke failed: $1" >&2
    if [ -n "$evidence_dir" ] && [ -d "$evidence_dir" ]; then
        printf '{"status":"failed","phase":"%s"}\n' "$phase" >"$evidence_dir/result.json"
    fi
    exit 1
}

run_with_timeout()
{
    timeout_seconds=$1
    shift

    python3 - "$timeout_seconds" "$@" <<'PY'
import subprocess
import sys

timeout_seconds = float(sys.argv[1])
command = sys.argv[2:]

try:
    completed = subprocess.run(
        command,
        timeout=timeout_seconds,
        check=False)
except subprocess.TimeoutExpired:
    print(f"Bounded iOS simulator command timed out after {timeout_seconds:g} seconds.", file=sys.stderr)
    sys.exit(124)

sys.exit(completed.returncode)
PY
}

boot_simulator()
{
    run_with_timeout 30 \
        xcrun simctl boot "$udid" >/dev/null 2>&1 ||
        return 1

    run_with_timeout 180 \
        xcrun simctl bootstatus "$udid" -b
}

restart_simulator()
{
    run_with_timeout 30 \
        xcrun simctl shutdown "$udid" >/dev/null 2>&1 ||
        true

    boot_simulator
}

install_app()
{
    run_with_timeout 180 \
        xcrun simctl install "$udid" "$app_path"
}

[ -n "$app_path" ] ||
    fail "app path is required."
[ -n "$expected_bundle_id" ] ||
    fail "expected bundle ID is required."
[ -d "$app_path" ] ||
    fail "app bundle does not exist."

for command_name in xcrun python3
do
    command -v "$command_name" >/dev/null 2>&1 ||
        fail "$command_name is required."
done

if [ -n "$evidence_dir" ]; then
    mkdir -p "$evidence_dir"
    record_phase preflight
fi

info_plist="$app_path/Info.plist"
[ -f "$info_plist" ] ||
    fail "app Info.plist is missing."

actual_bundle_id=$(
    /usr/libexec/PlistBuddy \
        -c 'Print :CFBundleIdentifier' \
        "$info_plist"
)

[ "$actual_bundle_id" = "$expected_bundle_id" ] ||
    fail "built bundle ID does not match the expected identifier."

record_phase runtime-selection

runtime_id=$(
    xcrun simctl list runtimes --json |
        python3 -c '
import json, sys
data=json.load(sys.stdin)
items=[
    runtime for runtime in data.get("runtimes", [])
    if runtime.get("isAvailable") and
       runtime.get("identifier", "").startswith(
           "com.apple.CoreSimulator.SimRuntime.iOS-26-0")
]
if not items:
    raise SystemExit(1)
print(items[0]["identifier"])
'
) || fail "no available iOS 26.0 Simulator runtime was found."

record_phase device-selection

udid=$(
    xcrun simctl list devices --json |
        python3 -c '
import json, sys
runtime_id=sys.argv[1]
data=json.load(sys.stdin)
devices=[
    device for device in data.get("devices", {}).get(runtime_id, [])
    if device.get("isAvailable") and
       device.get("name", "").startswith("iPhone")
]
if not devices:
    raise SystemExit(1)
preferred=[
    device for device in devices
    if "Pro" in device.get("name", "")
]
choice=(preferred or devices)[0]
print(choice["udid"])
' "$runtime_id"
) || fail "no preinstalled iPhone Simulator is available for iOS 26.0."

cleanup()
{
    if [ -n "${udid:-}" ]; then
        run_with_timeout 30 \
            xcrun simctl terminate \
                "$udid" \
                "$expected_bundle_id" >/dev/null 2>&1 ||
            true

        run_with_timeout 30 \
            xcrun simctl shutdown "$udid" >/dev/null 2>&1 ||
            true
    fi
}
trap cleanup EXIT HUP INT TERM

run_with_timeout 30 \
    xcrun simctl shutdown "$udid" >/dev/null 2>&1 ||
    true

record_phase reset
run_with_timeout 60 \
    xcrun simctl erase "$udid" ||
    fail "could not reset the iOS 26.0 Simulator within 60 seconds."

record_phase boot
if ! boot_simulator; then
    printf '%s\n' \
        "Initial iOS simulator boot did not become ready; retrying once." >&2

    restart_simulator ||
        fail "iOS 26.0 Simulator did not finish booting after one bounded retry."
fi

record_phase install
if ! install_app; then
    printf '%s\n' \
        "Initial FullWorth simulator install failed or timed out; rebooting and retrying once." >&2

    restart_simulator ||
        fail "iOS 26.0 Simulator did not recover for the install retry."

    install_app ||
        fail "FullWorth could not be installed into the iOS Simulator after one bounded retry."
fi

launch_app()
{
    run_with_timeout 180 \
        xcrun simctl launch "$udid" "$expected_bundle_id"
}

record_phase launch
launch_output=$(
    launch_app
) || fail "FullWorth could not be launched in the iOS Simulator within 180 seconds."

printf '%s\n' "$launch_output" |
    grep -Fq "$expected_bundle_id:" ||
    fail "simctl launch did not return the expected FullWorth process identifier."

sleep 3

# A successful launch can precede CoreSimulator's app-container lookup becoming
# responsive. Retry only this read, never installation or launch, and remain bounded.
record_phase app-container
container_attempt=1
while :
do
    if app_container=$(
        run_with_timeout 30 xcrun simctl get_app_container \
            "$udid" "$expected_bundle_id" app
    ); then
        if [ -n "$app_container" ] && [ -d "$app_container" ]; then
            break
        fi
    fi
    [ "$container_attempt" -lt 3 ] ||
        fail "FullWorth app container was not available after three bounded read attempts."
    container_attempt=$((container_attempt + 1))
    sleep 2
done

if [ -n "$evidence_dir" ]; then
    record_phase screenshot
    # Capture logged-out launch evidence only; no account credentials in CI.
    sleep 12
    run_with_timeout 30 \
        xcrun simctl io "$udid" screenshot "$evidence_dir/hosted-launch.png" ||
        fail "could not capture the hosted launch screen."
fi

record_phase complete
if [ -n "$evidence_dir" ]; then
    printf '{"status":"passed","phase":"complete"}\n' >"$evidence_dir/result.json"
fi

printf '%s\n' \
    "FullWorth iOS simulator install/launch smoke passed for $expected_bundle_id."
