"use client";

import React, { useEffect, useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useStaffData } from "./staffData";

export function StaffShell({ children }: { children: React.ReactNode }) {
    const pathname = usePathname();
    const { occupancy, issues, assignedLot, fetchAssignedLot } = useStaffData();
    const [currentTime, setCurrentTime] = useState("");

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

    const navItems = [
        {
            href: "/staff/dashboard",
            label: "Dashboard",
            icon: "📊",
        },
        {
            href: "/staff/entry",
            label: "Gate In",
            icon: "🚗",
        },
        {
            href: "/staff/exit",
            label: "Gate Out",
            icon: "💳",
        },
        {
            href: "/staff/sessions",
            label: "Guest & Sessions",
            icon: "📋",
            badge: occupancy.activeSessionsCount,
        },
        {
            href: "/staff/verification",
            label: "Needs Verification",
            icon: "⚠️",
            badge: issues.length,
            badgeDanger: issues.length > 0,
        },
    ];

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
                            width={112}
                            height={36}
                            className="staff-brand-img"
                            priority
                        />
                    </Link>
                    <div className="staff-sidebar-tag-wrap">
                        <span className="staff-brand-station-tag">
                            STAFF STATION
                        </span>
                        <span className="staff-brand-gate-id">
                            GATE 01 · ZONE B1/B2
                        </span>
                    </div>
                </div>

                <div className="staff-sidebar-section-title">
                    CHỨC NĂNG VẬN HÀNH
                </div>

                <nav
                    className="staff-vertical-nav"
                    aria-label="Staff Station Navigation"
                >
                    {navItems.map((item) => {
                        const isActive = pathname === item.href;
                        return (
                            <Link
                                key={item.href}
                                href={item.href}
                                className={`staff-vertical-nav-item ${isActive ? "active" : ""}`}
                            >
                                <div className="staff-nav-item-left">
                                    <span className="staff-nav-icon">
                                        {item.icon}
                                    </span>
                                    <span className="staff-nav-label">
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
                                Ca #2 · Sáng
                            </span>
                            <strong className="staff-operator-name">
                                Nguyễn Văn Hải
                            </strong>
                        </div>
                    </div>

                    <div className="staff-gate-health-card">
                        <div className="staff-gate-health-title">
                            <span className="staff-pulse-dot green" />
                            <span>TRẠNG THÁI BARRIER</span>
                        </div>
                        <div className="staff-gate-health-meta">
                            <div className="staff-health-row">
                                <span>Cổng Vào 01:</span>
                                <strong className="text-emerald">Online</strong>
                            </div>
                            <div className="staff-health-row">
                                <span>Cổng Ra 02:</span>
                                <strong className="text-emerald">Online</strong>
                            </div>
                        </div>
                    </div>

                    <Link
                        href="/"
                        className="staff-btn staff-btn-exit-sidebar"
                        title="Return to ParkWise Home"
                    >
                        <span>Thoát Trạm</span>
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
                            <span className="staff-lot-name">
                                {assignedLot.lotName}
                            </span>
                            <span className="staff-lot-zone">
                                {assignedLot.address || "Tầng Hầm B1/B2"}
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
                    </div>
                </header>

                <main className="staff-main-content">{children}</main>
            </div>
        </div>
    );
}
