#!/usr/bin/env node
/** Read-only BE-2 closeout ledger. No fetch, merge, code changes, or secrets. */
import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
export const DEFAULT_MATRIX = path.join(HERE, "closeout-matrix.json");
export function assertMatrix(m) {
    if (
        !m ||
        !Array.isArray(m.tasks) ||
        m.tasks.length !== 23 ||
        !Array.isArray(m.review_paths) ||
        m.review_paths.length !== 16
    )
        throw Error("invalid BE-2 matrix size");
    const keys = m.tasks.map((x) => x.id);
    if (
        new Set(keys).size !== 23 ||
        m.tasks.reduce((s, x) => s + x.story_points, 0) !== 80 ||
        m.tasks.filter((x) => x.sprint !== "Sau OJT").length !== 22
    )
        throw Error("invalid backlog totals");
    if (new Set(m.review_paths.map((x) => x.path)).size !== 16)
        throw Error("duplicate review path");
    if (m.tasks.some((x) => x.claim_done))
        throw Error("unverified task marked done");
    return m;
}
function git(repo, args) {
    const r = spawnSync("git", ["-C", repo, ...args], {
        encoding: "utf8",
        timeout: 15000,
        maxBuffer: 128 * 1024,
        windowsHide: true,
    });
    return r.status === 0 ? (r.stdout || "").trim() : null;
}
export function inspectGit(m, repo, cmd = git) {
    const target =
        repo && cmd(repo, ["rev-parse", "--is-inside-work-tree"]) === "true";
    const ref = (s) => "origin/" + s;
    const mainSha = target ? cmd(repo, ["rev-parse", ref("develop")]) : null;
    const featureSha = target
        ? cmd(repo, ["rev-parse", ref(m.feature_ref)])
        : null;
    const split = Object.fromEntries(
        m.split_refs.map((x) => [
            x,
            target ? cmd(repo, ["rev-parse", ref(x)]) : null,
        ]),
    );
    const paths = m.review_paths.map((x) => {
        const id = `${ref(m.feature_ref)}:${x.path}`;
        const bFeature = featureSha && cmd(repo, ["rev-parse", id]);
        const bDevelop =
            mainSha && cmd(repo, ["rev-parse", `${ref("develop")}:${x.path}`]);
        const exact = Object.entries(split)
            .filter(
                ([k, v]) =>
                    v &&
                    bFeature &&
                    cmd(repo, ["rev-parse", `${ref(k)}:${x.path}`]) ===
                        bFeature,
            )
            .map(([k]) => k);
        let state = !target
            ? "NO_GIT_REPOSITORY"
            : !featureSha
              ? "FEATURE_REF_MISSING"
              : !mainSha
                ? "DEVELOP_REF_MISSING"
                : !bFeature
                  ? "NOT_IN_INTEGRATED_REF"
                  : bDevelop === bFeature
                    ? "SAME_AS_DEVELOP"
                    : exact.length
                      ? "SAME_AS_SPLIT_BRANCH"
                      : m.split_refs.some((s) => !split[s])
                        ? "REVIEW_INCOMPLETE_SPLIT_REFS"
                        : "NEEDS_SEMANTIC_REVIEW";
        return {
            path: x.path,
            previous: x.prior_classification,
            decision: state,
            exact_branch_refs: exact,
            needs_owner_signoff: ![
                "SAME_AS_DEVELOP",
                "SAME_AS_SPLIT_BRANCH",
            ].includes(state),
        };
    });
    const mergeCount =
        featureSha && mainSha
            ? cmd(repo, [
                  "rev-list",
                  "--count",
                  "--merges",
                  `${ref("develop")}..${ref(m.feature_ref)}`,
              ])
            : null;
    return {
        repository_checked: Boolean(target),
        develop_sha: mainSha,
        integrated_sha: featureSha,
        split_refs: split,
        merge_commits_in_feature:
            mergeCount === null ? null : Number(mergeCount),
        paths,
    };
}
export function calculate(m, gitResult) {
    const sum = {
        all_tasks: m.tasks.length,
        ojt_tasks: m.tasks.filter((t) => t.sprint !== "Sau OJT").length,
        ojt_sp: m.tasks
            .filter((t) => t.sprint !== "Sau OJT")
            .reduce((s, t) => s + t.story_points, 0),
        all_sp: m.tasks.reduce((s, t) => s + t.story_points, 0),
        by_decision: {},
    };
    m.tasks.forEach(
        (t) =>
            (sum.by_decision[t.decision] =
                (sum.by_decision[t.decision] || 0) + 1),
    );
    const unresolved = gitResult.paths.filter(
        (x) => x.needs_owner_signoff,
    ).length;
    return {
        created_at: new Date().toISOString(),
        source_date: m.metadata.date,
        assessment: "NOT_ACCEPTED_END_TO_END",
        summary: sum,
        review_paths_unresolved: unresolved,
        git: gitResult,
        tasks: m.tasks,
        cross_service_gaps: m.cross_service_gaps,
        evidence_snapshot: m.evidence_snapshot,
        limitations: [
            "Historical backlog statuses and previous local Git logs are NOT live PR/CI/acceptance evidence.",
            "There is no verified real camera dataset or model field accuracy evidence.",
            "No real QA load/performance test or MF-01..13 Business E2E was executed.",
            "Automatic blob equality does not prove that a behavior is merged or correct.",
        ],
    };
}
export function toMarkdown(report) {
    const { summary: s, git: g } = report;
    const esc = (v) =>
        String(v ?? "—")
            .replaceAll("|", "\\|")
            .replaceAll("\n", " ");
    const rows = report.tasks
        .map(
            (t) =>
                `| ${esc(t.id)} | ${esc(t.assigned_owner)} | ${esc(t.decision)} | ${esc(t.immediate_action)} | ${esc(t.acceptance_evidence)} |`,
        )
        .join("\n");
    const files = g.paths
        .map(
            (p) =>
                `| \`${esc(p.path)}\` | ${p.decision} | ${esc(p.exact_branch_refs.join(", ") || "—")} | ${p.needs_owner_signoff ? "YES" : "NO"} |`,
        )
        .join("\n");
    const blockers = report.cross_service_gaps
        .map(
            (x) =>
                `| ${esc(x.name)} | ${esc(x.owner)} | ${esc(x.action)} | ${esc(x.acceptance)} |`,
        )
        .join("\n");
    const old = report.evidence_snapshot
        .map((x) => `| ${esc(x.name)} | ${esc(x.result)} | ${esc(x.note)} |`)
        .join("\n");
    return `# BE-2: complete closeout and ownership ledger\n\nGenerated: ${report.created_at}. Source ownership snapshot: ${report.source_date}. **Status: NOT ACCEPTED END TO END.**\n\n## Totals and constraints\n\n- OJT: **${s.ojt_tasks} tasks / ${s.ojt_sp} SP**; all including post-OJT: **${s.all_tasks} / ${s.all_sp} SP**.\n- Previously known integrated change: 52 paths (36 exact split blob matches, six divergent and ten not identical) from an earlier Git audit. This run rechecks the **16 unsettled paths**, not the historical 52.\n- Open review paths in this run: **${report.review_paths_unresolved}/16**.\n- Inspected develop: ${g.develop_sha || "UNAVAILABLE"}; old integrated: ${g.integrated_sha || "UNAVAILABLE"}.\n- Feature-only merge commit count: ${g.merge_commits_in_feature ?? "UNKNOWN"}.\n- **No source, migrations, data, Docker, CI or remote PR was changed by this audit.**\n\n## Every BE-2 backlog task (verified plan, NOT a completion claim)\n\n| ID | Current ownership | Decision | Work to do / avoid duplication | Exit evidence |\n|---|---|---|---|---|\n${rows}\n\n## All 16 potentially unsettled integrated files\n\n| Path | Live Git result | Identical split ref | Needs owner decision |\n|---|---|---|---|\n${files}\n\nNOTE: if any split ref is unavailable, classification remains provisional. File-level equality is not functional acceptance. Do not cherry-pick any of these 16 automatically.\n\n## Cross-service gaps without clear owner\n\n| Gap | Owner status | Action now | Definition of done |\n|---|---|---|---|\n${blockers}\n\n## Evidence on file (historical, not CI)\n\n| Item | Evidence status | Limits |\n|---|---|---|\n${old}\n\n## Non-negotiable review gates\n\n1. Confirm each of 23 task rows against current develop and the relevant owner. Historical Excel Xong is not acceptance.\n2. Preserve Lộc's four #329 split branches and #332/#333/#334, Bách's Payment RefundRequests and Đình Anh's owner editor. Do not rewrite their files.\n3. For US-AI-004, require **real anonymized labeled camera data**, session/vehicle-disjoint splits, near-duplicate evidence and class/site/lighting accuracy + coverage gates signed by Lead; use manual review on abstentions.\n4. For TECH-14, collect actual approved-load realtime and AI p95 from isolated QA, not this workstation; do not equate smoke samples with NFR.\n5. For TECH-15, collect Lộc's real MF-01..13 flow (booking, arrival, exit, settlement). No new duplicate E2E work.\n6. For Pricing, await BE-5 block/settlement rule before changing monetary calculations.\n7. No Jenkins, Docker, container, .NET or camera training is started by the offline one-click. Team-required CI and squash merge remain an external review gate, not a request to run Jenkins locally.\n`;
}
function arg(flag, def) {
    const k = process.argv.indexOf(flag);
    return k < 0 ? def : process.argv[k + 1];
}
if (
    process.argv[1] &&
    path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)
) {
    try {
        const m = assertMatrix(
            JSON.parse(
                fs.readFileSync(arg("--matrix", DEFAULT_MATRIX), "utf8"),
            ),
        );
        const repo = arg("--repo", "");
        const report = calculate(m, inspectGit(m, repo));
        const out = path.resolve(
            arg("--out", path.join(process.cwd(), "BE2_CLOSEOUT_REPORT")),
        );
        fs.mkdirSync(out, { recursive: true });
        fs.writeFileSync(
            path.join(out, "BE2_CLOSEOUT.json"),
            JSON.stringify(report, null, 2) + "\n",
        );
        fs.writeFileSync(path.join(out, "BE2_CLOSEOUT.md"), toMarkdown(report));
        console.log(
            `BE-2 report: ${report.summary.ojt_tasks} OJT tasks / ${report.summary.ojt_sp} SP; ${report.review_paths_unresolved}/16 paths require review; report=${out}`,
        );
    } catch (e) {
        console.error(`AUDIT_FAIL: ${e.message}`);
        process.exitCode = 2;
    }
}
