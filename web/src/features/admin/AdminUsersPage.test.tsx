import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { LanguageProvider } from "@/shared/i18n/LanguageContext";
import { AdminUsersPage } from "./AdminUsersPage";
import * as adminAccounts from "./adminAccounts";

const MOCK_SESSION = {
    accessToken: "mock-token",
    account: {
        id: "00000000-0000-0000-0000-000000000001",
        email: "admin@parkwise.vn",
        role: "SystemAdmin",
    },
};

vi.mock("@/features/auth/authSession", () => ({
    subscribeAuthSession: () => () => {},
    getAuthSessionSnapshot: () => MOCK_SESSION,
}));

vi.mock("./adminAccounts", async (importOriginal) => {
    const actual = await importOriginal<typeof import("./adminAccounts")>();
    return {
        ...actual,
        searchAccounts: vi.fn(),
        suspendAccount: vi.fn(),
        activateAccount: vi.fn(),
        unlockAccount: vi.fn(),
        changeAccountRole: vi.fn(),
    };
});

const MOCK_ACCOUNTS: adminAccounts.AccountSummary[] = [
    {
        id: "00000000-0000-0000-0000-000000000001",
        email: "admin@parkwise.vn",
        fullName: "Quản trị Hệ thống",
        role: "SystemAdmin",
        status: "EmailVerified",
        isLocked: false,
        lockoutEndUtc: null,
        createdAtUtc: "2026-09-01T00:00:00Z",
    },
    {
        id: "00000000-0000-0000-0000-000000000002",
        email: "customer@parkwise.vn",
        fullName: "Nguyễn Khách Hàng",
        role: "Customer",
        status: "EmailVerified",
        isLocked: false,
        lockoutEndUtc: null,
        createdAtUtc: "2026-10-01T00:00:00Z",
    },
    {
        id: "00000000-0000-0000-0000-000000000003",
        email: "owner@parkwise.vn",
        fullName: "Trần Chủ Bãi",
        role: "ParkingLotOwner",
        status: "Suspended",
        isLocked: true,
        lockoutEndUtc: "2026-11-10T10:00:00Z",
        createdAtUtc: "2026-10-05T00:00:00Z",
    },
    {
        id: "00000000-0000-0000-0000-000000000004",
        email: "pending@parkwise.vn",
        fullName: null,
        role: "Customer",
        status: "PendingEmailVerification",
        isLocked: false,
        lockoutEndUtc: null,
        createdAtUtc: "2026-10-12T00:00:00Z",
    },
];

function renderUsersPage() {
    const queryClient = new QueryClient({
        defaultOptions: {
            queries: { retry: false },
        },
    });
    return render(
        <QueryClientProvider client={queryClient}>
            <LanguageProvider>
                <AdminUsersPage />
            </LanguageProvider>
        </QueryClientProvider>,
    );
}

describe("AdminUsersPage UI/UX redesign", () => {
    beforeEach(() => {
        vi.clearAllMocks();
        vi.mocked(adminAccounts.searchAccounts).mockResolvedValue({
            items: MOCK_ACCOUNTS,
            page: 1,
            pageSize: 20,
            totalCount: MOCK_ACCOUNTS.length,
        });
    });

    it("renders accounts, KPI cards, and separates verification status from lockout status", async () => {
        renderUsersPage();

        await waitFor(() => {
            expect(screen.getByText("Nguyễn Khách Hàng")).toBeInTheDocument();
        });

        expect(screen.getByText("Trần Chủ Bãi")).toBeInTheDocument();
        expect(screen.getByText("customer@parkwise.vn")).toBeInTheDocument();

        // Separate status badges: EmailVerified, PendingEmailVerification, Suspended, and Lockout
        expect(screen.getAllByText("Đã xác minh").length).toBeGreaterThan(0);
        expect(
            screen.getAllByText("Chờ xác minh email").length,
        ).toBeGreaterThan(0);
        expect(screen.getAllByText("Đang bị đình chỉ").length).toBeGreaterThan(
            0,
        );
        expect(screen.getByText(/Khóa đăng nhập đến/i)).toBeInTheDocument();
    });

    it("opens confirmation dialog before suspending an account and calls suspendAccount", async () => {
        const user = userEvent.setup();
        vi.mocked(adminAccounts.suspendAccount).mockResolvedValue({
            message: "Suspended",
        });

        renderUsersPage();

        await waitFor(() => {
            expect(screen.getByText("Nguyễn Khách Hàng")).toBeInTheDocument();
        });

        const suspendBtn = screen.getByRole("button", {
            name: /^Khóa$/i,
        });
        await user.click(suspendBtn);

        const dialog = screen.getByRole("dialog");
        expect(
            within(dialog).getByText("Nguyễn Khách Hàng"),
        ).toBeInTheDocument();

        const confirmBtn = within(dialog).getByRole("button", {
            name: /Xác nhận thực hiện/i,
        });
        await user.click(confirmBtn);

        await waitFor(() => {
            expect(adminAccounts.suspendAccount).toHaveBeenCalledWith(
                "00000000-0000-0000-0000-000000000002",
            );
        });
    });

    it("displays roles as read-only badges and does not allow admin to change roles", async () => {
        renderUsersPage();

        await waitFor(() => {
            expect(screen.getByText("Nguyễn Khách Hàng")).toBeInTheDocument();
        });

        // Roles are displayed as badges
        expect(screen.getAllByText("Khách hàng").length).toBeGreaterThan(0);
        expect(screen.getAllByText("Chủ bãi").length).toBeGreaterThan(0);
        expect(screen.getAllByText("Quản trị viên").length).toBeGreaterThan(0);

        // No select dropdown to change user roles
        expect(
            screen.queryByLabelText(/Change role for/i),
        ).not.toBeInTheDocument();
    });
});
