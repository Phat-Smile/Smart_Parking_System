import React from "react";

export interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
    variant?: "primary" | "secondary" | "outline" | "ghost";
    size?: "sm" | "md" | "lg";
    href?: string;
    children: React.ReactNode;
}

export function Button({
    variant = "primary",
    size = "md",
    href,
    className = "",
    children,
    ...props
}: ButtonProps) {
    const baseStyles =
        "inline-flex items-center justify-center font-semibold transition-all cursor-pointer focus:outline-none disabled:opacity-50 disabled:pointer-events-none active:scale-95";

    const sizeStyles = {
        sm: "px-3.5 py-1.5 rounded-full text-xs gap-1.5",
        md: "px-5 py-2.5 rounded-xl text-xs sm:text-sm gap-2",
        lg: "px-7 py-3 rounded-xl text-sm sm:text-base gap-2.5",
    }[size];

    const variantStyles = {
        primary:
            "text-white shadow-glow-purple bg-gradient-to-r from-brand-500 to-brand-600 hover:from-brand-400 hover:to-brand-700",
        secondary:
            "border border-black/[0.08] dark:border-white/[0.08] bg-white dark:bg-[#0c0d18] text-[#111322] dark:text-[#D5D8E3] hover:border-brand-500/50 shadow-sm",
        outline:
            "border border-black/[0.12] dark:border-white/[0.12] text-[#111322] dark:text-white hover:border-brand-500 hover:bg-[#F4F5F8] dark:hover:bg-white/5",
        ghost: "text-[#4B5565] dark:text-[#8790A3] hover:text-[#111322] dark:hover:text-white hover:bg-black/5 dark:hover:bg-white/5",
    }[variant];

    const combinedClassName =
        `${baseStyles} ${sizeStyles} ${variantStyles} ${className}`.trim();

    if (href) {
        return (
            <a href={href} className={combinedClassName}>
                {children}
            </a>
        );
    }

    return (
        <button className={combinedClassName} {...props}>
            {children}
        </button>
    );
}
