/**
 * Notification types cho Owner Portal.
 * Interface rõ để sau thay bằng API.
 */

export type NotificationCategory =
    | "all"
    | "lots"
    | "reservations"
    | "sessions"
    | "layout"
    | "reports"
    | "system";

export interface OwnerNotification {
    id: string;
    category: Exclude<NotificationCategory, "all">;
    title: string;
    body: string;
    /** ISO 8601 */
    createdAt: string;
    read: boolean;
    /** Route để chuyển tới khi bấm */
    href?: string;
}
