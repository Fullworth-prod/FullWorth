#!/bin/sh

set -eu

repository_root="$(CDPATH= cd -- "$(dirname "$0")/../.." && pwd -P)"
compose_file="$repository_root/compose.production.yml"
dockerfile="$repository_root/Dockerfile"
verifier="$repository_root/deploy/verify-parser-containment.sh"
review="$repository_root/PARSER_NATIVE_ATTACK_SURFACE.md"

fail()
{
    printf '%s\n' "Parser native boundary regression failed: $1" >&2
    exit 1
}

for file in "$compose_file" "$dockerfile" "$verifier" "$review"
do
    [ -f "$file" ] || fail "required file is missing"
done

parser_service="$(
    awk '
        /^  parser-worker:$/ { capture = 1 }
        capture && /^  web:$/ { exit }
        capture { print }
    ' "$compose_file"
)"

require_parser()
{
    printf '%s\n' "$parser_service" | grep -Eq "$1" ||
        fail "$2"
}

require_parser '^    read_only: true$' "parser-worker root must be read-only"
require_parser '^    pids_limit: 64$' "parser-worker PID ceiling changed"
require_parser '^    cpus: ' "parser-worker CPU ceiling is missing"
require_parser '^    mem_limit: ' "parser-worker memory ceiling is missing"
require_parser '^    memswap_limit: ' "parser-worker swap ceiling is missing"
require_parser '^      core: 0$' "parser-worker core dumps must be disabled"
require_parser '^        soft: 512$' "parser-worker soft nofile ceiling changed"
require_parser '^        hard: 512$' "parser-worker hard nofile ceiling changed"
require_parser '^      - ALL$' "parser-worker must drop all capabilities"
require_parser '^      - no-new-privileges:true$' "no-new-privileges is missing"
require_parser '^      DOTNET_EnableDiagnostics: "0"$' "runtime diagnostics must remain disabled"
require_parser '^      - /tmp:rw,noexec,nosuid,nodev,size=64m,uid=1654,gid=1654,mode=0700$' "parser-worker temporary storage boundary changed"
require_parser '^      - parser_worker$' "parser-worker network attachment changed"

if printf '%s\n' "$parser_service" | grep -Eq '^    ports:|^    expose:'; then
    fail "parser-worker must not publish or expose a host port"
fi

if printf '%s\n' "$parser_service" | grep -Eq '^    init:'; then
    fail "an external init would occupy the delegated cgroup parent"
fi

grep -Fq 'HEALTHCHECK NONE' "$dockerfile" ||
    fail "Docker health-exec must stay disabled for the delegated cgroup parent"
grep -Fq 'DOTNET_EnableDiagnostics=0' "$dockerfile" ||
    fail "the parser image must disable runtime diagnostics"
grep -Fq 'libtesseract5' "$dockerfile" ||
    fail "the reviewed native OCR dependency is missing"
grep -Fq 'tesseract-ocr-eng' "$dockerfile" ||
    fail "the reviewed OCR model package is missing"

for evidence in     'root filesystem is writable'     'core dumps are not disabled'     'file-descriptor ceiling'     'publishes a host port'     'attached to more than one network'     'runtime diagnostics are enabled'     'effective Linux capabilities'     'bounding set'     'memory ceiling is not finite'     'CPU limit is not finite'     'PID limit is not finite'     'per-document OCR cgroup'     'per-image OCR cgroup'     'kernel OOM kill evidence'
do
    grep -Fq "$evidence" "$verifier" ||
        fail "live verifier no longer checks: $evidence"
done

for heading in     '## Threat model'     '## Native components'     '## Enforced boundary'     '## Verification'     '## Residual risk'     '## Change procedure'
do
    grep -Fq "$heading" "$review" ||
        fail "review documentation is missing: $heading"
done


# Execute the actual request-signing and transport fragments without Docker,
# production credentials, or network access. The token exists only as a file.
python3 - "$verifier" <<'PY'
import hashlib
import hmac
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile

source = Path(sys.argv[1]).read_text()
token = "synthetic-parser-proof-token-" + "a" * 32
timestamp = "1792032000"
nonce = "0123456789abcdef" * 2
body = b"synthetic-proof-image"
digest = hashlib.sha256(body).hexdigest()
signer = re.search(
    r"signature=\"\$\([\s\S]*?python3 -c '\n([\s\S]*?)\n' \"\$timestamp\"",
    source,
)
assert signer, "proof signer not found"
actual = subprocess.run(
    [sys.executable, "-c", signer[1], timestamp, nonce, digest],
    input=token, text=True, capture_output=True, check=True,
).stdout.strip()
canonical = "\n".join(("POST", "/v1/ocr/extract", timestamp, nonce, digest))
expected = hmac.new(token.encode(), canonical.encode(), hashlib.sha256).hexdigest()
assert actual == expected, "proof must sign real LF separators, not literal backslash-n"

transport = re.search(
    r"send_ocr_request\(\)[\s\S]*?sh -(?:e)?c '\n([\s\S]*?)\n        ' sh ",
    source,
)
assert transport, "proof transport not found"
with tempfile.TemporaryDirectory() as directory:
    work = Path(directory)
    secret = work / "token"
    secret.write_text(token)
    capture = work / "capture"
    fake_cat = work / "cat"
    fake_cat.write_text(
        "#!/bin/sh\n"
        'test "$#" -eq 1 || exit 65\n'
        'test "$1" = /run/secrets/ParserWorker__AuthenticationToken || exit 66\n'
        'exec /bin/cat "$TEST_SECRET"\n'
    )
    fake_cat.chmod(0o700)
    fake_curl = work / "curl"
    fake_curl.write_text(
        "#!" + sys.executable + "\n"
        "import json,os,pathlib,sys\n"
        "pathlib.Path(os.environ['TEST_CAPTURE']).write_text("
        "json.dumps({'args':sys.argv[1:],'body':sys.stdin.read()}))\n"
    )
    fake_curl.chmod(0o700)
    environment = dict(os.environ, PATH=str(work) + os.pathsep + os.environ["PATH"],
                       TEST_SECRET=str(secret), TEST_CAPTURE=str(capture))
    environment.pop("ParserWorker__AuthenticationToken", None)
    subprocess.run(
        ["sh", "-ec", transport[1], "sh", timestamp, nonce, digest, actual],
        input=body, env=environment, check=True,
    )
    request = json.loads(capture.read_text())
    assert "Authorization: Bearer " + token in request["args"]
    assert "X-FullWorth-Parser-Signature: " + expected in request["args"]
    assert request["body"] == body.decode()
    assert request["args"][-1] == "https://parser-worker:8081/v1/ocr/extract"
    assert "--cacert" in request["args"] and "--insecure" not in request["args"]
    capture.unlink()
    secret.unlink()
    failed = subprocess.run(
        ["sh", "-ec", transport[1], "sh", timestamp, nonce, digest, actual],
        input=body, env=environment, capture_output=True,
    )
    assert failed.returncode != 0 and not capture.exists(), "missing secret must fail closed"
    secret.write_text("too-short")
    failed = subprocess.run(
        ["sh", "-ec", transport[1], "sh", timestamp, nonce, digest, actual],
        input=body, env=environment, capture_output=True,
    )
    assert failed.returncode != 0 and not capture.exists(), "short secret must fail closed"
# Execute the actual cgroup membership observer with a simulated kernel
# creation/configuration race. Do not replace strict production limit checks
# with a permissive wait or increase the OCR memory ceiling.
observer = re.search(
    r"(?ms)^await_image_process_membership\(\)\n\{\n.*?^\}\n",
    source,
)
assert observer, "image cgroup membership observer not found"
assert source.index('image_pid="$(await_image_process_membership') < source.index(
    '[ "$(cat "$image_cgroup/memory.max")" = "402653184" ]'
), "the memory check must follow confirmed cgroup process membership"
assert '[ "$(cat "$image_cgroup/memory.swap.max")" = "0" ]' in source
assert '[ "$1" = "100000" ]' in source and '[ "$2" = "100000" ]' in source

with tempfile.TemporaryDirectory() as directory:
    scope = Path(directory)
    members = scope / "cgroup.procs"
    members.write_text("")
    invocation = observer.group() + '\nawait_image_process_membership "$1" "$2"\n'
    command = ["sh", "-c", invocation, "observer", str(scope), str(os.getpid())]
    # The cgroup exists, but membership (which the worker writes last) is
    # intentionally published only after the verifier begins observing it.
    process = subprocess.Popen(command, stdout=subprocess.PIPE,
                               stderr=subprocess.PIPE, text=True)
    import time
    time.sleep(0.07)
    members.write_text("123456\n")
    output, errors = process.communicate(timeout=3)
    assert process.returncode == 0 and output.strip() == "123456", (
        "observer must wait for configured cgroup membership", errors
    )
    members.write_text("not-a-pid\n")
    invalid = subprocess.run(command, capture_output=True, text=True,
                             timeout=3)
    assert invalid.returncode != 0, "invalid membership must fail closed"
    members.write_text("")
    missing = subprocess.run(
        ["sh", "-c", invocation, "observer", str(scope), "2147483647"],
        capture_output=True, text=True, timeout=3,
    )
    assert missing.returncode != 0, "missing membership must fail closed"

print("Parser image cgroup observation race regression passed.")

print("Parser proof signing and file-backed authentication regression passed.")
PY

printf '%s\n' "Parser native boundary regression passed."
