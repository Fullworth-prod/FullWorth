#!/bin/sh

set -eu

root_dir=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)

python3 - "$root_dir" <<'PY'
import os
import re
import sys

root = os.path.abspath(sys.argv[1])
controllers_root = os.path.join(
    root,
    "FullWorth.API",
    "Controllers")

owners = {
    "SubscriptionReviews": "Bills",
    "SubscriptionEvidence": "Bills",
    "BillStreams": "Bills",
    "BillAlerts": "Bills",
    "BankConnections": "Plaid",
    "BankAccounts": "Plaid",
    "BankTransactions": "Plaid",
    "PlaidLinkSessions": "Plaid",
    "PlaidLinkCompletionAttempts": "Plaid",
    "BillStatements": "Statements",
    "BillLineItems": "Statements",
    "BillChanges": "Statements",
    "BillStatementUploads": "Statements",
    "BillStatementAiEvaluations": "Statements",
    "PlanningPaySchedules": "Planning",
    "PlanningBillFundingPreferences": "Planning",
    "PlanningPaycheckAllocations": "Planning",
    "PlanningPaycheckPlanRuns": "Planning",
    "SubscriptionEntitlements": "Subscriptions",
    "UserProgramMemberships": "Subscriptions",
    "SubscriptionAccessKeys": "Subscriptions",
    "SubscriptionAccessKeyRedemptions": "Subscriptions",
}

entity_owners = {
    "SubscriptionReviewEntity": "Bills",
    "SubscriptionEvidenceEntity": "Bills",
    "BillStreamEntity": "Bills",
    "BillAlertEntity": "Bills",
    "BankConnectionEntity": "Plaid",
    "BankAccountEntity": "Plaid",
    "BankTransactionEntity": "Plaid",
    "PlaidLinkSessionEntity": "Plaid",
    "PlaidLinkCompletionAttemptEntity": "Plaid",
    "BillStatementEntity": "Statements",
    "BillLineItemEntity": "Statements",
    "BillChangeEntity": "Statements",
    "BillStatementUploadEntity": "Statements",
    "BillStatementAiEvaluationEntity": "Statements",
    "PlanningPayScheduleEntity": "Planning",
    "PlanningBillFundingPreferenceEntity": "Planning",
    "PlanningPaycheckAllocationEntity": "Planning",
    "PlanningPaycheckPlanRunEntity": "Planning",
    "SubscriptionEntitlementEntity": "Subscriptions",
    "UserProgramMembershipEntity": "Subscriptions",
    "SubscriptionAccessKeyEntity": "Subscriptions",
    "SubscriptionAccessKeyRedemptionEntity": "Subscriptions",
}

allowed_cross_owner_access = set()

def controller_module(filename):
    if filename.startswith("BillStatement"):
        return "Statements"
    if filename.startswith("BillStream") or filename.startswith("BillAlert"):
        return "Bills"
    if filename.startswith("Bank") or filename.startswith("Plaid"):
        return "Plaid"
    if filename.startswith("Subscription"):
        return "Subscriptions"
    if filename.startswith("Planning"):
        return "Planning"
    if filename.startswith("Account"):
        return "Accounts"
    if filename.startswith("Admin"):
        return "Admin"
    if filename.startswith("Identity"):
        return "Identity"
    return None

# Resolve declared contexts as the service guard does. Keep the two legacy
# names covered when a controller inherits its database field.
context_identifier_pattern = re.compile(r"\bFullWorthDbContext\s+([A-Za-z_][A-Za-z0-9_]*)")

def references(content, names, generic=False):
    identifiers = {"_dbContext", "dbContext"} | set(context_identifier_pattern.findall(content))
    result = set()
    alternatives = "|".join(sorted(map(re.escape, names), key=len, reverse=True))
    for identifier in identifiers:
        context = re.escape(identifier)
        pattern = (rf"\b{context}\s*\.\s*Set\s*<\s*({alternatives})\s*>\s*\("
                   if generic else rf"\b{context}\s*\.\s*({alternatives})\b")
        result.update(re.findall(pattern, content))
    return result

seen_allowances = set()
errors = []

if not os.path.isdir(controllers_root):
    errors.append(
        "missing controller directory: FullWorth.API/Controllers")
else:
    for filename in sorted(os.listdir(controllers_root)):
        if not filename.endswith(".cs"):
            continue

        path = os.path.join(
            controllers_root,
            filename)

        if not os.path.isfile(path):
            continue

        rel = os.path.relpath(
            path,
            root).replace(
                os.sep,
                "/")

        with open(
                path,
                "r",
                encoding="utf-8-sig") as handle:
            content = handle.read()

        referenced_sets = references(content, owners)
        referenced_entities = references(content, entity_owners, generic=True)

        if not referenced_sets and not referenced_entities:
            continue

        module = controller_module(
            filename)

        if module is None:
            errors.append(
                f"controller with direct owned-data access has no module mapping: {rel}")
            continue

        for db_set in sorted(
                referenced_sets):
            owner = owners[db_set]

            if owner == module:
                continue

            edge = (
                module,
                db_set,
                rel)

            if edge in allowed_cross_owner_access:
                seen_allowances.add(
                    edge)
                continue

            errors.append(
                f"forbidden direct controller cross-owner data access: "
                f"{module} -> {db_set} (owned by {owner}) in {rel}; "
                "use an explicit owner contract")

        for entity_type in sorted(referenced_entities):
            owner = entity_owners[entity_type]
            if owner == module:
                continue
            edge = (module, entity_type, rel)
            if edge in allowed_cross_owner_access:
                seen_allowances.add(edge)
                continue
            errors.append(
                f"forbidden direct controller cross-owner entity access: "
                f"{module} -> {entity_type} (owned by {owner}) in {rel}; "
                "use an explicit owner contract")

stale = sorted(
    allowed_cross_owner_access -
    seen_allowances)

for module, db_set, rel in stale:
    errors.append(
        f"stale controller data-ownership allowance: "
        f"{module} -> {db_set} in {rel}; remove the allowance "
        "in the same change that removes the dependency")

if errors:
    for error in errors:
        print(
            f"Controller data ownership boundary regression failed: {error}",
            file=sys.stderr)
    raise SystemExit(1)

print(
    "FullWorth controller data-ownership ratchet passed.")
PY
