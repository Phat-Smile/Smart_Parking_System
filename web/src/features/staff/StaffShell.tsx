"use client";

import React from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useStaffData } from "./staffData";

export function StaffShell({ children }: { children: React.ReactNode }) {
    const pathname = usePathname();
    const { occupancy, issues } = useStaffData();

    const navItems = [
        {
            href: "/staff/dashboard",
            label: "Dashboard",
            icon: "📊",
        },
        {
            href: "/staff/entry",
            label: "Gate In (Entry)",
            icon: "🚗",
        },
        {
            href: "/staff/exit",
            label: "Gate Out (Exit)",
            icon: "💳",
        },
        {
            href: "/staff/sessions",
            label: "Active Sessions",
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
                    <Link href="/staff/dashboard" className="staff-brand">
                        <span className="staff-brand-logo">🅿️</span>
                        <div className="staff-brand-meta">
                            <span className="staff-brand-name">PARKWISE</span>
                            <span className="staff-brand-tag">STAFF STATION</span>
                        </div>
                    </Link>

                    <div className="staff-lot-badge">
                        <span className="staff-lot-dot" />
                        <span>Vincom Center Basement · Zone B1/B2</span>
                    </div>
                </div>

                <div className="staff-topbar-right">
                    <div className="staff-shift-info">
                        <span className="staff-shift-label">Shift #2 (Morning)</span>
                        <span className="staff-shift-operator">
                            Operator: <strong>Staff Nguyễn Văn Hải</strong>
                        </span>
                    </div>

                    <div className="staff-barrier-status">
                        <span className="staff-barrier-badge green">
                            Gate In 01: READY
                        </span>
                        <span className="staff-barrier-badge green">
                            Gate Out 02: READY
                        </span>
                    </div>

                    <Link
                        href="/auth/login"
                        className="staff-btn staff-btn-ghost staff-btn-sm"
                        title="Sign out of Staff Station"
                    >
                        Sign out
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
