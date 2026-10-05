import React from "react";
import Image from "next/image";

export function GuestCtaBanner() {
    return (
        <section
            id="ctaSection"
            className="relative rounded-2xl overflow-hidden border border-black/[0.08] dark:border-white/[0.08] shadow-lg dark:shadow-2xl h-[240px] sm:h-[260px] group transition-all"
        >
            {/* Image for Dark Mode */}
            <div className="hidden dark:block absolute inset-0 w-full h-full">
                <Image
                    id="ctaImgDark"
                    src="/images/guest/cta-banner-dark.png"
                    alt="Cinematic luxury underground parking garage"
                    fill
                    sizes="(max-width: 1240px) 100vw, 1240px"
                    className="object-cover object-center group-hover:scale-105 transition-transform duration-700"
                />
                <div
                    id="ctaOverlayDark"
                    className="absolute inset-0 bg-gradient-to-t from-[#03010e]/95 via-[#03010e]/60 to-[#03010e]/40"
                />
            </div>

            {/* Image for Light Mode */}
            <div className="block dark:hidden absolute inset-0 w-full h-full">
                <Image
                    id="ctaImgLight"
                    src="/images/guest/cta-banner-light.png"
                    alt="Bright modern luxury parking garage"
                    fill
                    sizes="(max-width: 1240px) 100vw, 1240px"
                    className="object-cover object-center group-hover:scale-105 transition-transform duration-700"
                />
                <div
                    id="ctaOverlayLight"
                    className="absolute inset-0 bg-gradient-to-t from-white/95 via-white/80 to-white/40"
                />
            </div>

            {/* Centered Content */}
            <div className="relative z-10 h-full flex flex-col items-center justify-center text-center px-4 space-y-3">
                <h2
                    id="ctaTitle"
                    className="font-heading font-extrabold text-2xl sm:text-3xl text-[#111322] dark:text-white tracking-tight transition-colors"
                >
                    Ready to park smarter?
                </h2>
                <p
                    id="ctaDesc"
                    className="text-xs sm:text-sm text-[#4B5565] dark:text-[#D5D8E3] max-w-md transition-colors"
                >
                    Find, compare, and reserve parking in seconds.
                </p>
                <a
                    href="#search"
                    className="mt-1 px-6 py-2.5 rounded-full font-semibold text-xs sm:text-sm text-white shadow-glow-purple bg-gradient-to-r from-brand-500 to-brand-600 hover:from-brand-400 hover:to-brand-700 transition-all active:scale-95 inline-flex items-center gap-1.5 cursor-pointer"
                >
                    <span>Find Parking Now</span>
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
            </div>
        </section>
    );
}
