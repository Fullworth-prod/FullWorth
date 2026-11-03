# Candidate CodeQL reports

The existing GitHub default setup remains the source of GitHub's Code scanning alert queue. The authenticated development view reported that the branch had not been scanned, and the existing default CodeQL workflow offered no manual Run control. This workflow provides an independent exact-candidate static report without switching setup type or adding security-events permissions.

The workflow uses only contents-read permissions, exact-source checkout with credential persistence disabled, a commit-pinned official CodeQL action, and a linked tool bundle. It runs actions, C#, JavaScript/TypeScript and Python with the security-extended query suite. Source-only build mode is explicit; it does not replace the separately required compiled/backend/Android tests or prove complete symbol/extraction coverage.

Both SARIF-to-Code-scanning and CodeQL database uploads are disabled. Raw SARIF, unfiltered findings, diagnostics and an integrity/source manifest are retained as ordinary seven-day CI artifacts. No repository security setting, alert disposition, new credential/grant, production environment or private provider/financial data is used. There is no schedule or deployment trigger.

A successful report-production job does not mean zero vulnerabilities or release acceptance. The metadata step refuses absent reports/query metadata or unsuccessful/missing invocation records. Review every language's raw findings and extraction/configuration diagnostics; retain exact source/tree, action/tool version, run/attempt, artifact hash and limitations. Do not suppress findings, exclude tests/source or mark development's default alert queue scanned from these reports. Investigate actionable source findings with meaningful regressions and protected PRs before integration.

Authoritative action inputs: [pinned analyze definition](https://github.com/github/codeql-action/blob/1fc58d04a4d4374f921bdac573c3811c240f9c26/analyze/action.yml), [pinned init definition](https://github.com/github/codeql-action/blob/1fc58d04a4d4374f921bdac573c3811c240f9c26/init/action.yml). GitHub documents that [default setup rejects separate CodeQL SARIF uploads](https://docs.github.com/en/code-security/reference/code-scanning/sarif-files/troubleshoot-sarif-uploads/default-setup-enabled); this report does not attempt those uploads.

Initial execution and extraction/finding review are pending. A prepared workflow is not scan evidence.
