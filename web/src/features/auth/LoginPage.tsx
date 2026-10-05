"use client";

import { useState } from "react";
import { ThemeProvider, useTheme } from "@/shared/ui/theme/ThemeContext";
import { Logo } from "@/shared/ui/Logo";
import { LoginForm } from "./LoginForm";
import { LoginIcon } from "./LoginIcon";
import { LoginIllustration } from "./LoginIllustration";

function LoginThemeToggle() {
    const { isDark, toggleTheme } = useTheme();
    return (
        <button
            type="button"
            aria-label={isDark ? "Switch to light mode" : "Switch to dark mode"}
            onClick={toggleTheme}
            className="flex h-9 w-9 items-center justify-center rounded-lg border border-[var(--auth-panel-border)] bg-[var(--auth-feature)] text-[var(--auth-text-secondary)] hover:text-[var(--auth-accent)] focus-visible:outline-2 focus-visible:outline-[var(--auth-accent)]"
        >
            <LoginIcon name={isDark ? "sun" : "moon"} className="h-5 w-5" />
        </button>
    );
}

function LoginHeader() {
    const [menuOpen, setMenuOpen] = useState(false);
    return (
        <header className="fixed inset-x-0 top-0 z-50 border-b border-[var(--auth-panel-border)] bg-[var(--auth-navbar)] backdrop-blur-xl">
            <div className="flex h-16 w-full items-center justify-between px-4 sm:px-6">
                <Logo href="/guest" size="large" variant="auth" />
                <nav
                    className="hidden items-center gap-6 md:flex"
                    aria-label="Main navigation"
                >
                    <a
                        href="/guest#how-it-works"
                        className="font-mono text-sm tracking-[0.04em] text-[var(--auth-text-muted)] hover:text-[var(--auth-text)] focus-visible:outline-2 focus-visible:outline-[var(--auth-accent)]"
                    >
                        How It Works
                    </a>
                    <a
                        href="/guest#live-preview"
                        className="font-mono text-sm tracking-[0.04em] text-[var(--auth-text-muted)] hover:text-[var(--auth-text)] focus-visible:outline-2 focus-visible:outline-[var(--auth-accent)]"
                    >
                        Support
                    </a>
                </nav>
                <div className="flex items-center gap-4">
                    <LoginThemeToggle />
                    <span
                        className="flex h-8 w-8 items-center justify-center rounded-full bg-[var(--auth-accent)] text-white"
                        aria-hidden="true"
                    >
                        <LoginIcon
                            name="person"
                            className="h-[18px] w-[18px]"
                        />
                    </span>
                    <button
                        type="button"
                        className="flex h-9 w-9 items-center justify-center rounded-lg border border-[var(--auth-panel-border)] text-[var(--auth-text)] focus-visible:outline-2 focus-visible:outline-[var(--auth-accent)] md:hidden"
                        aria-label="Toggle navigation menu"
                        aria-expanded={menuOpen}
                        onClick={() => setMenuOpen((open) => !open)}
                    >
                        <span
                            aria-hidden="true"
                            className="text-xl leading-none"
                        >
                            {menuOpen ? "×" : "☰"}
                        </span>
                    </button>
                </div>
            </div>
            {menuOpen && (
                <nav
                    className="flex flex-col gap-3 border-t border-[var(--auth-panel-border)] bg-[var(--auth-navbar)] px-4 py-4 text-sm text-[var(--auth-text)] md:hidden"
                    aria-label="Mobile navigation"
                >
                    <a href="/guest#how-it-works">How It Works</a>
                    <a href="/guest#live-preview">Support</a>
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
            className="mx-auto mt-10 w-full max-w-4xl pt-6 lg:mt-12"
        >
            <div className="grid grid-cols-1 gap-4 md:grid-cols-3 lg:gap-8">
                {benefits.map((benefit) => (
                    <div
                        key={benefit.title}
                        className="flex items-center gap-4 rounded-2xl border border-[var(--auth-panel-border)] bg-[var(--auth-feature)] p-4 backdrop-blur-md"
                    >
                        <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-[var(--auth-feature-icon)] text-[var(--auth-feature-icon-color)]">
                            <LoginIcon
                                name={benefit.icon}
                                className="h-6 w-6"
                            />
                        </span>
                        <span className="flex flex-col">
                            <span className="font-heading text-xl font-semibold text-[var(--auth-text)]">
                                {benefit.title}
                            </span>
                            <span className="text-[13px] leading-5 text-[var(--auth-text-muted)]">
                                {benefit.detail}
                            </span>
                        </span>
                    </div>
                ))}
            </div>
        </section>
    );
}

function LoginFooter() {
    return (
        <footer className="w-full border-t border-[var(--auth-panel-border)] bg-[var(--auth-bg)]">
            <div className="flex w-full flex-col items-center justify-between gap-4 px-4 py-6 font-mono text-[10px] tracking-[0.08em] text-[var(--auth-text-muted)] sm:px-6 md:flex-row">
                <span>
                    © 2024 Smart Parking System. Telemetry Architecture.
                </span>
                <div className="flex flex-wrap justify-center gap-4">
                    <span>Privacy Policy</span>
                    <span>Terms of Service</span>
                    <span>Sensor Status</span>
                </div>
            </div>
        </footer>
    );
}

export function LoginPage() {
    return (
        <ThemeProvider defaultTheme="dark">
            <div className="min-h-screen bg-[var(--auth-bg)] text-[var(--auth-text)] transition-colors">
                <LoginHeader />
                <main className="flex min-h-[calc(100vh-4rem)] flex-col pt-16">
                    <div className="relative flex min-h-[calc(100vh-4rem)] w-full flex-col items-center justify-center overflow-hidden px-4 py-8 sm:px-6 sm:py-12 lg:px-8">
                        <div className="pointer-events-none absolute -top-40 -left-40 h-96 w-96 rounded-full bg-[var(--auth-accent)] opacity-20 blur-3xl" />
                        <div className="pointer-events-none absolute -right-40 -bottom-40 h-96 w-96 rounded-full bg-[var(--auth-accent)] opacity-15 blur-3xl" />
                        <div className="relative mx-auto flex w-full max-w-6xl flex-col items-center">
                            <div className="grid w-full grid-cols-1 items-stretch gap-6 lg:grid-cols-12 lg:gap-8">
                                <LoginIllustration />
                                <LoginForm />
                            </div>
                            <LoginBenefits />
                        </div>
                    </div>
                </main>
                <LoginFooter />
            </div>
        </ThemeProvider>
    );
}
