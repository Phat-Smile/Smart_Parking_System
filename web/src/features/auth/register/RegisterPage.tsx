"use client";

import Image from "next/image";
import Link from "next/link";
import { ThemeProvider, useTheme } from "@/shared/ui/theme/ThemeContext";
import { RegisterForm } from "./RegisterForm";
import { RegisterIcon } from "./RegisterIcons";
import styles from "./RegisterPage.module.css";

function RegisterHeader() {
    const { isDark, toggleTheme } = useTheme();
    return (
        <header className="flex h-[52px] shrink-0 items-center justify-between gap-4">
            <Link
                href="/"
                className="flex items-center gap-2.5 rounded-lg focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--reg-accent)]"
            >
                <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-[color:var(--reg-accent)]/15 text-[var(--reg-accent)] shadow-sm">
                    <RegisterIcon name="parking" className="h-5 w-5" />
                </span>
                <span className="font-heading text-[17px] font-bold tracking-tight">
                    Smart Parking System
                </span>
            </Link>
            <nav
                className="hidden items-center gap-7 text-[13px] text-[var(--reg-muted)] md:flex"
                aria-label="Registration navigation"
            >
                <Link
                    href="/guest#how-it-works"
                    className="hover:text-[var(--reg-text)]"
                >
                    How It Works
                </Link>
                <Link
                    href="/guest#live-preview"
                    className="hover:text-[var(--reg-text)]"
                >
                    Support
                </Link>
            </nav>
            <div className="flex items-center gap-3">
                <button
                    type="button"
                    onClick={toggleTheme}
                    aria-label={
                        isDark ? "Switch to light mode" : "Switch to dark mode"
                    }
                    className="flex h-8 w-8 items-center justify-center rounded-lg border border-[var(--reg-border)] bg-[var(--reg-badge)] text-[var(--reg-muted)] hover:text-[var(--reg-text)] focus-visible:outline-2 focus-visible:outline-[var(--reg-accent)]"
                >
                    <RegisterIcon
                        name={isDark ? "sun" : "moon"}
                        className="h-[17px] w-[17px]"
                    />
                </button>
                <Link
                    href="/login"
                    aria-label="Sign In"
                    className="flex h-8 w-8 items-center justify-center rounded-full bg-[var(--reg-accent)] text-white shadow-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--reg-accent)]"
                >
                    <RegisterIcon name="person" className="h-[17px] w-[17px]" />
                </Link>
            </div>
        </header>
    );
}

function Illustration() {
    const { isDark } = useTheme();
    return (
        <section
            className="flex min-w-0 flex-col gap-4 lg:col-span-7 lg:h-[475px] lg:justify-between lg:pr-2"
            aria-label="Smart Parking introduction"
        >
            <div className="flex flex-col gap-1">
                <span className="inline-flex items-center gap-1.5 self-start rounded-full border border-[var(--reg-border)] bg-[var(--reg-badge)] px-2.5 py-0.5 text-[11px] font-medium text-[var(--reg-muted)]">
                    <span className="h-1.5 w-1.5 rounded-full bg-[var(--reg-accent)]" />
                    Join us today
                </span>
                <h1 className="font-heading mt-0.5 text-[29px] leading-[1.15] font-bold tracking-tight">
                    Create your{" "}
                    <span className="text-[var(--reg-accent)]">account</span>
                </h1>
                <p className="max-w-lg text-[13px] leading-snug text-[var(--reg-muted)]">
                    Get started with smarter parking. Create an account to find,
                    compare, and reserve parking spaces easily.
                </p>
            </div>
            <div
                className="flex flex-wrap items-center gap-2"
                aria-label="Service benefits"
            >
                {(
                    [
                        ["bolt", "Fast & Easy"],
                        ["shield", "Secure & Private"],
                        ["car", "Smart Parking"],
                    ] as const
                ).map(([icon, label]) => (
                    <span
                        key={label}
                        className="inline-flex h-8 items-center gap-1.5 rounded-lg border border-[var(--reg-border)] bg-[var(--reg-badge)] px-3 text-[11.5px] font-medium"
                    >
                        <RegisterIcon
                            name={icon}
                            className="h-3.5 w-3.5 text-[var(--reg-accent)]"
                        />
                        {label}
                    </span>
                ))}
            </div>
            <div className="relative h-[260px] overflow-hidden rounded-2xl border border-[var(--reg-border)] bg-[var(--reg-panel)] shadow-xl sm:h-[310px]">
                <Image
                    src={
                        isDark
                            ? "/images/guest/hero-isometric.png"
                            : "/images/auth/parking-daylight.png"
                    }
                    alt={
                        isDark
                            ? "Smart parking facility at dusk"
                            : "Smart parking facility in daylight"
                    }
                    fill
                    sizes="(max-width: 1023px) 100vw, 58vw"
                    loading="eager"
                    className="object-cover object-center"
                />
                <div className="pointer-events-none absolute inset-0 bg-gradient-to-t from-black/60 via-transparent to-transparent" />
                <span className="absolute top-3 left-3 inline-flex items-center gap-1.5 rounded-full border border-white/10 bg-black/60 px-2.5 py-1 text-[11px] font-medium text-white backdrop-blur-md">
                    <span className="h-2 w-2 rounded-full bg-emerald-400" />
                    Smart parking preview
                </span>
                <span className="absolute right-3 bottom-3 inline-flex items-center gap-1.5 rounded-lg border border-white/10 bg-black/65 px-2.5 py-1 font-mono text-[10.5px] tracking-wide text-white/90 backdrop-blur-md">
                    <span className="h-1.5 w-1.5 rounded-full bg-[var(--reg-accent)]" />
                    ILLUSTRATION
                </span>
            </div>
        </section>
    );
}

const trustItems = [
    {
        icon: "signal",
        title: "Parking information",
        description: "Explore available features",
    },
    {
        icon: "shield",
        title: "Secure account access",
        description: "Email verification required",
    },
    {
        icon: "sparkle",
        title: "Smart parking experience",
        description: "Plan your next visit",
    },
] as const;

function RegisterContent() {
    return (
        <div className={styles.page} data-testid="register-page">
            <div className="mx-auto flex min-h-screen w-full max-w-[1340px] flex-col justify-between gap-5 px-6 py-2 sm:px-8 md:py-2.5">
                <RegisterHeader />
                <main
                    className="grid w-full min-w-0 grid-cols-1 items-stretch gap-6 lg:grid-cols-12"
                    aria-label="Create account"
                >
                    <Illustration />
                    <RegisterForm />
                </main>
                <footer className="flex flex-wrap items-center justify-center gap-3 py-2">
                    {trustItems.map(({ icon, title, description }) => (
                        <div
                            key={title}
                            className="flex h-11 w-[240px] items-center gap-2.5 rounded-xl border border-[var(--reg-border)] bg-[var(--reg-trust)] px-3"
                        >
                            <RegisterIcon
                                name={icon}
                                className="h-5 w-5 shrink-0 text-[var(--reg-accent)]"
                            />
                            <div className="flex min-w-0 flex-col">
                                <span className="text-[12px] leading-tight font-semibold">
                                    {title}
                                </span>
                                <span className="text-[10px] leading-tight text-[var(--reg-muted)]">
                                    {description}
                                </span>
                            </div>
                        </div>
                    ))}
                </footer>
            </div>
        </div>
    );
}

export function RegisterPage() {
    return (
        <ThemeProvider defaultTheme="dark">
            <RegisterContent />
        </ThemeProvider>
    );
}
