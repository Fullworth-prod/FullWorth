import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

// Tests only the shipped browser helper against synthetic session storage.
// No uploads, credentials, documents, or remote services are involved.
export async function verifyStatementUploadResumeModule() {
  const source = await readFile(
    new URL("../FullWorth.Web/wwwroot/js/bff.js", import.meta.url), "utf8");
  const module = await import("data:text/javascript;base64," +
    Buffer.from(source.replace(
      'import { confirmAction } from "./confirmation-dialog.js";',
      'const confirmAction = () => false;')).toString("base64"));

  const previous = globalThis.sessionStorage;
  const values = new Map();
  let blocked = false;
  globalThis.sessionStorage = {
    setItem(key, value) {
      if (blocked) throw Error("storage blocked");
      values.set(key, value);
    },
    getItem(key) {
      if (blocked) throw Error("storage blocked");
      return values.get(key) ?? null;
    },
    removeItem(key) {
      if (blocked) throw Error("storage blocked");
      values.delete(key);
    }
  };

  const bill = "1a111111-1111-4111-8111-111111111111";
  const upload = "2b222222-2222-4222-8222-222222222222";
  try {
    assert.equal(module.getPendingStatementUploadId(bill), null);
    assert.equal(module.rememberPendingStatementUpload(bill, upload), true);
    assert.equal(module.getPendingStatementUploadId(bill), upload);

    for (const invalid of ["", null, "another-user", "../bad", "000", 23]) {
      assert.equal(module.rememberPendingStatementUpload(invalid, upload), false);
      assert.equal(module.rememberPendingStatementUpload(bill, invalid), false);
      assert.equal(module.getPendingStatementUploadId(invalid), null);
      assert.equal(module.forgetPendingStatementUpload(invalid), false);
    }

    const otherBill = "3c333333-3333-4333-8333-333333333333";
    assert.equal(module.getPendingStatementUploadId(otherBill), null);
    assert.equal(module.forgetPendingStatementUpload(otherBill), true);
    assert.equal(module.getPendingStatementUploadId(bill), upload,
      "Finishing another bill must not erase this bill's pending status.");

    blocked = true;
    assert.equal(module.getPendingStatementUploadId(bill), null);
    assert.equal(module.rememberPendingStatementUpload(bill, upload), false);
    assert.equal(module.forgetPendingStatementUpload(bill), false);

    blocked = false;
    assert.equal(module.getPendingStatementUploadId(bill), upload);
    assert.equal(module.forgetPendingStatementUpload(bill), true);
    assert.equal(module.getPendingStatementUploadId(bill), null);
    assert.equal(values.size, 0, "No upload identifiers remain in storage.");
    console.log("Statement upload status resume: opaque-ID storage, isolation, blocking and clearing passed.");
  } finally {
    if (previous === undefined) delete globalThis.sessionStorage;
    else globalThis.sessionStorage = previous;
  }
}
