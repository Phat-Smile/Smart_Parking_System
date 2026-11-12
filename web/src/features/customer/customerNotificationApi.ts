import { gatewayFetch } from "@/shared/api/client";

export interface CustomerNotificationItem {
    id: string;
    type: "booking" | "parking" | "wallet" | string;
    title: string;
    description: string;
    href: string;
    createdAtUtc: string;
    readAtUtc: string | null;
}

export interface CustomerNotificationPage {
    items: CustomerNotificationItem[];
    totalCount: number;
    unreadCount: number;
    page: number;
    pageSize: number;
}

/** Endpoint: GET /api/notification/me */
export async function getMyNotifications(
    unreadOnly: boolean = false,
    page: number = 1,
    pageSize: number = 20,
    signal?: AbortSignal,
): Promise<CustomerNotificationPage> {
    const params = new URLSearchParams();
    if (unreadOnly) params.set("unreadOnly", "true");
    params.set("page", String(page));
    params.set("pageSize", String(pageSize));
    return await gatewayFetch<CustomerNotificationPage>(
        `/api/notification/me?${params.toString()}`,
        { signal },
    );
}

/** Endpoint: PATCH /api/notification/me/{id}/read */
export async function markNotificationRead(
    notificationId: string,
): Promise<void> {
    await gatewayFetch<void>(
        `/api/notification/me/${encodeURIComponent(notificationId)}/read`,
        { method: "PATCH" },
    );
}

/** Endpoint: PATCH /api/notification/me/read-all */
export async function markAllNotificationsRead(): Promise<{ updated: number }> {
    return await gatewayFetch<{ updated: number }>(
        "/api/notification/me/read-all",
        { method: "PATCH" },
    );
}
