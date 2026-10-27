"use client";

import React from "react";
import Link from "next/link";
import { PlateBadge, FormattedTime, formatVnd } from "./StaffUi";
import { useStaffData } from "./staffData";

export function StaffDashboardPage() {
    const { occupancy, activities, assignedLot, fetchAssignedLot } =
        useStaffData();

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
                        Staff Dashboard — {assignedLot.lotName}
                    </h1>
                    <p className="staff-subtitle">
                        Real-time gate traffic, slot capacity, and pending
                        verifications.
                    </p>
                </div>

                <div className="staff-header-actions">
                    <Link
                        href="/staff/entry"
                        className="staff-btn staff-btn-primary"
                    >
                        ➕ Process Vehicle Entry
                    </Link>
                    <Link
                        href="/staff/exit"
                        className="staff-btn staff-btn-secondary"
                    >
                        ⚡ Process Vehicle Exit
                    </Link>
                </div>
            </header>

            {/* 3.8 Current occupancy & High level stats */}
            <section className="staff-stats-grid">
                <div className="staff-stat-card occupancy-card">
                    <div className="staff-stat-header">
                        <span className="staff-stat-title">
                            Current Occupancy
                        </span>
                        <span className="staff-stat-pill">
                            {occupancyPercent}% FULL
                        </span>
                    </div>
                    <div className="staff-occupancy-value">
                        <strong>{occupancy.occupied}</strong>
                        <span>/ {occupancy.totalCapacity} Slots</span>
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
                            <span>Car Slots Available:</span>
                            <strong className="text-emerald">
                                {occupancy.carAvailable} slots
                            </strong>
                        </div>
                        <div className="staff-slot-sub">
                            <span>Motorbike Available:</span>
                            <strong className="text-emerald">
                                {occupancy.motorbikeAvailable} slots
                            </strong>
                        </div>
                    </div>
                </div>

                <div className="staff-stat-card">
                    <span className="staff-stat-title">Active Sessions</span>
                    <div className="staff-stat-number">
                        {occupancy.activeSessionsCount}
                    </div>
                    <p className="staff-stat-hint">Vehicles parked right now</p>
                    <Link href="/staff/sessions" className="staff-link">
                        View all active sessions →
                    </Link>
                </div>

                <div className="staff-stat-card">
                    <span className="staff-stat-title">Needs Verification</span>
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
                        Camera OCR flags & barrier issues
                    </p>
                    <Link
                        href="/staff/verification"
                        className="staff-link text-danger"
                    >
                        Resolve issues now →
                    </Link>
                </div>

                <div className="staff-stat-card">
                    <span className="staff-stat-title">
                        Barrier Gate Health
                    </span>
                    <div className="staff-stat-number text-emerald">ONLINE</div>
                    <p className="staff-stat-hint">Gate 01 & Gate 02 Synced</p>
                    <span className="staff-pill staff-pill-success">
                        Latency 24ms
                    </span>
                </div>
            </section>

            {/* Entry activity & Exit activity columns */}
            <div className="staff-two-col">
                {/* 3.8 Entry activity */}
                <div className="staff-card">
                    <div className="staff-card-header">
                        <h2>Entry Activity (Gate In)</h2>
                        <span className="staff-feed-live">● Live feed</span>
                    </div>

                    <div className="staff-activity-list">
                        {entryActivities.length === 0 ? (
                            <p className="staff-empty">
                                No recent vehicle entries.
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
                                            {act.vehicleType} ·{" "}
                                            {act.customerType}
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
                        <h2>Exit Activity (Gate Out)</h2>
                        <span className="staff-feed-live">● Live feed</span>
                    </div>

                    <div className="staff-activity-list">
                        {exitActivities.length === 0 ? (
                            <p className="staff-empty">
                                No recent vehicle exits.
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
                                            {act.vehicleType} ·{" "}
                                            {act.customerType}
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
