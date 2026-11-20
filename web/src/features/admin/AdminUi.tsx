"use client";

import type { ReactNode } from "react";
import type { ApprovalStatus } from "./types";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import {
    LayoutGrid,
    Clock,
    SquareParking,
    Building2,
    Users,
    User,
    Briefcase,
    FileCheck,
    Shield,
    Check,
    CheckCircle2,
    XCircle,
    X,
    ArrowRight,
    ArrowLeft,
    Search,
    MapPin,
    LogOut,
    AlertTriangle,
    Bell,
    RefreshCw,
    History,
    ChevronLeft,
    ChevronRight,
    ChevronDown,
    Menu,
    Home,
    Inbox,
    Calendar,
    Filter,
    Clipboard,
    Eye,
    Key,
    ArrowUpDown,
    Plus,
    Trash2,
    type LucideIcon,
} from "lucide-react";

const ICON_MAP: Record<string, LucideIcon> = {
    grid: LayoutGrid,
    clock: Clock,
    lots: SquareParking,
    building: Building2,
    users: Users,
    user: User,
    briefcase: Briefcase,
    fileCheck: FileCheck,
    shield: Shield,
    check: Check,
    checkCircle: CheckCircle2,
    xCircle: XCircle,
    close: X,
    arrow: ArrowRight,
    back: ArrowLeft,
    search: Search,
    pin: MapPin,
    logout: LogOut,
    alert: AlertTriangle,
    bell: Bell,
    refresh: RefreshCw,
    history: History,
    chevronLeft: ChevronLeft,
    chevronRight: ChevronRight,
    chevronDown: ChevronDown,
    menu: Menu,
    home: Home,
    inbox: Inbox,
    calendar: Calendar,
    filter: Filter,
    clipboard: Clipboard,
    eye: Eye,
    key: Key,
    sort: ArrowUpDown,
    plus: Plus,
    trash: Trash2,
};

export function AdminIcon({
    name,
    size = 18,
    className,
}: {
    name: string;
    size?: number;
    className?: string;
}) {
    const Component = ICON_MAP[name] ?? LayoutGrid;
    return <Component size={size} className={className} aria-hidden="true" />;
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
