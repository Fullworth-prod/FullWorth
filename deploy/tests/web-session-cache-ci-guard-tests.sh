#!/bin/sh
set -eu
root_dir=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
script="$root_dir/deploy/tests/web-session-cache-ci.sh"
for scenario in not_ci self_hosted missing_runner invalid_run missing_run
do
    actions=true
    runner=github-hosted
    run_id=123
    case "$scenario" in
        not_ci) actions=false ;;
        self_hosted) runner=self-hosted ;;
        missing_runner) runner= ;;
        invalid_run) run_id='not-a-run' ;;
        missing_run) run_id= ;;
    esac
    if output="$(GITHUB_ACTIONS="$actions" RUNNER_ENVIRONMENT="$runner" GITHUB_RUN_ID="$run_id" sh "$script" 2>&1)"; then
        printf '%s\n' "Unsafe session-cache acceptance context was allowed: $scenario" >&2
        exit 1
    else
        status=$?
        [ "$status" -eq 77 ] || {
            printf '%s\n' "Session-cache acceptance did not refuse before runtime access: $scenario" >&2
            exit 1
        }
    fi
done
printf '%s\n' 'Session-cache CI refusal boundaries passed (five contexts).'
