"use client";

import React, { useState } from "react";
import { PlateBadge, FormattedTime } from "./StaffUi";
import { useStaffData } from "./staffData";
import type { VerificationIssue } from "./types";

export function StaffVerificationPage() {
    const { issues, resolveIssue } = useStaffData();
    const [actionMsg, setActionMsg] = useState("");
    const [resolvingId, setResolvingId] = useState("");

    function handleResolve(issue: VerificationIssue, action: string) {
        setResolvingId(issue.id);
        setTimeout(() => {
            resolveIssue(issue.id);
            setResolvingId("");
            setActionMsg(
                `Issue #${issue.id} for ${issue.plateNumber} resolved: ${action}.`,
            );
            setTimeout(() => setActionMsg(""), 4000);
        }, 500);
    }

    return (
        <div className="staff-page">
            <header className="staff-header">
                <div>
                    <h1 className="staff-title">3.13 Needs Verification Queue</h1>
                    <p className="staff-subtitle">
                        Exceptional gate cases requiring manual operator confirmation, OCR overrides, and barrier interventions.
                    </p>
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
                            <th>Spot / Gate</th>
                            <th>Problem</th>
                            <th>Detected Plate</th>
                            <th>OCR Conf.</th>
                            <th>Current State</th>
                            <th>Reported At</th>
                            <th className="text-right">Verification Action</th>
                        </tr>
                    </thead>
                    <tbody>
                        {issues.length === 0 ? (
                            <tr>
                                <td colSpan={7} className="staff-empty">
                                    🎉 No issues currently pending operator verification!
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
                                            <span className="issue-icon">⚠️</span>
                                            <span>{issue.problem}</span>
                                        </div>
                                    </td>
                                    <td>
                                        <PlateBadge plate={issue.detectedPlate} />
                                    </td>
                                    <td>
                                        <span
                                            className={`staff-camera-conf ${
                                                issue.confidence >= 0.8
                                                    ? "conf-high"
                                                    : "conf-low"
                                            }`}
                                        >
                                            {(issue.confidence * 100).toFixed(0)}%
                                        </span>
                                    </td>
                                    <td>
                                        <span className="staff-state-chip">
                                            {issue.currentState}
                                        </span>
                                    </td>
                                    <td>
                                        <FormattedTime iso={issue.createdAt} dateTime />
                                    </td>
                                    <td className="text-right">
                                        <div className="staff-table-btn-group">
                                            <button
                                                type="button"
                                                className="staff-btn staff-btn-primary staff-btn-sm"
                                                disabled={resolvingId === issue.id}
                                                onClick={() =>
                                                    handleResolve(
                                                        issue,
                                                        "Manual barrier override granted",
                                                    )
                                                }
                                            >
                                                Approve Barrier
                                            </button>
                                            <button
                                                type="button"
                                                className="staff-btn staff-btn-ghost staff-btn-sm"
                                                disabled={resolvingId === issue.id}
                                                onClick={() =>
                                                    handleResolve(
                                                        issue,
                                                        "Dismissed as cleared",
                                                    )
                                                }
                                            >
                                                Dismiss
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
