import React from "react";

export interface LogoProps {
    href?: string;
    className?: string;
    showText?: boolean;
}

export function Logo({
    href = "#",
    className = "",
    showText = true,
}: LogoProps) {
    const content = (
        <div className={`flex items-center gap-2.5 group ${className}`.trim()}>
            <div className="w-7 h-7 rounded-lg bg-gradient-to-br from-brand-500 to-brand-600 flex items-center justify-center shadow-glow-purple group-hover:scale-105 transition-transform shrink-0">
                <span className="font-heading font-black text-white text-xs tracking-tighter">
                    P
                </span>
            </div>
            {showText && (
                <span className="font-heading font-bold text-base tracking-tight text-[#111322] dark:text-white transition-colors">
                    ParkWise
                </span>
            )}
        </div>
    );

    if (href) {
        return (
            <a href={href} className="focus:outline-none flex items-center">
                {content}
            </a>
        );
    }

    return content;
}
