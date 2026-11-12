"use client";

import React, { useEffect, useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useStaffData } from "./staffData";
import {
    getStaffAssignedShift,
    getStaffLots,
    getStaffProfile,
    saveStoredStaffLotId,
    type StaffProfile,
    type StaffShiftDetails,
} from "./staffApi";
import type { StaffLotItem } from "./types";
import { useLanguage, LanguageSwitcher } from "@/shared/i18n/LanguageContext";
import { StaffNotifications } from "./StaffNotifications";

export function StaffShell({ children }: { children: React.ReactNode }) {
    const pathname = usePathname();
    const { occupancy, issues, assignedLot, fetchAssignedLot } = useStaffData();
    const [currentTime, setCurrentTime] = useState("");

    const [staffProfile, setStaffProfile] = useState<StaffProfile | null>(null);
    const [staffLots, setStaffLots] = useState<StaffLotItem[]>([]);
    const [assignedShift, setAssignedShift] = useState<StaffShiftDetails>({
        shift: "morning",
        shiftName: "Ca 1 - Sáng",
        hours: "06:00 - 14:00",
    });

    useEffect(() => {
        fetchAssignedLot().catch(() => {});
        const updateClock = () => {
            const now = new Date();
            setCurrentTime(
                now.toLocaleTimeString("vi-VN", {
                    hour: "2-digit",
                    minute: "2-digit",
                    second: "2-digit",
                }),
            );
        };
        updateClock();
        const timer = setInterval(updateClock, 1000);
        return () => clearInterval(timer);
    }, [fetchAssignedLot]);

    useEffect(() => {
        let active = true;
        Promise.all([getStaffProfile(), getStaffLots()])
            .then(async ([profile, lots]) => {
                if (!active) return;
                setStaffProfile(profile);
                setStaffLots(lots);
                const targetLotId =
                    assignedLot?.lotId || (lots.length > 0 ? lots[0].id : "");
                const shiftInfo = await getStaffAssignedShift(
                    targetLotId,
                    profile?.id,
                );
                if (active) setAssignedShift(shiftInfo);
            })
            .catch(() => {});
        return () => {
            active = false;
        };
    }, [assignedLot?.lotId]);

    const handleLotSelect = (lotId: string) => {
        if (!lotId || lotId === assignedLot?.lotId) return;
        saveStoredStaffLotId(lotId);
        fetchAssignedLot(lotId).catch(() => {});
    };

    const { t, language } = useLanguage();

    const navItems = [
        {
            href: "/staff/dashboard",
            label: t("staffDashboard"),
            icon: "📊",
        },
        {
            href: "/staff/entry",
            label: t("staffEntryGate"),
            icon: "🚗",
        },
        {
            href: "/staff/exit",
            label: t("staffExitGate"),
            icon: "💳",
        },
        {
            href: "/staff/simulator",
            label:
                language === "en"
                    ? "IoT Barrier Simulator"
                    : "Mô phỏng Barrier IoT",
            icon: "🚧",
        },
        {
            href: "/staff/sessions",
            label: t("staffSessions"),
            icon: "📋",
            badge: occupancy.activeSessionsCount,
        },
        {
            href: "/staff/verification",
            label: t("dashNeedsVerif"),
            icon: "⚠️",
            badge: issues.length,
            badgeDanger: issues.length > 0,
        },
    ];

    const operatorName =
        staffProfile?.fullName ||
        (staffProfile?.email
            ? staffProfile.email.split("@")[0]
            : "Nhân viên trực bãi");

    return (
        <div className="staff-layout">
            {/* Left Sidebar (Navigation on the LEFT side of screen) */}
            <aside className="staff-left-sidebar">
                <div className="staff-sidebar-brand">
                    <Link
                        href="/staff/dashboard"
                        className="staff-brand"
                        title="ParkWise Staff Portal"
                    >
                        <Image
                            src="/images/parkwise/logo.png"
                            alt="ParkWise"
                            width={707}
                            height={353}
                            className="staff-brand-img"
                            priority
                        />
                    </Link>
                    <div className="staff-sidebar-tag-wrap">
                        <span className="staff-brand-station-tag">
                            {t("staffStation")}
                        </span>
                        <span className="staff-brand-gate-id">
                            GATE 01 · ZONE B1/B2
                        </span>
                    </div>
                </div>

                <div className="staff-sidebar-section-title">
                    {t("staffOperations")}
                </div>

                <nav
                    className="staff-vertical-nav"
                    aria-label="Staff Station Navigation"
                    suppressHydrationWarning
                >
                    {navItems.map((item) => {
                        const isActive = pathname === item.href;
                        return (
                            <Link
                                key={item.href}
                                href={item.href}
                                className={`staff-vertical-nav-item ${isActive ? "active" : ""}`}
                                suppressHydrationWarning
                            >
                                <div className="staff-nav-item-left">
                                    <span className="staff-nav-icon">
                                        {item.icon}
                                    </span>
                                    <span
                                        className="staff-nav-label"
                                        suppressHydrationWarning
                                    >
                                        {item.label}
                                    </span>
                                </div>
                                {typeof item.badge === "number" &&
                                    item.badge > 0 && (
                                        <span
                                            className={`staff-nav-badge ${
                                                item.badgeDanger
                                                    ? "badge-danger"
                                                    : ""
                                            }`}
                                        >
                                            {item.badge}
                                        </span>
                                    )}
                            </Link>
                        );
                    })}
                </nav>

                <div className="staff-sidebar-footer">
                    <div className="staff-operator-card">
                        <div className="staff-operator-avatar">👤</div>
                        <div className="staff-operator-info">
                            <span className="staff-operator-shift">
                                {assignedShift.shiftName}
                                {assignedShift.hours && (
                                    <small
                                        style={{
                                            display: "block",
                                            opacity: 0.8,
                                            fontSize: "0.75rem",
                                        }}
                                    >
                                        {assignedShift.hours}
                                    </small>
                                )}
                            </span>
                            <strong className="staff-operator-name">
                                {operatorName}
                            </strong>
                        </div>
                    </div>

                    <div className="staff-gate-health-card">
                        <div className="staff-gate-health-title">
                            <span className="staff-pulse-dot green" />
                            <span>{t("barrierStatus")}</span>
                        </div>
                        <div className="staff-gate-health-meta">
                            <div className="staff-health-row">
                                <span>{t("gateIn")} 01:</span>
                                <strong className="text-emerald">
                                    {t("gateOnline")}
                                </strong>
                            </div>
                            <div className="staff-health-row">
                                <span>{t("gateOut")} 02:</span>
                                <strong className="text-emerald">
                                    {t("gateOnline")}
                                </strong>
                            </div>
                        </div>
                    </div>

                    <Link
                        href="/"
                        className="staff-btn staff-btn-exit-sidebar"
                        title="Return to ParkWise Home"
                    >
                        <span>{t("signOut")}</span>
                        <span>⎋</span>
                    </Link>
                </div>
            </aside>

            {/* Right Side: Topbar + Main Page Content */}
            <div className="staff-main-wrapper">
                <header className="staff-topbar">
                    <div className="staff-topbar-left">
                        <div className="staff-lot-badge">
                            <span className="staff-lot-dot" />
                            {staffLots.length > 1 ? (
                                <select
                                    aria-label={
                                        language === "en"
                                            ? "Select active parking lot"
                                            : "Chọn bãi trực"
                                    }
                                    className="staff-lot-name"
                                    value={
                                        assignedLot?.lotId ??
                                        staffLots[0]?.id ??
                                        ""
                                    }
                                    onChange={(e) =>
                                        handleLotSelect(e.target.value)
                                    }
                                    style={{
                                        background: "transparent",
                                        border: "none",
                                        cursor: "pointer",
                                        outline: "none",
                                    }}
                                >
                                    {staffLots.map((lot) => (
                                        <option key={lot.id} value={lot.id}>
                                            {lot.name}
                                        </option>
                                    ))}
                                </select>
                            ) : (
                                <span className="staff-lot-name">
                                    {assignedLot?.lotName ??
                                        "Chưa được phân công bãi"}
                                </span>
                            )}
                            <span className="staff-lot-zone">
                                {assignedLot?.address || "Bãi trực phân công"}
                            </span>
                        </div>
                    </div>

                    <div className="staff-topbar-right">
                        {currentTime && (
                            <div
                                className="staff-clock-badge"
                                suppressHydrationWarning
                            >
                                <span className="staff-clock-icon">🕒</span>
                                <span className="staff-clock-time">
                                    {currentTime}
                                </span>
                                <span className="staff-clock-tz">
                                    ICT (UTC+7)
                                </span>
                            </div>
                        )}

                        <div className="staff-barrier-status">
                            <span
                                className="staff-barrier-badge green"
                                title="Barrier In 01 Ready"
                            >
                                <span className="staff-pulse-dot green" /> IN 01
                            </span>
                            <span
                                className="staff-barrier-badge green"
                                title="Barrier Out 02 Ready"
                            >
                                <span className="staff-pulse-dot green" /> OUT
                                02
                            </span>
                        </div>

                        <StaffNotifications />
                        <LanguageSwitcher variant="header" />
                    </div>
                </header>

                <main className="staff-main-content">{children}</main>
            </div>
        </div>
    );
}
