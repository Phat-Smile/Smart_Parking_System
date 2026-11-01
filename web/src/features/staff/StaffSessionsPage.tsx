"use client";

import React, { useState, useEffect } from "react";
import Link from "next/link";
import {
    PlateBadge,
    QrCodeModal,
    StatusPill,
    FormattedTime,
    formatVnd,
} from "./StaffUi";
import { cancelParkingSession, describeStaffError } from "./staffApi";
import { useStaffData } from "./staffData";
import type { CustomerType, SessionStatus, StaffSession } from "./types";
import { useLanguage } from "@/shared/i18n/LanguageContext";

export function StaffSessionsPage() {
    const { sessions, cancelSession, assignedLot, fetchAssignedLot } = useStaffData();
    const { t } = useLanguage();

    const [isRefreshing, setIsRefreshing] = useState(false);
    const [filterType, setFilterType] = useState<CustomerType | "All">("All");
    const [statusFilter, setStatusFilter] = useState<SessionStatus | "All">("All");
    const [search, setSearch] = useState("");
    const [selectedQrSession, setSelectedQrSession] =
        useState<StaffSession | null>(null);

    // Cancel modal state
    const [cancellingSession, setCancellingSession] =
        useState<StaffSession | null>(null);
    const [cancelReason, setCancelReason] = useState(
        "Tài xế yêu cầu quay đầu xe không vào bãi",
    );
    const [cancelBusy, setCancelBusy] = useState(false);
    const [errorMsg, setErrorMsg] = useState("");

    // Load 100% real sessions from backend DB on mount
    useEffect(() => {
        fetchAssignedLot().catch(() => {});
    }, [fetchAssignedLot]);

    async function handleRefresh() {
        setIsRefreshing(true);
        setErrorMsg("");
        try {
            await fetchAssignedLot();
        } catch (err) {
            setErrorMsg(describeStaffError(err));
        } finally {
            setIsRefreshing(false);
        }
    }

    const filtered = sessions
        .filter((s) =>
            filterType === "All" ? true : s.customerType === filterType,
        )
        .filter((s) =>
            statusFilter === "All" ? true : s.status === statusFilter,
        )
        .filter(
            (s) =>
                !search.trim() ||
                s.plateNumber
                    .toLowerCase()
                    .includes(search.trim().toLowerCase()) ||
                s.slotCode
                    .toLowerCase()
                    .includes(search.trim().toLowerCase()) ||
                (s.driverName &&
                    s.driverName
                        .toLowerCase()
                        .includes(search.trim().toLowerCase())),
        );

    async function handleConfirmCancel() {
        if (!cancellingSession) return;
        setCancelBusy(true);
        setErrorMsg("");

        try {
            // 100% Real API: Call POST /api/parking/sessions/{id}/cancel
            await cancelParkingSession(cancellingSession.id, cancelReason);
            cancelSession(cancellingSession.id);
            setCancellingSession(null);
        } catch (err) {
            setErrorMsg(describeStaffError(err));
        } finally {
            setCancelBusy(false);
        }
    }

    return (
        <div className="staff-page">
            <header className="staff-header">
                <div>
                    <h1 className="staff-title">{t("sessionsTitle")}</h1>
                    <p className="staff-subtitle">
                        {assignedLot?.lotName ? `Bãi trực: ${assignedLot.lotName} · ` : ""}
                        Dữ liệu phiên thực tế tải 100% từ CSDL hệ thống
                    </p>
                </div>

                <div className="staff-header-actions">
                    <button
                        type="button"
                        className="staff-btn staff-btn-secondary"
                        onClick={handleRefresh}
                        disabled={isRefreshing}
                    >
                        {isRefreshing ? "Đang tải CSDL..." : "🔄 Làm mới từ CSDL"}
                    </button>
                    <Link href="/staff/entry" className="staff-btn staff-btn-primary">
                        + Check-in xe mới
                    </Link>
                </div>
            </header>

            {errorMsg && (
                <div className="staff-alert staff-alert-danger">
                    <span>⚠️ {errorMsg}</span>
                    <button type="button" onClick={() => setErrorMsg("")}>
                        ✕
                    </button>
                </div>
            )}

            {/* Filter toolbar */}
            <div className="staff-toolbar">
                <div className="staff-search-box">
                    <span className="search-icon">🔍</span>
                    <input
                        type="text"
                        className="staff-input staff-search-input"
                        placeholder={t("sessionsSearchPlaceholder")}
                        value={search}
                        onChange={(e) => setSearch(e.target.value)}
                    />
                </div>

                {/* Status filter */}
                <div className="staff-filter-tabs">
                    {(["All", "Active", "Completed", "Cancelled"] as const).map((st) => (
                        <button
                            key={st}
                            type="button"
                            className={`staff-filter-btn ${statusFilter === st ? "active" : ""}`}
                            onClick={() => setStatusFilter(st)}
                        >
                            {st === "All"
                                ? "Tất cả trạng thái"
                                : st === "Active"
                                  ? "Đang đỗ (Active)"
                                  : st === "Completed"
                                    ? "Đã rời bãi (Completed)"
                                    : "Đã hủy (Cancelled)"}
                        </button>
                    ))}
                </div>

                {/* Customer type filter */}
                <div className="staff-filter-tabs">
                    {(["All", "Guest", "Registered"] as const).map((tab) => (
                        <button
                            key={tab}
                            type="button"
                            className={`staff-filter-btn ${filterType === tab ? "active" : ""}`}
                            onClick={() => setFilterType(tab)}
                        >
                            {tab === "Guest"
                                ? t("sessionsGuestTab")
                                : tab === "Registered"
                                  ? t("sessionsRegisteredTab")
                                  : t("sessionsAllTab")}
                        </button>
                    ))}
                </div>
            </div>

            {/* Sessions table */}
            <div className="staff-card staff-table-card">
                <table className="staff-table">
                    <thead>
                        <tr>
                            <th>{t("sessionsLicensePlate")}</th>
                            <th>{t("sessionsCategory")}</th>
                            <th>{t("sessionsVehicle")}</th>
                            <th>{t("sessionsSpot")}</th>
                            <th>{t("sessionsEntryTime")}</th>
                            <th>{t("sessionsStatus")}</th>
                            <th>{t("sessionsPaymentStatus")}</th>
                            <th>{t("sessionsQr")}</th>
                            <th className="text-right">
                                {t("sessionsActions")}
                            </th>
                        </tr>
                    </thead>
                    <tbody>
                        {filtered.length === 0 ? (
                            <tr>
                                <td colSpan={9} className="staff-empty" style={{ padding: "40px 20px" }}>
                                    <div style={{ textAlign: "center" }}>
                                        <p style={{ fontSize: "16px", fontWeight: "bold", color: "#64748b", marginBottom: "8px" }}>
                                            Chưa có phiên gửi xe nào trong CSDL phù hợp với bộ lọc
                                        </p>
                                        <p style={{ fontSize: "13px", color: "#94a3b8", marginBottom: "16px" }}>
                                            Hãy tiến hành Check-in xe tại cổng vào để tạo phiên mới trên CSDL thật.
                                        </p>
                                        <Link href="/staff/entry" className="staff-btn staff-btn-primary staff-btn-sm">
                                            Đi đến Cổng Vào (Check-in)
                                        </Link>
                                    </div>
                                </td>
                            </tr>
                        ) : (
                            filtered.map((s) => (
                                <tr key={s.id}>
                                    <td>
                                        <PlateBadge plate={s.plateNumber} />
                                    </td>
                                    <td>
                                        <span
                                            className={`staff-cust-type ${
                                                s.customerType === "Guest"
                                                    ? "type-guest"
                                                    : "type-reg"
                                            }`}
                                        >
                                            {s.customerType === "Guest"
                                                ? t("sessionsGuest")
                                                : t("sessionsRegistered")}
                                        </span>
                                    </td>
                                    <td>
                                        {s.vehicleType === "Car"
                                            ? t("staffCar")
                                            : t("staffMotorbike")}
                                    </td>
                                    <td>
                                        <span className="staff-slot-chip">
                                            {s.slotCode}
                                        </span>
                                    </td>
                                    <td>
                                        <FormattedTime
                                            iso={s.entryTime}
                                            dateTime
                                        />
                                    </td>
                                    <td>
                                        <StatusPill status={s.status} />
                                    </td>
                                    <td>
                                        <StatusPill status={s.paymentStatus} />
                                        {s.paymentStatus === "Pending" && (
                                            <div className="staff-cell-fee">
                                                {formatVnd(s.amountDueVnd)}
                                            </div>
                                        )}
                                    </td>
                                    <td>
                                        <button
                                            type="button"
                                            className="staff-btn staff-btn-ghost staff-btn-sm"
                                            onClick={() =>
                                                setSelectedQrSession(s)
                                            }
                                        >
                                            QR 🔍
                                        </button>
                                    </td>
                                    <td className="text-right">
                                        {s.status === "Active" ? (
                                            <div className="staff-table-btn-group">
                                                <button
                                                    type="button"
                                                    className="staff-btn staff-btn-danger staff-btn-sm"
                                                    onClick={() =>
                                                        setCancellingSession(s)
                                                    }
                                                >
                                                    {t("sessionsCancelEntry")}
                                                </button>
                                                <Link
                                                    href="/staff/exit"
                                                    className="staff-btn staff-btn-secondary staff-btn-sm"
                                                >
                                                    Cho xe ra ➔
                                                </Link>
                                            </div>
                                        ) : (
                                            <span style={{ fontSize: "12px", color: "#64748b" }}>
                                                —
                                            </span>
                                        )}
                                    </td>
                                </tr>
                            ))
                        )}
                    </tbody>
                </table>
            </div>

            {/* QR Modal view */}
            {selectedQrSession && (
                <QrCodeModal
                    qrToken={selectedQrSession.qrToken}
                    plateNumber={selectedQrSession.plateNumber}
                    vehicleType={selectedQrSession.vehicleType}
                    entryTime={selectedQrSession.entryTime}
                    onClose={() => setSelectedQrSession(null)}
                />
            )}

            {/* Cancel Session Confirmation Modal */}
            {cancellingSession && (
                <div
                    className="staff-modal-backdrop"
                    onClick={() => setCancellingSession(null)}
                >
                    <div
                        className="staff-modal"
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div className="staff-modal-header">
                            <h2>{t("sessionsCancelTitle")}</h2>
                            <button
                                type="button"
                                className="staff-modal-close"
                                onClick={() => setCancellingSession(null)}
                            >
                                ✕
                            </button>
                        </div>
                        <div className="staff-modal-body">
                            <p className="staff-modal-text">
                                {t("sessionsCancelConfirmText")}{" "}
                                <strong>
                                    {cancellingSession.plateNumber}
                                </strong>
                                ?
                            </p>
                            <p className="staff-modal-hint">
                                Phiên xe sẽ được đánh dấu Cancelled trong CSDL và chỗ đỗ sẽ được giải phóng ngay lập tức.
                            </p>

                            <div className="staff-form-group">
                                <label htmlFor="staff-cancel-reason">
                                    {t("sessionsCancelReasonLabel")}
                                </label>
                                <textarea
                                    id="staff-cancel-reason"
                                    className="staff-textarea"
                                    rows={3}
                                    value={cancelReason}
                                    onChange={(e) =>
                                        setCancelReason(e.target.value)
                                    }
                                />
                            </div>

                            <div className="staff-modal-action-bar">
                                <button
                                    type="button"
                                    className="staff-btn staff-btn-secondary"
                                    onClick={() => setCancellingSession(null)}
                                >
                                    {t("cancel")}
                                </button>
                                <button
                                    type="button"
                                    className="staff-btn staff-btn-danger staff-btn-flex"
                                    disabled={cancelBusy}
                                    onClick={handleConfirmCancel}
                                >
                                    {cancelBusy
                                        ? t("sessionsCancelling")
                                        : "Xác nhận hủy phiên"}
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
