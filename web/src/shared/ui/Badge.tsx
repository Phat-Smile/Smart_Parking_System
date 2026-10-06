import React from "react";

export interface BadgeProps extends React.HTMLAttributes<HTMLSpanElement> {
    variant?: "default" | "success" | "brand" | "glass";
    pulse?: boolean;
    children: React.ReactNode;
}

export function Badge({
    variant = "default",
    pulse = false,
    className = "",
    children,
    ...props
}: BadgeProps) {
    const variantStyles = {
        default:
            "border border-black/[0.08] dark:border-white/[0.08] bg-white dark:bg-[#0c0d18] text-[#4B5565] dark:text-[#D5D8E3]",
        success:
            "bg-[#EAFBF3] dark:bg-emerald-500/15 text-[#17A36B] dark:text-emerald-400 border border-[#17A36B]/20 dark:border-emerald-500/30",
        brand: "border border-brand-500/50 bg-brand-500/15 text-brand-600 dark:text-brand-300",
        glass: "backdrop-blur-md border border-black/10 dark:border-white/10 bg-white/95 dark:bg-[#070814]/85 text-[#111322] dark:text-white shadow-lg",
    }[variant];

    return (
        <span
            className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-[11px] font-semibold transition-colors ${variantStyles} ${className}`.trim()}
            {...props}
        >
            {pulse && (
                <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse shrink-0" />
            )}
            {children}
        </span>
    );
}
