"use client";

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
            <ThemeButton />
        </header>
    );
}

export function LoginPage() {
    return (
        <ThemeProvider defaultTheme="dark">
            <div className="min-h-screen w-full bg-[var(--login-bg)] text-[var(--login-text)]">
                <div className="mx-auto flex min-h-screen w-full max-w-[1404px] flex-col px-6 py-2.5 lg:px-8">
                    <LoginHeader />
                    <main className="my-6 grid w-full grid-cols-1 items-stretch gap-6 lg:min-h-[553px] lg:flex-1 lg:grid-cols-12">
                        <LoginIllustration />
                        <LoginForm />
                    </main>
                </div>
            </div>
        </ThemeProvider>
    );
}
