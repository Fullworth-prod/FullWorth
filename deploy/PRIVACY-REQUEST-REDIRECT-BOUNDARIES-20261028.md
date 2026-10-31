## Verified integration — 2026-10-28 UTC

Merged #1003 as e92f4768822b662e083bf78c7779654dd09320de, exact source0d6ba4bde24a29d09b9778167c112b2de82a5cb7/testede289bf7609a259c0ed179b562e8ebc9f3813f93f/tree09d045aae7c350189ad2336871f89788bab6bfd7 matched. All5 checks;3,100 .NET/434 Node, actual Android zero warnings/errors,59 pinned privacy HTTP cases per Chromium/WebKit, rendered privacy and synthetic download/disposal, full themes/recovery/virtual passkey/HTTP/parser/disposable encrypted restore passed. Artifact12843299724 digestc3287af5ce3647ec842ac4f0f2b1c898f4bde4787ee5f8ddba862056af0ed2b9 verified; three privacy screens inspected.21 bounded performance observations include Bills0.00184 and unsupported WebKit values, not a zero-shift/device/SLA claim. All seven exact readbacks, rules/reviews/threads, retained branch and unchanged master9540 verified. No real proof/export/delete/production recovery or deployment acceptance. Older pending requirements below are history.

---

# Privacy request redirect boundaries — 2026-10-28

## Reproduced actual-source failure

The privacy export/deletion modules use a separate shared helper from the account-security module hardened in #999. An isolated local HTTP execution of the actual modules showed a 307 forwarded a synthetic proof body from DELETE to a redirect destination and returned success after that destination replied 204. Export POST also forwarded the synthetic proof, then rejected the redirected export; rejecting the final response was too late to stop forwarding. A redirected token GET was followed and allowed the subsequent write. No real credentials, users, exports or deletion endpoints were involved.

Set Fetch redirect:error on both privacy writes and their token GET. Reject redirected deletion responses defensively. Require a direct status200 application/json token object with a nonblank visible-ASCII token of at most4096 characters before sending a write. Retain cancellation/object-URL cleanup, documented direct202 cleanup-pending and204 deletion outcomes, existing confirmation/password requirements and no application replay after uncertain writes. This does not make a received202 proof of immediate stored-file erasure, and no browser download proves a file was saved.

## Verification

66 new Node cases cover token values/typed envelopes/status/MIME/redirect flags, boundary-length safe tokens, one explicit write, no replay, direct deletion statuses and rejection of false successes. A standalone actual-module HTTP browser suite exercises59 cases per engine: direct synthetic controls, same/cross-origin301/302/303/307/308 redirects on token and write, and invalid token/status/MIME/JSON. Its isolated destination records zero redirect arrivals/proof forwarding; no real application or financial API is called. Added paths and a step to the existing pinned Chromium/WebKit workflow; no check or security gate weakened.

Local focused execution: all66 Node cases and59 installed-Chromium cases passed with source imports remapped in memory. Installed local engine versions differ from pinned CI; final-head protected CI, pinned Chromium/WebKit and actual rendered privacy regressions must pass before integration. The existing privacy module lifecycle/cancellation suite also passed locally. Final pinned-browser and rendered application CI remain required.

## Limits and next checkpoint

No production incident, actual credential disclosure, real export/delete acceptance, external delivery, installed device, provider, recovery or legal acceptance is asserted. This is a reproduced redirect hazard corrected in source. Preserve all branches; integrate only after exact final-head gates/review. No production/master deployment, spending, account/grant/secret changes, private export or destructive action is authorized.
