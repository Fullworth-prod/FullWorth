# Security and alert cleanup integration — 2026-10-27

This candidate reuses existing #979 rather than creating another integration PR. It is based on development11dc0b3d3babded2ac717018b76be30f5c20fcbd, preserving the integrated #983 tree. Source branches/history stay intact.

## Exact source work

- #979: eight distinct .NET packages across nine references to10.0.14.
- #980: immutable ASP.NET10.0 runtime index de0ee358cdd672f7017a003bed21e9ac9ed48a27c922a59b18175b6e82fa7fee.
- #981: immutable SDK10.0 index de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b.
- #986 bbfafef25eca3dbdececfc98a6cb24be86d9f5e5: unanimous normalized category evidence before typed alerts; preserve raw rows and accurate total-change alert. Eight reproduced baseline failures; focused corrected2,879 .NET/127 Node passed.
- #987 8441272b22db10a4ecff42ea3ceadae02e76375e: recovery-email action origin must match configured HTTPS host/effective port before a provider request; generic errors exclude tokens. Four reproduced baseline failures; focused corrected2,881 .NET/127 Node passed. No repository caller of the direct overload was found, so do not claim a demonstrated production Host-header exploit.

Microsoft's official10.0 release metadata identifies10.0.14 (2026-10-25) as a security release with SDK10.0.403. Read-only MCR manifests/configs independently matched those versions; see the milestone audit for exact Linux manifests. Keep content-addressed image pins and existing feature/financial safeguards. No account setting, real email or provider request was made.

## Verification boundary

Final combined-head main/dependency/PostgreSQL/browser/native execution is pending. Do not infer combined compatibility from earlier source runs. Re-read rules/reviews/development and exact-head checks before merge. After successful integration, verify the tested/integrated tree, then close represented source PRs as superseded (not individually merged) with branch retention.

#988 is independent tests-first evidence-retention work and is not included here. Real provider/device/inbox/private statement/recovery/legal prerequisites remain open. No deployment/master promotion or paid/secret/grant/production change is authorized by this candidate.
