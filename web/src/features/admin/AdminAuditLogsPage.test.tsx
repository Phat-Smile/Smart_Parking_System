import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { LanguageProvider } from "@/shared/i18n/LanguageContext";
import { AdminAuditLogsPage } from "./AdminAuditLogsPage";
import * as api from "./auditLogsApi";

const mockAdminSession = {
    account: { id: "admin-1", email: "admin@parkwise.vn", role: "SystemAdmin" },
    tokens: { accessToken: "jwt-admin" },
};

vi.mock("@/features/auth/authSession", () => ({
    getAuthSessionSnapshot: () => mockAdminSession,
    subscribeAuthSession: () => () => {},
}));

const mockAuditLogs: api.AuditLogsPagedResponse = {
    items: [
        {
            id: "11111111-1111-1111-1111-111111111111",
            occurredAtUtc: "2026-10-30T10:00:00Z",
            actionType: "WalletTopUp",
            sourceService: "Payment",
            actorId: "user-1",
            actorEmail: "khachhang@parkwise.vn",
            actorRole: "Customer",
            parkingLotId: null,
            targetType: "Wallet",
            targetId: "wallet-100",
            reason: "Nạp tiền ví 100,000 VND",
            ipAddress: "127.0.0.1",
            details: { amount: 100000, method: "VNPay" },
        },
        {
            id: "22222222-2222-2222-2222-222222222222",
            occurredAtUtc: "2026-10-30T10:30:00Z",
            actionType: "PricingConfigured",
            sourceService: "Parking",
            actorId: "owner-1",
            actorEmail: "chubai@parkwise.vn",
            actorRole: "ParkingLotOwner",
            parkingLotId: "lot-1",
            targetType: "ParkingLot",
            targetId: "lot-1",
            reason: "Cập nhật giá giờ cao điểm",
            ipAddress: "127.0.0.1",
            details: { hourlyRate: 25000 },
        },
    ],
    page: 1,
    pageSize: 20,
    totalItems: 2,
    totalPages: 1,
};

function renderPage() {
    const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false } },
    });
    return render(
        <QueryClientProvider client={queryClient}>
            <LanguageProvider>
                <AdminAuditLogsPage />
            </LanguageProvider>
        </QueryClientProvider>,
    );
}

describe("AdminAuditLogsPage", () => {
    beforeEach(() => {
        vi.restoreAllMocks();
        vi.spyOn(api, "getAdminAuditLogs").mockResolvedValue(mockAuditLogs);
    });

    it("renders heading and audit logs table with records", async () => {
        renderPage();

        expect(screen.getByText("Nhật ký kiểm toán hệ thống")).toBeInTheDocument();

        await waitFor(() => {
            expect(screen.getByText("WalletTopUp")).toBeInTheDocument();
            expect(screen.getByText("PricingConfigured")).toBeInTheDocument();
            expect(screen.getByText("khachhang@parkwise.vn")).toBeInTheDocument();
            expect(screen.getByText("chubai@parkwise.vn")).toBeInTheDocument();
        });
    });

    it("opens details modal when clicking Chi tiết button", async () => {
        renderPage();

        await waitFor(() => {
            expect(screen.getAllByText("Chi tiết")).toHaveLength(2);
        });

        await userEvent.click(screen.getAllByText("Chi tiết")[0]);

        expect(screen.getByText(/Chi tiết bản ghi kiểm toán/)).toBeInTheDocument();
        expect(screen.getAllByText("Nạp tiền ví 100,000 VND")).toHaveLength(2);
        expect(screen.getByText(/"method": "VNPay"/)).toBeInTheDocument();

        await userEvent.click(screen.getByText("Đóng"));
        expect(screen.queryByText(/Chi tiết bản ghi kiểm toán/)).not.toBeInTheDocument();
    });

    it("filters by actionType when selecting from dropdown", async () => {
        const getLogsSpy = vi.spyOn(api, "getAdminAuditLogs");
        renderPage();

        await waitFor(() => {
            expect(screen.getByText("WalletTopUp")).toBeInTheDocument();
        });

        const select = screen.getByRole("combobox", { name: "Loại hành động" });
        await userEvent.selectOptions(select, "WalletTopUp");

        expect(getLogsSpy).toHaveBeenCalledWith(
            expect.objectContaining({ actionType: "WalletTopUp" }),
        );
    });
});
