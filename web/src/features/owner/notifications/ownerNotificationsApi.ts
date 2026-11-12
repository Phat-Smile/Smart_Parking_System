import { gatewayFetch } from "@/shared/api/client";
import { OwnerNotification, NotificationCategory } from "./notificationTypes";

export interface BackendNotificationItem {
    id: string;
    type: string;
    title: string;
    description: string;
    href: string;
    createdAtUtc: string;
    readAtUtc?: string | null;
}

export interface BackendNotificationPage {
    items: BackendNotificationItem[];
    totalCount: number;
    unreadCount: number;
    page: number;
    pageSize: number;
}

function mapTypeToCategory(type: string): Exclude<NotificationCategory, "all"> {
    const lower = (type || "").toLowerCase();
    if (
        lower.includes("lot") ||
        lower.includes("parking-lot") ||
        lower.includes("verification")
    )
        return "lots";
    if (lower.includes("booking") || lower.includes("reservation"))
        return "reservations";
    if (lower.includes("session")) return "sessions";
    if (lower.includes("layout")) return "layout";
    if (lower.includes("report") || lower.includes("revenue")) return "reports";
    return "system";
}

export function toOwnerNotification(
    item: BackendNotificationItem,
): OwnerNotification {
    return {
        id: item.id,
        category: mapTypeToCategory(item.type),
        title: item.title,
        body: item.description,
        createdAt: item.createdAtUtc,
        read: Boolean(item.readAtUtc),
        href: item.href || undefined,
    };
}

/**
 * Fetch notifications from GET /api/notification/me
 */
export async function getMyNotificationsApi(
    unreadOnly = false,
): Promise<OwnerNotification[] | null> {
    try {
        const res = await gatewayFetch<BackendNotificationPage>(
            `/api/notification/me?unreadOnly=${unreadOnly}&page=1&pageSize=30`,
            { method: "GET" },
        );
        if (res && Array.isArray(res.items)) {
            return res.items.map(toOwnerNotification);
        }
        return null;
    } catch {
        return null;
    }
}

/**
 * Mark a single notification read: PATCH /api/notification/me/{id}/read
 */
export async function markNotificationReadApi(id: string): Promise<boolean> {
    try {
        await gatewayFetch(
            `/api/notification/me/${encodeURIComponent(id)}/read`,
            {
                method: "PATCH",
            },
        );
        return true;
    } catch {
        return false;
    }
}

/**
 * Mark all notifications read: PATCH /api/notification/me/read-all
 */
export async function markAllNotificationsReadApi(): Promise<boolean> {
    try {
        await gatewayFetch("/api/notification/me/read-all", {
            method: "PATCH",
        });
        return true;
    } catch {
        return false;
    }
}
