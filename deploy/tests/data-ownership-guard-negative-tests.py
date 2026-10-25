#!/usr/bin/env python3
"""Exercise the real ownership scanners against disposable synthetic C# fixtures."""
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

SCRIPT_NAMES = ("data-ownership-boundary-tests.sh", "controller-data-ownership-boundary-tests.sh")

def embedded_python(text):
    return text.split("<<'PY'\n", 1)[1].rsplit("\nPY", 1)[0]

ROOT = Path(__file__).resolve().parents[2]
GUARDS = {name: embedded_python((ROOT / "deploy" / "tests" / name).read_text(encoding="utf-8"))
          for name in SCRIPT_NAMES}

MODULES = ("Accounts", "Admin", "Bills", "Plaid", "Planning", "Statements", "Subscriptions")
SERVICE, CONTROLLER = SCRIPT_NAMES
CASES = [
    ("service_foreign_permit", SERVICE, "Services/Bills/Probe.cs",
     "class Probe(FullWorthDbContext context) { object Read() => context.PlaidLinkCompletionAttempts; }", True),
    ("service_foreign_generic_permit", SERVICE, "Services/Accounts/Probe.cs",
     "class Probe(FullWorthDbContext context) { object Read() => context.Set<PlaidLinkCompletionAttemptEntity>(); }", True),
    ("service_foreign_renamed_permit", SERVICE, "Services/Statements/Probe.cs",
     "class Probe(FullWorthDbContext stored) { object Read() => stored.PlaidLinkCompletionAttempts; }", True),
    ("service_owned_permit", SERVICE, "Services/Plaid/Probe.cs",
     "class Probe(FullWorthDbContext context) { object Read() => context.PlaidLinkCompletionAttempts; }", False),
    ("service_owned_generic_permit", SERVICE, "Services/Plaid/Probe.cs",
     "class Probe(FullWorthDbContext context) { object Read() => context.Set<PlaidLinkCompletionAttemptEntity>(); }", False),
    ("service_dto_name", SERVICE, "Services/Bills/Probe.cs",
     "class Probe(FullWorthDbContext context, Response dto) { object Read() => dto.PlaidLinkCompletionAttempts; }", False),
    ("controller_foreign_permit", CONTROLLER, "Controllers/AccountProbeController.cs",
     "class AccountProbeController(FullWorthDbContext dbContext) { object Read() => dbContext.PlaidLinkCompletionAttempts; }", True),
    ("controller_foreign_generic_permit", CONTROLLER, "Controllers/AccountProbeController.cs",
     "class AccountProbeController(FullWorthDbContext dbContext) { object Read() => dbContext.Set<PlaidLinkCompletionAttemptEntity>(); }", True),
    ("controller_foreign_renamed_permit", CONTROLLER, "Controllers/BillStreamProbeController.cs",
     "class BillStreamProbeController(FullWorthDbContext renamed) { object Read() => renamed.PlaidLinkCompletionAttempts; }", True),
    ("controller_foreign_renamed_generic", CONTROLLER, "Controllers/PlanningProbeController.cs",
     "class PlanningProbeController(FullWorthDbContext renamed) { object Read() => renamed.Set<PlaidLinkCompletionAttemptEntity>(); }", True),
    ("controller_owned_permit", CONTROLLER, "Controllers/PlaidProbeController.cs",
     "class PlaidProbeController(FullWorthDbContext dbContext) { object Read() => dbContext.PlaidLinkCompletionAttempts; }", False),
    ("controller_owned_generic_permit", CONTROLLER, "Controllers/PlaidProbeController.cs",
     "class PlaidProbeController(FullWorthDbContext renamed) { object Read() => renamed.Set<PlaidLinkCompletionAttemptEntity>(); }", False),
    ("controller_inherited_context", CONTROLLER, "Controllers/AccountProbeController.cs",
     "class AccountProbeController : Base { object Read() => _dbContext.PlaidLinkCompletionAttempts; }", True),
    ("controller_unmapped_module", CONTROLLER, "Controllers/UnexpectedController.cs",
     "class UnexpectedController(FullWorthDbContext dbContext) { object Read() => dbContext.PlaidLinkCompletionAttempts; }", True),
    ("controller_unmapped_generic_module", CONTROLLER, "Controllers/UnexpectedController.cs",
     "class UnexpectedController(FullWorthDbContext named) { object Read() => named.Set<PlaidLinkCompletionAttemptEntity>(); }", True),
    ("controller_existing_foreign_generic_bank", CONTROLLER, "Controllers/BillStreamProbeController.cs",
     "class BillStreamProbeController(FullWorthDbContext named) { object Read() => named.Set<BankConnectionEntity>(); }", True),
    ("controller_existing_owned_bank", CONTROLLER, "Controllers/BankProbeController.cs",
     "class BankProbeController(FullWorthDbContext named) { object Read() => named.Set<BankConnectionEntity>(); }", False),
    ("controller_dto_name", CONTROLLER, "Controllers/AccountProbeController.cs",
     "class AccountProbeController(FullWorthDbContext context, Response dto) { object Read() => dto.PlaidLinkCompletionAttempts; }", False),
]

class OwnershipGuardTests(unittest.TestCase):
    pass

def make_test(script, relative, source, reject):
    def check(self):
        with tempfile.TemporaryDirectory(prefix="fullworth-ownership-guard-") as temporary:
            root = Path(temporary)
            for module in MODULES:
                (root / "FullWorth.API" / "Services" / module).mkdir(parents=True)
            (root / "FullWorth.API" / "Controllers").mkdir()
            target = root / "FullWorth.API" / relative
            target.write_text(source, encoding="utf-8")
            result = subprocess.run([sys.executable, "-", str(root)], input=GUARDS[script],
                                    text=True, capture_output=True, timeout=10)
            self.assertEqual(result.returncode, 1 if reject else 0, result.stdout + result.stderr)
            if reject:
                if "Unexpected" in relative:
                    self.assertIn("no module mapping", result.stderr)
                else:
                    self.assertIn("cross-owner", result.stderr)
    return check

for name, script, relative, source, reject in CASES:
    setattr(OwnershipGuardTests, "test_" + name, make_test(script, relative, source, reject))

if __name__ == "__main__":
    unittest.main(verbosity=2)
