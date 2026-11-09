import { describe, it, expect, vi, beforeEach } from "vitest";
import {
    toOwnerNotification,
    getMyNotificationsApi,
    markNotificationReadApi,
    markAllNotificationsReadApi,
    type BackendNotificationItem,
} from "./ownerNotificationsApi";

describe("ownerNotificationsApi", () => {
    beforeEach(() => {
        vi.restoreAllMocks();
    });

    it("maps backend notification correctly to OwnerNotification format", () => {
        const item: BackendNotificationItem = {
            id: "ntf-101",
            type: "parking.slot-needs-verification",
            title: "Sự cố ô đỗ cần xác minh",
            description: "Ô B-12 quá 30 phút chưa có xe đỗ vào",
            href: "/owner/parking-lots",
            createdAtUtc: "2026-11-09T10:00:00Z",
            readAtUtc: null,
        };

        const mapped = toOwnerNotification(item);
        expect(mapped.id).toBe("ntf-101");
        expect(mapped.category).toBe("lots");
        expect(mapped.title).toBe("Sự cố ô đỗ cần xác minh");
        expect(mapped.body).toBe("Ô B-12 quá 30 phút chưa có xe đỗ vào");
        expect(mapped.read).toBe(false);
        expect(mapped.href).toBe("/owner/parking-lots");
    });

    it("correctly identifies read notifications", () => {
        const item: BackendNotificationItem = {
            id: "ntf-102",
            type: "booking.confirmed",
            title: "Đặt chỗ thành công",
            description: "Khách đã đặt trước",
            href: "/owner/reservations",
            createdAtUtc: "2026-11-09T10:00:00Z",
            readAtUtc: "2026-11-09T10:05:00Z",
        };

        const mapped = toOwnerNotification(item);
        expect(mapped.category).toBe("reservations");
        expect(mapped.read).toBe(true);
    });

    it("returns null gracefully if gateway fetch fails", async () => {
        global.fetch = vi.fn().mockRejectedValue(new Error("Network error"));
        const result = await getMyNotificationsApi();
        expect(result).toBeNull();
    });

    it("sends patch request to mark notification read", async () => {
        global.fetch = vi.fn().mockResolvedValue({
            ok: true,
            status: 204,
            headers: new Headers(),
            json: () => Promise.resolve({}),
        });

        const ok = await markNotificationReadApi("ntf-101");
        expect(ok).toBe(true);
        const [callUrl, callOptions] = (global.fetch as any).mock.calls[0];
        expect(String(callUrl)).toContain("/api/notification/me/ntf-101/read");
        expect(callOptions.method).toBe("PATCH");
    });

    it("sends patch request to mark all notifications read", async () => {
        global.fetch = vi.fn().mockResolvedValue({
            ok: true,
            status: 200,
            headers: new Headers({ "content-type": "application/json" }),
            json: () => Promise.resolve({ updated: 5 }),
        });

        const ok = await markAllNotificationsReadApi();
        expect(ok).toBe(true);
        const [callUrl, callOptions] = (global.fetch as any).mock.calls[0];
        expect(String(callUrl)).toContain("/api/notification/me/read-all");
        expect(callOptions.method).toBe("PATCH");
    });
});
