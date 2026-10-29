"use client";

import React, { ReactNode } from "react";
import { usePathname } from "next/navigation";
import { Be_Vietnam_Pro } from "next/font/google";
import { OwnerSidebar } from "../../../components/layout/OwnerSidebar";
import "../../../styles/owner-v4.css";

const beVietnamPro = Be_Vietnam_Pro({
    subsets: ["latin", "vietnamese"],
    weight: ["400", "500", "600", "700", "800"],
    variable: "--font-be-vietnam",
    display: "swap",
});

export default function OwnerRootLayout({ children }: { children: ReactNode }) {
    const pathname = usePathname();
    const isAuthPage =
        pathname === "/owner/register" ||
        pathname?.startsWith("/owner/register") ||
        pathname === "/owner/onboarding" ||
        pathname?.startsWith("/owner/onboarding");

    if (isAuthPage) {
        return (
            <div
                className={beVietnamPro.className}
                style={{ width: "100%", minHeight: "100vh" }}
            >
                {children}
            </div>
        );
    }

    return (
        <div className={`app ${beVietnamPro.className}`}>
            <OwnerSidebar />
            <main id="m">{children}</main>
        </div>
    );
}
