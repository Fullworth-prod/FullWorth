#!/bin/sh
set -eu
root_dir=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
temp_dir=$(mktemp -d)
trap 'rm -rf "$temp_dir"' EXIT HUP INT TERM
awk '/^configure_recovery_readonly\(\)/,/^}/; /^recovery_restic\(\)/,/^}/' "$root_dir/deploy/backup/backup.sh" > "$temp_dir/functions.sh"
. "$temp_dir/functions.sh"
restic() { printf '%s\n' "$*" > "$temp_dir/arguments"; }
RESTIC_REPOSITORY='s3:https://abc123.r2.cloudflarestorage.com/example-backup/'
BILLWATCH_RECOVERY_IMMUTABLE_COPY=false
configure_recovery_readonly
recovery_restic restore example --target /tmp/synthetic
[ "$(cat "$temp_dir/arguments")" = 'restore example --target /tmp/synthetic' ]
BILLWATCH_RECOVERY_IMMUTABLE_COPY=true
if (configure_recovery_readonly) >/dev/null 2>&1; then
    echo "Mutable repository wrongly accepted lock-free recovery" >&2; exit 1
fi
for repository in \
 'file:/tmp/recovery' \
 's3:https://user@abc123.r2.cloudflarestorage.com/example-backup/immutable-recovery/proof/' \
 's3:https://abc123.r2.cloudflarestorage.com/example-backup/immutable-recovery/../' \
 's3:https://abc123.r2.cloudflarestorage.com/example-backup/immutable-recovery/proof/?token=x'
do
    RESTIC_REPOSITORY="$repository"
    if (configure_recovery_readonly) >/dev/null 2>&1; then
        echo "Unsafe repository wrongly accepted" >&2; exit 1
    fi
done
RESTIC_REPOSITORY='s3:https://abc123.r2.cloudflarestorage.com/example-backup/immutable-recovery/proof/'
configure_recovery_readonly
recovery_restic restore example --target /tmp/synthetic
[ "$(cat "$temp_dir/arguments")" = '--no-lock restore example --target /tmp/synthetic' ]
BILLWATCH_RECOVERY_IMMUTABLE_COPY=invalid
if (configure_recovery_readonly) >/dev/null 2>&1; then
    echo "Invalid recovery mode accepted" >&2; exit 1
fi
# The opt-in must never alter capture, maintenance or normal repository checks.
for function in create_backup apply_retention_policy require_repository
do
    if awk "/^$function\\(\\)/,/^}/" "$root_dir/deploy/backup/backup.sh" | grep -q recovery_restic; then
        echo "Recovery-only override escaped into $function" >&2; exit 1
    fi
done
printf '%s\n' 'Frozen-copy recovery locking boundaries passed.'
