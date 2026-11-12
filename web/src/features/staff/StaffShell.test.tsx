import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const {
    mockGetStaffLots,
    mockGetAssignedLot,
    mockGetStaffProfile,
    mockGetStaffAssignedShift,
    mockListLotInsideSessions,
} = vi.hoisted(() => ({
    mockGetStaffLots: vi.fn(),
    mockGetAssignedLot: vi.fn(),
    mockGetStaffProfile: vi.fn(),
    mockGetStaffAssignedShift: vi.fn(),
    mockListLotInsideSessions: vi.fn(),
}));

vi.mock("next/navigation", () => ({
    usePathname: () => "/staff/entry",
}));

vi.mock("next/image", () => ({
    default: ({
        priority: _priority,
        ...props
    }: React.ImgHTMLAttributes<HTMLImageElement> & { priority?: boolean }) => (
        // eslint-disable-next-line @next/next/no-img-element
        <img {...props} alt={props.alt ?? ""} />
    ),
}));

vi.mock("next/link", () => ({
    default: ({
        href,
        children,
        ...rest
    }: {
        href: string;
        children: React.ReactNode;
    }) => (
        <a href={href} {...rest}>
            {children}
        </a>
    ),
}));

vi.mock("@/shared/i18n/LanguageContext", () => ({
    useLanguage: () => ({
        language: "vi",
        t: (key: string) => key,
    }),
    LanguageSwitcher: () => <div data-testid="language-switcher" />,
}));

vi.mock("./StaffNotifications", () => ({
    StaffNotifications: () => <div data-testid="staff-notifications" />,
}));

vi.mock("./staffApi", async (importOriginal) => {
    const actual = await importOriginal<typeof import("./staffApi")>();
    return {
        ...actual,
        getStaffLots: mockGetStaffLots,
        getAssignedLot: mockGetAssignedLot,
        getStaffProfile: mockGetStaffProfile,
        getStaffAssignedShift: mockGetStaffAssignedShift,
        listLotInsideSessions: mockListLotInsideSessions,
    };
});

import { StaffShell } from "./StaffShell";
import { staffStore } from "./staffData";

describe("StaffShell", () => {
    beforeEach(() => {
        vi.clearAllMocks();
        try {
            sessionStorage.clear();
        } catch {
            // ignore
        }
        staffStore.setAssignedLot(null);
        mockGetStaffProfile.mockResolvedValue({
            id: "staff-1",
            fullName: "Staff One",
            email: "staff@parkwise.local",
            role: "Staff",
        });
        mockGetStaffAssignedShift.mockResolvedValue({
            shift: "morning",
            shiftName: "Ca 1 - Sáng",
            hours: "06:00 - 14:00",
        });
        mockListLotInsideSessions.mockResolvedValue({
            items: [],
            totalCount: 0,
            page: 1,
            pageSize: 100,
        });
    });

    afterEach(() => {
        try {
            sessionStorage.clear();
        } catch {
            // ignore
        }
    });

    it("does not render a lot selector when staff manages only one lot", async () => {
        mockGetStaffLots.mockResolvedValue([
            {
                id: "lot-single",
                name: "Bãi Đơn",
                address: "99 Đường Đơn",
                type: "Outdoor",
            },
        ]);
        mockGetAssignedLot.mockResolvedValue({
            lotId: "lot-single",
            lotName: "Bãi Đơn",
            address: "99 Đường Đơn",
            availableMotorbikeSlots: 10,
            motorbikeCapacity: 20,
            availableCarSlots: 5,
        });

        render(
            <StaffShell>
                <div>Staff body</div>
            </StaffShell>,
        );

        await waitFor(() =>
            expect(screen.getByText("Bãi Đơn")).toBeInTheDocument(),
        );
        expect(screen.queryByRole("combobox")).not.toBeInTheDocument();
    });

    it("renders lot selector when staff manages multiple lots, switches lot, and persists to sessionStorage without reverting to lots[0]", async () => {
        mockGetStaffLots.mockResolvedValue([
            {
                id: "lot-moto",
                name: "Bãi Xe Máy",
                address: "1 Đường A",
                type: "Outdoor",
            },
            {
                id: "lot-car",
                name: "Bãi Ô Tô",
                address: "2 Đường B",
                type: "Indoor",
            },
        ]);

        mockGetAssignedLot.mockImplementation(async (preferredLotId) => {
            if (preferredLotId === "lot-moto") {
                return {
                    lotId: "lot-moto",
                    lotName: "Bãi Xe Máy",
                    address: "1 Đường A",
                    availableMotorbikeSlots: 30,
                    motorbikeCapacity: 40,
                    availableCarSlots: 0,
                };
            }
            return {
                lotId: "lot-car",
                lotName: "Bãi Ô Tô",
                address: "2 Đường B",
                availableMotorbikeSlots: 15,
                motorbikeCapacity: 30,
                availableCarSlots: 12,
            };
        });

        render(
            <StaffShell>
                <div>Staff body</div>
            </StaffShell>,
        );

        const selector = (await screen.findByRole(
            "combobox",
        )) as HTMLSelectElement;
        expect(selector.value).toBe("lot-car");
        expect(mockGetAssignedLot).not.toHaveBeenCalledWith("lot-moto");

        fireEvent.change(selector, { target: { value: "lot-moto" } });

        await waitFor(() => expect(selector.value).toBe("lot-moto"));
        expect(mockGetAssignedLot).toHaveBeenCalledWith("lot-moto");
        expect(sessionStorage.getItem("parkwise_staff_selected_lot_id")).toBe(
            "lot-moto",
        );
    });
});
