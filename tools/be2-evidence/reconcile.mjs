#!/usr/bin/env node
// Read-only exact Git tree comparison. Never rewrites branches or modifies source.
import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

export const splitBranches = [
    "features/Implementation_AiInsufficientDataRanking",
    "features/Implementation_GateArrivalConfirmation",
    "features/Implementation_SlotSensorPricingPrivacy",
    "features/Implementation_CustomerDashboardGps",
];
function git(root, ...args) {
    const r = spawnSync("git", ["-C", root, ...args], {
        encoding: "utf8",
        maxBuffer: 10 * 1024 * 1024,
    });
    if (r.status !== 0)
        throw new Error(
            `Git command failed (${args[0]}): ${String(r.stderr).slice(0, 120)}`,
        );
    return r.stdout;
}
function hasGitRef(root, ref) {
    return (
        spawnSync(
            "git",
            ["-C", root, "rev-parse", "--verify", "--quiet", ref + "^{commit}"],
            { stdio: "ignore" },
        ).status === 0
    );
}
function gitTree(root, ref) {
    const raw = git(root, "ls-tree", "-r", "-z", ref);
    const map = new Map();
    for (const token of raw.split("\0")) {
        if (!token) continue;
        const tab = token.indexOf("\t");
        if (tab < 0) continue;
        const match = /^\d+\s+blob\s+([a-f0-9]{40,64})$/.exec(
            token.slice(0, tab),
        );
        if (match) map.set(token.slice(tab + 1), match[1]);
    }
    return map;
}
export function classifyChange({
    path,
    baseOid,
    featureOid,
    splitOids,
    missingRefs = [],
}) {
    const exact = splitOids
        .filter((x) => x.oid && x.oid === featureOid && x.oid !== baseOid)
        .map((x) => x.branch);
    const divergent = splitOids
        .filter((x) => x.oid && x.oid !== baseOid && x.oid !== featureOid)
        .map((x) => x.branch);
    const assigned =
        /(^ai\/ai-service\/|\/GateReadings\/|\/ProcessSlotSensorEvent\/|\/Recommendations\/|^web\/src\/features\/customer\/CustomerDashboardPage|^src\/Services\/Parking\/Parking\.WebAPI\/(?:Endpoints\/PricingEndpoints|Security\/LocationResponsePrivacyMiddleware))/.test(
            path,
        );
    return {
        path,
        classification: exact.length
            ? "EXACT_BLOB_IN_SPLIT_BRANCH"
            : divergent.length
              ? "SAME_PATH_DIFFERENT_CONTENT_REVIEW"
              : missingRefs.length
                ? "SPLIT_BRANCH_REF_NOT_AVAILABLE"
                : assigned
                  ? "OWNER_RESERVED_REVIEW"
                  : "REVIEW_NONOVERLAP_BEFORE_PICK",
        matches: exact,
        otherChanges: divergent,
        missingRefs,
    };
}
export function reconcile(root, output) {
    const base = "origin/develop",
        feature = "origin/features/BE2_integrated_20261120";
    if (!hasGitRef(root, base) || !hasGitRef(root, feature))
        throw new Error(
            "Missing remote refs: fetch develop and feature branch before comparing",
        );
    const baseTree = gitTree(root, base),
        featureTree = gitTree(root, feature);
    const splitTrees = [],
        missing = [];
    for (const name of splitBranches) {
        const ref = "origin/" + name;
        if (hasGitRef(root, ref))
            splitTrees.push({ branch: name, tree: gitTree(root, ref) });
        else missing.push(name);
    }
    const changed = [...new Set([...baseTree.keys(), ...featureTree.keys()])]
        .filter((p) => baseTree.get(p) !== featureTree.get(p))
        .sort();
    const rows = changed.map((p) =>
        classifyChange({
            path: p,
            baseOid: baseTree.get(p),
            featureOid: featureTree.get(p),
            splitOids: splitTrees.map((x) => ({
                branch: x.branch,
                oid: x.tree.get(p),
            })),
            missingRefs: missing,
        }),
    );
    const mergeCount = git(
        root,
        "rev-list",
        "--merges",
        "--count",
        base + ".." + feature,
    ).trim();
    const report = {
        base,
        feature,
        split_refs_checked: splitTrees.map((x) => x.branch),
        missing_split_refs: missing,
        feature_only_merges: Number(mergeCount),
        total_changed: rows.length,
        note: "Exact blob matches are NOT proof full task acceptance; missing refs must never be treated as no overlap.",
        prohibited_auto_merge: true,
        paths: rows,
    };
    fs.mkdirSync(output, { recursive: true });
    fs.writeFileSync(
        path.join(output, "BE2_SPLIT_COMPARE.json"),
        JSON.stringify(report, null, 2) + "\n",
    );
    const lines = [
        "# Exact Git tree compare (read-only)",
        "",
        `Base: ${base}; Feature: ${feature}; changed paths: ${rows.length}; feature merges: ${mergeCount}.`,
        `Available split branches: ${splitTrees.length}/4.`,
        missing.length
            ? `Unavailable: ${missing.join(", ")}`
            : "All four split refs available.",
        "",
        "| Path | Category | Matching branches |",
        "|---|---|---|",
        ...rows.map(
            (r) =>
                `| \`${r.path.replaceAll("|", "\\|")}\` | ${r.classification} | ${r.matches.join(", ") || "—"} |`,
        ),
        "",
        "**No automatic cherry-pick, rebase, merge, or write is authorized by this comparison.**",
    ];
    fs.writeFileSync(
        path.join(output, "BE2_SPLIT_COMPARE.md"),
        lines.join("\n") + "\n",
    );
    console.log(
        `BE2_SPLIT_COMPARE: ${rows.length} paths; ${missing.length} split refs unavailable; no source modified`,
    );
    return report;
}
if (
    process.argv[1] &&
    fileURLToPath(import.meta.url) === path.resolve(process.argv[1])
) {
    const a = process.argv.slice(2);
    if (a.length !== 2) {
        console.error("Usage: node reconcile.mjs <repo-root> <output-dir>");
        process.exitCode = 2;
    } else
        try {
            reconcile(a[0], a[1]);
        } catch (e) {
            console.error("SPLIT_COMPARE_BLOCKED: " + e.message);
            process.exitCode = 2;
        }
}
