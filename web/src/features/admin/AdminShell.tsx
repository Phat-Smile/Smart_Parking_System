"use client";

import { useEffect, useSyncExternalStore, type ReactNode } from "react";
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
    const role = session?.account.role;

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
        { href: "/admin/parking-lots", label: t("adminParkingLots"), icon: "lots" },
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

                {/* Nút chuyển đổi ngôn ngữ ở góc trái (Language Switcher) */}
                <div style={{ marginTop: "auto", padding: "8px 0" }}>
                    <LanguageSwitcher variant="sidebar" />
                </div>

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
                {!session && (
                    <div className="admin-banner" role="status">
                        <AdminIcon name="alert" size={16} />
                        <span>
                            You&apos;re not signed in. Approve/Reject require a
                            SystemAdmin session (e.g.{" "}
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
