#!/usr/bin/env node
// Read-only traceability report. Presence of files is NOT acceptance evidence.
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
export const catalogPath = path.join(here, "be2-backlog-20261120.json");
export function summarize(items, exists = () => false) {
    if (!Array.isArray(items) || !items.length)
        throw new Error("BE-2 backlog items missing");
    const seen = new Set();
    const mapped = items.map((item) => {
        if (!item.id || seen.has(item.id))
            throw new Error("Invalid/duplicate backlog id");
        seen.add(item.id);
        const candidates = (item.candidate_paths || []).map((p) => ({
            path: p,
            exists: exists(p),
        }));
        const boundary = item.current_owner_boundary;
        const action =
            boundary === "UNASSIGNED"
                ? "PROPOSE_BE2_OWNERSHIP"
                : boundary === "POST_OJT"
                  ? "DEFER_AFTER_OJT"
                  : boundary === "LOC-329" || boundary === "LOC-E2E"
                    ? "DELEGATED_DO_NOT_EDIT"
                    : boundary === "COORDINATE"
                      ? "COORDINATE_BEFORE_CHANGE"
                      : "VERIFY_MERGE_AND_DOD";
        return {
            id: item.id,
            title: item.title,
            sp: item.story_points,
            sprint: item.sprint,
            excel_status: item.source_status_202610,
            ownership: boundary,
            action,
            fr: item.fr,
            file_candidates: candidates,
            evidence_status: "NOT_VERIFIED",
            source_excel_row: item.source_excel_row,
        };
    });
    return {
        source: "BE_V4 (1).xlsx Backlog + Context.docx; ownership file dated 2026-11-20",
        disclaimer:
            "These are static candidates, NOT evidence that any task meets acceptance criteria or Jenkins CI.",
        tasks: mapped.length,
        sp: mapped.reduce((a, r) => a + (r.sp || 0), 0),
        items: mapped,
    };
}
function mdCell(v) {
    return String(v ?? "")
        .replaceAll("|", "\\|")
        .replaceAll("\n", " ");
}
export function toMarkdown(report) {
    const lines = [
        "# BE-2 FR/backlog traceability — preliminary evidence",
        "",
        `Tasks: ${report.tasks}; story points: ${report.sp}.`,
        "**WARNING:** File existence is not functional acceptance, and historical Excel status is not current PR status.",
        "",
        "| ID | Sprint | SP | Historical status | Ownership/action | Candidate paths |",
        "|---|---|---:|---|---|---|",
    ];
    for (const item of report.items) {
        lines.push(
            `| ${mdCell(item.id)} | ${mdCell(item.sprint)} | ${mdCell(item.sp)} | ${mdCell(item.excel_status)} | ${mdCell(item.action)} | ${item.file_candidates.map((c) => `\`${mdCell(c.path)}\`: ${c.exists ? "exists" : "not seen"}`).join("; ") || "manual audit"} |`,
        );
    }
    lines.push(
        "",
        "## Meaning of action flags",
        "",
        "- `DELEGATED_DO_NOT_EDIT`: Lộc is taking this task in #329 split PRs or final E2E.",
        "- `VERIFY_MERGE_AND_DOD`: already marked done/review in the historical backlog; check new develop and Jenkins before claiming accepted.",
        "- `PROPOSE_BE2_OWNERSHIP`: candidate unclaimed from 20/11 team assignment, not a formal Lead approval.",
        "- `COORDINATE_BEFORE_CHANGE`: shared area; do not change teammates’ paths.",
        "- `DEFER_AFTER_OJT`: optional LightGBM comparison after OJT.",
        "",
        "Do not auto-modify any path based on this report.",
    );
    return lines.join("\n") + "\n";
}
export function run(root, outDir) {
    const manifest = JSON.parse(fs.readFileSync(catalogPath, "utf8"));
    const report = summarize(manifest.items, (p) =>
        fs.existsSync(path.join(root, p)),
    );
    fs.mkdirSync(outDir, { recursive: true });
    fs.writeFileSync(
        path.join(outDir, "BE2_FR_AUDIT.json"),
        JSON.stringify(report, null, 2) + "\n",
    );
    fs.writeFileSync(path.join(outDir, "BE2_FR_AUDIT.md"), toMarkdown(report));
    console.log(
        `BE2_FR_AUDIT: ${report.tasks} tasks/${report.sp} SP; acceptance remains unverified; report files written`,
    );
    return report;
}
if (
    process.argv[1] &&
    fileURLToPath(import.meta.url) === path.resolve(process.argv[1])
) {
    const args = process.argv.slice(2);
    if (args.length !== 2) {
        console.error("Usage: node fr-audit.mjs <repo-root> <output-dir>");
        process.exitCode = 2;
    } else
        try {
            run(args[0], args[1]);
        } catch (e) {
            console.error(e.message);
            process.exitCode = 2;
        }
}
