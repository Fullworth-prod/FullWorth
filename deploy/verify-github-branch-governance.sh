#!/usr/bin/env bash
set -euo pipefail

repository="${GITHUB_REPOSITORY:?GITHUB_REPOSITORY is required}"
token="${GITHUB_TOKEN:?GITHUB_TOKEN is required}"
api_url="${GITHUB_API_URL:-https://api.github.com}"
# Rulesets require check-run names emitted by jobs, not workflow display names.
required_checks_json='["Backend build and tests","MAUI Android build","Linux production container","Dependency review"]'

request_json() {
    local url="$1"

    curl \
        --fail-with-body \
        --silent \
        --show-error \
        --location \
        --header "Authorization: Bearer ${token}" \
        --header "Accept: application/vnd.github+json" \
        --header "X-GitHub-Api-Version: 2022-11-28" \
        "$url"
}

request_public_json() {
    local url="$1"

    curl \
        --fail-with-body \
        --silent \
        --show-error \
        --location \
        --header "Accept: application/vnd.github+json" \
        --header "X-GitHub-Api-Version: 2022-11-28" \
        "$url"
}

verify_branch() {
    local branch="$1"
    local response
    local protected
    local rulesets_response
    local ruleset_id
    local ruleset_response
    local missing_checks
    local strict_checks

    if ! response="$(request_json "${api_url}/repos/${repository}/branches/${branch}")"; then
        echo "::error title=Branch governance unavailable::Could not read GitHub protection state for ${branch}."
        return 1
    fi

    protected="$(
        jq -r 'if has("protected") then (.protected | tostring) else "missing" end' \
            <<<"$response"
    )"

    if [[ "$protected" != "true" ]]; then
        echo "::error title=Unprotected FullWorth branch::${branch} reports protected=${protected}. Configure GitHub branch protection or a repository ruleset before relying on merge governance."
        return 1
    fi

    if ! rulesets_response="$(request_public_json "${api_url}/repos/${repository}/rulesets?includes_parents=true")"; then
        echo "::error title=Branch governance unavailable::Could not read applicable public GitHub rulesets for ${branch}."
        return 1
    fi

    ruleset_id="$(
        jq -r '
            [
                .[] |
                select(
                    .name == "FullWorth protected branches" and
                    .enforcement == "active" and
                    .target == "branch"
                )
            ] |
            if length == 1 then .[0].id else "" end
        ' <<<"$rulesets_response"
    )"

    if [[ -z "$ruleset_id" || "$ruleset_id" == "null" ]]; then
        echo "::error title=Missing FullWorth branch ruleset::${branch} is not covered by exactly one active FullWorth protected branches ruleset."
        return 1
    fi

    if ! ruleset_response="$(request_public_json "${api_url}/repos/${repository}/rulesets/${ruleset_id}")"; then
        echo "::error title=Branch governance unavailable::Could not read the active public ruleset for ${branch}."
        return 1
    fi

    # The collection response contains metadata only. Read targeting from the
    # full detail response, and fail closed on exclusions we do not interpret.
    if ! jq -e --arg ref "refs/heads/${branch}" '
        .name == "FullWorth protected branches" and
        .target == "branch" and
        .enforcement == "active" and
        ((.conditions.ref_name.include // []) | index($ref) != null) and
        ((.conditions.ref_name.exclude // []) | length == 0)
    ' <<<"$ruleset_response" >/dev/null; then
        echo "::error title=Missing FullWorth branch ruleset::The full active ruleset must explicitly include ${branch} without exclusions."
        return 1
    fi

    # Owner approved a solo-maintainer policy on 2026-10-14. A PR remains
    # mandatory; zero independent approvals is permitted, not a missing rule.
    if ! jq -e '
        [.rules[]? | select(.type == "pull_request")] |
        length > 0 and all(.[];
            .parameters.required_approving_review_count |
            type == "number" and . >= 0 and . <= 10 and . == floor
        )
    ' <<<"$ruleset_response" >/dev/null; then
        echo "::error title=Pull request rule required::The active ruleset for ${branch} must require pull requests with a valid approval count."
        return 1
    fi

    missing_checks="$(
        jq -r --argjson required "$required_checks_json" '
            [
                .rules[]? |
                select(.type == "required_status_checks") |
                .parameters.required_status_checks[]?.context
            ] |
            unique as $configured |
            $required - $configured |
            join(", ")
        ' <<<"$ruleset_response"
    )"

    if [[ -n "$missing_checks" ]]; then
        echo "::error title=Required checks missing::The active ruleset for ${branch} is missing required checks: ${missing_checks}."
        return 1
    fi

    strict_checks="$(
        jq -r '
            [
                .rules[]? |
                select(.type == "required_status_checks") |
                .parameters.strict_required_status_checks_policy
            ] |
            length > 0 and all(.[]; . == true)
        ' <<<"$ruleset_response"
    )"

    if [[ "$strict_checks" != "true" ]]; then
        echo "::error title=Strict required checks missing::The active ruleset for ${branch} must require current-branch status checks."
        return 1
    fi

    echo "${branch}: protected; PR and current CI/security checks required (owner-approved solo-maintainer policy)"
}

failure=0

for branch in master development; do
    if ! verify_branch "$branch"; then
        failure=1
    fi
done

if (( failure != 0 )); then
    exit 1
fi

echo "FullWorth branch-governance verification passed."


