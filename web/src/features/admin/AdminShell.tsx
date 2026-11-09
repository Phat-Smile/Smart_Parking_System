"use client";

import { useEffect, useState, useSyncExternalStore, type ReactNode } from "react";
import Image from "next/image";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import {
    getAuthSessionSnapshot,
    subscribeAuthSession,
    setAuthSession,
} from "@/features/auth/authSession";
import { logoutParkWiseAccount } from "@/features/auth/logoutParkWiseAccount";
import { loginParkWiseAccount } from "@/features/auth/loginParkWiseAccount";
import { AdminIcon } from "./AdminUi";
import { countByStatus, useAdminLots } from "./adminLotsData";
import { getOwnerApplications } from "./ownerApplicationsApi";
import { AdminNotifications } from "./AdminNotifications";
import { useLanguage, LanguageSwitcher } from "@/shared/i18n/LanguageContext";

export function AdminShell({ children }: { children: ReactNode }) {
    const pathname = usePathname();
    const router = useRouter();
    const { t } = useLanguage();
    const session = useSyncExternalStore(
        subscribeAuthSession,
        getAuthSessionSnapshot,
        () => null,
    );
    const pending = countByStatus(useAdminLots()).Pending;
    const [pendingOwnerApps, setPendingOwnerApps] = useState<number>(0);
    const role = session?.account.role;

    useEffect(() => {
        if (!session || session.account.role !== "SystemAdmin") return;
        let active = true;
        getOwnerApplications({ status: "Pending" })
            .then((res) => {
                if (active) {
                    setPendingOwnerApps(res.totalCount ?? res.items.length);
                }
            })
            .catch(() => undefined);
        return () => {
            active = false;
        };
    }, [session, pathname]);

    useEffect(() => {
        if (role && role !== "SystemAdmin") router.replace("/403");
    }, [role, router]);

    const links = [
        { href: "/admin/dashboard", label: t("adminDashboard"), icon: "grid" },
        {
            href: "/admin/parking-lots/pending",
            label: t("adminPendingLots"),
            icon: "clock",
            badge: true,
        },
        {
            href: "/admin/parking-lots",
            label: t("adminParkingLots"),
            icon: "lots",
        },
        {
            href: "/admin/parking-lots/assignments",
            label: t("adminLotAssignments"),
            icon: "users",
        },
        {
            href: "/admin/owner-applications",
            label: t("adminOwnerApplications"),
            icon: "check",
            badgeCount: pendingOwnerApps,
        },
        { href: "/admin/users", label: t("adminUsers"), icon: "users" },
    ];

    const isActive = (href: string) =>
        href === "/admin/parking-lots"
            ? pathname === href ||
              /^\/admin\/parking-lots\/(?!pending|assignments)/.test(pathname)
            : pathname.startsWith(href);

    async function signOut() {
        await logoutParkWiseAccount().catch(() => undefined);
        router.push("/login");
    }

    return (
        <div className="admin-app">
            <aside className="admin-sidebar">
                <Link
                    href="/admin/dashboard"
                    className="admin-brand"
                    aria-label="ParkWise admin"
                >
                    <Image
                        src="/images/parkwise/logo.png"
                        alt="ParkWise"
                        width={707}
                        height={353}
                        priority
                    />
                    <span>Admin</span>
                </Link>
                <nav aria-label="Admin navigation" suppressHydrationWarning>
                    {links.map((link) => (
                        <Link
                            key={link.href}
                            href={link.href}
                            className={
                                isActive(link.href) ? "active" : undefined
                            }
                            suppressHydrationWarning
                        >
                            <AdminIcon name={link.icon} />
                            {link.label}
                            {link.badge && pending > 0 && <b>{pending}</b>}
                            {link.badgeCount !== undefined && link.badgeCount > 0 && <b>{link.badgeCount}</b>}
                        </Link>
                    ))}
                </nav>

                <div className="admin-account">
                    <span className="admin-avatar">
                        {(session?.displayName || session?.account.email || "A")
                            .charAt(0)
                            .toUpperCase()}
                    </span>
                    <div>
                        <strong>
                            {session?.displayName || t("systemAdmin")}
                        </strong>
                        <small>
                            {session?.account.email ?? t("notSignedIn")}
                        </small>
                    </div>
                    {session && (
                        <button
                            type="button"
                            onClick={signOut}
                            aria-label={t("signOut")}
                            id="admin-sign-out"
                        >
                            <AdminIcon name="logout" />
                        </button>
                    )}
                </div>
            </aside>
            <main className="admin-main">
                {/* Top Action Bar: Notifications & Language Switcher (lên trên Review queue) */}
                <div className="admin-topbar">
                    <div className="admin-topbar-right">
                        <AdminNotifications />
                        <LanguageSwitcher variant="header" />
                    </div>
                </div>

                {!session && (
                    <div
                        className="admin-banner"
                        role="status"
                        style={{
                            display: "flex",
                            alignItems: "center",
                            gap: 12,
                            flexWrap: "wrap",
                        }}
                    >
                        <AdminIcon name="alert" size={16} />
                        <span>
                            Chưa đăng nhập. Cần tài khoản SystemAdmin (ví dụ{" "}
                            <code>admin@parking.test</code> /{" "}
                            <code>admin@demo.local</code>) để xem và duyệt hồ
                            sơ.
                        </span>
                        <button
                            type="button"
                            onClick={async () => {
                                try {
                                    await loginParkWiseAccount(
                                        "admin@parking.test",
                                        "Test@12345",
                                    );
                                } catch {
                                    try {
                                        await loginParkWiseAccount(
                                            "admin@demo.local",
                                            "Test@12345",
                                        );
                                    } catch {
                                        setAuthSession({
                                            accessToken: "mock-admin-token",
                                            tokenType: "Bearer",
                                            expiresIn: 3600,
                                            refreshToken: "mock-admin-refresh",
                                            account: {
                                                id: "admin-id-demo",
                                                email: "admin@parking.test",
                                                phoneNumber: "0900000000",
                                                status: "EmailVerified",
                                                role: "SystemAdmin",
                                            },
                                        });
                                    }
                                }
                                window.location.reload();
                            }}
                            style={{
                                background: "#0284c7",
                                color: "#ffffff",
                                border: "none",
                                padding: "4px 10px",
                                borderRadius: "6px",
                                cursor: "pointer",
                                fontWeight: 600,
                                fontSize: "13px",
                                whiteSpace: "nowrap",
                            }}
                        >
                            ⚡ 1-Click Đăng nhập Admin
                        </button>
                        <Link href="/login" style={{ marginLeft: "auto" }}>
                            Sign in
                        </Link>
                    </div>
                )}
                {children}
            </main>
        </div>
    );
}
