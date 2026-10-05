"use client";

import { useState } from "react";
import Link from "next/link";
import { ThemeProvider, useTheme } from "@/shared/ui/theme/ThemeContext";
import { AuthGlyph } from "./AuthGlyph";
import { LoginForm } from "./LoginForm";
import { LoginIllustration } from "./LoginIllustration";

function ThemeButton() {
    const { isDark, toggleTheme } = useTheme();
    return (
        <button
            type="button"
            aria-label={isDark ? "Switch to light mode" : "Switch to dark mode"}
            onClick={toggleTheme}
            className="flex h-8 w-8 items-center justify-center rounded-lg bg-[var(--login-control)] text-[var(--login-muted)] hover:bg-[var(--login-control-hover)] hover:text-[var(--login-text)] focus-visible:outline-2 focus-visible:outline-[var(--login-accent)]"
        >
            <AuthGlyph
                name={isDark ? "sun" : "moon"}
                className="h-[18px] w-[18px]"
            />
        </button>
    );
}

function LoginHeader() {
    const [menuOpen, setMenuOpen] = useState(false);
    return (
        <header className="flex h-[52px] w-full shrink-0 items-center justify-between border-b border-[var(--login-border)]">
            <Link
                href="/"
                className="flex items-center gap-2.5 rounded-lg focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--login-accent)]"
            >
                <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-[var(--login-control)] text-[var(--login-accent)] shadow-sm">
                    <AuthGlyph name="parking" className="h-[19px] w-[19px]" />
                </span>
                <span className="font-heading text-lg font-bold tracking-tight text-[var(--login-text)]">
                    Smart Parking System
                </span>
            </Link>
            <nav
                className="hidden items-center gap-6 md:flex"
                aria-label="Main navigation"
            >
                <Link
                    href="/guest#how-it-works"
                    className="font-mono text-sm text-[var(--login-muted)] hover:text-[var(--login-text)] focus-visible:outline-2 focus-visible:outline-[var(--login-accent)]"
                >
                    How It Works
                </Link>
                <Link
                    href="/guest#pageFooter"
                    className="font-mono text-sm text-[var(--login-muted)] hover:text-[var(--login-text)] focus-visible:outline-2 focus-visible:outline-[var(--login-accent)]"
                >
                    Support
                </Link>
            </nav>
            <div className="flex items-center gap-3">
                <ThemeButton />
                <span
                    className="flex h-8 w-8 items-center justify-center rounded-full bg-[var(--login-accent)] text-[var(--login-accent-contrast)]"
                    aria-hidden="true"
                >
                    <AuthGlyph name="person" className="h-[17px] w-[17px]" />
                </span>
                <button
                    type="button"
                    aria-label="Toggle navigation menu"
                    aria-expanded={menuOpen}
                    onClick={() => setMenuOpen((open) => !open)}
                    className="flex h-8 w-8 items-center justify-center rounded-lg border border-[var(--login-border)] text-[var(--login-text)] focus-visible:outline-2 focus-visible:outline-[var(--login-accent)] md:hidden"
                >
                    <span aria-hidden="true">{menuOpen ? "×" : "☰"}</span>
                </button>
            </div>
            {menuOpen && (
                <nav
                    aria-label="Mobile navigation"
                    className="absolute top-[62px] right-6 left-6 z-30 flex flex-col gap-3 rounded-xl border border-[var(--login-border)] bg-[var(--login-panel)] p-4 text-sm text-[var(--login-text)] shadow-xl md:hidden"
                >
                    <Link href="/guest#how-it-works">How It Works</Link>
                    <Link href="/guest#pageFooter">Support</Link>
                </nav>
            )}
        </header>
    );
}

const benefits = [
    { icon: "car", title: "Real-time", detail: "availability" },
    { icon: "shield", title: "Secure", detail: "and reliable" },
    { icon: "pin", title: "Smart", detail: "parking experience" },
] as const;

function LoginBenefits() {
    return (
        <section
            aria-label="Smart Parking benefits"
            className="mt-3 mb-1 w-full shrink-0"
        >
            <div className="mx-auto grid max-w-[860px] grid-cols-1 gap-4 md:grid-cols-3">
                {benefits.map((item) => (
                    <div
                        key={item.title}
                        className="flex h-[54px] items-center gap-3 rounded-xl border border-[var(--login-border)] bg-[var(--login-benefit)] px-4 shadow-sm backdrop-blur-md"
                    >
                        <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-[var(--login-benefit-icon-bg)] text-[var(--login-accent)]">
                            <AuthGlyph
                                name={item.icon}
                                className="h-[18px] w-[18px]"
                            />
                        </span>
                        <span className="flex flex-col leading-tight">
                            <span className="font-heading text-[13px] font-semibold text-[var(--login-text)]">
                                {item.title}
                            </span>
                            <span className="text-[11px] text-[var(--login-muted)]">
                                {item.detail}
                            </span>
                        </span>
                    </div>
                ))}
            </div>
        </section>
    );
}

export function LoginPage() {
    return (
        <ThemeProvider defaultTheme="dark">
            <div className="min-h-screen w-full bg-[var(--login-bg)] text-[var(--login-text)]">
                <div className="mx-auto flex min-h-screen w-full max-w-[1404px] flex-col px-6 py-2.5 lg:h-[730px] lg:min-h-[730px] lg:px-8">
                    <LoginHeader />
                    <main className="my-6 grid w-full grid-cols-1 items-stretch gap-6 lg:my-auto lg:h-[475px] lg:shrink-0 lg:grid-cols-12">
                        <LoginIllustration />
                        <LoginForm />
                    </main>
                    <LoginBenefits />
                </div>
            </div>
        </ThemeProvider>
    );
}
