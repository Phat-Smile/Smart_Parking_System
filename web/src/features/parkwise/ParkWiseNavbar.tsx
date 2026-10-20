"use client";

import { useEffect, useState, type ReactNode } from "react";
import Image from "next/image";
import Link from "next/link";

const navigation = [
    { label: "Home", href: "/" },
    { label: "How It Works", href: "/#how-it-works" },
    { label: "About", href: "/#about" },
    { label: "FAQ", href: "/#how-it-works" },
];

/** Guest-facing portion of ParkWise's src/Components/Nav/Navbar.jsx. */
export function ParkWiseNavbar({
    variant = "home",
    accountControl,
}: {
    variant?: "home" | "login";
    accountControl?: ReactNode;
}) {
    const [menuOpen, setMenuOpen] = useState(false);
    const [scrolled, setScrolled] = useState(variant === "home");

    useEffect(() => {
        const onScroll = () =>
            setScrolled(window.scrollY > 100 || variant === "home");
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

    const dark = scrolled && !menuOpen;

    return (
        <>
            <div
                className={`parkwise-mobile-menu ${menuOpen ? "active" : ""}`}
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
                                >
                                    {label}
                                </Link>
                            ) : (
                                <span key={label} aria-disabled="true">
                                    {label}
                                </span>
                            ),
                        )}
                        {!accountControl && (
                            <div className="parkwise-mobile-auth">
                                <Link
                                    href="/login"
                                    className="parkwise-button parkwise-auth-login"
                                    onClick={() => setMenuOpen(false)}
                                    tabIndex={menuOpen ? 0 : -1}
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
                                    onClick={() => setMenuOpen(false)}
                                    tabIndex={menuOpen ? 0 : -1}
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
                            </div>
                        )}
                    </nav>
                </div>
            </div>
            <header
                className={`parkwise-navbar ${dark ? "parkwise-navbar-dark" : "parkwise-navbar-light"}`}
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
                                <Link key={label} href={href}>
                                    {label}
                                </Link>
                            ) : (
                                <span key={label} aria-disabled="true">
                                    {label}
                                </span>
                            ),
                        )}
                    </nav>
                    {accountControl ? (
                        <div className="parkwise-navbar-account-slot">
                            {accountControl}
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
