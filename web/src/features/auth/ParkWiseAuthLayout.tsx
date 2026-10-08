"use client";

import type { ReactNode } from "react";
import Image from "next/image";
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

export function ParkWiseAuthAside({
    description,
    afterWelcome,
    afterSupport,
}: {
    description: string;
    afterWelcome?: ReactNode;
    afterSupport?: ReactNode;
}) {
    return (
        <aside className="parkwise-login-aside">
            <div className="parkwise-welcome">
                <h1>Welcome!</h1>
                <div className="parkwise-aside-box">
                    <p>{description}</p>
                </div>
            </div>

            {afterWelcome}

            <div className="parkwise-aside-box parkwise-support">
                <p>Need assistance?</p>
                <div>
                    <svg
                        viewBox="0 0 24 24"
                        width="24"
                        height="24"
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="2"
                        aria-hidden="true"
                    >
                        <path d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                    </svg>
                    <h2>Contact support</h2>
                </div>
            </div>

            {afterSupport}

            <div className="parkwise-login-social">
                <p>
                    <svg
                        viewBox="0 0 24 24"
                        width="20"
                        height="20"
                        fill="currentColor"
                        aria-hidden="true"
                    >
                        <path d="M23 12a11 11 0 01-17.437 8.912L2 22l1.088-3.563A11 11 0 1123 12z" />
                    </svg>{" "}
                    Follow us on Social Media
                </p>
                <div>
                    {[1, 2, 3, 4, 5].map((number) => (
                        <span key={number}>
                            <Image
                                src={`/images/parkwise/as (${number}).svg`}
                                alt=""
                                width={24}
                                height={24}
                            />
                        </span>
                    ))}
                </div>
            </div>
        </aside>
    );
}
