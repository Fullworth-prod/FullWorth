#!/bin/sh

set -eu

root_dir=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
workflow="$root_dir/.github/workflows/repository-governance.yml"
verifier="$root_dir/deploy/verify-github-branch-governance.sh"

fail()
{
    printf '%s\n' "Repository governance regression failed: $1" >&2
    exit 1
}

[ -f "$workflow" ] || fail "workflow is missing."
[ -f "$verifier" ] || fail "branch protection verifier is missing."

grep -Fq 'branches:' "$workflow" ||
    fail "workflow has no branch trigger."
grep -Fq -- '- master' "$workflow" ||
    fail "workflow does not run after master updates."
grep -Fq 'schedule:' "$workflow" ||
    fail "workflow has no recurring governance check."
grep -Fq 'workflow_dispatch:' "$workflow" ||
    fail "workflow cannot be run manually."
grep -Fq 'contents: read' "$workflow" ||
    fail "workflow must retain read-only repository contents permission."
if grep -Fq 'administration:' "$workflow"; then
    fail "ruleset inspection must not broaden the workflow token permissions."
fi
grep -Fq 'bash deploy/verify-github-branch-governance.sh' "$workflow" ||
    fail "workflow does not execute the guarded verifier."

grep -Fq 'for branch in master development' "$verifier" ||
    fail "both long-lived branches are not verified."
grep -Fq '/branches/${branch}' "$verifier" ||
    fail "verifier does not read the GitHub branch resource."
grep -Fq '/rulesets?includes_parents=true' "$verifier" ||
    fail "verifier does not inspect applicable repository rulesets."
grep -Fq 'request_public_json' "$verifier" ||
    fail "ruleset inspection must use the public read-only API."
grep -Fq 'FullWorth protected branches' "$verifier" ||
    fail "verifier does not require the named active ruleset."
grep -Fq 'required_approving_review_count' "$verifier" ||
    fail "verifier does not require an approving review."
for check in 'Backend build and tests' 'MAUI Android build' 'Linux production container' 'Dependency review'; do
    grep -Fq "$check" "$verifier" ||
        fail "verifier does not require actual check-run context '$check'."
done
grep -Fq 'strict_required_status_checks_policy' "$verifier" ||
    fail "verifier does not require checks to match the current target branch."
grep -Fq 'if [[ "$protected" != "true" ]]' "$verifier" ||
    fail "unprotected branches are not rejected."
grep -Fq 'exit 1' "$verifier" ||
    fail "governance failure does not produce a failing result."

if grep -Eq 'echo .*GITHUB_TOKEN|printf .*GITHUB_TOKEN' "$verifier"; then
    fail "verifier must never print the GitHub token."
fi

mock_dir=$(mktemp -d)
trap 'rm -rf "$mock_dir"' EXIT HUP INT TERM
original_path=$PATH

cat > "$mock_dir/curl" <<'MOCK_CURL'
#!/bin/sh
set -eu

url=
for argument do
    url=$argument
done

case "$url" in
    */branches/*)
        if [ "${MOCK_MODE:-valid}" = unprotected ]; then
            printf '%s\n' '{"protected":false}'
        else
            printf '%s\n' '{"protected":true}'
        fi
        ;;
    */rulesets?includes_parents=true)
        if [ "${MOCK_MODE:-valid}" = missing_ruleset ]; then
            printf '%s\n' '[]'
        else
            printf '%s\n' '[{"id":42,"name":"FullWorth protected branches","enforcement":"active","conditions":{"ref_name":{"include":["refs/heads/master","refs/heads/development"]}}}]'
        fi
        ;;
    */rulesets/42)
        # Mirror the shape of the GitHub ruleset response, with actual job contexts.
        response='{"rules":[{"type":"pull_request","parameters":{"required_approving_review_count":1}},{"type":"required_status_checks","parameters":{"strict_required_status_checks_policy":true,"required_status_checks":[{"context":"Backend build and tests"},{"context":"MAUI Android build"},{"context":"Linux production container"},{"context":"Dependency review"}]}}]}'
        case "${MOCK_MODE:-valid}" in
            missing_approval)
                printf '%s\n' "$response" | jq '.rules[0].parameters.required_approving_review_count = 0'
                ;;
            missing_backend_check|missing_maui_check|missing_container_check|missing_dependency_check)
                case "$MOCK_MODE" in
                    missing_backend_check) missing='Backend build and tests' ;;
                    missing_maui_check) missing='MAUI Android build' ;;
                    missing_container_check) missing='Linux production container' ;;
                    missing_dependency_check) missing='Dependency review' ;;
                esac
                printf '%s\n' "$response" | jq --arg missing "$missing" '.rules[1].parameters.required_status_checks |= map(select(.context != $missing))'
                ;;
            workflow_names_only)
                printf '%s\n' "$response" | jq '.rules[1].parameters.required_status_checks = [{"context":"FullWorth CI"},{"context":"FullWorth Dependency Security"}]'
                ;;
            non_strict_checks)
                printf '%s\n' "$response" | jq '.rules[1].parameters.strict_required_status_checks_policy = false'
                ;;
            *)
                printf '%s\n' "$response"
                ;;
        esac
        ;;
    *)
        printf '%s\n' "Unexpected mocked GitHub URL: $url" >&2
        exit 1
        ;;
esac
MOCK_CURL
chmod +x "$mock_dir/curl"

run_verifier()
{
    mode=$1
    PATH="$mock_dir:$original_path" \
        MOCK_MODE="$mode" \
        GITHUB_REPOSITORY=Fullworth/FullWorth \
        GITHUB_TOKEN=test-token \
        /bin/bash "$verifier"
}

run_verifier valid >/dev/null ||
    fail "valid reviewed-PR and current-check rules were rejected."

for mode in unprotected missing_ruleset missing_approval missing_backend_check missing_maui_check missing_container_check missing_dependency_check workflow_names_only non_strict_checks; do
    if run_verifier "$mode" >/dev/null 2>&1; then
        fail "unsafe governance fixture '$mode' was accepted."
    fi
done

output=$(run_verifier valid) ||
    fail "valid governance fixture did not complete."
case "$output" in
    *test-token*) fail "verifier exposed the GitHub token." ;;
esac

printf '%s\n' "Repository governance workflow regression passed."

