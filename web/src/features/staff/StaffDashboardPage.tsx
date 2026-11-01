"use client";

import React from "react";
import Link from "next/link";
import { PlateBadge, FormattedTime, countText, formatVnd } from "./StaffUi";
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
    const occupancyPercent = totalCap > 0
        ? Math.min(100, Math.round((occupancy.occupied / totalCap) * 100))
        : 0;

    const entryActivities = activities
        .filter((a) => a.type === "entry")
        .slice(0, 6);
    const exitActivities = activities
        .filter((a) => a.type === "exit")
        .slice(0, 6);

    // 1 staff chỉ quản lý 1 bãi duy nhất
    const activeLotDetail =
        assignedLotsDetails.find(
            (l) =>
                l.lotId.toLowerCase() ===
                (assignedLot?.lotId || "").toLowerCase(),
        ) ||
        (assignedLotsDetails.length > 0 ? assignedLotsDetails[0] : null);

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

            {/* THÔNG TIN BÃI TRỰC & CA LÀM VIỆC DUY NHẤT TỪ ADMIN */}
            <section
                className="staff-card"
                style={{
                    marginBottom: "24px",
                    borderLeft: "4px solid var(--pw-admin-lime, #eafe89)",
                    background: "#ffffff",
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
                            Mỗi nhân viên trực 1 bãi duy nhất. Dưới đây là thông tin bãi đỗ xe và ca làm việc bạn được Admin phân công.
                        </p>
                    </div>

                    <div style={{ display: "flex", gap: "8px", alignItems: "center" }}>
                        <span
                            style={{
                                display: "inline-flex",
                                alignItems: "center",
                                gap: "6px",
                                background: "#dcfce7",
                                color: "#15803d",
                                border: "1px solid #86efac",
                                padding: "4px 12px",
                                borderRadius: "9999px",
                                fontWeight: 700,
                                fontSize: "0.8rem",
                            }}
                        >
                            ✓ Bãi trực duy nhất
                        </span>
                    </div>
                </div>

                {loadingLots && !activeLotDetail ? (
                    <p className="staff-empty">
                        Đang tải thông tin phân công bãi và ca trực từ CSDL...
                    </p>
                ) : !activeLotDetail ? (
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
                            gridTemplateColumns: "repeat(auto-fit, minmax(320px, 1fr))",
                            gap: "20px",
                            alignItems: "stretch",
                        }}
                    >
                        {/* Cột 1: Thông tin Bãi & Ca trực */}
                        <div
                            style={{
                                padding: "18px",
                                background: "var(--pw-admin-soft, #f8f9fa)",
                                borderRadius: "12px",
                                border: "1px solid var(--pw-admin-line, #e8eaef)",
                                display: "flex",
                                flexDirection: "column",
                                justifyContent: "space-between",
                                gap: "16px",
                            }}
                        >
                            <div>
                                <span
                                    style={{
                                        fontSize: "0.75rem",
                                        fontWeight: 700,
                                        textTransform: "uppercase",
                                        letterSpacing: "0.05em",
                                        color: "var(--pw-admin-muted, #64748b)",
                                    }}
                                >
                                    BÃI ĐỖ XE ĐƯỢC PHÂN QUYỀN
                                </span>
                                <h3
                                    style={{
                                        fontSize: "1.25rem",
                                        fontWeight: 800,
                                        margin: "4px 0 0",
                                        color: "var(--pw-admin-ink, #0f172a)",
                                    }}
                                >
                                    {activeLotDetail.lotName}
                                </h3>
                                <p
                                    style={{
                                        margin: "4px 0 0",
                                        fontSize: "0.85rem",
                                        color: "var(--pw-admin-muted, #64748b)",
                                    }}
                                >
                                    📍 {activeLotDetail.address}
                                </p>
                            </div>

                            {/* Khối Ca trực */}
                            <div
                                style={{
                                    padding: "14px 16px",
                                    background: "#ffffff",
                                    borderRadius: "10px",
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
                                            fontWeight: 600,
                                        }}
                                    >
                                        CA TRỰC CỦA BẠN:
                                    </span>
                                    <strong
                                        style={{
                                            fontSize: "1.05rem",
                                            color: "#0f172a",
                                        }}
                                    >
                                        {activeLotDetail.shiftName}
                                    </strong>
                                    <span
                                        style={{
                                            display: "block",
                                            color: "#64748b",
                                            fontSize: "0.8rem",
                                            marginTop: "2px",
                                        }}
                                    >
                                        Khung giờ làm việc: {activeLotDetail.hours}
                                    </span>
                                </div>

                                <div>
                                    {activeLotDetail.isCurrentShift ? (
                                        <span
                                            style={{
                                                display: "inline-flex",
                                                alignItems: "center",
                                                gap: "6px",
                                                background: "rgba(22, 163, 74, 0.12)",
                                                color: "#16a34a",
                                                border: "1px solid rgba(22, 163, 74, 0.3)",
                                                padding: "6px 12px",
                                                borderRadius: "8px",
                                                fontSize: "0.8rem",
                                                fontWeight: 700,
                                            }}
                                        >
                                            <span className="staff-pulse-dot green" />
                                            Đang trong ca trực
                                        </span>
                                    ) : (
                                        <span
                                            style={{
                                                background: "rgba(100, 116, 139, 0.1)",
                                                color: "#64748b",
                                                padding: "6px 12px",
                                                borderRadius: "8px",
                                                fontSize: "0.8rem",
                                                fontWeight: 600,
                                            }}
                                        >
                                            Ngoài ca trực
                                        </span>
                                    )}
                                </div>
                            </div>
                        </div>

                        {/* Cột 2: Tình trạng vận hành bãi */}
                        <div
                            style={{
                                padding: "18px",
                                background: "var(--pw-admin-soft, #f8f9fa)",
                                borderRadius: "12px",
                                border: "1px solid var(--pw-admin-line, #e8eaef)",
                                display: "flex",
                                flexDirection: "column",
                                justifyContent: "space-between",
                                gap: "12px",
                            }}
                        >
                            <span
                                style={{
                                    fontSize: "0.75rem",
                                    fontWeight: 700,
                                    textTransform: "uppercase",
                                    letterSpacing: "0.05em",
                                    color: "var(--pw-admin-muted, #64748b)",
                                }}
                            >
                                TÌNH TRẠNG VẬN HÀNH THỜI GIAN THỰC
                            </span>

                            <div
                                style={{
                                    display: "grid",
                                    gridTemplateColumns: "1fr 1fr",
                                    gap: "10px",
                                }}
                            >
                                <div
                                    style={{
                                        background: "#ffffff",
                                        padding: "12px",
                                        borderRadius: "8px",
                                        border: "1px solid #e2e8f0",
                                    }}
                                >
                                    <span style={{ color: "#64748b", fontSize: "0.8rem", display: "block" }}>
                                        Chỗ trống Ô tô
                                    </span>
                                    <strong style={{ color: "#16a34a", fontSize: "1.15rem" }}>
                                        {activeLotDetail.carAvailable} chỗ
                                    </strong>
                                </div>

                                <div
                                    style={{
                                        background: "#ffffff",
                                        padding: "12px",
                                        borderRadius: "8px",
                                        border: "1px solid #e2e8f0",
                                    }}
                                >
                                    <span style={{ color: "#64748b", fontSize: "0.8rem", display: "block" }}>
                                        Chỗ trống Xe máy
                                    </span>
                                    <strong style={{ color: "#16a34a", fontSize: "1.15rem" }}>
                                        {activeLotDetail.motorbikeAvailable} chỗ
                                    </strong>
                                </div>

                                <div
                                    style={{
                                        background: "#ffffff",
                                        padding: "12px",
                                        borderRadius: "8px",
                                        border: "1px solid #e2e8f0",
                                    }}
                                >
                                    <span style={{ color: "#64748b", fontSize: "0.8rem", display: "block" }}>
                                        Xe đang đỗ trong bãi
                                    </span>
                                    <strong style={{ color: "#0284c7", fontSize: "1.15rem" }}>
                                        {activeLotDetail.activeSessionsCount} xe
                                    </strong>
                                </div>

                                <div
                                    style={{
                                        background: "#ffffff",
                                        padding: "12px",
                                        borderRadius: "8px",
                                        border: "1px solid #e2e8f0",
                                    }}
                                >
                                    <span style={{ color: "#64748b", fontSize: "0.8rem", display: "block" }}>
                                        Rào chắn Barrier
                                    </span>
                                    <strong style={{ color: "#16a34a", fontSize: "1rem", display: "flex", alignItems: "center", gap: "6px" }}>
                                        <span className="staff-pulse-dot green" />
                                        Hoạt động tốt
                                    </strong>
                                </div>
                            </div>

                            <p style={{ margin: "4px 0 0", fontSize: "0.8rem", color: "var(--pw-admin-muted, #64748b)" }}>
                                Cổng IN 01 & Cổng OUT 02 đang kết nối đồng bộ và sẵn sàng xử lý phương tiện ra vào.
                            </p>
                        </div>
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
/ {countText(occupancy.totalCapacity || totalCap)} {t("dashSlots")}
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
                                {countText(occupancy.carAvailable)}{" "}
                                {t("dashSlots")}
                            </strong>
                        </div>
                        <div className="staff-slot-sub">
                            <span>{t("dashMotorbikeAvailable")}</span>
                            <strong className="text-emerald">
                                {countText(occupancy.motorbikeAvailable)}{" "}
                                {t("dashSlots")}
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
