import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import {
    DEFAULT_MATRIX,
    assertMatrix,
    inspectGit,
    calculate,
    toMarkdown,
} from "./audit.mjs";
const read = () => JSON.parse(fs.readFileSync(DEFAULT_MATRIX, "utf8"));
test("all 23 BE2 tasks 80 SP and 22 OJT 72 SP", () => {
    const m = assertMatrix(read());
    const s = calculate(m, inspectGit(m, "")).summary;
    assert.equal(s.ojt_tasks, 22);
    assert.equal(s.ojt_sp, 72);
    assert.equal(s.all_sp, 80);
});
test("do not mark historical Xong as accepted", () => {
    assert.ok(read().tasks.every((t) => t.claim_done === false));
});
test("exactly sixteen unsettled paths and four split refs", () => {
    const m = assertMatrix(read());
    assert.equal(m.review_paths.length, 16);
    assert.equal(m.split_refs.length, 4);
});
test("reject bad backlog counts", () => {
    const m = read();
    m.tasks.pop();
    assert.throws(() => assertMatrix(m));
});
test("missing repo remains BLOCKED, not nonoverlap", () => {
    const m = assertMatrix(read());
    const r = inspectGit(m, "/does-not-exist", (r, a) => null);
    assert.equal(r.paths[0].decision, "NO_GIT_REPOSITORY");
    assert.ok(r.paths.every((p) => p.needs_owner_signoff));
});
test("matching split git blob is not flagged as missing functionality", () => {
    const m = assertMatrix(read());
    const p = m.review_paths[0].path;
    const split = m.split_refs[0];
    const cb = (r, args) => {
        const str = args.join(" ");
        if (str.includes("--is-inside-work-tree")) return "true";
        if (str === `rev-parse origin/${m.feature_ref}:${p}`) return "same";
        if (str === `rev-parse origin/${split}:${p}`) return "same";
        if (str.startsWith("rev-parse origin/")) return "sha";
        return "0";
    };
    const report = inspectGit(m, "/fake", cb);
    assert.equal(report.paths[0].decision, "SAME_AS_SPLIT_BRANCH");
});
test("different split blob requires owner signoff", () => {
    const m = assertMatrix(read());
    const cb = (r, args) => {
        if (args.includes("--is-inside-work-tree")) return "true";
        if (args[0] === "rev-parse") {
            const a = args.at(-1);
            return a.includes(":")
                ? a.includes("BE2_integrated")
                    ? "blob-feature"
                    : "blob-other"
                : "sha";
        }
        return "0";
    };
    const report = inspectGit(m, "/fake", cb);
    assert.equal(report.paths[0].decision, "NEEDS_SEMANTIC_REVIEW");
});
test("render full task matrix and blockers", () => {
    const m = assertMatrix(read());
    const report = calculate(m, inspectGit(m, ""));
    const md = toMarkdown(report);
    assert.ok(md.includes("US-AI-004"));
    assert.ok(md.includes("TECH-15"));
    assert.ok(md.includes("BE-5"));
    assert.ok(md.includes("NOT ACCEPTED"));
});
