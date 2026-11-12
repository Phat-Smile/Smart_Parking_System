"use client";

import React, { useEffect, useState } from "react";
import { RefreshCw } from "lucide-react";
import { PlateBadge, FormattedTime } from "./StaffUi";
import { useStaffData } from "./staffData";
import {
    confirmGateReading,
    describeStaffError,
    listPendingGateReadings,
} from "./staffApi";
import type { VerificationIssue } from "./types";
import { useLanguage } from "@/shared/i18n/LanguageContext";

function getSpotText(issue: VerificationIssue, isVi: boolean): string {
    if (isVi) {
        if (issue.spotVi) return issue.spotVi;
        return issue.spot
            .replace("Gate In", "Cổng Vào")
            .replace("Gate Out", "Cổng Ra");
    }
    return issue.spotEn || issue.spot;
}

function getProblemText(issue: VerificationIssue, isVi: boolean): string {
    if (isVi) {
        if (issue.problemVi) return issue.problemVi;
        if (issue.problem.includes("Low Camera OCR Confidence")) {
            return issue.problem.replace(
                "Low Camera OCR Confidence",
                "Độ tin cậy OCR camera thấp",
            );
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
        if (issue.currentState.includes("Waiting for staff confirmation"))
            return "Chờ nhân viên xác nhận";
        if (issue.currentState.includes("Vehicle waiting at barrier"))
            return "Xe chờ tại rào chắn";
    }
    return issue.currentStateEn || issue.currentState;
}

export function StaffVerificationPage() {
    const { issues, resolveIssue, assignedLot } = useStaffData();
    const { t, language } = useLanguage();
    const isVi = language === "vi";
    const [actionMsg, setActionMsg] = useState("");
    const [errorMsg, setErrorMsg] = useState("");
    const [resolvingId, setResolvingId] = useState("");
    const [backendIssues, setBackendIssues] = useState<
        VerificationIssue[] | null
    >(null);
    const [isRefreshing, setIsRefreshing] = useState(false);

    const loadPendingReadings = React.useCallback(async (lotId: string) => {
        try {
            const data = await listPendingGateReadings(lotId);
            if (Array.isArray(data)) {
                const mapped: VerificationIssue[] = data.map((item) => ({
                    id: item.incidentId,
                    spot: "Gate In 01",
                    spotVi: "Cổng Vào 01 (Camera ANPR)",
                    spotEn: "Gate In 01 (ANPR Camera)",
                    plateNumber: item.observedPlate || "CHƯA RÕ",
                    problem: `Low Camera OCR Confidence: ${(item.confidence * 100).toFixed(0)}%`,
                    problemVi: `Độ tin cậy OCR thấp (${(item.confidence * 100).toFixed(0)}%)${!item.plateFormatValid ? " · Định dạng chưa chuẩn VN" : ""}`,
                    detectedPlate: item.observedPlate || "CHƯA RÕ",
                    confidence: Number(item.confidence),
                    currentState: "Waiting for staff confirmation",
                    currentStateVi: "Chờ nhân viên xác nhận biển số",
                    createdAt: item.occurredAt,
                    status: "Pending",
                }));
                setBackendIssues(mapped);
            }
        } catch {
            // fallback to local issues
        }
    }, []);

    useEffect(() => {
        let active = true;
        if (assignedLot?.lotId) {
            listPendingGateReadings(assignedLot.lotId)
                .then((data) => {
                    if (!active || !Array.isArray(data)) return;
                    const mapped: VerificationIssue[] = data.map((item) => ({
                        id: item.incidentId,
                        spot: "Gate In 01",
                        spotVi: "Cổng Vào 01 (Camera ANPR)",
                        spotEn: "Gate In 01 (ANPR Camera)",
                        plateNumber: item.observedPlate || "CHƯA RÕ",
                        problem: `Low Camera OCR Confidence: ${(item.confidence * 100).toFixed(0)}%`,
                        problemVi: `Độ tin cậy OCR thấp (${(item.confidence * 100).toFixed(0)}%)${!item.plateFormatValid ? " · Định dạng chưa chuẩn VN" : ""}`,
                        detectedPlate: item.observedPlate || "CHƯA RÕ",
                        confidence: Number(item.confidence),
                        currentState: "Waiting for staff confirmation",
                        currentStateVi: "Chờ nhân viên xác nhận biển số",
                        createdAt: item.occurredAt,
                        status: "Pending",
                    }));
                    setBackendIssues(mapped);
                })
                .catch(() => {});
        }
        return () => {
            active = false;
        };
    }, [assignedLot?.lotId]);

    async function handleRefresh() {
        if (!assignedLot?.lotId) return;
        setIsRefreshing(true);
        setErrorMsg("");
        try {
            await loadPendingReadings(assignedLot.lotId);
        } catch (err) {
            setErrorMsg(describeStaffError(err));
        } finally {
            setIsRefreshing(false);
        }
    }

    async function handleResolve(issue: VerificationIssue, action: string) {
        setResolvingId(issue.id);
        setErrorMsg("");
        setActionMsg("");

        try {
            // 100% Real API: Nếu là sự cố từ Backend Gate Readings thì gọi confirmGateReading
            const isBackendItem = backendIssues?.some((b) => b.id === issue.id);
            if (isBackendItem) {
                const plateToConfirm =
                    issue.detectedPlate && issue.detectedPlate !== "CHƯA RÕ"
                        ? issue.detectedPlate
                        : issue.plateNumber && issue.plateNumber !== "CHƯA RÕ"
                          ? issue.plateNumber
                          : "";
                await confirmGateReading(issue.id, plateToConfirm, "Car");
                if (assignedLot?.lotId) {
                    await loadPendingReadings(assignedLot.lotId);
                }
            } else {
                resolveIssue(issue.id);
            }

            const msg = isVi
                ? `Biển số ${issue.detectedPlate || issue.plateNumber} đã được xác nhận thành công: ${action}.`
                : `Issue #${issue.id} for ${issue.plateNumber} resolved: ${action}.`;
            setActionMsg(msg);
            setTimeout(() => setActionMsg(""), 5000);
        } catch (err) {
            setErrorMsg(describeStaffError(err));
        } finally {
            setResolvingId("");
        }
    }

    const displayedIssues = backendIssues ?? issues;

    return (
        <div className="staff-page">
            <header className="staff-header">
                <div>
                    <h1 className="staff-title">{t("verifTitle")}</h1>
                    <p className="staff-subtitle">
                        {assignedLot?.lotName
                            ? `Bãi trực: ${assignedLot.lotName} · `
                            : ""}
                        {t("verifSubtitle")}
                    </p>
                </div>
                <div className="staff-header-actions">
                    <button
                        type="button"
                        className="staff-btn staff-btn-secondary"
                        onClick={handleRefresh}
                        disabled={isRefreshing}
                        title="Đồng bộ lại danh sách OCR từ CSDL"
                    >
                        <RefreshCw
                            size={14}
                            strokeWidth={2.2}
                            className={isRefreshing ? "staff-spin" : ""}
                        />
                        <span>
                            {isRefreshing
                                ? "Đang tải CSDL..."
                                : "Làm mới từ CSDL"}
                        </span>
                    </button>
                </div>
            </header>

            {errorMsg && (
                <div className="staff-alert staff-alert-danger">
                    <span>⚠️ {errorMsg}</span>
                </div>
            )}

            {actionMsg && (
                <div className="staff-alert staff-alert-success">
                    <span>✓ {actionMsg}</span>
                </div>
            )}

            <div className="staff-card staff-table-card staff-verif-card">
                <table className="staff-table staff-table-verif">
                    <thead>
                        <tr>
                            <th style={{ width: "120px" }}>
                                {t("verifColSpot")}
                            </th>
                            <th>{t("verifColProblem")}</th>
                            <th style={{ width: "140px" }}>
                                {t("verifColDetectedPlate")}
                            </th>
                            <th style={{ width: "85px" }}>
                                {t("verifColConf")}
                            </th>
                            <th style={{ width: "190px" }}>
                                {t("verifColState")}
                            </th>
                            <th style={{ width: "150px" }}>
                                {t("verifColReportedAt")}
                            </th>
                            <th
                                style={{ width: "230px" }}
                                className="text-right"
                            >
                                {t("verifColAction")}
                            </th>
                        </tr>
                    </thead>
                    <tbody>
                        {displayedIssues.length === 0 ? (
                            <tr>
                                <td
                                    colSpan={7}
                                    className="staff-empty"
                                    style={{
                                        padding: "40px 20px",
                                        textAlign: "center",
                                    }}
                                >
                                    <span style={{ fontSize: "28px" }}>✅</span>
                                    <p
                                        style={{
                                            marginTop: "8px",
                                            fontWeight: "bold",
                                            color: "#10b981",
                                            fontSize: "15px",
                                        }}
                                    >
                                        Tất cả các làn cổng đều hoạt động ổn
                                        định
                                    </p>
                                    <p
                                        style={{
                                            fontSize: "13px",
                                            color: "#94a3b8",
                                            marginTop: "4px",
                                        }}
                                    >
                                        Không có sự cố nhận diện biển số hoặc
                                        lỗi mã QR nào cần xác minh thủ công lúc
                                        này.
                                    </p>
                                </td>
                            </tr>
                        ) : (
                            displayedIssues.map((issue) => (
                                <tr key={issue.id}>
                                    <td>
                                        <span className="staff-gate-badge">
                                            {getSpotText(issue, isVi)}
                                        </span>
                                    </td>
                                    <td className="cell-wrap">
                                        <div className="staff-issue-prob">
                                            <span
                                                className="issue-icon"
                                                aria-hidden="true"
                                            >
                                                ⚠️
                                            </span>
                                            <span>
                                                {getProblemText(issue, isVi)}
                                            </span>
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
