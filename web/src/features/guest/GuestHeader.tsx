"use client";

import React, { useState } from "react";
import Link from "next/link";
import { Logo, ThemeToggle } from "@/shared/ui";

export function GuestHeader() {
    const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

    return (
        <header className="sticky top-0 z-50 px-4 sm:px-6 lg:px-8 pt-3 pb-2">
            <nav
                id="navbar"
                aria-label="Guest Navigation"
                className="max-w-[1240px] mx-auto rounded-full px-5 py-2.5 backdrop-blur-xl border border-black/[0.08] dark:border-white/[0.07] bg-white/90 dark:bg-[#0b0c17]/80 shadow-sm dark:shadow-lg flex items-center justify-between transition-colors"
            >
                {/* Brand Logo */}
                <Logo href="#search" />

                {/* Desktop Nav Links & Actions */}
                <div className="hidden md:flex items-center gap-6 text-xs font-semibold">
                    <a
                        id="navLink1"
                        href="#how-it-works"
                        className="text-[#4B5565] dark:text-[#8790A3] hover:text-[#111322] dark:hover:text-white transition-colors"
                    >
                        How It Works
                    </a>
                    <a
                        id="navLink2"
                        href="#live-preview"
                        className="text-[#4B5565] dark:text-[#8790A3] hover:text-[#111322] dark:hover:text-white transition-colors"
                    >
                        Support
                    </a>
                    <div className="flex items-center gap-3 pl-2">
                        {/* Theme Toggle Button */}
                        <ThemeToggle />

                        {/* Log In Button */}
                        <Link
                            id="loginBtn"
                            href="/login"
                            className="px-5 py-1.5 rounded-full text-xs font-semibold border border-black/[0.12] dark:border-white/[0.12] text-[#111322] dark:text-white hover:border-brand-500 hover:bg-[#F4F5F8] dark:hover:bg-white/5 transition-all active:scale-95"
                        >
                            Log In
                        </Link>
                    </div>
                </div>

                {/* Mobile Menu Controls */}
                <div className="flex md:hidden items-center gap-2">
                    <ThemeToggle />
                    <button
                        type="button"
                        aria-expanded={mobileMenuOpen}
                        aria-label="Toggle navigation menu"
                        onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
                        className="w-8 h-8 rounded-full flex items-center justify-center border border-black/[0.08] dark:border-white/[0.08] bg-[#F4F5F8] dark:bg-[#101426] text-[#111322] dark:text-white focus:outline-none"
                    >
                        <svg
                            className="w-4 h-4"
                            fill="none"
                            stroke="currentColor"
                            strokeWidth="2"
                            viewBox="0 0 24 24"
                        >
                            {mobileMenuOpen ? (
                                <path
                                    d="M6 18L18 6M6 6l12 12"
                                    strokeLinecap="round"
                                    strokeLinejoin="round"
                                />
                            ) : (
                                <path
                                    d="M4 6h16M4 12h16M4 18h16"
                                    strokeLinecap="round"
                                    strokeLinejoin="round"
                                />
                            )}
                        </svg>
                    </button>
                </div>
            </nav>

            {/* Mobile Dropdown Menu */}
            {mobileMenuOpen && (
                <div
                    id="mobileMenu"
                    className="md:hidden mt-2 max-w-[1240px] mx-auto rounded-2xl p-4 border border-black/[0.08] dark:border-white/[0.08] bg-white/95 dark:bg-[#0b0c17]/95 backdrop-blur-xl shadow-xl flex flex-col gap-3 text-xs font-semibold"
                >
                    <a
                        href="#how-it-works"
                        onClick={() => setMobileMenuOpen(false)}
                        className="py-2 text-[#4B5565] dark:text-[#8790A3] hover:text-[#111322] dark:hover:text-white transition-colors"
                    >
                        How It Works
                    </a>
                    <a
                        href="#live-preview"
                        onClick={() => setMobileMenuOpen(false)}
                        className="py-2 text-[#4B5565] dark:text-[#8790A3] hover:text-[#111322] dark:hover:text-white transition-colors"
                    >
                        Support
                    </a>
                    <div className="pt-2 border-t border-black/[0.08] dark:border-white/[0.08] flex items-center justify-between">
                        <Link
                            href="/login"
                            onClick={() => setMobileMenuOpen(false)}
                            className="w-full text-center px-4 py-2 rounded-xl border border-black/[0.12] dark:border-white/[0.12] text-[#111322] dark:text-white hover:border-brand-500"
                        >
                            Log In
                        </Link>
                    </div>
                </div>
            )}
        </header>
    );
}
