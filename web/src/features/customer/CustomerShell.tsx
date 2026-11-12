"use client";

import {
    useEffect,
    useId,
    useRef,
    useState,
    useSyncExternalStore,
} from "react";
import Image from "next/image";
import Link from "next/link";
import { useRouter } from "next/navigation";
import type { ReactNode } from "react";
import { ParkWiseNavbar } from "@/features/parkwise/ParkWiseNavbar";
import { ParkWiseFooter } from "@/features/parkwise/ParkWiseFooter";
import {
    getAuthSessionSnapshot,
    subscribeAuthSession,
} from "@/features/auth/authSession";
import { logoutParkWiseAccount } from "@/features/auth/logoutParkWiseAccount";
import { CustomerLanguageSwitcher } from "./i18n/CustomerLanguageSwitcher";
import { useCustomerLanguage } from "./i18n/customerLanguage";
import { getMyNotifications } from "./customerNotificationApi";
import { getMyProfile } from "./customerProfileApi";

export type CustomerIconName =
    | "arrow"
    | "bell"
    | "bookmark"
    | "calendar"
    | "car"
    | "check"
    | "chevron"
    | "clock"
    | "cloud"
    | "dashboard"
    | "filter"
    | "history"
    | "id-card"
    | "location"
    | "motorbike"
    | "price"
    | "sort-az"
    | "sparkles"
    | "distance"
    | "search"
    | "sign-in"
    | "sign-out"
    | "sun"
    | "target"
    | "user"
    | "zap"
    | "chevron-right";

export function CustomerIcon({
    name,
    size = 20,
}: {
    name: CustomerIconName;
    size?: number;
}) {
    const paths: Record<CustomerIconName, ReactNode> = {
        arrow: <path d="M4 12h16m-6-6 6 6-6 6" />,
        bell: (
            <>
                <path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9Z" />
                <path d="M10 21h4" />
            </>
        ),
        bookmark: <path d="M6 4h12v17l-6-4-6 4V4Z" />,
        calendar: (
            <>
                <rect x="3" y="5" width="18" height="16" rx="2" />
                <path d="M7 3v4m10-4v4M3 10h18" />
            </>
        ),
        car: (
            <>
                <path d="m5 11 1.7-4.2A2 2 0 0 1 8.6 5h6.8a2 2 0 0 1 1.9 1.8L19 11" />
                <rect x="3" y="11" width="18" height="7" rx="2" />
                <path d="M5 18v2m14-2v2M7 14h.01M17 14h.01" />
            </>
        ),
        check: <path d="m4 12 5 5L20 6" />,
        chevron: <path d="m6 9 6 6 6-6" />,
        clock: (
            <>
                <circle cx="12" cy="12" r="9" />
                <path d="M12 7v5l3 2" />
            </>
        ),
        cloud: (
            <path d="M7 19h10a4 4 0 0 0 .5-7.97A6 6 0 0 0 6 9.8 4.7 4.7 0 0 0 7 19Z" />
        ),
        dashboard: (
            <>
                <rect x="3" y="3" width="7" height="7" rx="1" />
                <rect x="14" y="3" width="7" height="7" rx="1" />
                <rect x="3" y="14" width="7" height="7" rx="1" />
                <rect x="14" y="14" width="7" height="7" rx="1" />
            </>
        ),
        filter: <path d="M4 7h16M7 12h10m-7 5h4M8 5v4m8 1v4" />,
        history: (
            <>
                <path d="M3 12a9 9 0 1 0 3-6.7L3 8" />
                <path d="M3 3v5h5m4-1v5l3 2" />
            </>
        ),
        "id-card": (
            <>
                <rect x="3" y="5" width="18" height="14" rx="2" />
                <circle cx="8" cy="10" r="1.5" />
                <path d="M5.7 15c.3-1.3 1.1-2 2.3-2s2 .7 2.3 2M13 9h5m-5 4h5" />
            </>
        ),
        location: (
            <>
                <path d="M20 10c0 5-8 12-8 12S4 15 4 10a8 8 0 1 1 16 0Z" />
                <circle cx="12" cy="10" r="2.5" />
            </>
        ),
        motorbike: (
            <>
                <circle cx="5" cy="17" r="3" />
                <circle cx="19" cy="17" r="3" />
                <path d="M8 17h3l3-6h2l3 6M12 11 9 8H6m9-1h3l2 3" />
            </>
        ),
        price: (
            <>
                <rect x="3" y="5" width="18" height="14" rx="2" />
                <circle cx="12" cy="12" r="3" />
                <path d="M6 9h.01M18 15h.01" />
            </>
        ),
        "sort-az": <path d="M4 6h11M4 12h8M4 18h5m8-12v12m-3-3 3 3 3-3" />,
        sparkles: (
            <path d="m12 2 1.8 6.2L20 10l-6.2 1.8L12 18l-1.8-6.2L4 10l6.2-1.8L12 2Zm7 14 .7 2.3L22 19l-2.3.7L19 22l-.7-2.3L16 19l2.3-.7L19 16Z" />
        ),
        distance: (
            <>
                <circle cx="5" cy="17" r="2" />
                <circle cx="19" cy="7" r="2" />
                <path d="M7 17h5a3 3 0 0 0 0-6h-1a2 2 0 0 1 0-4h6" />
            </>
        ),
        search: (
            <>
                <circle cx="10.5" cy="10.5" r="6.5" />
                <path d="m16 16 5 5" />
            </>
        ),
        "sign-in": (
            <path d="M10 4h8a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-8M4 12h12m-4-4 4 4-4 4" />
        ),
        "sign-out": (
            <path d="M14 4H6a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h8M20 12H8m4-4-4 4 4 4" />
        ),
        sun: (
            <>
                <circle cx="12" cy="12" r="4" />
                <path d="M12 2v2m0 16v2M2 12h2m16 0h2M4.9 4.9l1.4 1.4m11.4 11.4 1.4 1.4M19.1 4.9l-1.4 1.4M6.3 17.7l-1.4 1.4" />
            </>
        ),
        target: (
            <>
                <circle cx="12" cy="12" r="8" />
                <circle cx="12" cy="12" r="3" />
                <path d="M12 1v3m0 16v3M1 12h3m16 0h3" />
            </>
        ),
        user: (
            <>
                <circle cx="12" cy="8" r="3.5" />
                <path d="M5 20c.4-3.5 3.1-5.5 7-5.5s6.6 2 7 5.5" />
            </>
        ),
        zap: <path d="m13 2-9 12h7l-1 8 10-12h-7l1-8Z" />,
        "chevron-right": <path d="m9 5 7 7-7 7" />,
    };

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
            {paths[name]}
        </svg>
    );
}

export function CustomerHeader({
    active,
    profilePhotoUrl,
    showTriggerName = true,
}: {
    active:
        | "home"
        | "search"
        | "detail"
        | "wallet"
        | "bookings"
        | "history"
        | "sessions"
        | "profile"
        | "notifications"
        | "vehicles"
        | "completeProfile";
    profilePhotoUrl?: string;
    showTriggerName?: boolean;
}) {
    const activeHref = {
        home: "/customer/dashboard",
        search: "/search",
        detail: "/search",
        wallet: "/customer/wallet",
        bookings: "/customer/bookings",
        history: "/customer/parking-history",
        sessions: "/customer/sessions",
        profile: "/customer/profile",
        notifications: "/customer/notifications",
        vehicles: "/customer/vehicles",
        completeProfile: "/customer/complete-profile",
    }[active];

    return (
        <ParkWiseNavbar
            variant="customer"
            navigationMode="dashboard"
            activeHref={activeHref}
            accountControl={
                <div className="dashboard-header-actions">
                    <CustomerLanguageSwitcher />
                    <CustomerNotifications />
                    <CustomerAccountMenu
                        activeHref={activeHref}
                        profilePhotoUrl={profilePhotoUrl}
                        showTriggerName={showTriggerName}
                    />
                </div>
            }
        />
    );
}

function CustomerNotifications() {
    const { copy } = useCustomerLanguage();
    const session = useSyncExternalStore(
        subscribeAuthSession,
        getAuthSessionSnapshot,
        () => null,
    );
    const [open, setOpen] = useState(false);
    const [unreadCount, setUnreadCount] = useState(0);
    const rootRef = useRef<HTMLDivElement>(null);
    const triggerRef = useRef<HTMLButtonElement>(null);
    const panelId = useId();

    useEffect(() => {
        if (!session) return;
        let active = true;
        const refresh = () => {
            void getMyNotifications(true, 1, 1)
                .then((result) => {
                    if (active) setUnreadCount(result.unreadCount);
                })
                .catch(() => {
                    if (active) setUnreadCount(0);
                });
        };

        refresh();
        const onFocus = () => refresh();
        const onVisibilityChange = () => {
            if (document.visibilityState === "visible") refresh();
        };
        window.addEventListener("focus", onFocus);
        window.addEventListener("customer-notifications-changed", onFocus);
        document.addEventListener("visibilitychange", onVisibilityChange);
        const timer = window.setInterval(refresh, 30_000);
        return () => {
            active = false;
            window.clearInterval(timer);
            window.removeEventListener("focus", onFocus);
            window.removeEventListener(
                "customer-notifications-changed",
                onFocus,
            );
            document.removeEventListener(
                "visibilitychange",
                onVisibilityChange,
            );
        };
    }, [session]);

    useEffect(() => {
        if (!open) return;
        function onPointerDown(event: PointerEvent) {
            if (!rootRef.current?.contains(event.target as Node))
                setOpen(false);
        }
        function onKeyDown(event: KeyboardEvent) {
            if (event.key === "Escape") {
                setOpen(false);
                triggerRef.current?.focus();
            }
        }
        document.addEventListener("pointerdown", onPointerDown);
        document.addEventListener("keydown", onKeyDown);
        return () => {
            document.removeEventListener("pointerdown", onPointerDown);
            document.removeEventListener("keydown", onKeyDown);
        };
    }, [open]);

    return (
        <div className="dashboard-notification" ref={rootRef}>
            <button
                ref={triggerRef}
                type="button"
                aria-label={
                    unreadCount > 0
                        ? copy(
                              `Notifications, ${unreadCount} unread`,
                              `Thông báo, ${unreadCount} chưa đọc`,
                          )
                        : copy("Notifications", "Thông báo")
                }
                aria-expanded={open}
                aria-controls={open ? panelId : undefined}
                onClick={() => setOpen((current) => !current)}
                className={unreadCount > 0 ? "has-unread" : undefined}
            >
                <CustomerIcon name="bell" size={22} />
                {unreadCount > 0 && (
                    <span
                        className="dashboard-notification-badge"
                        aria-hidden="true"
                    >
                        {unreadCount > 99 ? "99+" : unreadCount}
                    </span>
                )}
            </button>
            <span
                className="dashboard-notification-sr-only"
                role="status"
                aria-atomic="true"
            >
                {unreadCount > 0
                    ? copy(
                          `${unreadCount} unread notifications`,
                          `${unreadCount} thông báo chưa đọc`,
                      )
                    : ""}
            </span>
            {open && (
                <div
                    className="dashboard-notification-panel"
                    id={panelId}
                    role="region"
                    aria-label={copy("Notifications", "Thông báo")}
                >
                    <strong>{copy("Notifications", "Thông báo")}</strong>
                    <p>
                        {unreadCount > 0
                            ? copy(
                                  `You have ${unreadCount} unread notification${unreadCount === 1 ? "" : "s"}.`,
                                  `Bạn có ${unreadCount} thông báo chưa đọc.`,
                              )
                            : copy(
                                  "You're all caught up. New wallet and parking updates will appear here.",
                                  "Bạn đã xem hết thông báo. Cập nhật ví và bãi đỗ sẽ xuất hiện tại đây.",
                              )}
                    </p>
                    <Link
                        href="/customer/notifications"
                        onClick={() => setOpen(false)}
                    >
                        {copy("Open notifications", "Xem thông báo")}
                    </Link>
                </div>
            )}
        </div>
    );
}

const accountLinks: {
    href: string;
    label: string;
    icon: CustomerIconName;
}[] = [
    {
        href: "/customer/profile",
        label: "My Profile",
        icon: "user",
    },
    { href: "/customer/dashboard", label: "Dashboard", icon: "dashboard" },
    { href: "/search", label: "Find Parking", icon: "location" },
    { href: "/customer/vehicles", label: "My Vehicles", icon: "car" },
    { href: "/customer/bookings", label: "My Bookings", icon: "calendar" },
    { href: "/customer/sessions", label: "My Sessions", icon: "clock" },
    {
        href: "/customer/parking-history",
        label: "Parking History",
        icon: "history",
    },
    { href: "/customer/wallet", label: "Wallet", icon: "price" },
    { href: "/customer/notifications", label: "Notifications", icon: "bell" },
];

function initials(name: string) {
    return name
        .trim()
        .split(/\s+/)
        .filter(Boolean)
        .slice(-2)
        .map((part) => part[0])
        .join("")
        .toUpperCase();
}

export function CustomerAccountMenu({
    activeHref,
    profilePhotoUrl,
    showTriggerName = true,
}: {
    activeHref?: string;
    profilePhotoUrl?: string;
    showTriggerName?: boolean;
}) {
    const { copy, isVietnamese } = useCustomerLanguage();
    const router = useRouter();
    const session = useSyncExternalStore(
        subscribeAuthSession,
        getAuthSessionSnapshot,
        () => null,
    );
    const [open, setOpen] = useState(false);
    const [loggingOut, setLoggingOut] = useState(false);
    const [logoutError, setLogoutError] = useState("");
    const [profileNameState, setProfileName] = useState({
        accountId: "",
        name: "",
    });
    const rootRef = useRef<HTMLDivElement>(null);
    const triggerRef = useRef<HTMLButtonElement>(null);
    const menuId = useId();
    const accountId = session?.account.id;
    const profileName =
        profileNameState.accountId === accountId ? profileNameState.name : "";

    useEffect(() => {
        if (!accountId) return;

        let active = true;
        let profileWasChanged = false;
        const onProfileNameChanged = (event: Event) => {
            profileWasChanged = true;
            const name = (event as CustomEvent<string>).detail?.trim();
            if (name) setProfileName({ accountId, name });
        };

        window.addEventListener(
            "customer-profile-name-changed",
            onProfileNameChanged,
        );
        void getMyProfile()
            .then((profile) => {
                if (active && !profileWasChanged) {
                    setProfileName({
                        accountId,
                        name: profile.fullName?.trim() ?? "",
                    });
                }
            })
            .catch(() => undefined);

        return () => {
            active = false;
            window.removeEventListener(
                "customer-profile-name-changed",
                onProfileNameChanged,
            );
        };
    }, [accountId]);

    const accountName =
        profileName ||
        session?.displayName?.trim() ||
        session?.account.email ||
        copy("Sign in required", "Cần đăng nhập");
    const accountEmail =
        session?.account.email ||
        copy("No account email available", "Chưa có email tài khoản");
    const accountLabels: Record<string, string> = {
        "My Profile": "Hồ sơ của tôi",
        Dashboard: "Trang chủ",
        "Find Parking": "Tìm bãi đỗ",
        "My Vehicles": "Xe của tôi",
        "My Bookings": "Đặt chỗ của tôi",
        "My Sessions": "Phiên đỗ của tôi",
        "Parking History": "Lịch sử đỗ xe",
        Wallet: "Ví của tôi",
        Notifications: "Thông báo",
    };

    useEffect(() => {
        if (!open) return;
        function onPointerDown(event: PointerEvent) {
            if (!rootRef.current?.contains(event.target as Node))
                setOpen(false);
        }
        function onKeyDown(event: KeyboardEvent) {
            if (event.key === "Escape") {
                setOpen(false);
                triggerRef.current?.focus();
            }
        }
        document.addEventListener("pointerdown", onPointerDown);
        document.addEventListener("keydown", onKeyDown);
        return () => {
            document.removeEventListener("pointerdown", onPointerDown);
            document.removeEventListener("keydown", onKeyDown);
        };
    }, [open]);

    async function handleLogout() {
        setLogoutError("");
        setLoggingOut(true);
        try {
            await logoutParkWiseAccount();
            router.replace("/login");
        } catch {
            setLogoutError(
                copy(
                    "We couldn't sign you out. Please try again.",
                    "Không thể đăng xuất. Vui lòng thử lại.",
                ),
            );
        } finally {
            setLoggingOut(false);
        }
    }

    const avatar = profilePhotoUrl ? (
        <Image
            src={profilePhotoUrl}
            alt=""
            width={48}
            height={48}
            unoptimized
        />
    ) : profileName || session?.displayName?.trim() ? (
        initials(profileName || session?.displayName?.trim() || "")
    ) : (
        <CustomerIcon name="user" size={22} />
    );

    return (
        <div className="customer-account-menu" ref={rootRef}>
            <button
                ref={triggerRef}
                type="button"
                className={`customer-account-trigger${showTriggerName ? "" : " is-compact"}`}
                aria-expanded={open}
                aria-controls={open ? menuId : undefined}
                aria-label={`${copy("Account menu", "Tài khoản")}: ${accountName}`}
                onClick={() => setOpen((current) => !current)}
            >
                <span
                    className="customer-account-trigger-avatar"
                    aria-hidden="true"
                >
                    {avatar}
                </span>
                {showTriggerName && (
                    <span className="customer-account-trigger-name">
                        {accountName}
                    </span>
                )}
                <CustomerIcon name="chevron" size={17} />
            </button>
            {open && (
                <nav
                    id={menuId}
                    className="customer-account-panel"
                    aria-label={copy(
                        "Account navigation",
                        "Điều hướng tài khoản",
                    )}
                >
                    <div className="customer-account-identity">
                        <span
                            className="customer-account-identity-avatar"
                            aria-hidden="true"
                        >
                            {avatar}
                        </span>
                        <span className="customer-account-identity-copy">
                            <strong>{accountName}</strong>
                            <small>{accountEmail}</small>
                        </span>
                    </div>
                    <div className="customer-account-links">
                        {accountLinks.map(({ href, label, icon }) => (
                            <Link
                                key={href}
                                href={href}
                                className="customer-account-link"
                                aria-current={
                                    activeHref === href ? "page" : undefined
                                }
                                onClick={() => setOpen(false)}
                            >
                                <CustomerIcon name={icon} size={21} />
                                <span>
                                    {isVietnamese
                                        ? (accountLabels[label] ?? label)
                                        : label}
                                </span>
                                <CustomerIcon name="chevron-right" size={17} />
                            </Link>
                        ))}
                    </div>
                    <div className="customer-account-auth">
                        {session ? (
                            <button
                                type="button"
                                onClick={handleLogout}
                                disabled={loggingOut}
                            >
                                <CustomerIcon name="sign-out" size={21} />
                                <span>
                                    {loggingOut
                                        ? copy(
                                              "Signing out…",
                                              "Đang đăng xuất…",
                                          )
                                        : copy("Log out", "Đăng xuất")}
                                </span>
                                <CustomerIcon name="chevron-right" size={17} />
                            </button>
                        ) : (
                            <Link href="/login" onClick={() => setOpen(false)}>
                                <CustomerIcon name="sign-in" size={21} />
                                <span>{copy("Log in", "Đăng nhập")}</span>
                                <CustomerIcon name="chevron-right" size={17} />
                            </Link>
                        )}
                        {logoutError && <p role="alert">{logoutError}</p>}
                    </div>
                </nav>
            )}
        </div>
    );
}

export function CustomerFooter() {
    const { language } = useCustomerLanguage();
    return <ParkWiseFooter language={language} />;
}
