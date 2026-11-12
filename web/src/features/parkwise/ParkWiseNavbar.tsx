"use client";

import {
    useEffect,
    useState,
    useSyncExternalStore,
    type ReactNode,
} from "react";
import Image from "next/image";
import Link from "next/link";
import {
    getAuthSessionSnapshot,
    subscribeAuthSession,
} from "@/features/auth/authSession";
import { CUSTOMER_UI_PREVIEW } from "@/features/customer/customerUiPreview";
import { CustomerLanguageSwitcher } from "@/features/customer/i18n/CustomerLanguageSwitcher";
import { useCustomerLanguage } from "@/features/customer/i18n/customerLanguage";

const landingNavigation = [
    { label: "Home", href: "/" },
    { label: "How It Works", href: "/#how-it-works" },
    { label: "About", href: "/#about" },
];

const customerNavigation = [
    { label: "Customer", href: "/customer/dashboard" },
    { label: "Find Parking", href: "/search" },
    { label: "Vehicles", href: "/customer/vehicles" },
];
const dashboardNavigation = [
    { label: "Overview", href: "/customer/dashboard" },
    { label: "Search", href: "/search" },
    { label: "Vehicles", href: "/customer/vehicles" },
    { label: "Bookings", href: "/customer/bookings" },
    { label: "Sessions", href: "/customer/sessions" },
    { label: "History", href: "/customer/parking-history" },
    { label: "Wallet", href: "/customer/wallet" },
];

const customerLandingNavigation = [
    { label: "Home", href: "/" },
    { label: "Customer", href: "/customer/dashboard" },
    { label: "How It Works", href: "/#how-it-works" },
    { label: "About", href: "/#about" },
];

/** Guest-facing portion of ParkWise's src/Components/Nav/Navbar.jsx. */
export function ParkWiseNavbar({
    variant = "home",
    navigationMode = "landing",
    activeHref,
    accountControl,
}: {
    variant?: "home" | "login" | "customer";
    navigationMode?: "landing" | "customer" | "dashboard";
    activeHref?: string;
    accountControl?: ReactNode;
}) {
    const session = useSyncExternalStore(
        subscribeAuthSession,
        getAuthSessionSnapshot,
        () => null,
    );
    const isCustomer = session?.account.role === "Customer";
    const { language, copy } = useCustomerLanguage();
    const canViewCustomer = CUSTOMER_UI_PREVIEW || isCustomer;
    const navigation = canViewCustomer
        ? navigationMode === "dashboard"
            ? dashboardNavigation
            : navigationMode === "customer"
              ? customerNavigation
              : customerLandingNavigation
        : landingNavigation;
    const visibleAccountControl =
        accountControl && canViewCustomer ? (
            accountControl
        ) : session ? (
            canViewCustomer ? (
                <Link
                    className="parkwise-customer-account-link"
                    href="/customer/profile"
                >
                    {copy("My Profile", "Hồ sơ của tôi")}
                </Link>
            ) : (
                <span className="parkwise-signed-in-account">
                    {session.account.email}
                </span>
            )
        ) : null;
    const [menuOpen, setMenuOpen] = useState(false);
    const [scrolled, setScrolled] = useState(variant === "home");

    useEffect(() => {
        const onScroll = () =>
            setScrolled(window.scrollY > 50 || variant === "home");
        onScroll();
        window.addEventListener("scroll", onScroll, { passive: true });
        return () => window.removeEventListener("scroll", onScroll);
    }, [variant]);

    useEffect(() => {
        document.body.style.overflow = menuOpen ? "hidden" : "";
        return () => {
            document.body.style.overflow = "";
        };
    }, [menuOpen]);

    useEffect(() => {
        if (variant !== "customer") return;
        document.documentElement.lang = language;
    }, [variant, language]);

    const dark = scrolled && !menuOpen;
    const customerLabels: Record<string, string> = {
        Overview: "Tổng quan",
        Wallet: "Ví",
        Sessions: "Phiên đỗ",
        Home: "Trang chủ",
        Customer: "Khách hàng",
        "Find Parking": "Tìm bãi",
        Search: "Tìm bãi",
        Bookings: "Đặt chỗ",
        Vehicles: "Xe",
        History: "Lịch sử",
        "My Profile": "Hồ sơ của tôi",
        "How It Works": "Cách hoạt động",
        About: "Giới thiệu",
    };
    const customerLabel = (label: string) =>
        variant === "customer" && language === "vi"
            ? (customerLabels[label] ?? label)
            : label;

    return (
        <>
            <div
                className={`parkwise-mobile-menu ${variant === "customer" ? "parkwise-mobile-menu-customer" : ""} ${menuOpen ? "active" : ""}`}
                aria-hidden={!menuOpen}
            >
                <div className="parkwise-container">
                    <nav aria-label="Mobile navigation">
                        {navigation.map(({ label, href }) =>
                            href ? (
                                <Link
                                    key={label}
                                    href={href}
                                    onClick={() => setMenuOpen(false)}
                                    tabIndex={menuOpen ? 0 : -1}
                                    aria-current={
                                        activeHref === href ? "page" : undefined
                                    }
                                >
                                    {customerLabel(label)}
                                </Link>
                            ) : (
                                <span key={label} aria-disabled="true">
                                    {customerLabel(label)}
                                </span>
                            ),
                        )}
                        {canViewCustomer && navigationMode === "customer" && (
                            <Link
                                href="/customer/complete-profile"
                                onClick={() => setMenuOpen(false)}
                                tabIndex={menuOpen ? 0 : -1}
                            >
                                {copy("My Profile", "Hồ sơ của tôi")}
                            </Link>
                        )}
                        {menuOpen && variant === "customer" && (
                            <CustomerLanguageSwitcher mobile />
                        )}
                        {!session && (
                            <div className="parkwise-mobile-auth">
                                <Link
                                    href="/login"
                                    className="parkwise-button parkwise-auth-login"
                                    onClick={() => setMenuOpen(false)}
                                    tabIndex={menuOpen ? 0 : -1}
                                >
                                    <span className="parkwise-auth-label">
                                        {variant === "customer"
                                            ? copy("Login", "Đăng nhập")
                                            : "Login"}
                                    </span>
                                    <span
                                        className="parkwise-auth-label-fill"
                                        aria-hidden="true"
                                    >
                                        {variant === "customer"
                                            ? copy("Login", "Đăng nhập")
                                            : "Login"}
                                    </span>
                                </Link>
                                <Link
                                    href="/sign-up"
                                    className="parkwise-button parkwise-auth-signup"
                                    onClick={() => setMenuOpen(false)}
                                    tabIndex={menuOpen ? 0 : -1}
                                >
                                    <span className="parkwise-auth-label">
                                        {variant === "customer"
                                            ? copy("Sign Up", "Đăng ký")
                                            : "Sign Up"}
                                    </span>
                                    <span
                                        className="parkwise-auth-label-fill"
                                        aria-hidden="true"
                                    >
                                        {variant === "customer"
                                            ? copy("Sign Up", "Đăng ký")
                                            : "Sign Up"}
                                    </span>
                                </Link>
                                <Link
                                    href="/owner/register"
                                    className="parkwise-button parkwise-auth-owner"
                                    onClick={() => setMenuOpen(false)}
                                    tabIndex={menuOpen ? 0 : -1}
                                >
                                    <span className="parkwise-auth-label">
                                        {variant === "customer"
                                            ? copy(
                                                  "For Lot Owners",
                                                  "Dành cho chủ bãi",
                                              )
                                            : "For Lot Owners"}
                                    </span>
                                    <span
                                        className="parkwise-auth-label-fill"
                                        aria-hidden="true"
                                    >
                                        {variant === "customer"
                                            ? copy(
                                                  "For Lot Owners",
                                                  "Dành cho chủ bãi",
                                              )
                                            : "For Lot Owners"}
                                    </span>
                                </Link>
                                <Link
                                    href="/owner/register"
                                    className="parkwise-button parkwise-auth-owner"
                                    onClick={() => setMenuOpen(false)}
                                    tabIndex={menuOpen ? 0 : -1}
                                >
                                    <span className="parkwise-auth-label">
                                        For Lot Owners
                                    </span>
                                    <span
                                        className="parkwise-auth-label-fill"
                                        aria-hidden="true"
                                    >
                                        For Lot Owners
                                    </span>
                                </Link>
                            </div>
                        )}
                        {session && !isCustomer && (
                            <span className="parkwise-mobile-signed-in">
                                {session.account.email}
                            </span>
                        )}
                    </nav>
                </div>
            </div>
            <header
                className={`parkwise-navbar ${variant === "customer" ? "parkwise-navbar-customer" : ""} ${dark ? "parkwise-navbar-dark" : "parkwise-navbar-light"}`}
            >
                <div className="parkwise-container parkwise-navbar-inner">
                    <Link
                        href="/"
                        className="parkwise-brand"
                        aria-label="ParkWise home"
                        onClick={() => setMenuOpen(false)}
                    >
                        <Image
                            src="/images/parkwise/logo.png"
                            alt="ParkWise"
                            width={707}
                            height={353}
                            priority
                        />
                    </Link>
                    <nav
                        className="parkwise-desktop-nav"
                        aria-label="Main navigation"
                    >
                        {navigation.map(({ label, href }) =>
                            href ? (
                                <Link
                                    key={label}
                                    href={href}
                                    aria-current={
                                        activeHref === href ? "page" : undefined
                                    }
                                >
                                    {customerLabel(label)}
                                </Link>
                            ) : (
                                <span key={label} aria-disabled="true">
                                    {customerLabel(label)}
                                </span>
                            ),
                        )}
                    </nav>
                    {visibleAccountControl ? (
                        <div className="parkwise-navbar-account-slot">
                            {visibleAccountControl}
                        </div>
                    ) : (
                        <div className="parkwise-desktop-auth">
                            <Link
                                href="/login"
                                className="parkwise-button parkwise-auth-login"
                            >
                                <span className="parkwise-auth-label">
                                    Login
                                </span>
                                <span
                                    className="parkwise-auth-label-fill"
                                    aria-hidden="true"
                                >
                                    Login
                                </span>
                            </Link>
                            <Link
                                href="/sign-up"
                                className="parkwise-button parkwise-auth-signup"
                            >
                                <span className="parkwise-auth-label">
                                    Sign Up
                                </span>
                                <span
                                    className="parkwise-auth-label-fill"
                                    aria-hidden="true"
                                >
                                    Sign Up
                                </span>
                            </Link>
                            <Link
                                href="/owner/register"
                                className="parkwise-button parkwise-auth-owner"
                                title="Register as Lot Owner"
                            >
                                <span className="parkwise-auth-label">
                                    For Lot Owners
                                </span>
                                <span
                                    className="parkwise-auth-label-fill"
                                    aria-hidden="true"
                                >
                                    For Lot Owners
                                </span>
                            </Link>
                        </div>
                    )}
                    <button
                        className={`parkwise-menu-button ${menuOpen ? "active" : ""}`}
                        type="button"
                        aria-expanded={menuOpen}
                        aria-label={menuOpen ? "Close menu" : "Open menu"}
                        onClick={() => setMenuOpen((value) => !value)}
                    >
                        <span />
                        <span />
                        <span />
                    </button>
                </div>
            </header>
        </>
    );
}
