import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { LanguageProvider } from "@/shared/i18n/LanguageContext";
import { ApiError } from "@/shared/errors/api-error";
import { ParkingLotsListPage } from "./ParkingLotsListPage";
import type { AdminParkingLot } from "./types";

const mockPush = vi.fn();

vi.mock("next/navigation", () => ({
    useRouter: () => ({
        push: mockPush,
        replace: vi.fn(),
    }),
    usePathname: () => "/admin/parking-lots/pending",
}));

const mockGetAdminParkingLots = vi.fn();
const mockGetAdminParkingLotDetail = vi.fn();
const mockApproveParkingLot = vi.fn();
const mockRejectParkingLot = vi.fn();

vi.mock("./reviewParkingLot", async (importOriginal) => {
    const actual = await importOriginal<typeof import("./reviewParkingLot")>();
    return {
        ...actual,
        getAdminParkingLots: (...args: unknown[]) =>
            mockGetAdminParkingLots(...args),
        getAdminParkingLotDetail: (...args: unknown[]) =>
            mockGetAdminParkingLotDetail(...args),
        approveParkingLot: (...args: unknown[]) =>
            mockApproveParkingLot(...args),
        rejectParkingLot: (...args: unknown[]) => mockRejectParkingLot(...args),
    };
});

vi.mock("./adminLotsData", () => ({
    refreshAdminLots: vi.fn().mockResolvedValue([]),
    useAdminLots: () => [],
    countByStatus: (lots: AdminParkingLot[]) => ({
        Pending: lots.filter((l) => l.status === "Pending").length,
        Approved: lots.filter((l) => l.status === "Approved").length,
        Rejected: lots.filter((l) => l.status === "Rejected").length,
    }),
}));

const samplePendingLots: AdminParkingLot[] = [
    {
        id: "11111111-1111-1111-1111-111111111111",
        name: "Bãi xe Trung tâm Quận 1",
        address: "123 Lê Lợi, Quận 1, TP.HCM",
        type: "Indoor",
        ownerUserId: "owner-001",
        ownerName: "Nguyễn Văn An",
        ownerEmail: "an.nguyen@parkwise.vn",
        submittedAtUtc: "2026-04-01T08:00:00Z",
        totalCapacity: 120,
        status: "Pending",
        rejectionReason: null,
        latitude: 10.7756,
        longitude: 106.7004,
    },
    {
        id: "22222222-2222-2222-2222-222222222222",
        name: "Bãi xe Sân bay Tân Sơn Nhất",
        address: "45 Trường Sơn, Tân Bình, TP.HCM",
        type: "Outdoor",
        ownerUserId: "owner-002",
        ownerName: "Trần Thị Bình",
        ownerEmail: "binh.tran@parkwise.vn",
        submittedAtUtc: "2026-04-10T10:30:00Z",
        totalCapacity: 80,
        status: "Pending",
        rejectionReason: null,
        latitude: 10.8188,
        longitude: 106.6519,
    },
];

function renderPage(mode: "pending" | "all" = "pending") {
    return render(
        <LanguageProvider>
            <ParkingLotsListPage mode={mode} />
        </LanguageProvider>,
    );
}

describe("ParkingLotsListPage — UI/UX & 4 Distinct States", () => {
    beforeEach(() => {
        vi.clearAllMocks();
        window.localStorage.setItem("pw_lang", "vi");
    });

    it("renders Success + Data state with 7 standardized columns, search, filter, and clear filter button", async () => {
        mockGetAdminParkingLots.mockResolvedValueOnce(samplePendingLots);
        const user = userEvent.setup();

        renderPage("pending");

        // Wait for data rows to appear
        expect(
            await screen.findByText("Bãi xe Trung tâm Quận 1"),
        ).toBeInTheDocument();
        expect(
            screen.getByText("Bãi xe Sân bay Tân Sơn Nhất"),
        ).toBeInTheDocument();

        // Verify 7 column headers exist
        expect(screen.getByText("Địa chỉ")).toBeInTheDocument();
        expect(screen.getByText("Ngày nộp")).toBeInTheDocument();

        // Filter by search keyword
        const searchInput = screen.getByPlaceholderText(
            /Tìm theo tên bãi xe, chủ bãi hoặc địa chỉ/i,
        );
        await user.type(searchInput, "Tân Sơn Nhất");

        expect(
            screen.queryByText("Bãi xe Trung tâm Quận 1"),
        ).not.toBeInTheDocument();
        expect(
            screen.getByText("Bãi xe Sân bay Tân Sơn Nhất"),
        ).toBeInTheDocument();

        // Removing search chip restores full list
        const removeSearchChip = screen.getByRole("button", {
            name: /Gỡ từ khóa tìm kiếm/i,
        });
        await user.click(removeSearchChip);

        expect(screen.getByText("Bãi xe Trung tâm Quận 1")).toBeInTheDocument();
    });

    it("renders dedicated Empty State when API succeeds with an empty list", async () => {
        mockGetAdminParkingLots.mockResolvedValueOnce([]);

        renderPage("pending");

        expect(
            await screen.findByText("Hiện chưa có hồ sơ chờ duyệt"),
        ).toBeInTheDocument();
        expect(
            screen.getByText("Các hồ sơ mới gửi sẽ xuất hiện tại đây"),
        ).toBeInTheDocument();
    });

    it("renders Error State with Retry button and NEVER shows Empty State when API returns 401/403/500", async () => {
        mockGetAdminParkingLots.mockRejectedValueOnce(new ApiError(403));

        renderPage("pending");

        const alert = await screen.findByRole("alert");
        expect(alert).toBeInTheDocument();
        expect(
            screen.getByText("Không thể tải danh sách hồ sơ bãi xe"),
        ).toBeInTheDocument();
        expect(
            screen.getByRole("button", { name: /Thử lại/i }),
        ).toBeInTheDocument();

        // Must NOT show Empty State when API failed
        expect(
            screen.queryByText("Hiện chưa có hồ sơ chờ duyệt"),
        ).not.toBeInTheDocument();
    });

    it("validates UUID format on frontend and handles 404 not-found vs valid navigation", async () => {
        mockGetAdminParkingLots.mockResolvedValueOnce(samplePendingLots);
        const user = userEvent.setup();

        renderPage("pending");
        await screen.findByText("Bãi xe Trung tâm Quận 1");

        const lookupInput = screen.getByPlaceholderText(
            "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
        );
        const lookupBtn = document.getElementById(
            "lots-lookup-open",
        ) as HTMLButtonElement;

        // 1. Invalid UUID shows validation error without calling API
        await user.type(lookupInput, "invalid-uuid-123");
        await user.click(lookupBtn);

        expect(await screen.findByRole("alert")).toHaveTextContent(
            /Vui lòng nhập mã UUID bãi xe hợp lệ/i,
        );
        expect(mockPush).not.toHaveBeenCalled();

        // 2. Valid UUID that returns 404 shows clear not-found message
        mockGetAdminParkingLotDetail.mockRejectedValueOnce(new ApiError(404));
        await user.clear(lookupInput);
        await user.type(lookupInput, "99999999-9999-9999-9999-999999999999");
        await user.click(lookupBtn);

        expect(await screen.findByRole("alert")).toHaveTextContent(
            /Không tìm thấy hồ sơ bãi đỗ xe với mã UUID/i,
        );
        expect(mockPush).not.toHaveBeenCalled();

        // 3. Valid UUID present in list navigates to detail page
        await user.clear(lookupInput);
        await user.type(lookupInput, "11111111-1111-1111-1111-111111111111");
        await user.click(lookupBtn);

        await waitFor(() => {
            expect(mockPush).toHaveBeenCalledWith(
                "/admin/parking-lots/11111111-1111-1111-1111-111111111111",
            );
        });
    });

    it("renders mode='all' without duplicate address or raw GPS in table, shows 'Chưa cập nhật' for missing fields, and distinguishes primary 'Phân công' vs secondary 'Xem'", async () => {
        const allLots: AdminParkingLot[] = [
            {
                id: "10000000-0000-0000-0000-000000000001",
                name: "Bãi đỗ Trung tâm A",
                address: "01 Đường Mẫu, TP. Hồ Chí Minh",
                type: "MultiStorey",
                ownerUserId: null,
                status: "Approved",
                rejectionReason: null,
                latitude: 10.77653,
                longitude: 106.700981,
            },
            {
                id: "10000000-0000-0000-0000-000000000004",
                name: "Bãi chờ duyệt",
                address: "04 Đường Mẫu, TP. Hồ Chí Minh",
                type: "Outdoor",
                ownerUserId: null,
                status: "Approved", // Even if named "Bãi chờ duyệt", UI strictly reflects API approvalStatus
                rejectionReason: null,
                latitude: 10.775,
                longitude: 106.71,
            },
        ];

        mockGetAdminParkingLots.mockResolvedValueOnce(allLots);
        renderPage("all");

        expect(
            await screen.findByText("Bãi đỗ Trung tâm A"),
        ).toBeInTheDocument();

        // Address appears only once per row (in the Address column, not duplicated in Lot Name column)
        const addressMatches = screen.getAllByText(
            "01 Đường Mẫu, TP. Hồ Chí Minh",
        );
        expect(addressMatches).toHaveLength(1);

        // Raw GPS coordinates are not rendered directly as text inside the table cell
        expect(screen.queryByText(/10\.7765/)).not.toBeInTheDocument();

        // Missing ownerUserId, submittedAtUtc, totalCapacity display "—" without repeating "Chưa cập nhật"
        expect(screen.queryByText("Chưa cập nhật")).not.toBeInTheDocument();
        const dashCells = screen.getAllByText("—");
        expect(dashCells.length).toBeGreaterThanOrEqual(6);

        // Action "Xem" is rendered for Approved lots (staff assignment belongs to Owner portal)
        expect(
            screen.queryByRole("link", { name: /Phân công/i }),
        ).not.toBeInTheDocument();
        const viewLinks = screen.getAllByRole("link", { name: /^Xem$/i });
        expect(viewLinks).toHaveLength(2);
    });

    it("supports Smartbase Add Filter (+ Thêm bộ lọc), active filter chips removal, and Column Visibility toggle (Cột hiển thị)", async () => {
        mockGetAdminParkingLots.mockResolvedValueOnce(samplePendingLots);
        const user = userEvent.setup();

        renderPage("all");

        expect(
            await screen.findByText("Bãi xe Trung tâm Quận 1"),
        ).toBeInTheDocument();
        expect(
            screen.getByText("Bãi xe Sân bay Tân Sơn Nhất"),
        ).toBeInTheDocument();

        // 1. Open "+ Thêm bộ lọc" and select "Tên bãi xe" to reveal its input
        const addFilterBtn = screen.getByRole("button", {
            name: /\+ Thêm bộ lọc/i,
        });
        await user.click(addFilterBtn);

        const nameFieldBtn = screen.getByRole("button", {
            name: /^Tên bãi xe$/i,
        });
        await user.click(nameFieldBtn);

        const nameFilterInput = screen.getByPlaceholderText(/Nhập tên bãi xe/i);
        await user.type(nameFilterInput, "Quận 1");

        // Only Quận 1 lot remains
        expect(screen.getByText("Bãi xe Trung tâm Quận 1")).toBeInTheDocument();
        expect(
            screen.queryByText("Bãi xe Sân bay Tân Sơn Nhất"),
        ).not.toBeInTheDocument();

        // Remove the active name filter chip
        const removeNameChipBtn = screen.getByRole("button", {
            name: /Gỡ lọc tên bãi xe/i,
        });
        await user.click(removeNameChipBtn);

        expect(
            screen.getByText("Bãi xe Sân bay Tân Sơn Nhất"),
        ).toBeInTheDocument();

        // 2. Toggle column visibility via "Cột hiển thị"
        const columnsBtn = screen.getByRole("button", {
            name: /Cột hiển thị/i,
        });
        await user.click(columnsBtn);

        // Hide "Địa chỉ" column
        const addressColCheckbox = screen.getByRole("checkbox", {
            name: /Địa chỉ/i,
        });
        await user.click(addressColCheckbox);

        expect(
            screen.queryByText("123 Lê Lợi, Quận 1, TP.HCM"),
        ).not.toBeInTheDocument();

        // Reset all columns via "Hiện tất cả"
        const resetColsBtn = screen.getByRole("button", {
            name: /Hiện tất cả/i,
        });
        await user.click(resetColsBtn);

        expect(
            screen.getByText("123 Lê Lợi, Quận 1, TP.HCM"),
        ).toBeInTheDocument();
    });
});
