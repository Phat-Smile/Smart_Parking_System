"use client";

import type { ReactNode } from "react";
import type { ApprovalStatus } from "./types";
import { useLanguage } from "@/shared/i18n/LanguageContext";

export function AdminIcon({
    name,
    size = 18,
}: {
    name: string;
    size?: number;
}) {
    const paths: Record<string, ReactNode> = {
        grid: <path d="M4 4h7v7H4zM13 4h7v7h-7zM4 13h7v7H4zM13 13h7v7h-7z" />,
        clock: (
            <>
                <circle cx="12" cy="12" r="9" />
                <path d="M12 7v5l3 2" />
            </>
        ),
        lots: (
            <>
                <rect x="4" y="3" width="16" height="18" rx="3" />
                <path d="M10 16V8h3a2.5 2.5 0 0 1 0 5h-3" />
            </>
        ),
        users: (
            <>
                <circle cx="9" cy="8" r="3.5" />
                <path d="M3 20c.6-3.4 3-5 6-5s5.4 1.6 6 5M16 4.5a3.5 3.5 0 0 1 0 7M18 15c1.7.6 2.7 2.2 3 5" />
            </>
        ),
        check: <path d="m5 12.5 4.5 4.5L19 7.5" />,
        close: <path d="M6 6l12 12M18 6 6 18" />,
        arrow: <path d="M4 12h16m-6-6 6 6-6 6" />,
        back: <path d="M20 12H4m6-6-6 6 6 6" />,
        search: (
            <>
                <circle cx="11" cy="11" r="6.5" />
                <path d="m16 16 4 4" />
            </>
        ),
        pin: (
            <>
                <path d="M12 21s-6.5-5.6-6.5-11a6.5 6.5 0 0 1 13 0c0 5.4-6.5 11-6.5 11Z" />
                <circle cx="12" cy="10" r="2.3" />
            </>
        ),
        logout: <path d="M14 4h5v16h-5M10 8l-4 4 4 4M6 12h10" />,
        alert: (
            <>
                <path d="M12 3 2.5 20h19L12 3Z" />
                <path d="M12 10v4m0 3h.01" />
            </>
        ),
    };
    return (
        <svg
            width={size}
            height={size}
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.8"
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
        >
            {paths[name]}
        </svg>
    );
}

export function StatusPill({ status }: { status: ApprovalStatus }) {
    const { t } = useLanguage();
    const label =
        status === "Pending"
            ? t("pending")
            : status === "Approved"
              ? t("approved")
              : t("rejected");
    return (
        <span className={`admin-pill admin-pill-${status.toLowerCase()}`}>
            <i aria-hidden="true" />
            {label}
        </span>
    );
}

export const vnd = (value: number) => `${value.toLocaleString("vi-VN")} ₫`;

export const formatDate = (iso: string | null) =>
    iso
        ? new Date(iso).toLocaleString("en-GB", {
              day: "2-digit",
              month: "short",
              year: "numeric",
              hour: "2-digit",
              minute: "2-digit",
          })
        : "—";
