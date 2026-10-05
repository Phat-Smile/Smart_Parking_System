"use client";

import React from "react";
import { GuestHeader } from "./GuestHeader";
import { GuestHero } from "./GuestHero";
import { GuestSearchPanel } from "./GuestSearchPanel";
import { GuestHowItWorks } from "./GuestHowItWorks";
import { GuestLiveParkingViewer } from "./GuestLiveParkingViewer";
import { GuestCtaBanner } from "./GuestCtaBanner";
import { GuestFooter } from "./GuestFooter";
import { ThemeProvider } from "@/shared/ui";

export function GuestPage() {
    return (
        <ThemeProvider defaultTheme="dark">
            <div
                id="pageBody"
                className="min-h-screen bg-[#F8F9FC] dark:bg-[#03010E] text-[#111322] dark:text-white font-sans antialiased selection:bg-brand-500 selection:text-white transition-colors duration-200"
            >
                {/* Fixed/Sticky Top Navigation Header */}
                <GuestHeader />

                {/* Main Content Sections */}
                <main className="max-w-[1240px] mx-auto px-4 sm:px-6 lg:px-8 pt-4 pb-12 space-y-12">
                    <div>
                        {/* Hero Section and Search Panel share one Stitch section */}
                        <GuestHero />
                        <GuestSearchPanel />
                    </div>

                    {/* How It Works Section */}
                    <GuestHowItWorks />

                    {/* Live Parking View Section */}
                    <GuestLiveParkingViewer />

                    {/* Final CTA Banner */}
                    <GuestCtaBanner />
                </main>

                {/* Footer */}
                <GuestFooter />
            </div>
        </ThemeProvider>
    );
}
