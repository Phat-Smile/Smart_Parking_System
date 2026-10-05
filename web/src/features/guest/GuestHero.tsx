import React from "react";
import Image from "next/image";

export function GuestHero() {
    return (
        <section className="relative pt-2 sm:pt-4">
            <div className="grid grid-cols-1 lg:grid-cols-12 gap-8 items-center">
                {/* Hero Left: Content (~44%) */}
                <div className="lg:col-span-5 space-y-5">
                    {/* Guest Access Pill */}
                    <div
                        id="guestAccessPill"
                        className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-[11px] font-semibold border border-black/[0.08] dark:border-white/[0.08] bg-white dark:bg-[#0c0d18] text-[#4B5565] dark:text-[#D5D8E3] shadow-sm transition-colors"
                    >
                        <svg
                            className="w-3 h-3 text-brand-500 dark:text-brand-400"
                            fill="currentColor"
                            viewBox="0 0 20 20"
                            aria-hidden="true"
                        >
                            <path
                                fillRule="evenodd"
                                clipRule="evenodd"
                                d="M10 9a3 3 0 100-6 3 3 0 000 6zm-7 9a7 7 0 1114 0H3z"
                            />
                        </svg>
                        <span>Guest Access</span>
                    </div>

                    {/* Heading */}
                    <h1
                        id="heroHeading"
                        className="font-heading font-extrabold text-4xl sm:text-5xl lg:text-[54px] tracking-tight leading-[1.08] lg:leading-none text-[#111322] dark:text-white transition-colors"
                    >
                        Find <span className="text-brand-500">parking</span>
                        <br />
                        before you arrive.
                    </h1>

                    {/* Subheading */}
                    <p
                        id="heroSubheading"
                        className="text-xs sm:text-sm text-[#4B5565] dark:text-[#8790A3] leading-5 max-w-sm transition-colors"
                    >
                        See real-time availability, compare nearby parking, and
                        choose the best spot before you go.
                    </p>

                    {/* CTA Buttons Row */}
                    <div className="flex items-center gap-3 pt-1">
                        <a
                            href="#search"
                            className="px-5 py-2.5 rounded-xl font-semibold text-xs sm:text-sm text-white flex items-center gap-1.5 shadow-glow-purple bg-gradient-to-r from-brand-500 to-brand-600 hover:from-brand-400 hover:to-brand-700 transition-all active:scale-95"
                        >
                            <span>Find Parking</span>
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
                        </a>
                        <a
                            id="howItWorksBtn"
                            href="#how-it-works"
                            className="px-4 py-2.5 rounded-xl font-semibold text-xs sm:text-sm flex items-center gap-2 border border-black/[0.08] dark:border-white/[0.08] bg-white dark:bg-[#0c0d18] text-[#111322] dark:text-[#D5D8E3] hover:border-brand-500/50 shadow-sm transition-all"
                        >
                            <span className="w-4 h-4 rounded-full bg-brand-500/20 text-brand-500 dark:text-brand-400 flex items-center justify-center text-[9px] pl-0.5">
                                ▶
                            </span>
                            <span>How It Works</span>
                        </a>
                    </div>

                    {/* Inline 3 Benefit Badges */}
                    <div
                        id="benefitsRow"
                        className="pt-2 flex flex-wrap items-center gap-4 sm:gap-6 text-xs text-[#4B5565] dark:text-[#8790A3] font-medium transition-colors"
                    >
                        <div className="flex items-center gap-1.5">
                            <svg
                                className="w-3.5 h-3.5 text-brand-500"
                                fill="none"
                                stroke="currentColor"
                                strokeWidth="2"
                                viewBox="0 0 24 24"
                                aria-hidden="true"
                            >
                                <path
                                    d="M13 10V3L4 14h7v7l9-11h-7z"
                                    strokeLinecap="round"
                                    strokeLinejoin="round"
                                />
                            </svg>
                            <span>Live availability</span>
                        </div>
                        <div className="flex items-center gap-1.5">
                            <svg
                                className="w-3.5 h-3.5 text-brand-500"
                                fill="none"
                                stroke="currentColor"
                                strokeWidth="2"
                                viewBox="0 0 24 24"
                                aria-hidden="true"
                            >
                                <path
                                    d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"
                                    strokeLinecap="round"
                                    strokeLinejoin="round"
                                />
                            </svg>
                            <span>Easy reservations</span>
                        </div>
                        <div className="flex items-center gap-1.5">
                            <svg
                                className="w-3.5 h-3.5 text-brand-500"
                                fill="none"
                                stroke="currentColor"
                                strokeWidth="2"
                                viewBox="0 0 24 24"
                                aria-hidden="true"
                            >
                                <path
                                    d="M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4"
                                    strokeLinecap="round"
                                    strokeLinejoin="round"
                                />
                            </svg>
                            <span>2D / 3D preview</span>
                        </div>
                    </div>
                </div>

                {/* Hero Right: 3D Isometric Visual (~56%) */}
                <div className="lg:col-span-7 relative">
                    <div
                        id="heroImageCard"
                        className="relative rounded-[22px] overflow-hidden border border-black/[0.08] dark:border-white/[0.08] shadow-lg dark:shadow-2xl group transition-all"
                    >
                        <div className="relative w-full h-[320px] sm:h-[390px]">
                            <Image
                                src="/images/guest/hero-isometric.png"
                                alt="Isometric 3D Smart Parking Garage"
                                fill
                                priority
                                sizes="(max-width: 1024px) 100vw, 56vw"
                                className="object-cover object-center group-hover:scale-[1.01] transition-transform duration-500"
                            />
                        </div>

                        {/* Top Right Overlay: 156 Spaces Available */}
                        <div
                            id="badgeSpaces"
                            className="absolute top-3.5 right-3.5 backdrop-blur-md px-3.5 py-1.5 rounded-xl border border-black/[0.08] dark:border-white/10 bg-white/95 dark:bg-[#070814]/85 shadow-md dark:shadow-lg flex items-center gap-2.5 transition-colors"
                        >
                            <span className="w-2.5 h-2.5 rounded-full bg-emerald-500 animate-pulse" />
                            <div>
                                <div
                                    id="badgeSpacesNum"
                                    className="font-heading font-extrabold text-xs text-[#111322] dark:text-white leading-tight"
                                >
                                    156
                                </div>
                                <div
                                    id="badgeSpacesSub"
                                    className="text-[10px] text-[#7C8496] dark:text-zinc-400 leading-tight"
                                >
                                    spaces available
                                </div>
                            </div>
                        </div>

                        {/* Bottom Right Overlay: From $3/hour */}
                        <div
                            id="badgePrice"
                            className="absolute bottom-3.5 right-3.5 backdrop-blur-md px-3 py-1.5 rounded-xl border border-black/[0.08] dark:border-white/10 bg-white/95 dark:bg-[#070814]/85 shadow-md dark:shadow-lg flex items-center gap-2 transition-colors"
                        >
                            <div className="w-6 h-6 rounded-md bg-brand-500/20 text-brand-500 dark:text-brand-400 flex items-center justify-center">
                                <svg
                                    className="w-3.5 h-3.5"
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
                            </div>
                            <div>
                                <span
                                    id="badgePriceFrom"
                                    className="text-[9px] text-[#7C8496] dark:text-zinc-400 block leading-tight"
                                >
                                    From
                                </span>
                                <span
                                    id="badgePriceNum"
                                    className="font-heading font-bold text-xs text-[#111322] dark:text-white leading-tight"
                                >
                                    $3/hour
                                </span>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </section>
    );
}
