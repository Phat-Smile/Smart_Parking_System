"use client";

import React from "react";
import Link from "next/link";
import { PlateBadge, FormattedTime, formatVnd } from "./StaffUi";
import { useStaffData } from "./staffData";
import {
    getStaffAssignedLotsDetails,
    getStaffProfile,
    type StaffAssignedLotDetail,
    type StaffProfile,
} from "./staffApi";
import { useLanguage } from "@/shared/i18n/LanguageContext";

export function StaffDashboardPage() {
    const { occupancy, activities, assignedLot, fetchAssignedLot } =
        useStaffData();
    const { t } = useLanguage();

    const [assignedLotsDetails, setAssignedLotsDetails] = React.useState<
        StaffAssignedLotDetail[]
    >([]);
    const [loadingLots, setLoadingLots] = React.useState(false);
    const [staffProfile, setStaffProfile] = React.useState<StaffProfile | null>(
        null,
    );

    const loadAssignedLotsDetails = React.useCallback(async () => {
        setLoadingLots(true);
        try {
            const profile = await getStaffProfile();
            setStaffProfile(profile);
            const list = await getStaffAssignedLotsDetails(profile?.id);
            setAssignedLotsDetails(list);
        } catch {
            // ignore
        } finally {
            setLoadingLots(false);
        }
    }, []);

    React.useEffect(() => {
        fetchAssignedLot().catch(() => {});
        loadAssignedLotsDetails().catch(() => {});
    }, [fetchAssignedLot, loadAssignedLotsDetails]);

    const totalCap = occupancy.totalCapacity || 50;
    const occupancyPercent =
        totalCap > 0
            ? Math.min(100, Math.round((occupancy.occupied / totalCap) * 100))
            : 0;

    const entryActivities = activities
        .filter((a) => a.type === "entry")
        .slice(0, 6);
    const exitActivities = activities
        .filter((a) => a.type === "exit")
        .slice(0, 6);

    const activeLotDetail = assignedLotsDetails.find(
        (l) => l.lotId === assignedLot.lotId,
    );

    return (
        <div className="staff-page">
            <header className="staff-header">
                <div>
                    <h1 className="staff-title">{t("dashTitle")}</h1>
                    <p className="staff-subtitle">
                        {assignedLot?.lotName
                            ? `Bãi đang trực: ${assignedLot.lotName} (${assignedLot.address || "Hệ thống CSDL"}) · `
                            : ""}
                        {t("dashSubtitle")}
                    </p>
                </div>
                <div className="staff-header-actions">
                    <button
                        type="button"
                        className="staff-btn staff-btn-secondary"
                        onClick={() => {
                            fetchAssignedLot();
                            loadAssignedLotsDetails();
                        }}
                        disabled={loadingLots}
                    >
                        🔄 {loadingLots ? "Đang làm mới..." : "Cập nhật số liệu CSDL"}
                    </button>
                </div>
            </header>

            {/* BẢNG THÔNG TIN PHÂN CÔNG BÃI TRỰC & CA LÀM VIỆC TỪ ADMIN */}
            <section
                className="staff-card"
                style={{
                    marginBottom: "24px",
                    borderLeft: "4px solid var(--pw-admin-lime, #eafe89)",
                }}
            >
                <div
                    className="staff-card-header"
                    style={{ marginBottom: "16px", flexWrap: "wrap" }}
                >
                    <div>
                        <h2
                            style={{
                                display: "flex",
                                alignItems: "center",
                                gap: "8px",
                            }}
                        >
                            <span>🏢</span> Bãi trực & Ca làm việc của bạn (Admin phân công)
                        </h2>
                        <p
                            className="staff-card-hint"
                            style={{ margin: "4px 0 0" }}
                        >
                            {staffProfile?.fullName
                                ? `Nhân viên: ${staffProfile.fullName} (${staffProfile.email}) · `
                                : ""}
                            Xem các bãi đỗ xe Admin đã phân quyền cho bạn, lịch ca trực và tình trạng vận hành thực tế.
                        </p>
                    </div>

                    <div style={{ display: "flex", gap: "8px", alignItems: "center" }}>
                        {activeLotDetail && (
                            <span
                                style={{
                                    display: "inline-flex",
                                    alignItems: "center",
                                    gap: "6px",
                                    background: "rgba(22, 163, 74, 0.12)",
                                    color: "#16a34a",
                                    border: "1px solid rgba(22, 163, 74, 0.3)",
                                    padding: "4px 10px",
                                    borderRadius: "8px",
                                    fontWeight: 700,
                                    fontSize: "0.8rem",
                                }}
                            >
                                <span className="staff-pulse-dot green" />
                                Ca hiện tại: {activeLotDetail.shiftName} ({activeLotDetail.hours})
                            </span>
                        )}
                        <span className="staff-pill staff-pill-success">
                            {assignedLotsDetails.length} bãi được phân quyền
                        </span>
                    </div>
                </div>

                {loadingLots && assignedLotsDetails.length === 0 ? (
                    <p className="staff-empty">
                        Đang tải danh sách bãi và ca trực từ CSDL...
                    </p>
                ) : assignedLotsDetails.length === 0 ? (
                    <div
                        style={{
                            padding: "20px",
                            background: "var(--pw-admin-soft, #f8f9fa)",
                            borderRadius: "8px",
                            textAlign: "center",
                        }}
                    >
                        <p style={{ margin: 0, color: "var(--pw-admin-muted, #64748b)" }}>
                            Bạn chưa được quản trị viên phân công vào bãi xe nào. Vui lòng liên hệ Admin để được phân quyền trực bãi.
                        </p>
                    </div>
                ) : (
                    <div
                        style={{
                            display: "grid",
                            gridTemplateColumns:
                                "repeat(auto-fit, minmax(320px, 1fr))",
                            gap: "16px",
                        }}
                    >
                        {assignedLotsDetails.map((lot) => {
                            const isCurrentActive =
                                lot.lotId.toLowerCase() ===
                                (assignedLot?.lotId || "").toLowerCase();
                            return (
                                <div
                                    key={lot.lotId}
                                    style={{
                                        border: isCurrentActive
                                            ? "2px solid #22c55e"
                                            : "1px solid var(--pw-admin-line, #e8eaef)",
                                        borderRadius: "12px",
                                        padding: "16px",
                                        background: isCurrentActive
                                            ? "#ffffff"
                                            : "var(--pw-admin-soft, #f8f9fa)",
                                        boxShadow: isCurrentActive
                                            ? "0 4px 16px rgba(34, 197, 94, 0.12)"
                                            : "none",
                                        display: "flex",
                                        flexDirection: "column",
                                        justifyContent: "space-between",
                                        gap: "14px",
                                    }}
                                >
                                    <div>
                                        {/* Row 1: Tên bãi & Badge đang trực */}
                                        <div
                                            style={{
                                                display: "flex",
                                                justifyContent: "space-between",
                                                alignItems: "flex-start",
                                                gap: "8px",
                                            }}
                                        >
                                            <div>
                                                <strong
                                                    style={{
                                                        fontSize: "1.05rem",
                                                        color: "var(--pw-admin-ink, #0f172a)",
                                                    }}
                                                >
                                                    {lot.lotName}
                                                </strong>
                                                <p
                                                    style={{
                                                        margin: "3px 0 0",
                                                        fontSize: "0.8rem",
                                                        color: "var(--pw-admin-muted, #64748b)",
                                                    }}
                                                >
                                                    📍 {lot.address}
                                                </p>
                                            </div>
                                            {isCurrentActive ? (
                                                <span
                                                    style={{
                                                        background: "#dcfce7",
                                                        color: "#15803d",
                                                        padding: "3px 8px",
                                                        borderRadius: "9999px",
                                                        fontSize: "0.75rem",
                                                        fontWeight: 700,
                                                        whiteSpace: "nowrap",
                                                        border: "1px solid #86efac",
                                                    }}
                                                >
                                                    ✓ Đang đứng trực
                                                </span>
                                            ) : null}
                                        </div>

                                        {/* Row 2: Ca trực của bạn tại bãi này */}
                                        <div
                                            style={{
                                                marginTop: "12px",
                                                padding: "10px 12px",
                                                background: "#ffffff",
                                                borderRadius: "8px",
                                                border: "1px solid #e2e8f0",
                                                display: "flex",
                                                alignItems: "center",
                                                justifyContent: "space-between",
                                            }}
                                        >
                                            <div>
                                                <span
                                                    style={{
                                                        fontSize: "0.75rem",
                                                        color: "#64748b",
                                                        display: "block",
                                                    }}
                                                >
                                                    Ca trực được phân công:
                                                </span>
                                                <strong
                                                    style={{
                                                        fontSize: "0.95rem",
                                                        color: "#0f172a",
                                                    }}
                                                >
                                                    {lot.shiftName}
                                                </strong>
                                                <small
                                                    style={{
                                                        display: "block",
                                                        color: "#64748b",
                                                        fontSize: "0.75rem",
                                                    }}
                                                >
                                                    Khung giờ: {lot.hours}
                                                </small>
                                            </div>

                                            <div>
                                                {lot.isCurrentShift ? (
                                                    <span
                                                        style={{
                                                            background:
                                                                "rgba(22, 163, 74, 0.12)",
                                                            color: "#16a34a",
                                                            border: "1px solid rgba(22, 163, 74, 0.3)",
                                                            padding: "3px 8px",
                                                            borderRadius: "6px",
                                                            fontSize: "0.75rem",
                                                            fontWeight: 600,
                                                        }}
                                                    >
                                                        ● Đang trong ca
                                                    </span>
                                                ) : (
                                                    <span
                                                        style={{
                                                            background:
                                                                "rgba(100, 116, 139, 0.1)",
                                                            color: "#64748b",
                                                            padding: "3px 8px",
                                                            borderRadius: "6px",
                                                            fontSize: "0.75rem",
                                                        }}
                                                    >
                                                        Ngoài ca trực
                                                    </span>
                                                )}
                                            </div>
                                        </div>

                                        {/* Row 3: Tình trạng bãi thời gian thực */}
                                        <div
                                            style={{
                                                marginTop: "12px",
                                                display: "grid",
                                                gridTemplateColumns: "1fr 1fr",
                                                gap: "8px",
                                                fontSize: "0.8rem",
                                            }}
                                        >
                                            <div
                                                style={{
                                                    background: "#ffffff",
                                                    padding: "8px 10px",
                                                    borderRadius: "6px",
                                                    border: "1px solid #e2e8f0",
                                                }}
                                            >
                                                <span
                                                    style={{
                                                        color: "#64748b",
                                                        display: "block",
                                                    }}
                                                >
                                                    Chỗ trống Ô tô
                                                </span>
                                                <strong
                                                    style={{
                                                        color: "#16a34a",
                                                        fontSize: "0.95rem",
                                                    }}
                                                >
                                                    {lot.carAvailable} chỗ
                                                </strong>
                                            </div>
                                            <div
                                                style={{
                                                    background: "#ffffff",
                                                    padding: "8px 10px",
                                                    borderRadius: "6px",
                                                    border: "1px solid #e2e8f0",
                                                }}
                                            >
                                                <span
                                                    style={{
                                                        color: "#64748b",
                                                        display: "block",
                                                    }}
                                                >
                                                    Chỗ trống Xe máy
                                                </span>
                                                <strong
                                                    style={{
                                                        color: "#16a34a",
                                                        fontSize: "0.95rem",
                                                    }}
                                                >
                                                    {lot.motorbikeAvailable} chỗ
                                                </strong>
                                            </div>
                                            <div
                                                style={{
                                                    background: "#ffffff",
                                                    padding: "8px 10px",
                                                    borderRadius: "6px",
                                                    border: "1px solid #e2e8f0",
                                                }}
                                            >
                                                <span
                                                    style={{
                                                        color: "#64748b",
                                                        display: "block",
                                                    }}
                                                >
                                                    Phiên xe trong bãi
                                                </span>
                                                <strong
                                                    style={{
                                                        color: "#0284c7",
                                                        fontSize: "0.95rem",
                                                    }}
                                                >
                                                    {lot.activeSessionsCount} xe
                                                </strong>
                                            </div>
                                            <div
                                                style={{
                                                    background: "#ffffff",
                                                    padding: "8px 10px",
                                                    borderRadius: "6px",
                                                    border: "1px solid #e2e8f0",
                                                }}
                                            >
                                                <span
                                                    style={{
                                                        color: "#64748b",
                                                        display: "block",
                                                    }}
                                                >
                                                    Rào chắn Barrier
                                                </span>
                                                <strong
                                                    style={{
                                                        color: "#16a34a",
                                                        fontSize: "0.85rem",
                                                    }}
                                                >
                                                    ● Hoạt động
                                                </strong>
                                            </div>
                                        </div>
                                    </div>

                                    {/* Action Button: Chuyển bãi trực nếu được phân nhiều bãi */}
                                    <div style={{ marginTop: "6px" }}>
                                        {isCurrentActive ? (
                                            <button
                                                type="button"
                                                className="staff-btn staff-btn-secondary"
                                                style={{
                                                    width: "100%",
                                                    justifyContent: "center",
                                                    opacity: 0.85,
                                                    background: "#ecfdf5",
                                                    borderColor: "#86efac",
                                                    color: "#15803d",
                                                    fontWeight: 600,
                                                }}
                                                disabled
                                            >
                                                ✓ Đang trực bãi này
                                            </button>
                                        ) : (
                                            <button
                                                type="button"
                                                className="staff-btn staff-btn-primary"
                                                style={{
                                                    width: "100%",
                                                    justifyContent: "center",
                                                    fontWeight: 600,
                                                }}
                                                onClick={() =>
                                                    fetchAssignedLot(lot.lotId)
                                                }
                                            >
                                                🔄 Chuyển sang trực bãi này
                                            </button>
                                        )}
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                )}
            </section>

            {/* Current occupancy & High level stats */}
            <section className="staff-stats-grid">
                <div className="staff-stat-card occupancy-card">
                    <div className="staff-stat-header">
                        <span className="staff-stat-title">
                            {t("dashOccupancy")}
                        </span>
                        <span className="staff-stat-pill">
                            {occupancyPercent}% {t("dashFull")}
                        </span>
                    </div>
                    <div className="staff-occupancy-value">
                        <strong>{occupancy.occupied}</strong>
                        <span>
                            / {totalCap} {t("dashSlots")}
                        </span>
                    </div>

                    <div className="staff-progress-bar">
                        <div
                            className="staff-progress-fill"
                            style={{
                                width: `${occupancyPercent}%`,
                                backgroundColor:
                                    occupancyPercent > 85
                                        ? "#ef4444"
                                        : "#10b981",
                            }}
                        />
                    </div>

                    <div className="staff-slot-breakdown">
                        <div className="staff-slot-sub">
                            <span>{t("dashCarAvailable")}</span>
                            <strong className="text-emerald">
                                {occupancy.carAvailable} {t("dashSlots")}
                            </strong>
                        </div>
                        <div className="staff-slot-sub">
                            <span>{t("dashMotorbikeAvailable")}</span>
                            <strong className="text-emerald">
                                {occupancy.motorbikeAvailable} {t("dashSlots")}
                            </strong>
                        </div>
                    </div>
                </div>

                <div className="staff-stat-card">
                    <span className="staff-stat-title">
                        {t("dashActiveSessions")}
                    </span>
                    <div className="staff-stat-number">
                        {occupancy.activeSessionsCount}
                    </div>
                    <p className="staff-stat-hint">{t("dashActiveHint")}</p>
                    <Link href="/staff/sessions" className="staff-link">
                        {t("dashViewSessions")}
                    </Link>
                </div>

                <div className="staff-stat-card">
                    <span className="staff-stat-title">
                        {t("dashNeedsVerif")}
                    </span>
                    <div
                        className={`staff-stat-number ${
                            occupancy.needsVerificationCount > 0
                                ? "text-danger"
                                : ""
                        }`}
                    >
                        {occupancy.needsVerificationCount}
                    </div>
                    <p className="staff-stat-hint">{t("dashNeedsVerifHint")}</p>
                    <Link
                        href="/staff/verification"
                        className="staff-link text-danger"
                    >
                        {t("dashResolveNow")}
                    </Link>
                </div>

                <div className="staff-stat-card">
                    <span className="staff-stat-title">
                        {t("dashGateHealth")}
                    </span>
                    <div className="staff-stat-number text-emerald">
                        {t("gateOnline").toUpperCase()}
                    </div>
                    <p className="staff-stat-hint">{t("dashGateSynced")}</p>
                    <span className="staff-pill staff-pill-success">
                        {t("dashLatency")} 24ms
                    </span>
                </div>
            </section>

            {/* Entry activity & Exit activity columns */}
            <div className="staff-two-col">
                {/* Entry activity */}
                <div className="staff-card">
                    <div className="staff-card-header">
                        <h2>{t("dashEntryActivity")}</h2>
                        <span className="staff-feed-live">
                            {t("dashLiveFeed")}
                        </span>
                    </div>

                    <div className="staff-activity-list">
                        {entryActivities.length === 0 ? (
                            <p className="staff-empty">
                                Chưa có lượt xe vào nào được ghi nhận trong phiên làm việc.
                            </p>
                        ) : (
                            entryActivities.map((act) => (
                                <div
                                    key={act.id}
                                    className="staff-activity-item"
                                >
                                    <div className="staff-act-icon in">IN</div>
                                    <div className="staff-act-info">
                                        <PlateBadge plate={act.plateNumber} />
                                        <span className="staff-act-meta">
                                            {act.vehicleType === "Car"
                                                ? t("staffCar")
                                                : t("staffMotorbike")}{" "}
                                            ·{" "}
                                            {act.customerType === "Guest"
                                                ? t("sessionsGuest")
                                                : t("sessionsRegistered")}
                                            {act.slotCode &&
                                                ` · Slot ${act.slotCode}`}
                                        </span>
                                    </div>
                                    <span className="staff-act-time">
                                        <FormattedTime iso={act.timestamp} />
                                    </span>
                                </div>
                            ))
                        )}
                    </div>
                </div>

                {/* Exit activity */}
                <div className="staff-card">
                    <div className="staff-card-header">
                        <h2>{t("dashExitActivity")}</h2>
                        <span className="staff-feed-live">
                            {t("dashLiveFeed")}
                        </span>
                    </div>

                    <div className="staff-activity-list">
                        {exitActivities.length === 0 ? (
                            <p className="staff-empty">
                                Chưa có lượt xe ra nào được ghi nhận trong phiên làm việc.
                            </p>
                        ) : (
                            exitActivities.map((act) => (
                                <div
                                    key={act.id}
                                    className="staff-activity-item"
                                >
                                    <div className="staff-act-icon out">
                                        OUT
                                    </div>
                                    <div className="staff-act-info">
                                        <PlateBadge plate={act.plateNumber} />
                                        <span className="staff-act-meta">
                                            {act.vehicleType === "Car"
                                                ? t("staffCar")
                                                : t("staffMotorbike")}{" "}
                                            ·{" "}
                                            {act.customerType === "Guest"
                                                ? t("sessionsGuest")
                                                : t("sessionsRegistered")}
                                        </span>
                                    </div>
                                    <div className="staff-act-right">
                                        {typeof act.amount === "number" && (
                                            <span className="staff-act-fee">
                                                {formatVnd(act.amount)}
                                            </span>
                                        )}
                                        <span className="staff-act-time">
                                            <FormattedTime
                                                iso={act.timestamp}
                                            />
                                        </span>
                                    </div>
                                </div>
                            ))
                        )}
                    </div>
                </div>
            </div>
        </div>
    );
}
