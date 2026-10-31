#!/bin/sh

set -eu

fail()
{
    printf '%s\n' "Service swap persistence test failed: $1" >&2
    exit 1
}

# Deliberately reloads the manager only on an isolated hosted runner.
# Refuse production, developer and self-hosted contexts before Docker/systemd.
[ "${GITHUB_ACTIONS:-}" = true ] &&
[ "${RUNNER_ENVIRONMENT:-}" = github-hosted ] &&
[ -n "${GITHUB_RUN_ID:-}" ] ||
    fail "requires a disposable GitHub-hosted Actions runner."

root_dir=${1:-}
[ -n "$root_dir" ] && [ -f "$root_dir/compose.production.yml" ] ||
    fail "a production Compose fixture directory is required."

[ "$(docker info --format '{{.CgroupDriver}}')" = systemd ] &&
[ "$(docker info --format '{{.CgroupVersion}}')" = 2 ] ||
    fail "the reload regression requires systemd with cgroup v2."

states="$(mktemp)"
trap 'rm -f "$states"' EXIT HUP INT TERM

assert_no_swap()
{
    service=$1
    parent=$2
    scope=$3
    [ "$(cat "$parent/memory.swap.max")" = 0 ] ||
        fail "$service kernel swap limit is not zero."
    [ "$(systemctl show "$scope" --property=MemorySwapMax --value)" = 0 ] ||
        fail "$service systemd scope does not own the explicit zero swap boundary."
}

for service in api parser-worker web
do
    container_id="$(docker compose --file "$root_dir/compose.production.yml" ps -q "$service")"
    [ -n "$container_id" ] || fail "$service fixture is not running."
    container_id="$(docker inspect --format '{{.Id}}' "$container_id")"
    pid="$(docker inspect --format '{{.State.Pid}}' "$container_id")"
    started_at="$(docker inspect --format '{{.State.StartedAt}}' "$container_id")"
    [ "$pid" -gt 0 ] || fail "$service fixture has no live process."

    relative="$(awk -F: '$1 == "0" && $2 == "" { print $3; exit }' "/proc/$pid/cgroup")"
    case "$relative" in
        *..*) fail "unexpected $service cgroup path." ;;
        /system.slice/docker-*) ;;
        *) fail "$service fixture is not in a dedicated systemd scope." ;;
    esac
    if [ "$service" = parser-worker ]; then
        case "$relative" in
            */fullworth-supervisor) relative=${relative%/fullworth-supervisor} ;;
            *) fail "parser supervisor is not in its delegated leaf." ;;
        esac
    fi
    scope=${relative##*/}
    [ "$scope" = "docker-$container_id.scope" ] ||
        fail "$service scope does not match the inspected container."
    parent="/sys/fs/cgroup$relative"
    assert_no_swap "$service" "$parent" "$scope"
    printf '%s|%s|%s|%s|%s|%s\n' \
        "$service" "$container_id" "$pid" "$started_at" "$parent" "$scope" >> "$states"
done

sudo -n systemctl daemon-reload

while IFS='|' read -r service container_id pid started_at parent scope
do
    assert_no_swap "$service" "$parent" "$scope"
    [ "$(docker inspect --format '{{.State.Pid}}' "$container_id")" = "$pid" ] &&
    [ "$(docker inspect --format '{{.State.StartedAt}}' "$container_id")" = "$started_at" ] ||
        fail "$service restarted instead of preserving its boundary."
done < "$states"

docker compose --file "$root_dir/compose.production.yml" exec -T api \
    curl --fail --silent --show-error \
        --retry 10 --retry-all-errors --retry-delay 1 \
        --cacert /var/run/fullworth-parser-tls/parser-worker.cer.pem \
        https://parser-worker:8081/health/ready >/dev/null

printf '%s\n' 'API, parser and Web no-swap boundaries survived systemd reload without restart; parser readiness passed.'
