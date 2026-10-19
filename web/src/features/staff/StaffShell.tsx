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
            label: "Dashboard (3.8)",
            icon: "📊",
        },
        {
            href: "/staff/entry",
            label: "Gate In (3.9 - 3.11)",
            icon: "🚗",
        },
        {
            href: "/staff/exit",
            label: "Gate Out (3.14 - 3.18)",
            icon: "💳",
        },
        {
            href: "/staff/sessions",
            label: "Guest & Sessions (3.12)",
            icon: "📋",
            badge: occupancy.activeSessionsCount,
        },
        {
            href: "/staff/verification",
            label: "Needs Verification (3.13)",
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

            {/* Main navigation tabs */}
            <nav className="staff-nav">
                <div className="staff-nav-container">
                    {navItems.map((item) => {
                        const isActive = pathname === item.href;
                        return (
                            <Link
                                key={item.href}
                                href={item.href}
                                className={`staff-nav-tab ${isActive ? "active" : ""}`}
                            >
                                <span className="staff-nav-icon">{item.icon}</span>
                                <span>{item.label}</span>
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
                </div>
            </nav>

            {/* Page content */}
            <main className="staff-main">{children}</main>
        </div>
    );
}
