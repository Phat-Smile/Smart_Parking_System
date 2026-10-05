import React from "react";

export interface CardProps extends React.HTMLAttributes<HTMLDivElement> {
    children: React.ReactNode;
    variant?: "default" | "subtle" | "elevated";
}

export function Card({
    children,
    variant = "default",
    className = "",
    ...props
}: CardProps) {
    const variantStyles = {
        default:
            "rounded-2xl border border-black/[0.08] dark:border-white/[0.08] bg-white dark:bg-[#0c0d18] shadow-card-light dark:shadow-card-dark",
        subtle: "rounded-xl border border-black/[0.08] dark:border-white/[0.08] bg-[#F8F9FC] dark:bg-[#101426]",
        elevated:
            "rounded-2xl border border-black/[0.08] dark:border-white/[0.08] bg-white/90 dark:bg-[#0c0d18]/90 backdrop-blur-xl shadow-card-light dark:shadow-card-dark",
    }[variant];

    return (
        <div className={`${variantStyles} ${className}`.trim()} {...props}>
            {children}
        </div>
    );
}
