"use client";

import React from "react";
import Link from "next/link";
import {
    Building2,
    MapPin,
    Clock,
    RefreshCw,
    CheckCircle2,
    UserCheck,
    Car,
    Bike,
    Activity,
    ShieldCheck,
    Gauge,
    Layers,
    ScanLine,
    ArrowRight,
    ArrowDownLeft,
    ArrowUpRight,
} from "lucide-react";
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
    const [loadingLots, setLoadingLots] = React.useState(true);
    const [staffProfile, setStaffProfile] = React.useState<StaffProfile | null>(
        null,
    );

    const loadAssignedLotsDetails = React.useCallback(async () => {
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
        // eslint-disable-next-line react-hooks/set-state-in-effect
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

    // 1 staff chỉ quản lý 1 bãi duy nhất
    const activeLotDetail =
        assignedLotsDetails.find(
            (l) =>
                l.lotId.toLowerCase() ===
                (assignedLot?.lotId || "").toLowerCase(),
        ) || (assignedLotsDetails.length > 0 ? assignedLotsDetails[0] : null);

    return (
        <div className="staff-page">
            <header className="staff-header">
                <div>
                    <h1 className="staff-title">{t("dashTitle")}</h1>
                    <p className="staff-subtitle">{t("dashSubtitle")}</p>
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
                        <RefreshCw
                            size={15}
                            strokeWidth={2.2}
                            className={loadingLots ? "staff-spin" : ""}
                        />
                        <span>
                            {loadingLots
                                ? "Đang làm mới..."
                                : "Cập nhật số liệu CSDL"}
                        </span>
                    </button>
                </div>
            </header>

            {/* THÔNG TIN BÃI TRỰC & CA LÀM VIỆC DUY NHẤT TỪ ADMIN */}
            <section className="staff-card staff-assignment-card">
                <div className="staff-assign-header">
                    <div className="staff-assign-title-group">
                        <div className="staff-assign-icon-badge">
                            <Building2 size={18} strokeWidth={2.2} />
                        </div>
                        <div>
                            <h2 className="staff-assign-heading">
                                Bãi trực & Ca làm việc của bạn
                            </h2>
                            <p className="staff-assign-sub">
                                Mỗi nhân viên trực 1 bãi duy nhất theo phân công
                                từ Quản trị viên (Admin).
                            </p>
                        </div>
                    </div>

                    <div className="staff-assign-badges">
                        {staffProfile?.fullName && (
                            <span
                                className="staff-assign-operator-pill"
                                title={staffProfile.email}
                            >
                                <UserCheck size={13} strokeWidth={2.2} />
                                <span>{staffProfile.fullName}</span>
                            </span>
                        )}
                        <span className="staff-assign-role-pill">
                            <CheckCircle2 size={13} strokeWidth={2.3} />
                            <span>Bãi trực duy nhất · Admin phân công</span>
                        </span>
                    </div>
                </div>

                {loadingLots && !activeLotDetail ? (
                    <p className="staff-empty">
                        Đang tải thông tin phân công bãi và ca trực từ CSDL...
                    </p>
                ) : !activeLotDetail ? (
                    <div className="staff-assign-empty">
                        <p>
                            Bạn chưa được quản trị viên phân công vào bãi xe
                            nào. Vui lòng liên hệ Admin để được phân quyền trực
                            bãi.
                        </p>
                    </div>
                ) : (
                    <div className="staff-assign-grid">
                        {/* Cột 1: Thông tin Bãi & Ca trực */}
                        <div className="staff-assign-lot-box">
                            <div className="staff-assign-lot-top">
                                <span className="staff-eyebrow-label">
                                    BÃI ĐỖ XE ĐƯỢC PHÂN QUYỀN
                                </span>
                                <h3 className="staff-assign-lot-name">
                                    {activeLotDetail.lotName}
                                </h3>
                                <p className="staff-assign-lot-addr">
                                    <MapPin size={14} strokeWidth={2.2} />
                                    <span>{activeLotDetail.address}</span>
                                </p>
                            </div>

                            {/* Khối Ca trực */}
                            <div className="staff-shift-strip">
                                <div className="staff-shift-strip-left">
                                    <div className="staff-shift-clock-icon">
                                        <Clock size={16} strokeWidth={2.2} />
                                    </div>
                                    <div className="staff-shift-meta">
                                        <span className="staff-shift-eyebrow">
                                            CA TRỰC CỦA BẠN
                                        </span>
                                        <div className="staff-shift-main-line">
                                            <strong className="staff-shift-name">
                                                {activeLotDetail.shiftName}
                                            </strong>
                                            <span className="staff-shift-hours">
                                                {activeLotDetail.hours}
                                            </span>
                                        </div>
                                    </div>
                                </div>

                                <div className="staff-shift-strip-right">
                                    {activeLotDetail.isCurrentShift ? (
                                        <span className="staff-shift-status active">
                                            <span className="staff-pulse-dot green" />
                                            <span>Đang trong ca trực</span>
                                        </span>
                                    ) : (
                                        <span className="staff-shift-status inactive">
                                            <span>Ngoài ca trực</span>
                                        </span>
                                    )}
                                </div>
                            </div>
                        </div>

                        {/* Cột 2: Tình trạng vận hành bãi (Real-time Operation) */}
                        <div className="staff-ops-box">
                            <div className="staff-ops-box-head">
                                <span className="staff-eyebrow-label">
                                    TÌNH TRẠNG VẬN HÀNH THỜI GIAN THỰC
                                </span>
                                <span className="staff-ops-sync-tag">
                                    <span className="staff-pulse-dot green" />
                                    IN 01 & OUT 02 đồng bộ
                                </span>
                            </div>

                            <div className="staff-ops-mini-grid">
                                <div className="staff-ops-mini-card">
                                    <div className="staff-ops-mini-icon emerald">
                                        <Car size={16} strokeWidth={2.2} />
                                    </div>
                                    <div className="staff-ops-mini-content">
                                        <span className="staff-ops-mini-label">
                                            Chỗ trống Ô tô
                                        </span>
                                        <strong className="staff-ops-mini-val text-emerald">
                                            {activeLotDetail.carAvailable}{" "}
                                            <small>chỗ</small>
                                        </strong>
                                    </div>
                                </div>

                                <div className="staff-ops-mini-card">
                                    <div className="staff-ops-mini-icon emerald">
                                        <Bike size={16} strokeWidth={2.2} />
                                    </div>
                                    <div className="staff-ops-mini-content">
                                        <span className="staff-ops-mini-label">
                                            Chỗ trống Xe máy
                                        </span>
                                        <strong className="staff-ops-mini-val text-emerald">
                                            {activeLotDetail.motorbikeAvailable}{" "}
                                            <small>chỗ</small>
                                        </strong>
                                    </div>
                                </div>

                                <div className="staff-ops-mini-card">
                                    <div className="staff-ops-mini-icon blue">
                                        <Activity size={16} strokeWidth={2.2} />
                                    </div>
                                    <div className="staff-ops-mini-content">
                                        <span className="staff-ops-mini-label">
                                            Xe đang đỗ
                                        </span>
                                        <strong className="staff-ops-mini-val text-sky">
                                            {
                                                activeLotDetail.activeSessionsCount
                                            }{" "}
                                            <small>xe</small>
                                        </strong>
                                    </div>
                                </div>

                                <div className="staff-ops-mini-card">
                                    <div className="staff-ops-mini-icon emerald">
                                        <ShieldCheck
                                            size={16}
                                            strokeWidth={2.2}
                                        />
                                    </div>
                                    <div className="staff-ops-mini-content">
                                        <span className="staff-ops-mini-label">
                                            Trạng thái Barrier
                                        </span>
                                        <strong className="staff-ops-mini-val text-emerald status-inline">
                                            <span className="staff-pulse-dot green" />
                                            <span>Hoạt động tốt</span>
                                        </strong>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                )}
            </section>

            {/* Current occupancy & High level KPI stats */}
            <section className="staff-stats-grid">
                {/* KPI 1: Công suất bãi đỗ hiện tại */}
                <div className="staff-stat-card occupancy-card">
                    <div className="staff-stat-top">
                        <div className="staff-stat-header">
                            <div className="staff-stat-title-wrap">
                                <span className="staff-stat-icon icon-ink">
                                    <Gauge size={16} strokeWidth={2.2} />
                                </span>
                                <span className="staff-stat-title">
                                    {t("dashOccupancy")}
                                </span>
                            </div>
                            <span
                                className={`staff-stat-pill ${
                                    occupancyPercent > 85
                                        ? "pill-danger"
                                        : "pill-emerald"
                                }`}
                            >
                                {occupancyPercent}% {t("dashFull")}
                            </span>
                        </div>

                        <div className="staff-occupancy-value">
                            <strong>{occupancy.occupied}</strong>
                            <span>
                                /{" "}
                                {countText(occupancy.totalCapacity || totalCap)}{" "}
                                {t("dashSlots")}
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
                    </div>

                    <div className="staff-slot-breakdown">
                        <div className="staff-slot-sub">
                            <span className="staff-slot-sub-label">
                                <Car size={12} strokeWidth={2.2} />
                                {t("dashCarAvailable")}
                            </span>
                            <strong className="text-emerald">
                                {countText(occupancy.carAvailable)}{" "}
                                {t("dashSlots")}
                            </strong>
                        </div>
                        <div className="staff-slot-sub">
                            <span className="staff-slot-sub-label">
                                <Bike size={12} strokeWidth={2.2} />
                                {t("dashMotorbikeAvailable")}
                            </span>
                            <strong className="text-emerald">
                                {countText(occupancy.motorbikeAvailable)}{" "}
                                {t("dashSlots")}
                            </strong>
                        </div>
                    </div>
                </div>

                {/* KPI 2: Phiên xe đang gửi */}
                <div className="staff-stat-card">
                    <div className="staff-stat-top">
                        <div className="staff-stat-header">
                            <div className="staff-stat-title-wrap">
                                <span className="staff-stat-icon icon-blue">
                                    <Layers size={16} strokeWidth={2.2} />
                                </span>
                                <span className="staff-stat-title">
                                    {t("dashActiveSessions")}
                                </span>
                            </div>
                            <span className="staff-stat-pill">LIVE</span>
                        </div>
                        <div className="staff-stat-number">
                            {occupancy.activeSessionsCount}
                        </div>
                        <p className="staff-stat-hint">{t("dashActiveHint")}</p>
                    </div>
                    <div className="staff-stat-footer">
                        <Link href="/staff/sessions" className="staff-link">
                            <span>
                                {t("dashViewSessions").replace(/\s*→$/, "")}
                            </span>
                            <ArrowRight size={14} strokeWidth={2.3} />
                        </Link>
                    </div>
                </div>

                {/* KPI 3: Cần xác thực OCR */}
                <div className="staff-stat-card">
                    <div className="staff-stat-top">
                        <div className="staff-stat-header">
                            <div className="staff-stat-title-wrap">
                                <span
                                    className={`staff-stat-icon ${
                                        occupancy.needsVerificationCount > 0
                                            ? "icon-danger"
                                            : "icon-amber"
                                    }`}
                                >
                                    <ScanLine size={16} strokeWidth={2.2} />
                                </span>
                                <span className="staff-stat-title">
                                    {t("dashNeedsVerif")}
                                </span>
                            </div>
                            <span
                                className={`staff-stat-pill ${
                                    occupancy.needsVerificationCount > 0
                                        ? "pill-danger"
                                        : "pill-emerald"
                                }`}
                            >
                                {occupancy.needsVerificationCount > 0
                                    ? "CẦN XỬ LÝ"
                                    : "ỔN ĐỊNH"}
                            </span>
                        </div>
                        <div
                            className={`staff-stat-number ${
                                occupancy.needsVerificationCount > 0
                                    ? "text-danger"
                                    : ""
                            }`}
                        >
                            {occupancy.needsVerificationCount}
                        </div>
                        <p className="staff-stat-hint">
                            {t("dashNeedsVerifHint")}
                        </p>
                    </div>
                    <div className="staff-stat-footer">
                        <Link
                            href="/staff/verification"
                            className={`staff-link ${
                                occupancy.needsVerificationCount > 0
                                    ? "text-danger"
                                    : ""
                            }`}
                        >
                            <span>
                                {t("dashResolveNow").replace(/\s*→$/, "")}
                            </span>
                            <ArrowRight size={14} strokeWidth={2.3} />
                        </Link>
                    </div>
                </div>

                {/* KPI 4: Tình trạng Barrier */}
                <div className="staff-stat-card">
                    <div className="staff-stat-top">
                        <div className="staff-stat-header">
                            <div className="staff-stat-title-wrap">
                                <span className="staff-stat-icon icon-emerald">
                                    <ShieldCheck size={16} strokeWidth={2.2} />
                                </span>
                                <span className="staff-stat-title">
                                    {t("dashGateHealth")}
                                </span>
                            </div>
                            <span className="staff-stat-pill pill-emerald">
                                IN 01 · OUT 02
                            </span>
                        </div>
                        <div className="staff-stat-number text-emerald">
                            {t("gateOnline").toUpperCase()}
                        </div>
                        <p className="staff-stat-hint">{t("dashGateSynced")}</p>
                    </div>
                    <div className="staff-stat-footer">
                        <span className="staff-pill staff-pill-success">
                            <span className="staff-pulse-dot green" />
                            <span>{t("dashLatency")} 24ms</span>
                        </span>
                    </div>
                </div>
            </section>

            {/* Entry activity & Exit activity columns */}
            <div className="staff-two-col">
                {/* Entry activity */}
                <div className="staff-card">
                    <div className="staff-card-header">
                        <div className="staff-feed-header-left">
                            <span className="staff-feed-title-icon in">
                                <ArrowDownLeft size={16} strokeWidth={2.3} />
                            </span>
                            <h2>{t("dashEntryActivity")}</h2>
                        </div>
                        <span className="staff-feed-live">
                            <span className="staff-pulse-dot green" />
                            <span>
                                {t("dashLiveFeed").replace(/^●\s*/, "")}
                            </span>
                        </span>
                    </div>

                    <div className="staff-activity-list">
                        {entryActivities.length === 0 ? (
                            <p className="staff-empty">
                                Chưa có lượt xe vào nào được ghi nhận trong
                                phiên làm việc.
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
                        <div className="staff-feed-header-left">
                            <span className="staff-feed-title-icon out">
                                <ArrowUpRight size={16} strokeWidth={2.3} />
                            </span>
                            <h2>{t("dashExitActivity")}</h2>
                        </div>
                        <span className="staff-feed-live">
                            <span className="staff-pulse-dot green" />
                            <span>
                                {t("dashLiveFeed").replace(/^●\s*/, "")}
                            </span>
                        </span>
                    </div>

                    <div className="staff-activity-list">
                        {exitActivities.length === 0 ? (
                            <p className="staff-empty">
                                Chưa có lượt xe ra nào được ghi nhận trong phiên
                                làm việc.
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
