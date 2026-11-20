"use client";

import React from "react";
import { useTheme } from "./theme/ThemeContext";

export interface ThemeToggleProps {
    className?: string;
    id?: string;
}

export function ThemeToggle({
    className = "",
    id = "themeToggleBtn",
}: ThemeToggleProps) {
    const { isDark, toggleTheme } = useTheme();

    return (
        <button
            type="button"
            id={id}
            aria-label="Switch theme"
            title={isDark ? "Switch to Light Mode" : "Switch to Dark Mode"}
            onClick={toggleTheme}
            className={`w-11 h-11 min-w-[44px] min-h-[44px] rounded-full flex items-center justify-center border transition-all cursor-pointer hover:scale-105 active:scale-95 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-500 shadow-sm ${
                isDark
                    ? "border-white/[0.08] bg-[#101426] text-brand-300 hover:border-brand-500/50"
                    : "border-[rgba(17,19,34,0.08)] bg-[#F4F5F8] text-[#111322] hover:border-brand-500/50"
            } ${className}`.trim()}
        >
            {isDark ? (
                // Moon Icon for Dark Mode
                <svg
                    className="w-4 h-4 block"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    viewBox="0 0 24 24"
                    aria-hidden="true"
                >
                    <path
                        d="M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                    />
                </svg>
            ) : (
                // Sun Icon for Light Mode
                <svg
                    className="w-4 h-4 block text-[#111322]"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    viewBox="0 0 24 24"
                    aria-hidden="true"
                >
                    <path
                        d="M12 3v1m0 16v1m9-9h-1M4 12H3m15.364 6.364l-.707-.707M6.343 6.343l-.707-.707m12.728 0l-.707.707M6.343 17.657l-.707.707M16 12a4 4 0 11-8 0 4 4 0 018 0z"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                    />
                </svg>
            )}
        </button>
    );
}
