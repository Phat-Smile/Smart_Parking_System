import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { LanguageProvider } from "@/shared/i18n/LanguageContext";
import { AdminDashboardPage } from "./AdminDashboardPage";
import { AdminShell } from "./AdminShell";
import { refreshAdminLots } from "./adminLotsData";
import * as api from "./reviewParkingLot";

vi.mock("next/navigation", () => ({
    usePathname: () => "/admin/dashboard",
    useRouter: () => ({
        push: vi.fn(),
        replace: vi.fn(),
    }),
}));

const mockAdminSession = {
    displayName: "Nguyễn Quản Trị",
    account: {
        id: "admin-001",
        email: "admin@parkwise.vn",
        role: "SystemAdmin" as const,
    },
    tokens: { accessToken: "jwt-admin-token" },
};

vi.mock("@/features/auth/authSession", () => ({
    getAuthSessionSnapshot: () => mockAdminSession,
    subscribeAuthSession: () => () => {},
}));

describe("AdminDashboardPage & AdminShell UI/UX", () => {
    beforeEach(() => {
        window.localStorage.clear();
    });

    afterEach(() => {
        vi.restoreAllMocks();
    });

    it("renders 4 KPI cards, oldest pending queue, and recent decisions timeline from real API data", async () => {
        vi.spyOn(api, "getAdminParkingLots").mockResolvedValue([
            {
                id: "10000000-0000-0000-0000-000000000001",
                name: "Bãi Vincom Đồng Khởi",
                address: "72 Lê Thánh Tôn, Quận 1",
                type: "Basement",
                ownerUserId: "20000000-0000-0000-0000-000000000001",
                ownerName: "Trần Chủ Bãi",
                status: "Pending",
                rejectionReason: null,
                latitude: 10.77,
                longitude: 106.7,
                submittedAtUtc: "2026-10-20T08:00:00Z",
            },
            {
                id: "10000000-0000-0000-0000-000000000002",
                name: "Bãi Bitexco Financial",
                address: "2 Hải Triều, Quận 1",
                type: "Indoor",
                ownerUserId: "20000000-0000-0000-0000-000000000002",
                status: "Approved",
                rejectionReason: null,
                latitude: 10.77,
                longitude: 106.7,
                reviewedAtUtc: "2026-10-25T10:00:00Z",
            },
            {
                id: "10000000-0000-0000-0000-000000000003",
                name: "Bãi Thảo Điền",
                address: "12 Quốc Hương, TP. Thủ Đức",
                type: "Outdoor",
                ownerUserId: "20000000-0000-0000-0000-000000000003",
                status: "Rejected",
                rejectionReason: "Thiếu hồ sơ PCCC",
                latitude: 10.8,
                longitude: 106.73,
                reviewedAtUtc: "2026-10-26T14:30:00Z",
            },
        ]);

        await refreshAdminLots();

        render(
            <LanguageProvider>
                <AdminShell>
                    <AdminDashboardPage />
                </AdminShell>
            </LanguageProvider>,
        );

        // Verify Sidebar business groups & collapse toggle
        expect(screen.getAllByText("Tổng quan").length).toBeGreaterThan(0);
        expect(screen.getByText("Quản lý bãi xe")).toBeInTheDocument();
        expect(screen.getByText("Quản lý đối tác")).toBeInTheDocument();
        expect(screen.getByText("Vận hành")).toBeInTheDocument();
        expect(screen.getByText("Hệ thống")).toBeInTheDocument();

        // Verify 4 KPI Cards
        expect(screen.getByText("Tổng số bãi xe")).toBeInTheDocument();
        expect(screen.getByText("Bãi xe đang hoạt động")).toBeInTheDocument();
        expect(screen.getByText("Bãi xe chờ duyệt")).toBeInTheDocument();
        expect(screen.getByText("Hồ sơ bị từ chối")).toBeInTheDocument();

        // Verify Oldest Pending Queue
        expect(screen.getByText("Bãi Vincom Đồng Khởi")).toBeInTheDocument();
        expect(screen.getByText("Trần Chủ Bãi")).toBeInTheDocument();

        // Verify Activity Timeline (Recent Decisions)
        expect(screen.getByText("Bãi Bitexco Financial")).toBeInTheDocument();
        expect(screen.getByText("Bãi Thảo Điền")).toBeInTheDocument();
        expect(screen.getByText(/Thiếu hồ sơ PCCC/)).toBeInTheDocument();
    });

    it("surfaces real API errors with a Retry button instead of hiding errors", async () => {
        const user = userEvent.setup();
        const spy = vi
            .spyOn(api, "getAdminParkingLots")
            .mockRejectedValueOnce(new TypeError("Failed to fetch"));

        await refreshAdminLots();

        render(
            <LanguageProvider>
                <AdminDashboardPage />
            </LanguageProvider>,
        );

        expect(await screen.findByRole("alert")).toBeInTheDocument();
        expect(
            screen.getByText("Không thể tải dữ liệu bảng điều khiển"),
        ).toBeInTheDocument();

        spy.mockResolvedValueOnce([]);
        await user.click(screen.getByRole("button", { name: /Thử lại/i }));

        await waitFor(() => {
            expect(screen.queryByRole("alert")).not.toBeInTheDocument();
        });
    });
});
