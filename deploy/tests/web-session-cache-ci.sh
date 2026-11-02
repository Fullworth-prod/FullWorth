#!/bin/sh
# Real session compatibility against the disposable production-like CI stack.
set -eu

refuse()
{
    printf '%s\n' 'Web session cache acceptance is restricted to GitHub-hosted CI fixtures.' >&2
    exit 77
}

[ "${GITHUB_ACTIONS:-}" = true ] || refuse
[ "${RUNNER_ENVIRONMENT:-}" = github-hosted ] || refuse
case "${GITHUB_RUN_ID:-}" in
    ''|*[!0-9]*) refuse ;;
esac

umask 077
work_dir="$(mktemp -d)"
trap 'rm -rf "$work_dir"' EXIT HUP INT TERM

expected_image="$(
    docker compose --file compose.production.yml config --format json |
        python3 -c '
import json, re, sys
image = json.load(sys.stdin)["services"]["web-session-cache"]["image"]
assert re.fullmatch(r"redis:[0-9]+\.[0-9]+\.[0-9]+-alpine@sha256:[0-9a-f]{64}", image)
print(image)
'
)"
expected_version="${expected_image#redis:}"
expected_version="${expected_version%%-alpine@*}"
container_id="$(docker compose --file compose.production.yml ps -q web-session-cache)"
[ -n "$container_id" ]
expected_image_id="$(docker image inspect --format '{{.Id}}' "$expected_image")"
actual_image_id="$(docker inspect --format '{{.Image}}' "$container_id")"
[ "$actual_image_id" = "$expected_image_id" ]

# Read-only negative: authentication must still be required by the actual server.
unauthenticated="$(
    docker compose --file compose.production.yml exec --no-TTY web-session-cache \
        redis-cli --raw ping 2>&1
)" || :
[ "$unauthenticated" = 'NOAUTH Authentication required.' ]
unset unauthenticated

authenticated="$(
    docker compose --file compose.production.yml exec --no-TTY web-session-cache \
        sh -ec 'REDISCLI_AUTH="$(cat /run/secrets/redis_password)" redis-cli --no-auth-warning --raw ping'
)"
[ "$authenticated" = PONG ]
unset authenticated

actual_version="$(
    docker compose --file compose.production.yml exec --no-TTY web-session-cache \
        sh -ec 'REDISCLI_AUTH="$(cat /run/secrets/redis_password)" redis-cli --no-auth-warning --raw INFO server' |
        tr -d '\r' | sed -n 's/^redis_version://p'
)"
[ "$actual_version" = "$expected_version" ]
printf '%s\n' 'PASS exact configured Redis image/version and authenticated/unauthenticated boundary'

# Trust only this disposable edge's CA for curl; do not weaken certificate checks
# or alter machine trust. Reuse the existing recovery fixture, never a real owner.
docker compose --file compose.production.yml cp \
    edge:/data/caddy/pki/authorities/local/root.crt "$work_dir/ca.pem" >/dev/null
chmod 600 "$work_dir/ca.pem"
printf '%s' 'FullWorth!CiRecovery123' > "$work_dir/password"
chmod 600 "$work_dir/password"

CURL_CA_BUNDLE="$work_dir/ca.pem" \
    BILLWATCH_WEB_SMOKE_EMAIL="ci-recovery-${GITHUB_RUN_ID}@fullworth.local" \
    BILLWATCH_WEB_SMOKE_PASSWORD_FILE="$work_dir/password" \
    sh deploy/smoke-web-bff.sh https://web.localhost
