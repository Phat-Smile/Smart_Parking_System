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
import {
    ArrowRight,
    Bell,
    Bookmark,
    Calendar,
    Car,
    Check,
    ChevronDown,
    Clock,
    Cloud,
    LayoutGrid,
    SlidersHorizontal,
    History,
    Contact,
    MapPin,
    Bike,
    Banknote,
    ArrowDownAZ,
    Sparkles,
    Route,
    Search,
    LogIn,
    LogOut,
    Sun,
    Crosshair,
    User,
    Zap,
    ChevronRight,
    type LucideIcon,
} from "lucide-react";
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

const CUSTOMER_ICON_MAP: Record<CustomerIconName, LucideIcon> = {
    arrow: ArrowRight,
    bell: Bell,
    bookmark: Bookmark,
    calendar: Calendar,
    car: Car,
    check: Check,
    chevron: ChevronDown,
    clock: Clock,
    cloud: Cloud,
    dashboard: LayoutGrid,
    filter: SlidersHorizontal,
    history: History,
    "id-card": Contact,
    location: MapPin,
    motorbike: Bike,
    price: Banknote,
    "sort-az": ArrowDownAZ,
    sparkles: Sparkles,
    distance: Route,
    search: Search,
    "sign-in": LogIn,
    "sign-out": LogOut,
    sun: Sun,
    target: Crosshair,
    user: User,
    zap: Zap,
    "chevron-right": ChevronRight,
};

export function CustomerIcon({
    name,
    size = 20,
}: {
    name: CustomerIconName;
    size?: number;
}) {
    const Component = CUSTOMER_ICON_MAP[name] ?? Sparkles;
    return <Component size={size} aria-hidden="true" />;
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

export function CustomerNotifications() {
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
