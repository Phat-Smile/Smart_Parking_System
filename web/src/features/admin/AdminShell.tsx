"use client";

import { useEffect, useSyncExternalStore, type ReactNode } from "react";
import Image from "next/image";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import {
    getAuthSessionSnapshot,
    setAuthSession,
    subscribeAuthSession,
} from "@/features/auth/authSession";
import type { LoginResponse } from "@/features/auth/loginParkWiseAccount";
import { logoutParkWiseAccount } from "@/features/auth/logoutParkWiseAccount";
import { getAccessToken } from "@/shared/api/access-token";
import { AdminIcon } from "./AdminUi";
import { countByStatus, useAdminLots } from "./adminLotsData";
import { getAdminParkingLots } from "./reviewParkingLot";
import { AdminNotifications } from "./AdminNotifications";
import { useLanguage, LanguageSwitcher } from "@/shared/i18n/LanguageContext";

const ADMIN_AUTH_CACHE_KEY = "parkwise:admin-auth-cache";
let adminSessionRestored = false;

function syncAdminSessionCache() {
    if (typeof window === "undefined") return;
    try {
        const current = getAuthSessionSnapshot();
        const token = getAccessToken();
        if (current && current.account.role === "SystemAdmin" && token) {
            const payload: LoginResponse = {
                accessToken: token,
                tokenType: "Bearer",
                expiresIn: 3600,
                refreshToken: current.refreshToken ?? "",
                account: current.account,
            };
            window.sessionStorage.setItem(
                ADMIN_AUTH_CACHE_KEY,
                JSON.stringify(payload),
            );
        } else if (current && current.account.role !== "SystemAdmin") {
            window.sessionStorage.removeItem(ADMIN_AUTH_CACHE_KEY);
        }
    } catch {
        // Ignore storage errors
    }
}

function restoreAdminSessionIfNeeded() {
    if (typeof window === "undefined") return;
    const current = getAuthSessionSnapshot();
    if (current) {
        syncAdminSessionCache();
        return;
    }
    if (adminSessionRestored) return;
    adminSessionRestored = true;
    try {
        const raw = window.sessionStorage.getItem(ADMIN_AUTH_CACHE_KEY);
        if (!raw) return;
        const parsed = JSON.parse(raw) as LoginResponse;
        if (
            parsed?.accessToken &&
            parsed?.account?.role === "SystemAdmin" &&
            !parsed.accessToken.startsWith("mock-token-")
        ) {
            setAuthSession(parsed);
        } else {
            window.sessionStorage.removeItem(ADMIN_AUTH_CACHE_KEY);
        }
    } catch {
        // Ignore corrupted cache
    }
}

if (typeof window !== "undefined") {
    subscribeAuthSession(() => {
        syncAdminSessionCache();
    });
}

export function AdminShell({ children }: { children: ReactNode }) {
    restoreAdminSessionIfNeeded();
    const pathname = usePathname();
    const router = useRouter();
    const { t } = useLanguage();
    const session = useSyncExternalStore(
        subscribeAuthSession,
        getAuthSessionSnapshot,
        () => null,
    );
    const pending = countByStatus(useAdminLots()).Pending;
    const role = session?.account.role;

    useEffect(() => {
        restoreAdminSessionIfNeeded();
        syncAdminSessionCache();
    }, [session]);

    useEffect(() => {
        if (role && role !== "SystemAdmin") router.replace("/403");
        if (role === "SystemAdmin") {
            getAdminParkingLots("All").catch(() => undefined);
        }
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
        },
        { href: "/admin/users", label: t("adminUsers"), icon: "users" },
    ];

    const isActive = (href: string) =>
        href === "/admin/parking-lots"
            ? pathname === href ||
              /^\/admin\/parking-lots\/(?!pending|assignments)/.test(pathname)
            : pathname.startsWith(href);

    async function signOut() {
        if (typeof window !== "undefined") {
            try {
                window.sessionStorage.removeItem(ADMIN_AUTH_CACHE_KEY);
            } catch {
                // Ignore storage errors
            }
        }
        adminSessionRestored = false;
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
                    <div className="admin-banner" role="status">
                        <AdminIcon name="alert" size={16} />
                        <span>
                            You&apos;re not signed in. Approve/Reject require a
                            SystemAdmin session (e.g.{" "}
                            <code>admin@test.local</code> /{" "}
                            <code>admin@demo.local</code> /{" "}
                            <code>admin@parking.test</code>).
                        </span>
                        <Link href="/login">Sign in</Link>
                    </div>
                )}
                {children}
            </main>
        </div>
    );
}
