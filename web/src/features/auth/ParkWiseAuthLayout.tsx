"use client";

import type { ReactNode } from "react";
import { ParkWiseNavbar } from "@/features/parkwise/ParkWiseNavbar";
import { ParkWiseFooter } from "@/features/parkwise/ParkWiseFooter";

export function ParkWiseAuthLayout({
    children,
    fontClassName,
    className = "",
}: {
    children: ReactNode;
    fontClassName: string;
    className?: string;
}) {
    return (
        <div
            className={`parkwise-page parkwise-login-page ${className} ${fontClassName}`}
        >
            <ParkWiseNavbar variant="login" />
            <main className="parkwise-login-main">
                <section className="parkwise-login-section">
                    <div className="parkwise-login-container">
                        <div className="parkwise-login-card">{children}</div>
                    </div>
                </section>
            </main>
            <ParkWiseFooter />
        </div>
    );
}
