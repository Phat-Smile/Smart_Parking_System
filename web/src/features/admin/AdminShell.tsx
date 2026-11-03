"use client";

import {
    useEffect,
    useState,
    useSyncExternalStore,
    type ReactNode,
} from "react";
import Image from "next/image";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import {
    getAuthSessionSnapshot,
    subscribeAuthSession,
} from "@/features/auth/authSession";
import { logoutParkWiseAccount } from "@/features/auth/logoutParkWiseAccount";
import { AdminIcon } from "./AdminUi";
import { countByStatus, useAdminLots } from "./adminLotsData";
import { getAdminParkingLots } from "./reviewParkingLot";
import { AdminNotifications } from "./AdminNotifications";
import { useLanguage, LanguageSwitcher } from "@/shared/i18n/LanguageContext";

export function AdminShell({ children }: { children: ReactNode }) {
    const pathname = usePathname();
    const router = useRouter();
    const { t } = useLanguage();
    const mounted = useSyncExternalStore(
        () => () => {},
        () => true,
        () => false,
    );

    const externalSession = useSyncExternalStore(
        subscribeAuthSession,
        getAuthSessionSnapshot,
        () => null,
    );

    const session = mounted
        ? (externalSession ?? getAuthSessionSnapshot())
        : null;
    const pending = countByStatus(useAdminLots()).Pending;
    const role = session?.account.role;

    useEffect(() => {
        if (!mounted) return;
        if (role && role !== "SystemAdmin") router.replace("/403");
        if (role === "SystemAdmin") {
            getAdminParkingLots("All").catch(() => undefined);
        }
    }, [role, router, mounted]);

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
        {
            href: "/admin/audit-logs",
            label: "Kiểm toán & Đối soát",
            icon: "clock",
        },
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
                            {session?.account.email ??
                                (mounted ? t("notSignedIn") : "...")}
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
                {/* Top Action Bar: Notifications & Language Switcher */}
                <div className="admin-topbar">
                    <div className="admin-topbar-right">
                        <AdminNotifications />
                        <LanguageSwitcher variant="header" />
                    </div>
                </div>

                {mounted && !session && (
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
