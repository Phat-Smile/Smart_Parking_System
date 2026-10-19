"use client";

import React, { useEffect, useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useStaffData } from "./staffData";

export function StaffShell({ children }: { children: React.ReactNode }) {
    const pathname = usePathname();
    const { occupancy, issues } = useStaffData();
    const [currentTime, setCurrentTime] = useState("");

    useEffect(() => {
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
    }, []);

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
            {/* Topbar */}
            <header className="staff-topbar">
                <div className="staff-topbar-left">
                    <Link href="/staff/dashboard" className="staff-brand" title="ParkWise Staff Portal">
                        <div className="staff-brand-logo-wrap">
                            <Image
                                src="/images/parkwise/logo.png"
                                alt="ParkWise"
                                width={118}
                                height={38}
                                className="staff-brand-img"
                                priority
                            />
                        </div>
                        <div className="staff-brand-badge">
                            <span className="staff-brand-station-tag">STAFF STATION</span>
                            <span className="staff-brand-gate-id">GATE 01</span>
                        </div>
                    </Link>

                    <div className="staff-lot-badge">
                        <span className="staff-lot-dot" />
                        <span className="staff-lot-name">ParkWise Central Tower</span>
                        <span className="staff-lot-zone">Basement B1/B2</span>
                    </div>
                </div>

                <div className="staff-topbar-right">
                    {currentTime && (
                        <div className="staff-clock-badge" suppressHydrationWarning>
                            <span className="staff-clock-icon">🕒</span>
                            <span className="staff-clock-time">{currentTime}</span>
                            <span className="staff-clock-tz">ICT (UTC+7)</span>
                        </div>
                    )}

                    <div className="staff-shift-info">
                        <span className="staff-shift-label">Shift #2 · Morning</span>
                        <span className="staff-shift-operator">
                            Operator: <strong>Nguyễn Văn Hải</strong>
                        </span>
                    </div>

                    <div className="staff-barrier-status">
                        <span className="staff-barrier-badge green" title="Barrier In 01 Online">
                            <span className="staff-pulse-dot green" /> IN 01
                        </span>
                        <span className="staff-barrier-badge green" title="Barrier Out 02 Online">
                            <span className="staff-pulse-dot green" /> OUT 02
                        </span>
                    </div>

                    <Link
                        href="/"
                        className="staff-btn staff-btn-ghost staff-btn-sm"
                        title="Return to ParkWise Home"
                    >
                        Exit Station ⎋
                    </Link>
                </div>
            </header>

            {/* Main content + Right-docked Vertical Navigation */}
            <div className="staff-body-wrapper">
                <main className="staff-main-content">{children}</main>

                {/* Vertical Navigation Dock on the RIGHT side of screen */}
                <aside className="staff-right-sidebar">
                    <div className="staff-sidebar-header">
                        <span className="staff-sidebar-tag">STAFF FUNCTIONS</span>
                        <span className="staff-sidebar-sub">Control Center</span>
                    </div>

                    <nav className="staff-vertical-nav" aria-label="Staff Station Navigation">
                        {navItems.map((item) => {
                            const isActive = pathname === item.href;
                            return (
                                <Link
                                    key={item.href}
                                    href={item.href}
                                    className={`staff-vertical-nav-item ${isActive ? "active" : ""}`}
                                >
                                    <div className="staff-nav-item-left">
                                        <span className="staff-nav-icon">{item.icon}</span>
                                        <span className="staff-nav-label">{item.label}</span>
                                    </div>
                                    {typeof item.badge === "number" && item.badge > 0 && (
                                        <span
                                            className={`staff-nav-badge ${
                                                item.badgeDanger ? "badge-danger" : ""
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
                        <div className="staff-gate-health-card">
                            <div className="staff-gate-health-title">
                                <span className="staff-pulse-dot green" />
                                <span>GATE HEALTH</span>
                            </div>
                            <div className="staff-gate-health-meta">
                                <div className="staff-health-row">
                                    <span>Gate In 01:</span>
                                    <strong className="text-emerald">Online (Ready)</strong>
                                </div>
                                <div className="staff-health-row">
                                    <span>Gate Out 02:</span>
                                    <strong className="text-emerald">Online (Ready)</strong>
                                </div>
                                <div className="staff-health-row">
                                    <span>Latency:</span>
                                    <span className="staff-health-val">24ms</span>
                                </div>
                            </div>
                        </div>
                    </div>
                </aside>
            </div>
        </div>
    );
}
