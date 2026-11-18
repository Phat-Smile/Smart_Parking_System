import { fireEvent, render, screen, waitFor } from "@testing-library/react";
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
        expect(screen.getByText("Hệ thống")).toBeInTheDocument();

        // Verify 4 KPI Cards
        expect(screen.getByText("Tổng số bãi xe")).toBeInTheDocument();
        expect(screen.getByText("Bãi xe đang hoạt động")).toBeInTheDocument();
        expect(screen.getByText("Bãi xe chờ duyệt")).toBeInTheDocument();
        expect(screen.getByText("Hồ sơ bị từ chối")).toBeInTheDocument();

        // Verify KPI card navigation links & arrow placement in progress bar area
        const kpiLinks = screen
            .getAllByRole("link")
            .filter((l) => l.className.includes("admin-stat"));
        expect(kpiLinks).toHaveLength(4);
        expect(kpiLinks[0]).toHaveAttribute("href", "/admin/parking-lots");
        expect(kpiLinks[1]).toHaveAttribute(
            "href",
            "/admin/parking-lots?status=Approved",
        );
        expect(kpiLinks[2]).toHaveAttribute(
            "href",
            "/admin/parking-lots/pending",
        );
        expect(kpiLinks[3]).toHaveAttribute(
            "href",
            "/admin/owner-applications",
        );

        // Verify Main Analytics: Activity Trends (Bar Chart) & Status Distribution (Doughnut Chart)
        expect(screen.getByText("Xu hướng hoạt động")).toBeInTheDocument();
        expect(
            screen.getByRole("img", {
                name: /Biểu đồ cột xu hướng hoạt động/i,
            }),
        ).toBeInTheDocument();
        expect(screen.getByText("Tỷ lệ trạng thái")).toBeInTheDocument();
        expect(
            screen.getByRole("img", {
                name: /Biểu đồ tròn tỷ lệ trạng thái bãi đỗ xe/i,
            }),
        ).toBeInTheDocument();
        expect(screen.getByText("Tổng bản ghi")).toBeInTheDocument();

        // Verify Oldest Pending Queue
        expect(screen.getByText("Bãi Vincom Đồng Khởi")).toBeInTheDocument();
        expect(screen.getByText("Trần Chủ Bãi")).toBeInTheDocument();

        // Verify Activity Timeline (Recent Decisions)
        expect(screen.getByText("Bãi Bitexco Financial")).toBeInTheDocument();
        expect(screen.getByText("Bãi Thảo Điền")).toBeInTheDocument();
        expect(screen.getByText(/Thiếu hồ sơ PCCC/)).toBeInTheDocument();
    });

    it("paginates recent decisions by 2 items per page with 1 2 3 pager buttons", async () => {
        vi.spyOn(api, "getAdminParkingLots").mockResolvedValue([
            { id: "1", name: "Bãi Quyết Định 1", address: "Q1", type: "Outdoor", ownerUserId: "u1", status: "Approved", rejectionReason: null, reviewedAtUtc: "2026-11-05T10:00:00Z", latitude: 10.77, longitude: 106.7 },
            { id: "2", name: "Bãi Quyết Định 2", address: "Q2", type: "Outdoor", ownerUserId: "u2", status: "Approved", rejectionReason: null, reviewedAtUtc: "2026-11-04T10:00:00Z", latitude: 10.77, longitude: 106.7 },
            { id: "3", name: "Bãi Quyết Định 3", address: "Q3", type: "Outdoor", ownerUserId: "u3", status: "Approved", rejectionReason: null, reviewedAtUtc: "2026-11-03T10:00:00Z", latitude: 10.77, longitude: 106.7 },
            { id: "4", name: "Bãi Quyết Định 4", address: "Q4", type: "Outdoor", ownerUserId: "u4", status: "Approved", rejectionReason: null, reviewedAtUtc: "2026-11-02T10:00:00Z", latitude: 10.77, longitude: 106.7 },
        ]);

        await refreshAdminLots();

        render(
            <LanguageProvider>
                <AdminDashboardPage />
            </LanguageProvider>,
        );

        // Page 1 should show items 1 and 2
        expect(screen.getByText("Bãi Quyết Định 1")).toBeInTheDocument();
        expect(screen.getByText("Bãi Quyết Định 2")).toBeInTheDocument();
        expect(screen.queryByText("Bãi Quyết Định 3")).not.toBeInTheDocument();
        expect(screen.queryByText("Bãi Quyết Định 4")).not.toBeInTheDocument();

        // Pager buttons 1 and 2 should exist
        const page1Btn = screen.getByRole("button", { name: "1" });
        const page2Btn = screen.getByRole("button", { name: "2" });
        expect(page1Btn).toHaveClass("is-active");
        expect(page2Btn).toBeInTheDocument();

        // Click page 2 button
        fireEvent.click(page2Btn);

        // Now page 2 items are visible, page 1 items are not
        expect(screen.getByText("Bãi Quyết Định 3")).toBeInTheDocument();
        expect(screen.getByText("Bãi Quyết Định 4")).toBeInTheDocument();
        expect(screen.queryByText("Bãi Quyết Định 1")).not.toBeInTheDocument();
        expect(page2Btn).toHaveClass("is-active");
    });

    it("renders honest empty state for Activity Trends when backend items do not contain historical timestamps", async () => {
        vi.spyOn(api, "getAdminParkingLots").mockResolvedValue([
            {
                id: "10000000-0000-0000-0000-000000000010",
                name: "Bãi Không Timestamp",
                address: "10 Nguyễn Huệ, Quận 1",
                type: "Outdoor",
                ownerUserId: "20000000-0000-0000-0000-000000000010",
                status: "Approved",
                rejectionReason: null,
                latitude: 10.77,
                longitude: 106.7,
            },
        ]);

        await refreshAdminLots();

        render(
            <LanguageProvider>
                <AdminDashboardPage />
            </LanguageProvider>,
        );

        expect(
            screen.getByText("Chưa có dữ liệu lịch sử từ hệ thống"),
        ).toBeInTheDocument();
        expect(
            screen.getByRole("img", {
                name: /Biểu đồ tròn tỷ lệ trạng thái bãi đỗ xe/i,
            }),
        ).toBeInTheDocument();
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
