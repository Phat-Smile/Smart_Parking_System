import { describe, it, expect, vi, beforeEach } from "vitest";
import { getAdminAuditLogs } from "./auditLogsApi";
import * as client from "@/shared/api/client";

describe("auditLogsApi", () => {
    beforeEach(() => {
        vi.restoreAllMocks();
    });

    it("fetches audit logs with default page and pageSize", async () => {
        const mockResponse = {
            items: [
                {
                    id: "log-1",
                    occurredAtUtc: "2026-10-30T10:00:00Z",
                    actionType: "WalletTopUp",
                    sourceService: "Payment",
                    actorId: "user-1",
                    actorEmail: "test@example.com",
                    actorRole: "Customer",
                    parkingLotId: null,
                    targetType: "Wallet",
                    targetId: "wallet-1",
                    reason: "Topup 50,000 VND",
                    ipAddress: "127.0.0.1",
                    details: { amount: 50000 },
                },
            ],
            page: 1,
            pageSize: 20,
            totalItems: 1,
            totalPages: 1,
        };

        const fetchSpy = vi
            .spyOn(client, "gatewayFetch")
            .mockResolvedValueOnce(mockResponse);

        const result = await getAdminAuditLogs();

        expect(fetchSpy).toHaveBeenCalledWith(
            "/api/payment/admin/audit-logs?page=1&pageSize=20",
            { method: "GET" },
        );
        expect(result.items).toHaveLength(1);
        expect(result.items[0].actionType).toBe("WalletTopUp");
    });

    it("serializes all filter query parameters correctly", async () => {
        const fetchSpy = vi
            .spyOn(client, "gatewayFetch")
            .mockResolvedValueOnce({
                items: [],
                page: 2,
                pageSize: 10,
                totalItems: 0,
                totalPages: 0,
            });

        await getAdminAuditLogs({
            page: 2,
            pageSize: 10,
            actionType: "LotPricingConfigured",
            parkingLotId: "lot-123",
            actorId: "actor-456",
            fromUtc: "2026-10-01T00:00:00Z",
            toUtc: "2026-10-31T23:59:59Z",
        });

        const calledUrl = fetchSpy.mock.calls[0][0];
        expect(calledUrl).toContain("page=2");
        expect(calledUrl).toContain("pageSize=10");
        expect(calledUrl).toContain("actionType=LotPricingConfigured");
        expect(calledUrl).toContain("parkingLotId=lot-123");
        expect(calledUrl).toContain("actorId=actor-456");
        expect(calledUrl).toContain("fromUtc=2026-10-01T00%3A00%3A00Z");
        expect(calledUrl).toContain("toUtc=2026-10-31T23%3A59%3A59Z");
    });
});
