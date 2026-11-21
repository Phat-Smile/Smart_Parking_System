"use client";

import React, { useState, useSyncExternalStore } from "react";
import Image from "next/image";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import {
    getAuthSessionSnapshot,
    subscribeAuthSession,
} from "@/features/auth/authSession";
import { logoutParkWiseAccount } from "@/features/auth/logoutParkWiseAccount";
import { NotificationBell } from "@/features/owner/notifications/NotificationBell";
import { useOwnerViewMode } from "@/features/owner/mode/useOwnerViewMode";

const IC: Record<string, string> = {
    grid: "M4 4h6v6H4zM14 4h6v6h-6zM4 14h6v6H4zM14 14h6v6h-6z",
    lot: "M4 20V8l8-4 8 4v12M9 20v-6h6v6",
    cube: "M12 3l8 4.5v9L12 21l-8-4.5v-9zM4 7.5l8 4.5 8-4.5M12 12v9",
    // Sơ đồ 2D – icon lưới phẳng
    grid2d: "M3 3h7v7H3zM14 3h7v7h-7zM3 14h7v7H3zM14 14h7v7h-7z",
    cal: "M5 5h14v15H5zM5 10h14M9 3v4M15 3v4",
    car: "M4 15l1.5-5h13L20 15v4h-3v-2H7v2H4zM6 15h12",
    chart: "M4 20V4M4 20h16M8 16v-5M13 16V8M18 16v-9",
    clock: "M12 2a10 10 0 1 0 10 10A10 10 0 0 0 12 2zm0 18a8 8 0 1 1 8-8 8 8 0 0 1-8 8zm.5-13H11v6l5.2 3.1.8-1.3-4.5-2.7z",
    tag: "M20.59 13.41l-7.17 7.17a2 2 0 0 1-2.83 0L2 12V2h10l8.59 8.59a2 2 0 0 1 0 2.82zM7 7h.01",
};

/** SVG icon helper */
function Icon({ d, size = 18 }: { d: string; size?: number }) {
    return (
        <svg
            width={size}
            height={size}
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.8"
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
        >
            <path d={d} />
        </svg>
    );
}

export function OwnerSidebar() {
    const pathname = usePathname();
    const router = useRouter();
    const [viewMode, setViewMode] = useOwnerViewMode();
    const isSampleMode =
        viewMode === "sample" || pathname.startsWith("/owner/simulation");

    const session = useSyncExternalStore(
        subscribeAuthSession,
        getAuthSessionSnapshot,
        () => null,
    );

    const displayName =
        session?.displayName ||
        (session?.account?.email
            ? session.account.email.split("@")[0]
            : "Chủ bãi");
    const initial = (displayName.charAt(0) || "M").toUpperCase();

    async function handleLogout() {
        await logoutParkWiseAccount().catch(() => undefined);
        router.push("/login");
    }

    // Submenu "Sơ đồ 2D / 3D" tự mở khi đang ở /owner/layout/*
    const isLayoutActive = pathname.startsWith("/owner/layout");
    const [layoutOpen, setLayoutOpen] = useState(isLayoutActive);

    // Nav items tự thích ứng: Ở Bản mẫu thì link luôn giữ trong Bản mẫu, không đẩy ra Bản chính
    const navItems = [
        {
            href: isSampleMode ? "/owner/simulation" : "/owner/dashboard",
            label: "Tổng quan",
            icon: "grid",
            match: (p: string) =>
                isSampleMode
                    ? p.startsWith("/owner/simulation")
                    : p === "/owner/dashboard" || p === "/owner",
        },
        {
            href: isSampleMode
                ? "/owner/parking-lots?mode=sample"
                : "/owner/parking-lots",
            label: "Bãi của tôi",
            icon: "lot",
            match: (p: string) => p.startsWith("/owner/parking-lots"),
        },
        {
            href: isSampleMode
                ? "/owner/opening-hours?mode=sample"
                : "/owner/opening-hours",
            label: "Giờ hoạt động",
            icon: "clock",
            match: (p: string) => p.startsWith("/owner/opening-hours"),
        },
        {
            href: isSampleMode
                ? "/owner/pricing?mode=sample"
                : "/owner/pricing",
            label: "Bảng giá & Cọc",
            icon: "tag",
            match: (p: string) => p.startsWith("/owner/pricing"),
        },
        {
            href: isSampleMode
                ? "/owner/reservations?mode=sample"
                : "/owner/reservations",
            label: "Đặt chỗ",
            icon: "cal",
            match: (p: string) => p.startsWith("/owner/reservations"),
        },
        {
            href: isSampleMode
                ? "/owner/sessions?mode=sample"
                : "/owner/sessions",
            label: "Phiên đỗ xe",
            icon: "car",
            match: (p: string) => p.startsWith("/owner/sessions"),
        },
        {
            href: isSampleMode
                ? "/owner/reports?mode=sample"
                : "/owner/reports",
            label: "Báo cáo doanh thu",
            icon: "chart",
            match: (p: string) => p.startsWith("/owner/reports"),
        },
    ];

    const layoutSubmenu = [
        {
            href: isSampleMode
                ? "/owner/layout/3d?mode=sample"
                : "/owner/layout/3d",
            label: "Sơ đồ 3D",
            icon: "cube",
        },
        {
            href: isSampleMode
                ? "/owner/layout/2d?mode=sample"
                : "/owner/layout/2d",
            label: "Sơ đồ 2D",
            icon: "grid2d",
        },
        {
            href: isSampleMode
                ? "/owner/layout/editor?mode=sample"
                : "/owner/layout/editor",
            label: "Chỉnh sửa sơ đồ",
            icon: "grid",
        },
    ];

    return (
        <aside className="side" id="side">
            {/* Brand / Logo đồng nhất trang chủ & Chuông thông báo */}
            <div className="side-brand-header">
                <Link
                    href={
                        isSampleMode ? "/owner/simulation" : "/owner/dashboard"
                    }
                    className="brand-logo-link"
                    aria-label="ParkWise Dashboard"
                >
                    <Image
                        src="/images/parkwise/logo.png"
                        alt="ParkWise"
                        width={130}
                        height={40}
                        style={{
                            height: 34,
                            width: "auto",
                            objectFit: "contain",
                        }}
                        priority
                    />
                </Link>
                <NotificationBell variant="sidebar" />
            </div>

            {/* Mục thẳng trước submenu: Tổng quan và Bãi của tôi */}
            {navItems.slice(0, 2).map((item) => {
                const isActive = item.match(pathname);
                return (
                    <Link
                        key={item.href}
                        href={item.href}
                        className={isActive ? "on" : ""}
                    >
                        <Icon d={IC[item.icon]} />
                        {item.label}
                    </Link>
                );
            })}

            {/* ── Submenu: Sơ đồ 2D / 3D ── */}
            <button
                className={`nav-parent ${isLayoutActive ? "on" : ""}`}
                onClick={() => setLayoutOpen((o) => !o)}
                aria-expanded={layoutOpen}
                aria-controls="layout-submenu"
            >
                <Icon d={IC.cube} />
                <span style={{ flex: 1, textAlign: "left" }}>
                    Sơ đồ 2D / 3D
                </span>
                <span className="nav-arrow" aria-hidden="true">
                    {layoutOpen ? "▾" : "▸"}
                </span>
            </button>

            {layoutOpen && (
                <div id="layout-submenu" className="nav-sub">
                    {layoutSubmenu.map((item) => {
                        const isActive =
                            pathname === item.href ||
                            pathname.startsWith(item.href + "/");
                        return (
                            <Link
                                key={item.href}
                                href={item.href}
                                className={`nav-sub-item ${isActive ? "on" : ""}`}
                            >
                                <Icon d={IC[item.icon]} size={15} />
                                {item.label}
                            </Link>
                        );
                    })}
                </div>
            )}

            {/* Mục thẳng sau submenu */}
            {navItems.slice(2).map((item) => {
                const isActive = item.match(pathname);
                return (
                    <Link
                        key={item.href}
                        href={item.href}
                        className={isActive ? "on" : ""}
                    >
                        <Icon d={IC[item.icon]} />
                        {item.label}
                    </Link>
                );
            })}

            {/* Khu vực tiện ích: Chế độ mô phỏng Sandbox */}
            <div
                style={{
                    marginTop: 12,
                    marginBottom: 8,
                    borderTop: "1px solid rgba(255,255,255,0.08)",
                    paddingTop: 10,
                }}
            >
                {isSampleMode ? (
                    <div
                        style={{
                            display: "flex",
                            flexDirection: "column",
                            gap: 6,
                            padding: "8px 10px",
                            borderRadius: 10,
                            background: "rgba(234, 179, 8, 0.12)",
                            border: "1px solid rgba(234, 179, 8, 0.35)",
                        }}
                    >
                        <div
                            style={{
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "space-between",
                            }}
                        >
                            <span
                                style={{
                                    fontSize: 11.5,
                                    fontWeight: 750,
                                    color: "#facc15",
                                }}
                            >
                                🟡 Đang Xem Mẫu
                            </span>
                            <span
                                style={{
                                    fontSize: 9,
                                    padding: "2px 5px",
                                    borderRadius: 4,
                                    background: "#ca8a04",
                                    color: "#fff",
                                    textTransform: "uppercase",
                                    fontWeight: 800,
                                }}
                            >
                                Mô phỏng
                            </span>
                        </div>
                        <button
                            type="button"
                            onClick={() => {
                                setViewMode("live");
                                router.push("/owner/dashboard");
                            }}
                            style={{
                                width: "100%",
                                border: "1px solid rgba(255, 255, 255, 0.2)",
                                background: "rgba(0, 0, 0, 0.45)",
                                color: "#ffffff",
                                padding: "6px 8px",
                                borderRadius: 6,
                                fontSize: 11,
                                fontWeight: 700,
                                cursor: "pointer",
                                textAlign: "center",
                                transition: "all 0.15s ease",
                            }}
                        >
                            Về Dashboard chính →
                        </button>
                    </div>
                ) : (
                    <Link
                        href="/owner/simulation"
                        onClick={() => setViewMode("sample")}
                        style={{
                            display: "flex",
                            alignItems: "center",
                            gap: 10,
                            background: "rgba(255, 255, 255, 0.04)",
                            border: "1px solid rgba(234, 179, 8, 0.3)",
                            color: "var(--pw-dark-muted)",
                            borderRadius: 10,
                            padding: "8px 12px",
                            fontSize: 12.5,
                            fontWeight: 600,
                            textDecoration: "none",
                            transition: "all 0.15s ease",
                        }}
                    >
                        <span style={{ flex: 1 }}>Bản mô phỏng</span>
                        <span
                            style={{
                                fontSize: 9,
                                padding: "2px 5px",
                                borderRadius: 4,
                                background: "rgba(255,255,255,0.12)",
                                color: "#fff",
                                textTransform: "uppercase",
                                fontWeight: 800,
                            }}
                        >
                            Mô phỏng
                        </span>
                    </Link>
                )}
            </div>

            {/* User profile card - liên kết trực tiếp tới /owner/profile */}
            <Link
                href="/owner/profile"
                className={`user ${pathname === "/owner/profile" ? "on" : ""}`}
                style={{ textDecoration: "none", cursor: "pointer" }}
                title="Xem hồ sơ chủ bãi"
            >
                <span className="av">{initial}</span>
                <div style={{ minWidth: 0, overflow: "hidden" }}>
                    <div
                        className="uname"
                        style={{
                            textOverflow: "ellipsis",
                            overflow: "hidden",
                            whiteSpace: "nowrap",
                        }}
                    >
                        {displayName}
                    </div>
                    <div className="urole">Chủ bãi</div>
                </div>
            </Link>

            {/* Nút Đăng xuất */}
            <button
                type="button"
                className="logout-btn"
                onClick={handleLogout}
                aria-label="Đăng xuất"
            >
                <Icon d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4M16 17l5-5-5-5M21 12H9" />
                <span>Đăng xuất</span>
            </button>

            {/* CSS scoped cho submenu */}
            <style>{`
        .logout-btn {
          display: flex;
          gap: 12px;
          align-items: center;
          border: 0;
          background: none;
          color: var(--pw-dark-muted);
          padding: 10px 12px;
          border-radius: 10px;
          font: inherit;
          font-size: 13.5px;
          cursor: pointer;
          width: 100%;
          margin-top: 10px;
          text-align: left;
          transition: background 0.15s ease, color 0.15s ease;
        }
        .logout-btn:hover {
          background: var(--pw-dark-3);
          color: #fff;
        }
        .nav-parent {
          display: flex;
          gap: 12px;
          align-items: center;
          border: 0;
          background: none;
          color: var(--pw-dark-muted);
          padding: 10px 12px;
          border-radius: 10px;
          font: inherit;
          font-size: 13.5px;
          cursor: pointer;
          text-align: left;
          width: 100%;
          text-decoration: none;
          transition: background 0.15s ease, color 0.15s ease;
        }
        .nav-parent:hover { background: var(--pw-dark-3); color: #fff; }
        .nav-parent.on {
          background: var(--pw-lime-soft);
          color: var(--pw-lime);
          font-weight: 600;
        }
        .nav-arrow { font-size: 11px; opacity: 0.7; flex-shrink: 0; }

        .nav-sub {
          margin-left: 8px;
          border-left: 2px solid var(--pw-dark-border, rgba(255,255,255,0.07));
          padding-left: 4px;
          display: flex;
          flex-direction: column;
          gap: 2px;
          animation: subOpen 0.15s ease;
        }
        @keyframes subOpen {
          from { opacity: 0; transform: translateY(-4px); }
          to   { opacity: 1; transform: translateY(0); }
        }

        .nav-sub-item {
          display: flex;
          gap: 10px;
          align-items: center;
          color: var(--pw-dark-muted);
          padding: 8px 12px;
          border-radius: 8px;
          font-size: 13px;
          text-decoration: none;
          transition: background 0.12s ease, color 0.12s ease;
        }
        .nav-sub-item:hover { background: var(--pw-dark-3); color: #fff; }
        .nav-sub-item.on {
          background: var(--pw-lime-soft);
          color: var(--pw-lime);
          font-weight: 600;
        }
      `}</style>
        </aside>
    );
}
