import React from "react";

export function GuestHowItWorks() {
    return (
        <section id="how-it-works" className="py-2 scroll-mt-20">
            <div className="text-center space-y-1 mb-8">
                <h2
                    id="hiwTitle"
                    className="font-heading font-extrabold text-2xl sm:text-3xl text-[#111322] dark:text-white transition-colors"
                >
                    How it works
                </h2>
                <p
                    id="hiwSub"
                    className="text-xs text-[#7C8496] dark:text-[#8790A3] transition-colors"
                >
                    Find and reserve parking in three simple steps.
                </p>
            </div>

            {/* 3 Steps with Connector */}
            <div className="relative grid grid-cols-1 md:grid-cols-3 gap-6 max-w-4xl mx-auto">
                {/* Step 1 */}
                <div className="flex flex-col items-center text-center relative px-3">
                    <div
                        id="stepIcon1"
                        className="w-11 h-11 rounded-2xl flex items-center justify-center relative mb-3 bg-white dark:bg-[#101426] border border-black/[0.08] dark:border-white/[0.08] text-brand-600 dark:text-brand-400 shadow-sm dark:shadow-md transition-colors"
                    >
                        <span className="absolute -top-1.5 -left-1.5 w-4 h-4 rounded-full bg-brand-500 text-white font-bold text-[9px] flex items-center justify-center">
                            1
                        </span>
                        <svg
                            className="w-5 h-5 text-brand-500 dark:text-brand-400"
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
                            <path
                                d="M15 11a3 3 0 11-6 0 3 3 0 016 0z"
                                strokeLinecap="round"
                                strokeLinejoin="round"
                            />
                        </svg>
                    </div>
                    <h3
                        id="stepTitle1"
                        className="font-heading font-bold text-sm text-[#111322] dark:text-white mb-1 transition-colors"
                    >
                        Search destination
                    </h3>
                    <p
                        id="stepDesc1"
                        className="text-[11px] text-[#7C8496] dark:text-[#8790A3] max-w-[200px] transition-colors"
                    >
                        Enter where you&apos;re going to see nearby parking.
                    </p>
                    {/* Connector line to step 2 */}
                    <div
                        id="stepLine1"
                        className="hidden md:block absolute top-5 -right-3 w-6 border-b border-dashed border-[#DDE2EB] dark:border-zinc-700"
                    />
                </div>

                {/* Step 2 */}
                <div className="flex flex-col items-center text-center relative px-3">
                    <div
                        id="stepIcon2"
                        className="w-11 h-11 rounded-2xl flex items-center justify-center relative mb-3 bg-white dark:bg-[#101426] border border-black/[0.08] dark:border-white/[0.08] text-brand-600 dark:text-brand-400 shadow-sm dark:shadow-md transition-colors"
                    >
                        <span className="absolute -top-1.5 -left-1.5 w-4 h-4 rounded-full bg-brand-500 text-white font-bold text-[9px] flex items-center justify-center">
                            2
                        </span>
                        <svg
                            className="w-5 h-5 text-brand-500 dark:text-brand-400"
                            fill="none"
                            stroke="currentColor"
                            strokeWidth="2"
                            viewBox="0 0 24 24"
                            aria-hidden="true"
                        >
                            <path
                                d="M3 4a1 1 0 011-1h16a1 1 0 011 1v2.586a1 1 0 01-.293.707l-6.414 6.414a1 1 0 00-.293.707V17l-4 4v-6.586a1 1 0 00-.293-.707L3.293 7.293A1 1 0 013 6.586V4z"
                                strokeLinecap="round"
                                strokeLinejoin="round"
                            />
                        </svg>
                    </div>
                    <h3
                        id="stepTitle2"
                        className="font-heading font-bold text-sm text-[#111322] dark:text-white mb-1 transition-colors"
                    >
                        Compare spaces
                    </h3>
                    <p
                        id="stepDesc2"
                        className="text-[11px] text-[#7C8496] dark:text-[#8790A3] max-w-[200px] transition-colors"
                    >
                        View availability, prices and 2D / 3D previews.
                    </p>
                    {/* Connector line to step 3 */}
                    <div
                        id="stepLine2"
                        className="hidden md:block absolute top-5 -right-3 w-6 border-b border-dashed border-[#DDE2EB] dark:border-zinc-700"
                    />
                </div>

                {/* Step 3 */}
                <div className="flex flex-col items-center text-center relative px-3">
                    <div
                        id="stepIcon3"
                        className="w-11 h-11 rounded-2xl flex items-center justify-center relative mb-3 bg-white dark:bg-[#101426] border border-black/[0.08] dark:border-white/[0.08] text-brand-600 dark:text-brand-400 shadow-sm dark:shadow-md transition-colors"
                    >
                        <span className="absolute -top-1.5 -left-1.5 w-4 h-4 rounded-full bg-brand-500 text-white font-bold text-[9px] flex items-center justify-center">
                            3
                        </span>
                        <svg
                            className="w-5 h-5 text-brand-500 dark:text-brand-400"
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
                    </div>
                    <h3
                        id="stepTitle3"
                        className="font-heading font-bold text-sm text-[#111322] dark:text-white mb-1 transition-colors"
                    >
                        Go or reserve
                    </h3>
                    <p
                        id="stepDesc3"
                        className="text-[11px] text-[#7C8496] dark:text-[#8790A3] max-w-[200px] transition-colors"
                    >
                        Choose a spot and go or reserve in seconds.
                    </p>
                </div>
            </div>
        </section>
    );
}
