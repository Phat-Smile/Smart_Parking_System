"use client";

import React, { useState } from "react";
import { PlateBadge, FormattedTime } from "./StaffUi";
import { useStaffData } from "./staffData";
import type { VerificationIssue } from "./types";
import { useLanguage } from "@/shared/i18n/LanguageContext";

export function StaffVerificationPage() {
    const { issues, resolveIssue } = useStaffData();
    const { t, language } = useLanguage();
    const [actionMsg, setActionMsg] = useState("");
    const [resolvingId, setResolvingId] = useState("");

    function handleResolve(issue: VerificationIssue, action: string) {
        setResolvingId(issue.id);
        setTimeout(() => {
            resolveIssue(issue.id);
            setResolvingId("");
            const msg =
                language === "vi"
                    ? `Sự cố #${issue.id} cho xe ${issue.plateNumber} đã được xử lý: ${action}.`
                    : `Issue #${issue.id} for ${issue.plateNumber} resolved: ${action}.`;
            setActionMsg(msg);
            setTimeout(() => setActionMsg(""), 4000);
        }, 500);
    }

    return (
        <div className="staff-page">
            <header className="staff-header">
                <div>
                    <h1 className="staff-title">{t("verifTitle")}</h1>
                    <p className="staff-subtitle">{t("verifSubtitle")}</p>
                </div>
            </header>

            {actionMsg && (
                <div className="staff-alert staff-alert-success">
                    <span>✓ {actionMsg}</span>
                </div>
            )}

            <div className="staff-card staff-table-card">
                <table className="staff-table">
                    <thead>
                        <tr>
                            <th>{t("verifColSpot")}</th>
                            <th>{t("verifColProblem")}</th>
                            <th>{t("verifColDetectedPlate")}</th>
                            <th>{t("verifColConf")}</th>
                            <th>{t("verifColState")}</th>
                            <th>{t("verifColReportedAt")}</th>
                            <th className="text-right">
                                {t("verifColAction")}
                            </th>
                        </tr>
                    </thead>
                    <tbody>
                        {issues.length === 0 ? (
                            <tr>
                                <td colSpan={7} className="staff-empty">
                                    {t("verifEmpty")}
                                </td>
                            </tr>
                        ) : (
                            issues.map((issue) => (
                                <tr key={issue.id}>
                                    <td>
                                        <strong>{issue.spot}</strong>
                                    </td>
                                    <td>
                                        <div className="staff-issue-prob">
                                            <span className="issue-icon">
                                                ⚠️
                                            </span>
                                            <span>{issue.problem}</span>
                                        </div>
                                    </td>
                                    <td>
                                        <PlateBadge
                                            plate={issue.detectedPlate}
                                        />
                                    </td>
                                    <td>
                                        <span
                                            className={`staff-camera-conf ${
                                                issue.confidence >= 0.8
                                                    ? "conf-high"
                                                    : "conf-low"
                                            }`}
                                        >
                                            {(issue.confidence * 100).toFixed(
                                                0,
                                            )}
                                            %
                                        </span>
                                    </td>
                                    <td>
                                        <span className="staff-state-chip">
                                            {issue.currentState}
                                        </span>
                                    </td>
                                    <td>
                                        <FormattedTime
                                            iso={issue.createdAt}
                                            dateTime
                                        />
                                    </td>
                                    <td className="text-right">
                                        <div className="staff-table-btn-group">
                                            <button
                                                type="button"
                                                className="staff-btn staff-btn-primary staff-btn-sm"
                                                disabled={
                                                    resolvingId === issue.id
                                                }
                                                onClick={() =>
                                                    handleResolve(
                                                        issue,
                                                        language === "vi"
                                                            ? "Đã cho phép mở barrier thủ công"
                                                            : "Manual barrier override granted",
                                                    )
                                                }
                                            >
                                                {t("verifApproveBarrier")}
                                            </button>
                                            <button
                                                type="button"
                                                className="staff-btn staff-btn-ghost staff-btn-sm"
                                                disabled={
                                                    resolvingId === issue.id
                                                }
                                                onClick={() =>
                                                    handleResolve(
                                                        issue,
                                                        language === "vi"
                                                            ? "Đã bỏ qua sự cố"
                                                            : "Dismissed as cleared",
                                                    )
                                                }
                                            >
                                                {t("verifDismiss")}
                                            </button>
                                        </div>
                                    </td>
                                </tr>
                            ))
                        )}
                    </tbody>
                </table>
            </div>
        </div>
    );
}
