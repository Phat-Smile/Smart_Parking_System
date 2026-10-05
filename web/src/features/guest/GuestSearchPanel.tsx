"use client";

import React, { useState } from "react";

export function GuestSearchPanel() {
    const [searchQuery, setSearchQuery] = useState("");
    const [activeFilter, setActiveFilter] = useState<
        "nearby" | "available" | "cheapest"
    >("nearby");

    const handleSubmit = (e: React.FormEvent) => {
        e.preventDefault();
        // Visual search interaction only - Guest does not participate in registered-user Search flow
    };

    return (
        <div
            id="search"
            className="max-w-3xl mx-auto mt-7 sm:mt-9 mb-14 sm:mb-16 relative z-20"
        >
            <div
                id="searchContainer"
                className="rounded-2xl p-5 sm:p-6 backdrop-blur-xl border border-black/[0.08] dark:border-white/[0.08] bg-white/90 dark:bg-[#0c0d18]/90 shadow-card-light dark:shadow-card-dark transition-all"
            >
                <form
                    onSubmit={handleSubmit}
                    className="flex flex-col sm:flex-row items-stretch sm:items-center gap-3"
                >
                    <div className="relative flex-1 flex items-center">
                        <svg
                            id="searchIcon"
                            className="w-4 h-4 text-[#7C8496] dark:text-[#8790A3] absolute left-4 pointer-events-none"
                            fill="none"
                            stroke="currentColor"
                            strokeWidth="2"
                            viewBox="0 0 24 24"
                            aria-hidden="true"
                        >
                            <path
                                d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"
                                strokeLinecap="round"
                                strokeLinejoin="round"
                            />
                        </svg>
                        <input
                            id="searchInput"
                            type="text"
                            value={searchQuery}
                            onChange={(e) => setSearchQuery(e.target.value)}
                            placeholder="Where are you going?"
                            className="w-full pl-11 pr-4 py-3 rounded-xl text-xs sm:text-sm bg-[#F8F9FC] dark:bg-[#101426] text-[#111322] dark:text-white placeholder:text-[#7C8496] dark:placeholder:text-[#8790A3] border border-black/[0.08] dark:border-white/[0.08] focus:outline-none focus:border-brand-500 transition-colors"
                        />
                    </div>
                    <button
                        type="submit"
                        className="px-7 py-3 rounded-xl font-semibold text-xs sm:text-sm text-white shadow-glow-purple bg-gradient-to-r from-brand-500 to-brand-600 hover:from-brand-400 hover:to-brand-700 transition-all active:scale-95 whitespace-nowrap flex items-center justify-center gap-2 cursor-pointer"
                    >
                        <span>Search</span>
                        <svg
                            className="w-3.5 h-3.5"
                            fill="none"
                            stroke="currentColor"
                            strokeWidth="2.5"
                            viewBox="0 0 24 24"
                            aria-hidden="true"
                        >
                            <path
                                d="M14 5l7 7m0 0l-7 7m7-7H3"
                                strokeLinecap="round"
                                strokeLinejoin="round"
                            />
                        </svg>
                    </button>
                </form>

                {/* Quick Filters */}
                <div
                    id="filterRow"
                    className="flex flex-wrap items-center gap-2 pt-3.5 mt-3.5 border-t border-black/[0.06] dark:border-white/[0.07]"
                >
                    <span
                        id="quickFilterLabel"
                        className="text-[11px] text-[#7C8496] dark:text-[#8790A3] mr-1 hidden sm:inline"
                    >
                        Quick filters:
                    </span>

                    <button
                        type="button"
                        onClick={() => setActiveFilter("nearby")}
                        className={`filter-btn px-3.5 py-1.5 rounded-full text-[11px] font-medium flex items-center gap-1.5 transition-colors cursor-pointer ${
                            activeFilter === "nearby"
                                ? "border border-brand-500/50 bg-brand-500/15 text-brand-600 dark:text-brand-300"
                                : "border border-black/[0.08] dark:border-white/[0.08] bg-white dark:bg-[#101426] text-[#4B5565] dark:text-[#8790A3] hover:text-[#111322] dark:hover:text-white"
                        }`}
                    >
                        <svg
                            className="w-3 h-3 text-brand-500 dark:text-brand-400"
                            fill="none"
                            stroke="currentColor"
                            strokeWidth="2"
                            viewBox="0 0 24 24"
                            aria-hidden="true"
                        >
                            <path
                                d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z"
                                strokeLinecap="round"
                                strokeLinejoin="round"
                            />
                        </svg>
                        <span>Nearby</span>
                    </button>

                    <button
                        type="button"
                        onClick={() => setActiveFilter("available")}
                        className={`filter-btn px-3.5 py-1.5 rounded-full text-[11px] font-medium flex items-center gap-1.5 transition-colors cursor-pointer ${
                            activeFilter === "available"
                                ? "border border-brand-500/50 bg-brand-500/15 text-brand-600 dark:text-brand-300"
                                : "border border-black/[0.08] dark:border-white/[0.08] bg-white dark:bg-[#101426] text-[#4B5565] dark:text-[#8790A3] hover:text-[#111322] dark:hover:text-white"
                        }`}
                    >
                        <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 dark:bg-emerald-400" />
                        <span>Available now</span>
                    </button>

                    <button
                        type="button"
                        onClick={() => setActiveFilter("cheapest")}
                        className={`filter-btn px-3.5 py-1.5 rounded-full text-[11px] font-medium flex items-center gap-1.5 transition-colors cursor-pointer ${
                            activeFilter === "cheapest"
                                ? "border border-brand-500/50 bg-brand-500/15 text-brand-600 dark:text-brand-300"
                                : "border border-black/[0.08] dark:border-white/[0.08] bg-white dark:bg-[#101426] text-[#4B5565] dark:text-[#8790A3] hover:text-[#111322] dark:hover:text-white"
                        }`}
                    >
                        <svg
                            className="w-3 h-3 text-brand-500 dark:text-brand-400"
                            fill="none"
                            stroke="currentColor"
                            strokeWidth="2"
                            viewBox="0 0 24 24"
                            aria-hidden="true"
                        >
                            <path
                                d="M7 7h.01M7 3h5c.512 0 1.024.195 1.414.586l7 7a2 2 0 010 2.828l-7 7a2 2 0 01-2.828 0l-7-7A1.994 1.994 0 013 12V7a4 4 0 014-4z"
                                strokeLinecap="round"
                                strokeLinejoin="round"
                            />
                        </svg>
                        <span>Cheapest</span>
                    </button>
                </div>
            </div>
        </div>
    );
}
