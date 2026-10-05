import React from "react";

export interface LogoProps {
    href?: string;
    className?: string;
    showText?: boolean;
    size?: "default" | "large";
    variant?: "brand" | "auth";
}

export function Logo({
    href = "#",
    className = "",
    showText = true,
    size = "default",
    variant = "brand",
}: LogoProps) {
    const isLarge = size === "large";
    const content = (
        <div className={`flex items-center gap-2.5 group ${className}`.trim()}>
            <div
                className={`${isLarge ? "w-9 h-9" : "w-7 h-7"} rounded-lg flex items-center justify-center group-hover:scale-105 transition-transform shrink-0 ${variant === "auth" ? "bg-[var(--auth-feature-icon)]" : "bg-gradient-to-br from-brand-500 to-brand-600 shadow-glow-purple"}`}
            >
                <span
                    className={`font-heading font-black tracking-tighter ${variant === "auth" ? "text-[var(--auth-feature-icon-color)]" : "text-white"} ${isLarge ? "text-lg" : "text-xs"}`}
                >
                    P
                </span>
            </div>
            {showText && (
                <span
                    className={`font-heading font-bold tracking-tight text-[#111322] dark:text-white transition-colors ${isLarge ? "text-xl" : "text-sm"}`}
                >
                    Smart Parking System
                </span>
            )}
        </div>
    );

    if (href) {
        return (
            <a
                href={href}
                className="flex items-center rounded-lg focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--guest-primary)]"
            >
                {content}
            </a>
        );
    }

    return content;
}
