import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { LanguageProvider } from "@/shared/i18n/LanguageContext";
import { AdminLotAssignmentsPage } from "./AdminLotAssignmentsPage";
import * as reviewParkingLot from "./reviewParkingLot";
import * as lotAssignmentsApi from "./lotAssignmentsApi";

vi.mock("next/navigation", () => ({
    usePathname: () => "/admin/parking-lots/assignments",
    useRouter: () => ({ push: vi.fn() }),
}));

vi.mock("./reviewParkingLot", () => ({
    getAdminParkingLots: vi.fn(),
}));

vi.mock("./lotAssignmentsApi", async (importOriginal) => {
    const actual = await importOriginal<typeof import("./lotAssignmentsApi")>();
    return {
        ...actual,
        getAvailableStaffAccounts: vi.fn(),
        getLotAssignments: vi.fn(),
        assignLotMember: vi.fn(),
        removeLotMember: vi.fn(),
    };
});

const MOCK_LOTS = [
    {
        id: "10000000-0000-0000-0000-000000000001",
        name: "Bãi xe Trung tâm Quận 1",
        address: "123 Lê Lợi, Quận 1, TP.HCM",
        type: "Indoor",
        ownerUserId: "30000000-0000-0000-0000-000000000001",
        status: "Approved" as const,
        rejectionReason: null,
        latitude: 10.77,
        longitude: 106.69,
    },
    {
        id: "10000000-0000-0000-0000-000000000002",
        name: "Bãi xe Khu Công Nghệ Cao",
        address: "Lô E1, TP. Thủ Đức",
        type: "Outdoor",
        ownerUserId: null,
        status: "Pending" as const,
        rejectionReason: null,
        latitude: 10.85,
        longitude: 106.79,
    },
];

const MOCK_STAFF_ACCOUNTS = [
    {
        accountId: "20000000-0000-0000-0000-000000000001",
        fullName: "Nguyễn Văn Trực",
        email: "truc.nv@parkwise.vn",
        phoneNumber: "0901234567",
        role: "Staff" as const,
    },
    {
        accountId: "20000000-0000-0000-0000-000000000002",
        fullName: "Trần Thị Sẵn Sàng",
        email: "sansang.tt@parkwise.vn",
        phoneNumber: "0909888777",
        role: "Staff" as const,
    },
    {
        accountId: "30000000-0000-0000-0000-000000000001",
        fullName: "Lê Chủ Bãi",
        email: "owner.le@parkwise.vn",
        phoneNumber: "0912345678",
        role: "Owner" as const,
    },
];

const MOCK_LOT_1_ASSIGNMENTS = [
    {
        lotId: "10000000-0000-0000-0000-000000000001",
        accountId: "20000000-0000-0000-0000-000000000001",
        role: "Staff" as const,
        assignedAtUtc: "2026-10-20T08:00:00Z",
        shift: "morning" as const,
        shiftName: "Ca 1 - Sáng",
        hours: "06:00 - 14:00",
    },
];

function renderPage() {
    return render(
        <LanguageProvider>
            <AdminLotAssignmentsPage />
        </LanguageProvider>,
    );
}

describe("AdminLotAssignmentsPage UI/UX redesign", () => {
    beforeEach(() => {
        vi.clearAllMocks();
        vi.stubGlobal(
            "fetch",
            vi.fn().mockResolvedValue(
                new Response(JSON.stringify([]), {
                    status: 200,
                    headers: { "Content-Type": "application/json" },
                }),
            ),
        );

        vi.mocked(reviewParkingLot.getAdminParkingLots).mockResolvedValue(
            MOCK_LOTS,
        );
        vi.mocked(
            lotAssignmentsApi.getAvailableStaffAccounts,
        ).mockResolvedValue(MOCK_STAFF_ACCOUNTS);
        vi.mocked(lotAssignmentsApi.getLotAssignments).mockImplementation(
            async (lotId: string) => {
                if (lotId === MOCK_LOTS[0].id) return MOCK_LOT_1_ASSIGNMENTS;
                return [];
            },
        );
    });

    it("renders KPI statistics, lot list, shift schedule, and enriches assigned staff identity data", async () => {
        renderPage();

        await waitFor(() => {
            expect(
                screen.getAllByText("Bãi xe Trung tâm Quận 1").length,
            ).toBeGreaterThan(0);
            expect(
                screen.getAllByText("Nguyễn Văn Trực").length,
            ).toBeGreaterThan(0);
        });

        // Enriched staff name, email, and phone number in the table
        expect(screen.getByText("truc.nv@parkwise.vn")).toBeInTheDocument();
        expect(screen.getByText("0901234567")).toBeInTheDocument();

        // Owner name resolved on lot card
        expect(screen.getAllByText("Lê Chủ Bãi").length).toBeGreaterThan(0);

        // Shift overview strip displays the 4 shifts
        expect(screen.getAllByText("Ca 1 - Sáng").length).toBeGreaterThan(0);
        expect(screen.getAllByText("Ca 2 - Chiều").length).toBeGreaterThan(0);
        expect(screen.getAllByText("Ca 3 - Đêm").length).toBeGreaterThan(0);
    });

    it("opens the Assign Staff modal, previews selected staff, and submits assignment", async () => {
        const user = userEvent.setup();
        vi.mocked(lotAssignmentsApi.assignLotMember).mockResolvedValue({
            lotId: MOCK_LOTS[0].id,
            accountId: MOCK_STAFF_ACCOUNTS[1].accountId,
            role: "Staff",
            shift: "afternoon",
            shiftName: "Ca 2 - Chiều",
            hours: "14:00 - 22:00",
        });

        renderPage();

        await waitFor(() => {
            expect(
                screen.getAllByText("Bãi xe Trung tâm Quận 1").length,
            ).toBeGreaterThan(0);
        });

        const openModalBtn = screen.getAllByRole("button", {
            name: /\+ Phân công nhân viên/i,
        })[0];
        await user.click(openModalBtn);

        const dialog = screen.getByRole("dialog");
        expect(dialog).toBeInTheDocument();

        // Select available staff member inside modal
        const selects = within(dialog).getAllByRole("combobox");
        await user.selectOptions(selects[1], MOCK_STAFF_ACCOUNTS[1].accountId);

        // Preview card displays selected staff email
        expect(
            within(dialog).getByText("sansang.tt@parkwise.vn"),
        ).toBeInTheDocument();

        // Select afternoon shift
        await user.selectOptions(selects[3], "afternoon");

        const confirmBtn = within(dialog).getByRole("button", {
            name: /Xác nhận phân công/i,
        });
        await user.click(confirmBtn);

        await waitFor(() => {
            expect(lotAssignmentsApi.assignLotMember).toHaveBeenCalledWith(
                MOCK_LOTS[0].id,
                MOCK_STAFF_ACCOUNTS[1].accountId,
                "Staff",
                "afternoon",
            );
        });
    });

    it("opens confirmation dialog before removing an assigned staff member", async () => {
        const user = userEvent.setup();
        vi.mocked(lotAssignmentsApi.removeLotMember).mockResolvedValue();

        renderPage();

        await waitFor(() => {
            expect(screen.getByText("truc.nv@parkwise.vn")).toBeInTheDocument();
        });

        const removeBtn = screen.getByRole("button", {
            name: /Gỡ khỏi bãi/i,
        });
        await user.click(removeBtn);

        const dialog = screen.getByRole("dialog");
        expect(
            within(dialog).getByText(/Xác nhận gỡ nhân sự khỏi bãi/i),
        ).toBeInTheDocument();

        const confirmRemoveBtn = within(dialog).getByRole("button", {
            name: /Xác nhận gỡ khỏi bãi/i,
        });
        await user.click(confirmRemoveBtn);

        await waitFor(() => {
            expect(lotAssignmentsApi.removeLotMember).toHaveBeenCalledWith(
                MOCK_LOTS[0].id,
                MOCK_LOT_1_ASSIGNMENTS[0].accountId,
            );
        });
    });
});
