"use client";

import type { ReactNode } from "react";
import type { ApprovalStatus } from "./types";
import { useLanguage } from "@/shared/i18n/LanguageContext";

export function AdminIcon({
    name,
    size = 18,
    className,
}: {
    name: string;
    size?: number;
    className?: string;
}) {
    const paths: Record<string, ReactNode> = {
        grid: (
            <>
                <rect x="3.5" y="3.5" width="7" height="7" rx="1.8" />
                <rect x="13.5" y="3.5" width="7" height="7" rx="1.8" />
                <rect x="3.5" y="13.5" width="7" height="7" rx="1.8" />
                <rect x="13.5" y="13.5" width="7" height="7" rx="1.8" />
            </>
        ),
        clock: (
            <>
                <circle cx="12" cy="12" r="9" />
                <path d="M12 7v5l3.2 2" />
            </>
        ),
        lots: (
            <>
                <rect x="4" y="3" width="16" height="18" rx="3" />
                <path d="M9.5 16V8h3.2a2.5 2.5 0 0 1 0 5H9.5" />
            </>
        ),
        building: (
            <>
                <path d="M4 21h16" />
                <path d="M6 21V5a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v16" />
                <path d="M9 7h2m2 0h2M9 11h2m2 0h2M9 15h2m2 0h2" />
            </>
        ),
        users: (
            <>
                <circle cx="9" cy="8" r="3.5" />
                <path d="M3 20c.6-3.4 3-5 6-5s5.4 1.6 6 5M16 4.5a3.5 3.5 0 0 1 0 7M18 15c1.7.6 2.7 2.2 3 5" />
            </>
        ),
        user: (
            <>
                <circle cx="12" cy="8" r="4" />
                <path d="M5 20c.8-3.8 3.5-6 7-6s6.2 2.2 7 6" />
            </>
        ),
        briefcase: (
            <>
                <rect x="3" y="7" width="18" height="13" rx="2.5" />
                <path d="M8 7V5a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                <path d="M3 12h18" />
            </>
        ),
        fileCheck: (
            <>
                <path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8Z" />
                <path d="M14 3v5h5" />
                <path d="m9 14 2 2 4-4" />
            </>
        ),
        shield: (
            <>
                <path d="M12 3 4 6.5v5.5c0 5 3.4 8.3 8 9.5 4.6-1.2 8-4.5 8-9.5V6.5L12 3Z" />
                <path d="m9 12 2 2 4-4" />
            </>
        ),
        check: <path d="m5 12.5 4.5 4.5L19 7.5" />,
        checkCircle: (
            <>
                <circle cx="12" cy="12" r="9" />
                <path d="m8.5 12.3 2.4 2.4 4.8-4.8" />
            </>
        ),
        xCircle: (
            <>
                <circle cx="12" cy="12" r="9" />
                <path d="m15 9-6 6M9 9l6 6" />
            </>
        ),
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
        bell: (
            <>
                <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" />
                <path d="M13.73 21a2 2 0 0 1-3.46 0" />
            </>
        ),
        refresh: (
            <>
                <path d="M21 12a9 9 0 0 0-15.4-6.4L3 8" />
                <path d="M3 3v5h5" />
                <path d="M3 12a9 9 0 0 0 15.4 6.4L21 16" />
                <path d="M21 21v-5h-5" />
            </>
        ),
        history: (
            <>
                <path d="M3 12a9 9 0 1 0 3-6.7L3 8" />
                <path d="M3 3v5h5" />
                <path d="M12 7v5l3 2" />
            </>
        ),
        chevronLeft: <path d="m15 18-6-6 6-6" />,
        chevronRight: <path d="m9 18 6-6-6-6" />,
        chevronDown: <path d="m6 9 6 6 6-6" />,
        menu: <path d="M4 6h16M4 12h16M4 18h16" />,
        home: (
            <>
                <path d="m3 10.5 9-7 9 7" />
                <path d="M5 9v10a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1V9" />
            </>
        ),
        inbox: (
            <>
                <rect x="3" y="4" width="18" height="16" rx="2.5" />
                <path d="M3 13h5l2 3h4l2-3h5" />
            </>
        ),
        calendar: (
            <>
                <rect x="3" y="5" width="18" height="16" rx="2" />
                <path d="M16 3v4M8 3v4M3 10h18" />
            </>
        ),
        filter: (
            <>
                <path d="M3 5h18l-7 8v5l-4 2v-7L3 5Z" />
            </>
        ),
        clipboard: (
            <>
                <rect x="8" y="3" width="8" height="4" rx="1" />
                <path d="M8 5H6a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2h-2" />
            </>
        ),
        eye: (
            <>
                <path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7Z" />
                <circle cx="12" cy="12" r="3" />
            </>
        ),
        key: (
            <>
                <circle cx="8" cy="15" r="4" />
                <path d="m10.8 12.2 8.7-8.7M16 6l2.5 2.5M18 4l2.5 2.5" />
            </>
        ),
        sort: (
            <>
                <path d="m3 8 4-4 4 4M7 4v16M21 16l-4 4-4-4M17 20V4" />
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
            className={className}
        >
            {paths[name] ?? paths.grid}
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

export function AdminEmptyState({
    icon = "inbox",
    title,
    description,
    action,
}: {
    icon?: string;
    title: string;
    description?: string;
    action?: ReactNode;
}) {
    return (
        <div className="admin-empty-box">
            <span className="admin-empty-icon">
                <AdminIcon name={icon} size={22} />
            </span>
            <strong>{title}</strong>
            {description && <p className="admin-empty">{description}</p>}
            {action && <div className="admin-empty-action">{action}</div>}
        </div>
    );
}

export function AdminErrorState({
    title,
    message,
    onRetry,
    retryLabel = "Thử lại",
}: {
    title?: string;
    message: string;
    onRetry?: () => void;
    retryLabel?: string;
}) {
    return (
        <div className="admin-error-box" role="alert">
            <span className="admin-error-icon">
                <AdminIcon name="alert" size={20} />
            </span>
            <div className="admin-error-body">
                {title && <strong>{title}</strong>}
                <p>{message}</p>
            </div>
            {onRetry && (
                <button
                    type="button"
                    onClick={onRetry}
                    className="admin-btn admin-btn-sm admin-btn-danger"
                >
                    <AdminIcon name="refresh" size={14} />
                    {retryLabel}
                </button>
            )}
        </div>
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

export function formatWaitingDuration(
    iso: string | null | undefined,
    language: "vi" | "en" = "vi",
): string | null {
    if (!iso) return null;
    const timestamp = new Date(iso).getTime();
    if (Number.isNaN(timestamp)) return null;
    const diffMs = Math.max(0, Date.now() - timestamp);
    const diffMinutes = Math.floor(diffMs / 60000);
    if (diffMinutes < 60) {
        return language === "vi"
            ? `Chờ ${Math.max(1, diffMinutes)} phút`
            : `Waiting ${Math.max(1, diffMinutes)}m`;
    }
    const diffHours = Math.floor(diffMinutes / 60);
    if (diffHours < 24) {
        return language === "vi"
            ? `Chờ ${diffHours} giờ`
            : `Waiting ${diffHours}h`;
    }
    const diffDays = Math.floor(diffHours / 24);
    return language === "vi" ? `Chờ ${diffDays} ngày` : `Waiting ${diffDays}d`;
}
