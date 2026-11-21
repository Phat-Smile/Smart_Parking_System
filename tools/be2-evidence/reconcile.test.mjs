import { test } from "node:test";
import assert from "node:assert/strict";
import { classifyChange, splitBranches } from "./reconcile.mjs";
test("exact copied file recognized without claiming feature acceptance", () => {
    const r = classifyChange({
        path: "ai/ai-service/file.py",
        baseOid: "old",
        featureOid: "same",
        splitOids: [{ branch: "split", oid: "same" }],
    });
    assert.equal(r.classification, "EXACT_BLOB_IN_SPLIT_BRANCH");
});
test("diverging file cannot be declared duplicate or safe", () => {
    const r = classifyChange({
        path: "ai/ai-service/file.py",
        baseOid: "old",
        featureOid: "a",
        splitOids: [{ branch: "split", oid: "b" }],
    });
    assert.equal(r.classification, "SAME_PATH_DIFFERENT_CONTENT_REVIEW");
});
test("no split ref cannot imply no overlap", () => {
    const r = classifyChange({
        path: "src/Services/Parking/Parking.WebAPI/Endpoints/GateReadings.cs",
        baseOid: "x",
        featureOid: "y",
        splitOids: [],
        missingRefs: ["loc"],
    });
    assert.equal(r.classification, "SPLIT_BRANCH_REF_NOT_AVAILABLE");
});
test("unclaimed path is only review candidate, never auto-cherry-pick", () => {
    const r = classifyChange({
        path: "tools/new/file.mjs",
        baseOid: undefined,
        featureOid: "x",
        splitOids: [],
    });
    assert.equal(r.classification, "REVIEW_NONOVERLAP_BEFORE_PICK");
});
test("four specifically assigned split branch names declared", () => {
    assert.equal(splitBranches.length, 4);
    assert.ok(
        splitBranches.every((x) => x.startsWith("features/Implementation_")),
    );
});
