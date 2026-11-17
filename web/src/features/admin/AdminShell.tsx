"use client";

import {
    useEffect,
    useMemo,
    useRef,
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

interface NavItem {
    href: string;
    label: string;
    icon: string;
    badge?: boolean;
}

interface NavGroup {
    id: string;
    titleVi: string;
    titleEn: string;
    items: NavItem[];
}

export function AdminShell({ children }: { children: ReactNode }) {
    const pathname = usePathname();
    const router = useRouter();
    const { t, language } = useLanguage();
    const isVi = language === "vi";

    const [collapsed, setCollapsed] = useState(false);
    const [mobileOpen, setMobileOpen] = useState(false);
    const [accountMenuOpen, setAccountMenuOpen] = useState(false);
    const accountMenuRef = useRef<HTMLDivElement>(null);

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
        if (!session) {
            router.replace("/login");
            return;
        }
        if (role !== "SystemAdmin") {
            router.replace("/403");
            return;
        }
        getAdminParkingLots("All").catch(() => undefined);
    }, [role, router, mounted, session]);

    // Close account dropdown on outside click
    useEffect(() => {
        function handleClickOutside(event: MouseEvent) {
            if (
                accountMenuRef.current &&
                !accountMenuRef.current.contains(event.target as Node)
            ) {
                setAccountMenuOpen(false);
            }
        }
        function handleKeyDown(event: KeyboardEvent) {
            if (event.key === "Escape") setAccountMenuOpen(false);
        }
        if (accountMenuOpen) {
            document.addEventListener("mousedown", handleClickOutside);
            document.addEventListener("keydown", handleKeyDown);
        }
        return () => {
            document.removeEventListener("mousedown", handleClickOutside);
            document.removeEventListener("keydown", handleKeyDown);
        };
    }, [accountMenuOpen]);

    const navGroups: NavGroup[] = useMemo(
        () => [
            {
                id: "overview",
                titleVi: "Tổng quan",
                titleEn: "Overview",
                items: [
                    {
                        href: "/admin/dashboard",
                        label: t("adminDashboard"),
                        icon: "grid",
                    },
                ],
            },
            {
                id: "parking-lots",
                titleVi: "Quản lý bãi xe",
                titleEn: "Parking Lot Management",
                items: [
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
                ],
            },
            {
                id: "partners",
                titleVi: "Quản lý đối tác",
                titleEn: "Partner Management",
                items: [
                    {
                        href: "/admin/owner-applications",
                        label: t("adminOwnerApplications"),
                        icon: "fileCheck",
                    },
                ],
            },
            {
                id: "operations",
                titleVi: "Vận hành",
                titleEn: "Operations",
                items: [
                    {
                        href: "/admin/parking-lots/assignments",
                        label: t("adminLotAssignments"),
                        icon: "briefcase",
                    },
                ],
            },
            {
                id: "system",
                titleVi: "Hệ thống",
                titleEn: "System",
                items: [
                    {
                        href: "/admin/users",
                        label: t("adminUsers"),
                        icon: "users",
                    },
                    {
                        href: "/admin/audit-logs",
                        label: isVi
                            ? "Kiểm toán & Đối soát"
                            : "Audit & Reconciliation",
                        icon: "shield",
                    },
                ],
            },
        ],
        [t, isVi],
    );

    const isActive = (href: string) =>
        href === "/admin/parking-lots"
            ? pathname === href ||
              /^\/admin\/parking-lots\/(?!pending|assignments)/.test(pathname)
            : pathname.startsWith(href);

    const breadcrumbs = useMemo<Array<{ label: string; href?: string }>>(() => {
        const root = {
            label: "Admin",
            href: "/admin/dashboard",
        };
        if (pathname.startsWith("/admin/parking-lots/pending")) {
            return [
                root,
                {
                    label: isVi ? "Quản lý bãi xe" : "Parking Lots",
                    href: "/admin/parking-lots",
                },
                { label: t("adminPendingLots") },
            ];
        }
        if (pathname.startsWith("/admin/parking-lots/assignments")) {
            return [
                root,
                {
                    label: isVi ? "Vận hành" : "Operations",
                    href: "/admin/parking-lots/assignments",
                },
                { label: t("adminLotAssignments") },
            ];
        }
        if (/^\/admin\/parking-lots\/[^/]+/.test(pathname)) {
            return [
                root,
                {
                    label: isVi ? "Quản lý bãi xe" : "Parking Lots",
                    href: "/admin/parking-lots",
                },
                {
                    label: isVi
                        ? "Chi tiết & Thẩm định bãi xe"
                        : "Parking Lot Review",
                },
            ];
        }
        if (pathname.startsWith("/admin/parking-lots")) {
            return [
                root,
                {
                    label: isVi ? "Quản lý bãi xe" : "Parking Lots",
                    href: "/admin/parking-lots",
                },
                { label: t("adminParkingLots") },
            ];
        }
        if (pathname.startsWith("/admin/owner-applications")) {
            return [
                root,
                {
                    label: isVi ? "Quản lý đối tác" : "Partner Management",
                },
                { label: t("adminOwnerApplications") },
            ];
        }
        if (pathname.startsWith("/admin/users")) {
            return [
                root,
                {
                    label: isVi ? "Hệ thống" : "System",
                },
                { label: t("adminUsers") },
            ];
        }
        if (pathname.startsWith("/admin/audit-logs")) {
            return [
                root,
                {
                    label: isVi ? "Hệ thống" : "System",
                },
                {
                    label: isVi
                        ? "Kiểm toán & Đối soát"
                        : "Audit & Reconciliation",
                },
            ];
        }
        return [
            root,
            {
                label: isVi ? "Tổng quan" : "Overview",
            },
            { label: t("adminDashboard") },
        ];
    }, [pathname, isVi, t]);

    async function signOut() {
        setAccountMenuOpen(false);
        await logoutParkWiseAccount().catch(() => undefined);
        router.push("/login");
    }

    const displayName = session?.displayName || t("systemAdmin");
    const emailText =
        session?.account.email ?? (mounted ? t("notSignedIn") : "...");
    const avatarInitial = (
        session?.displayName ||
        session?.account.email ||
        "A"
    )
        .charAt(0)
        .toUpperCase();

    if (!mounted || !session || role !== "SystemAdmin") {
        return <main aria-live="polite">Checking admin access…</main>;
    }

    return (
        <div
            className={`admin-app ${collapsed ? "is-collapsed" : ""} ${mobileOpen ? "is-mobile-open" : ""}`}
        >
            {/* Mobile Backdrop */}
            {mobileOpen && (
                <div
                    className="admin-sidebar-backdrop"
                    onClick={() => setMobileOpen(false)}
                    aria-hidden="true"
                />
            )}

            <aside className="admin-sidebar">
                <div className="admin-sidebar-header">
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
                        <span className="admin-brand-badge">Admin</span>
                    </Link>
                    <button
                        type="button"
                        className="admin-sidebar-collapse-btn"
                        onClick={() => setCollapsed((prev) => !prev)}
                        aria-label={
                            collapsed
                                ? isVi
                                    ? "Mở rộng thanh điều hướng"
                                    : "Expand sidebar"
                                : isVi
                                  ? "Thu gọn thanh điều hướng"
                                  : "Collapse sidebar"
                        }
                        title={
                            collapsed
                                ? isVi
                                    ? "Mở rộng menu"
                                    : "Expand sidebar"
                                : isVi
                                  ? "Thu gọn menu"
                                  : "Collapse sidebar"
                        }
                    >
                        <AdminIcon
                            name={collapsed ? "chevronRight" : "chevronLeft"}
                            size={16}
                        />
                    </button>
                </div>

                <nav
                    className="admin-sidebar-nav"
                    aria-label="Admin navigation"
                    suppressHydrationWarning
                >
                    {navGroups.map((group) => (
                        <div key={group.id} className="admin-nav-group">
                            <span className="admin-nav-group-label">
                                {isVi ? group.titleVi : group.titleEn}
                            </span>
                            <div className="admin-nav-group-items">
                                {group.items.map((link) => {
                                    const active = isActive(link.href);
                                    return (
                                        <Link
                                            key={link.href}
                                            href={link.href}
                                            onClick={() => setMobileOpen(false)}
                                            className={
                                                active ? "active" : undefined
                                            }
                                            title={link.label}
                                            aria-current={
                                                active ? "page" : undefined
                                            }
                                            suppressHydrationWarning
                                        >
                                            <span className="admin-nav-icon">
                                                <AdminIcon
                                                    name={link.icon}
                                                    size={18}
                                                />
                                            </span>
                                            <span className="admin-nav-label">
                                                {link.label}
                                            </span>
                                            {link.badge && pending > 0 && (
                                                <b
                                                    title={
                                                        isVi
                                                            ? `${pending} hồ sơ chờ duyệt`
                                                            : `${pending} pending`
                                                    }
                                                >
                                                    {pending}
                                                </b>
                                            )}
                                        </Link>
                                    );
                                })}
                            </div>
                        </div>
                    ))}
                </nav>

                <div className="admin-account">
                    <span className="admin-avatar" title={displayName}>
                        {avatarInitial}
                    </span>
                    <div className="admin-account-info">
                        <strong title={displayName}>{displayName}</strong>
                        <small title={emailText}>{emailText}</small>
                    </div>
                    {session && (
                        <button
                            type="button"
                            onClick={signOut}
                            aria-label={t("signOut")}
                            title={t("signOut")}
                            id="admin-sign-out"
                            className="admin-account-logout"
                        >
                            <AdminIcon name="logout" size={17} />
                        </button>
                    )}
                </div>
            </aside>

            <main className="admin-main">
                {/* Top Header Bar: Breadcrumb, Notifications, Language Switcher, Profile Dropdown */}
                <div className="admin-topbar">
                    <div className="admin-topbar-left">
                        <button
                            type="button"
                            className="admin-mobile-menu-btn"
                            onClick={() => setMobileOpen((prev) => !prev)}
                            aria-label={
                                isVi ? "Mở menu điều hướng" : "Toggle menu"
                            }
                        >
                            <AdminIcon name="menu" size={20} />
                        </button>

                        <nav
                            className="admin-breadcrumb"
                            aria-label="Breadcrumb"
                        >
                            <ol>
                                {breadcrumbs.map((crumb, idx) => {
                                    const isLast =
                                        idx === breadcrumbs.length - 1;
                                    return (
                                        <li key={`${crumb.label}-${idx}`}>
                                            {idx === 0 && (
                                                <AdminIcon
                                                    name="home"
                                                    size={14}
                                                    className="admin-breadcrumb-home"
                                                />
                                            )}
                                            {crumb.href && !isLast ? (
                                                <Link href={crumb.href}>
                                                    {crumb.label}
                                                </Link>
                                            ) : (
                                                <span
                                                    aria-current={
                                                        isLast
                                                            ? "page"
                                                            : undefined
                                                    }
                                                    className={
                                                        isLast
                                                            ? "is-current"
                                                            : undefined
                                                    }
                                                >
                                                    {crumb.label}
                                                </span>
                                            )}
                                            {!isLast && (
                                                <span
                                                    className="admin-breadcrumb-sep"
                                                    aria-hidden="true"
                                                >
                                                    /
                                                </span>
                                            )}
                                        </li>
                                    );
                                })}
                            </ol>
                        </nav>
                    </div>

                    <div className="admin-topbar-right">
                        <AdminNotifications />
                        <LanguageSwitcher variant="header" />

                        {/* Admin Account Profile Dropdown */}
                        <div
                            ref={accountMenuRef}
                            className="admin-header-profile-wrap"
                        >
                            <button
                                type="button"
                                className={`admin-header-profile-btn ${accountMenuOpen ? "is-open" : ""}`}
                                onClick={() =>
                                    setAccountMenuOpen((prev) => !prev)
                                }
                                aria-expanded={accountMenuOpen}
                                aria-label={
                                    isVi
                                        ? "Tài khoản Quản trị viên"
                                        : "Admin Account Menu"
                                }
                            >
                                <span className="admin-header-avatar">
                                    {avatarInitial}
                                </span>
                                <span className="admin-header-user-meta">
                                    <strong className="admin-header-user-name">
                                        {displayName}
                                    </strong>
                                    <small className="admin-header-user-role">
                                        {session?.account.role === "SystemAdmin"
                                            ? isVi
                                                ? "Quản trị hệ thống"
                                                : "System Admin"
                                            : isVi
                                              ? "Khách / Chưa đăng nhập"
                                              : "Guest"}
                                    </small>
                                </span>
                                <AdminIcon
                                    name="chevronDown"
                                    size={15}
                                    className="admin-header-chevron"
                                />
                            </button>

                            {accountMenuOpen && (
                                <div
                                    className="admin-dropdown-panel admin-profile-dropdown"
                                    role="menu"
                                >
                                    <div className="admin-profile-dropdown-head">
                                        <span className="admin-profile-dropdown-avatar">
                                            {avatarInitial}
                                        </span>
                                        <div className="admin-profile-dropdown-info">
                                            <strong>{displayName}</strong>
                                            <small>{emailText}</small>
                                            <span className="admin-profile-role-badge">
                                                <i aria-hidden="true" />
                                                {session?.account.role ??
                                                    "SystemAdmin"}
                                            </span>
                                        </div>
                                    </div>

                                    <div className="admin-profile-dropdown-links">
                                        <Link
                                            href="/admin/dashboard"
                                            role="menuitem"
                                            onClick={() =>
                                                setAccountMenuOpen(false)
                                            }
                                        >
                                            <AdminIcon name="grid" size={16} />
                                            <span>{t("adminDashboard")}</span>
                                        </Link>
                                        <Link
                                            href="/admin/parking-lots/pending"
                                            role="menuitem"
                                            onClick={() =>
                                                setAccountMenuOpen(false)
                                            }
                                        >
                                            <AdminIcon name="clock" size={16} />
                                            <span>{t("adminPendingLots")}</span>
                                            {pending > 0 && (
                                                <b className="admin-dropdown-count">
                                                    {pending}
                                                </b>
                                            )}
                                        </Link>
                                        <Link
                                            href="/admin/users"
                                            role="menuitem"
                                            onClick={() =>
                                                setAccountMenuOpen(false)
                                            }
                                        >
                                            <AdminIcon name="users" size={16} />
                                            <span>{t("adminUsers")}</span>
                                        </Link>
                                        <Link
                                            href="/admin/audit-logs"
                                            role="menuitem"
                                            onClick={() =>
                                                setAccountMenuOpen(false)
                                            }
                                        >
                                            <AdminIcon
                                                name="shield"
                                                size={16}
                                            />
                                            <span>
                                                {isVi
                                                    ? "Kiểm toán & Đối soát"
                                                    : "Audit & Reconciliation"}
                                            </span>
                                        </Link>
                                    </div>

                                    <div className="admin-profile-dropdown-foot">
                                        {session ? (
                                            <button
                                                type="button"
                                                role="menuitem"
                                                onClick={signOut}
                                                className="admin-profile-signout-btn"
                                            >
                                                <AdminIcon
                                                    name="logout"
                                                    size={16}
                                                />
                                                <span>{t("signOut")}</span>
                                            </button>
                                        ) : (
                                            <Link
                                                href="/login"
                                                role="menuitem"
                                                className="admin-profile-signin-link"
                                            >
                                                <AdminIcon
                                                    name="user"
                                                    size={16}
                                                />
                                                <span>
                                                    {isVi
                                                        ? "Đăng nhập"
                                                        : "Sign in"}
                                                </span>
                                            </Link>
                                        )}
                                    </div>
                                </div>
                            )}
                        </div>
                    </div>
                </div>

                <div className="admin-page-content">{children}</div>
            </main>
        </div>
    );
}
