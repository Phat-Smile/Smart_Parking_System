import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import {
    translations,
    type SupportedLanguage,
} from "@/shared/i18n/translations";
import { emitLiveToast } from "@/shared/ui/ToastNotificationHub";

const { mockState, mockOpenParkingSession, mockConfirmSlotParked } = vi.hoisted(() => ({
    mockState: { language: "vi" as SupportedLanguage },
    mockOpenParkingSession: vi.fn(),
    mockConfirmSlotParked: vi.fn(),
}));

vi.mock("@/shared/i18n/LanguageContext", () => ({
    useLanguage: () => ({
        language: mockState.language,
        t: (key: keyof (typeof translations)["vi"]) =>
            translations[mockState.language][key] ?? key,
    }),
}));

vi.mock("./RealCameraFeed", () => ({
    RealCameraFeed: () => <div data-testid="real-camera-feed" />,
}));

vi.mock("./audio", () => ({
    useGateVoice: () => ({
        announceCheckIn: vi.fn(),
        announceWarning: vi.fn(),
    }),
}));

vi.mock("@/shared/ui/ToastNotificationHub", () => ({
    emitLiveToast: vi.fn(),
}));

vi.mock("./staffData", () => ({
    useStaffData: () => ({
        addSession: vi.fn(),
        occupancy: {
            carAvailable: 12,
            motorbikeAvailable: 30,
            totalCapacity: 60,
            occupied: 18,
            activeSessionsCount: 18,
        },
        assignedLot: {
            lotId: "lot-1",
            lotName: "Bãi Test",
            address: "1 Đường Test",
            availableCarSlots: 12,
            availableMotorbikeSlots: 30,
            motorbikeCapacity: 40,
        },
        fetchAssignedLot: vi.fn().mockResolvedValue(null),
    }),
}));

vi.mock("./staffApi", async (importOriginal) => {
    const actual = await importOriginal<typeof import("./staffApi")>();
    return {
        ...actual,
        openParkingSession: mockOpenParkingSession,
        confirmSlotParked: mockConfirmSlotParked,
    };
});

import { StaffEntryPage } from "./StaffEntryPage";

describe("StaffEntryPage", () => {
    beforeEach(() => {
        vi.clearAllMocks();
        mockState.language = "vi";
    });

    it("renders neutral account status instead of hardcoded Nguyễn Văn An in Registered mode (vi & en)", () => {
        const { rerender } = render(<StaffEntryPage />);

        const categorySelect = document.getElementById(
            "staff-cust-category",
        ) as HTMLSelectElement;
        fireEvent.change(categorySelect, { target: { value: "Registered" } });

        expect(screen.queryByText(/Nguyễn Văn An/i)).not.toBeInTheDocument();
        expect(
            screen.getByText(/Đã liên kết tài khoản \(ví khả dụng\)/i),
        ).toBeInTheDocument();

        mockState.language = "en";
        rerender(<StaffEntryPage />);

        expect(screen.queryByText(/Nguyễn Văn An/i)).not.toBeInTheDocument();
        expect(
            screen.getByText(/Linked account \(wallet active\)/i),
        ).toBeInTheDocument();
    });

    it("displays real driver name when session response includes it", async () => {
        mockOpenParkingSession.mockResolvedValue({
            sessionId: "ses-1",
            lotId: "lot-1",
            plateNumber: "51F-782.91",
            vehicleType: "Car",
            customerType: "Registered",
            status: "Active",
            entryTime: "2026-11-11T09:00:00Z",
            isAccountLinked: true,
            reservationId: null,
            qrToken: "QR-123",
            availableMotorbikeSlots: 30,
            driverName: "Trần Thị Bình",
        });

        render(<StaffEntryPage />);

        const categorySelect = document.getElementById(
            "staff-cust-category",
        ) as HTMLSelectElement;
        fireEvent.change(categorySelect, { target: { value: "Registered" } });

        fireEvent.click(
            screen.getByRole("button", {
                name: /Xác thực & Mở rào chắn/i,
            }),
        );

        await waitFor(() =>
            expect(screen.getByText(/Trần Thị Bình/)).toBeInTheDocument(),
        );
        expect(screen.queryByText(/Nguyễn Văn An/i)).not.toBeInTheDocument();
    });

    it("uses English for the check-in toast when English is selected", async () => {
        mockState.language = "en";
        mockOpenParkingSession.mockResolvedValue({
            sessionId: "ses-1",
            lotId: "lot-1",
            plateNumber: "51F-782.91",
            vehicleType: "Car",
            customerType: "Guest",
            status: "Active",
            entryTime: "2026-11-11T09:00:00Z",
            isAccountLinked: false,
            reservationId: null,
            qrToken: "QR-123",
            availableMotorbikeSlots: 30,
        });

        render(<StaffEntryPage />);
        fireEvent.click(
            screen.getByRole("button", {
                name: "Create Session & Issue QR (Allow Entry)",
            }),
        );

        await waitFor(() =>
            expect(emitLiveToast).toHaveBeenCalledWith(
                "Vehicle check-in successful!",
                "License plate 51F-782.91 passed the gate. The parking ticket is ready.",
                "success",
            ),
        );
    });

    it("renders confirm-parked button when vehicle passes gate and handles manual confirmation", async () => {
        mockOpenParkingSession.mockResolvedValue({
            sessionId: "ses-1",
            lotId: "lot-1",
            plateNumber: "51F-782.91",
            vehicleType: "Car",
            customerType: "Guest",
            status: "Active",
            entryTime: "2026-11-11T09:00:00Z",
            isAccountLinked: false,
            reservationId: null,
            qrToken: "QR-123",
            availableMotorbikeSlots: 30,
            slotCode: "A-01",
        });

        render(<StaffEntryPage />);
        fireEvent.click(
            screen.getByRole("button", {
                name: /Tạo phiên & Cấp vé QR/i,
            }),
        );

        const confirmBtn = await screen.findByTestId(
            "confirm-parked-btn",
            {},
            { timeout: 3000 },
        );
        expect(confirmBtn).toBeInTheDocument();
        expect(confirmBtn).toHaveTextContent("Xác nhận đã vào chỗ");
        expect(confirmBtn).not.toBeDisabled();

        fireEvent.click(confirmBtn);

        await waitFor(() => {
            expect(confirmBtn).toBeDisabled();
            expect(confirmBtn).toHaveTextContent("Đã vào chỗ đỗ");
        });

        expect(emitLiveToast).toHaveBeenCalledWith(
            "Xác nhận đã vào chỗ thành công!",
            expect.stringContaining("51F-782.91"),
            "success",
        );
    });

    it("renders confirm-parked button in English when language is set to English", async () => {
        mockState.language = "en";
        mockOpenParkingSession.mockResolvedValue({
            sessionId: "ses-1",
            lotId: "lot-1",
            plateNumber: "51F-782.91",
            vehicleType: "Car",
            customerType: "Guest",
            status: "Active",
            entryTime: "2026-11-11T09:00:00Z",
            isAccountLinked: false,
            reservationId: null,
            qrToken: "QR-123",
            availableMotorbikeSlots: 30,
            slotCode: "A-01",
        });

        render(<StaffEntryPage />);
        fireEvent.click(
            screen.getByRole("button", {
                name: "Create Session & Issue QR (Allow Entry)",
            }),
        );

        const confirmBtn = await screen.findByTestId(
            "confirm-parked-btn",
            {},
            { timeout: 3000 },
        );
        expect(confirmBtn).toBeInTheDocument();
        expect(confirmBtn).toHaveTextContent("Confirm Parked");

        fireEvent.click(confirmBtn);

        await waitFor(() => {
            expect(confirmBtn).toBeDisabled();
            expect(confirmBtn).toHaveTextContent("Parked Confirmed");
        });

        expect(emitLiveToast).toHaveBeenCalledWith(
            "Parked confirmed!",
            expect.stringContaining("51F-782.91"),
            "success",
        );
    });
});
