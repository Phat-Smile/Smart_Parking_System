"use client";

import React, { useState } from "react";
import { PlateBadge, FormattedTime } from "./StaffUi";
import { useStaffData } from "./staffData";
import type { VerificationIssue } from "./types";
import { useLanguage } from "@/shared/i18n/LanguageContext";

function getSpotText(issue: VerificationIssue, isVi: boolean): string {
    if (isVi) {
        if (issue.spotVi) return issue.spotVi;
        return issue.spot.replace("Gate In", "Cổng Vào").replace("Gate Out", "Cổng Ra");
    }
    return issue.spotEn || issue.spot;
}

function getProblemText(issue: VerificationIssue, isVi: boolean): string {
    if (isVi) {
        if (issue.problemVi) return issue.problemVi;
        if (issue.problem.includes("Low Camera OCR Confidence")) {
            return issue.problem.replace("Low Camera OCR Confidence", "Độ tin cậy OCR camera thấp");
        }
        if (issue.problem.includes("QR Damaged / Unreadable on Guest Phone")) {
            return "Mã QR mờ / hỏng trên điện thoại khách";
        }
    }
    return issue.problemEn || issue.problem;
}

function getStateText(issue: VerificationIssue, isVi: boolean): string {
    if (isVi) {
        if (issue.currentStateVi) return issue.currentStateVi;
        if (issue.currentState.includes("Waiting for staff confirmation")) return "Chờ nhân viên xác nhận";
        if (issue.currentState.includes("Vehicle waiting at barrier")) return "Xe chờ tại rào chắn";
    }
    return issue.currentStateEn || issue.currentState;
}

export function StaffVerificationPage() {
    const { issues, resolveIssue } = useStaffData();
    const { t, language } = useLanguage();
    const isVi = language === "vi";
    const [actionMsg, setActionMsg] = useState("");
    const [resolvingId, setResolvingId] = useState("");

    function handleResolve(issue: VerificationIssue, action: string) {
        setResolvingId(issue.id);
        setTimeout(() => {
            resolveIssue(issue.id);
            setResolvingId("");
            const msg = isVi
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

            <div className="staff-card staff-table-card staff-verif-card">
                <table className="staff-table staff-table-verif">
                    <thead>
                        <tr>
                            <th style={{ width: "120px" }}>{t("verifColSpot")}</th>
                            <th>{t("verifColProblem")}</th>
                            <th style={{ width: "140px" }}>{t("verifColDetectedPlate")}</th>
                            <th style={{ width: "85px" }}>{t("verifColConf")}</th>
                            <th style={{ width: "190px" }}>{t("verifColState")}</th>
                            <th style={{ width: "150px" }}>{t("verifColReportedAt")}</th>
                            <th className="text-right" style={{ width: "180px" }}>
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
                                        <span className="staff-gate-badge">
                                            {getSpotText(issue, isVi)}
                                        </span>
                                    </td>
                                    <td className="cell-wrap">
                                        <div className="staff-issue-prob">
                                            <span className="issue-icon" aria-hidden="true">
                                                ⚠️
                                            </span>
                                            <span>{getProblemText(issue, isVi)}</span>
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
                                    <td className="cell-wrap">
                                        <span className="staff-state-chip">
                                            <span className="staff-pulse-dot amber" />
                                            {getStateText(issue, isVi)}
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
                                                        isVi
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
                                                        isVi
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
