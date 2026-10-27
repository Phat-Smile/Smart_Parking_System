"use client";

import React from "react";
import Link from "next/link";
import { PlateBadge, FormattedTime, formatVnd } from "./StaffUi";
import { useStaffData } from "./staffData";
import { useLanguage } from "@/shared/i18n/LanguageContext";

export function StaffDashboardPage() {
    const { occupancy, activities, assignedLot, fetchAssignedLot } =
        useStaffData();
    const { t } = useLanguage();

    React.useEffect(() => {
        fetchAssignedLot().catch(() => {});
    }, [fetchAssignedLot]);

    const occupancyPercent = Math.min(
        100,
        Math.round((occupancy.occupied / occupancy.totalCapacity) * 100),
    );

    const entryActivities = activities
        .filter((a) => a.type === "entry")
        .slice(0, 6);
    const exitActivities = activities
        .filter((a) => a.type === "exit")
        .slice(0, 6);

    return (
        <div className="staff-page">
            <header className="staff-header">
                <div>
                    <h1 className="staff-title">
                        {t("dashTitle")} — {assignedLot.lotName}
                    </h1>
                    <p className="staff-subtitle">{t("dashSubtitle")}</p>
                </div>

                <div className="staff-header-actions">
                    <Link
                        href="/staff/entry"
                        className="staff-btn staff-btn-primary"
                    >
                        {t("dashProcessEntry")}
                    </Link>
                    <Link
                        href="/staff/exit"
                        className="staff-btn staff-btn-secondary"
                    >
                        {t("dashProcessExit")}
                    </Link>
                </div>
            </header>

            {/* 3.8 Current occupancy & High level stats */}
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
                            / {occupancy.totalCapacity} {t("dashSlots")}
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
                {/* 3.8 Entry activity */}
                <div className="staff-card">
                    <div className="staff-card-header">
                        <h2>{t("dashEntryActivity")}</h2>
                        <span className="staff-feed-live">
                            {t("dashLiveFeed")}
                        </span>
                    </div>

                    <div className="staff-activity-list">
                        {entryActivities.length === 0 ? (
                            <p className="staff-empty">{t("dashNoEntry")}</p>
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

                {/* 3.8 Exit activity */}
                <div className="staff-card">
                    <div className="staff-card-header">
                        <h2>{t("dashExitActivity")}</h2>
                        <span className="staff-feed-live">
                            {t("dashLiveFeed")}
                        </span>
                    </div>

                    <div className="staff-activity-list">
                        {exitActivities.length === 0 ? (
                            <p className="staff-empty">{t("dashNoExit")}</p>
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
