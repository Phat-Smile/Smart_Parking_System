import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import {
    translations,
    type SupportedLanguage,
} from "@/shared/i18n/translations";
import { emitLiveToast } from "@/shared/ui/ToastNotificationHub";

const {
    mockState,
    mockOpenParkingSession,
    mockConfirmSlotParked,
    mockRecordGateReading,
    mockConfirmGateReading,
} = vi.hoisted(() => ({
    mockState: { language: "vi" as SupportedLanguage },
    mockOpenParkingSession: vi.fn(),
    mockConfirmSlotParked: vi.fn(),
    mockRecordGateReading: vi.fn(),
    mockConfirmGateReading: vi.fn(),
}));

vi.mock("@/shared/i18n/LanguageContext", () => ({
    useLanguage: () => ({
        language: mockState.language,
        t: (key: keyof (typeof translations)["vi"]) =>
            translations[mockState.language][key] ?? key,
    }),
}));

vi.mock("./RealCameraFeed", () => ({
    RealCameraFeed: (props: {
        onPlateDetected?: (
            plate: string,
            type: "Car",
            confidence: number,
        ) => void;
    }) => (
        <>
            <button
                type="button"
                data-testid="real-camera-feed"
                onClick={() =>
                    props.onPlateDetected?.("51A-123.4S", "Car", 0.85)
                }
            >
                Scan
            </button>
            <button
                type="button"
                data-testid="different-ocr-frame"
                onClick={() =>
                    props.onPlateDetected?.("51A-999.99", "Car", 0.9)
                }
            >
                Scan again
            </button>
        </>
    ),
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
        recordGateReading: mockRecordGateReading,
        confirmGateReading: mockConfirmGateReading,
    };
});

import { StaffEntryPage, calculateLateArrivalMinutes } from "./StaffEntryPage";

describe("StaffEntryPage", () => {
    beforeEach(() => {
        vi.clearAllMocks();
        mockState.language = "vi";
        mockRecordGateReading.mockReset();
        mockConfirmGateReading.mockReset();
        mockOpenParkingSession.mockReset();
    });

    function configureArrival() {
        mockRecordGateReading.mockResolvedValue({
            incidentId: "reading-1",
            lotId: "lot-1",
            decision: "PendingStaff",
        });
        mockConfirmGateReading.mockResolvedValue({
            incidentId: "reading-1",
            lotId: "lot-1",
            decision: "StaffConfirmed",
            plateNumber: "51A-123.45",
            vehicleType: "Car",
        });
        mockOpenParkingSession.mockResolvedValue({
            sessionId: "ses-1",
            lotId: "lot-1",
            plateNumber: "51A-123.45",
            vehicleType: "Car",
            customerType: "Guest",
            status: "Active",
            entryTime: "2026-11-19T04:17:00Z",
            isAccountLinked: false,
            reservationId: null,
            qrToken: "QR-1",
            availableMotorbikeSlots: 30,
        });
    }

    function scanAndCorrect() {
        fireEvent.click(screen.getByTestId("real-camera-feed"));
        fireEvent.change(document.getElementById("staff-edit-plate")!, {
            target: { value: "51A-123.45" },
        });
    }

    function submitEntry() {
        fireEvent.click(
            screen.getByRole("button", { name: /Tạo phiên & Cấp vé QR/i }),
        );
    }

    it("records at detection, deduplicates repeated scans and confirms corrected plate before opening", async () => {
        configureArrival();
        render(<StaffEntryPage />);
        scanAndCorrect();
        expect(mockRecordGateReading).toHaveBeenCalledWith({
            lotId: "lot-1",
            plateNumber: "51A-123.4S",
            vehicleType: "Car",
            confidence: 0.85,
        });
        fireEvent.click(screen.getByTestId("real-camera-feed"));
        fireEvent.click(screen.getByTestId("different-ocr-frame"));
        expect(mockRecordGateReading).toHaveBeenCalledTimes(1);
        expect(mockConfirmGateReading).not.toHaveBeenCalled();
        expect(mockOpenParkingSession).not.toHaveBeenCalled();
        submitEntry();
        await waitFor(() =>
            expect(mockOpenParkingSession).toHaveBeenCalledWith({
                lotId: "lot-1",
                plateNumber: "51A-123.45",
                vehicleType: "Car",
                gateReadingIncidentId: "reading-1",
            }),
        );
        expect(mockConfirmGateReading).toHaveBeenCalledWith(
            "reading-1",
            "51A-123.45",
            "Car",
        );
        expect(mockConfirmGateReading.mock.invocationCallOrder[0]).toBeLessThan(
            mockOpenParkingSession.mock.invocationCallOrder[0]!,
        );
    });

    it("does not silently open without evidence if recording fails", async () => {
        configureArrival();
        mockRecordGateReading.mockRejectedValueOnce(
            new Error("Reading failed"),
        );
        render(<StaffEntryPage />);
        scanAndCorrect();
        submitEntry();
        await waitFor(() =>
            expect(
                screen.getByRole("button", { name: /Tạo phiên & Cấp vé QR/i }),
            ).not.toBeDisabled(),
        );
        expect(mockConfirmGateReading).not.toHaveBeenCalled();
        expect(mockOpenParkingSession).not.toHaveBeenCalled();
    });

    it("does not open if staff confirmation fails", async () => {
        configureArrival();
        mockConfirmGateReading.mockRejectedValueOnce(
            new Error("Confirmation failed"),
        );
        render(<StaffEntryPage />);
        scanAndCorrect();
        submitEntry();
        await waitFor(() =>
            expect(mockConfirmGateReading).toHaveBeenCalledTimes(1),
        );
        await waitFor(() =>
            expect(
                screen.getByRole("button", { name: /Tạo phiên & Cấp vé QR/i }),
            ).not.toBeDisabled(),
        );
        expect(mockOpenParkingSession).not.toHaveBeenCalled();
    });

    it("reuses confirmed incident after failed open, without sending a second confirmation", async () => {
        configureArrival();
        mockOpenParkingSession.mockRejectedValueOnce(new Error("Open failed"));
        render(<StaffEntryPage />);
        scanAndCorrect();
        submitEntry();
        await waitFor(() =>
            expect(mockOpenParkingSession).toHaveBeenCalledTimes(1),
        );
        await waitFor(() =>
            expect(
                screen.getByRole("button", { name: /Tạo phiên & Cấp vé QR/i }),
            ).not.toBeDisabled(),
        );
        submitEntry();
        await waitFor(() =>
            expect(mockOpenParkingSession).toHaveBeenCalledTimes(2),
        );
        expect(mockConfirmGateReading).toHaveBeenCalledTimes(1);
        expect(
            mockOpenParkingSession.mock.calls[1]?.[0].gateReadingIncidentId,
        ).toBe("reading-1");
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

    describe("calculateLateArrivalMinutes", () => {
        it("returns null when either entry time or scheduled arrival time is missing", () => {
            expect(calculateLateArrivalMinutes(null, null)).toBeNull();
            expect(
                calculateLateArrivalMinutes("2026-11-20T10:15:00Z", null),
            ).toBeNull();
            expect(
                calculateLateArrivalMinutes(null, "2026-11-20T10:00:00Z"),
            ).toBeNull();
        });

        it("returns null for invalid date strings", () => {
            expect(
                calculateLateArrivalMinutes(
                    "not-a-date",
                    "2026-11-20T10:00:00Z",
                ),
            ).toBeNull();
            expect(
                calculateLateArrivalMinutes("2026-11-20T10:00:00Z", "invalid"),
            ).toBeNull();
        });

        it("returns null when vehicle arrives early or exactly on time", () => {
            // Early by 10 minutes
            expect(
                calculateLateArrivalMinutes(
                    "2026-11-20T09:50:00Z",
                    "2026-11-20T10:00:00Z",
                ),
            ).toBeNull();
            // Exactly on time
            expect(
                calculateLateArrivalMinutes(
                    "2026-11-20T10:00:00Z",
                    "2026-11-20T10:00:00Z",
                ),
            ).toBeNull();
        });

        it("returns positive minutes when vehicle arrives after scheduled arrival time", () => {
            expect(
                calculateLateArrivalMinutes(
                    "2026-11-20T10:07:00Z",
                    "2026-11-20T10:00:00Z",
                ),
            ).toBe(7);
            expect(
                calculateLateArrivalMinutes(
                    "2026-11-20T10:14:45Z",
                    "2026-11-20T10:00:00Z",
                ),
            ).toBe(14);
        });
    });

    it("does not render manual delay input when switched to Booking mode", () => {
        render(<StaffEntryPage />);

        const categorySelect = document.getElementById(
            "staff-cust-category",
        ) as HTMLSelectElement;
        fireEvent.change(categorySelect, { target: { value: "Booking" } });

        expect(
            document.getElementById("staff-booking-delay"),
        ).not.toBeInTheDocument();
        expect(screen.queryByText(/Độ trễ giờ hẹn/i)).not.toBeInTheDocument();
        expect(screen.getByText(/Khách đặt chỗ trước/i)).toBeInTheDocument();
    });

    it("derives and displays late arrival pill and notice when session response has scheduledArrivalTimeUtc", async () => {
        mockOpenParkingSession.mockResolvedValue({
            sessionId: "ses-booking-late",
            lotId: "lot-1",
            plateNumber: "51F-782.91",
            vehicleType: "Car",
            customerType: "Booking",
            status: "Active",
            entryTime: "2026-11-20T10:12:00Z",
            scheduledArrivalTimeUtc: "2026-11-20T10:00:00Z",
            isAccountLinked: true,
            reservationId: "res-98765432-abcd",
            qrToken: "QR-BOOK-123",
            availableMotorbikeSlots: 30,
            slotCode: "B-02",
        });

        render(<StaffEntryPage />);
        const categorySelect = document.getElementById(
            "staff-cust-category",
        ) as HTMLSelectElement;
        fireEvent.change(categorySelect, { target: { value: "Booking" } });

        fireEvent.click(
            screen.getByRole("button", {
                name: /Xác thực & Mở rào chắn/i,
            }),
        );

        await waitFor(() =>
            expect(screen.getAllByText("Trễ 12 phút")).toHaveLength(2),
        );
        expect(
            screen.getByText(/Xe đặt trước tới muộn trong vòng 15 phút:/i),
        ).toBeInTheDocument();
    });

    it("does not display late warning when session has no scheduledArrivalTimeUtc or is on time", async () => {
        mockOpenParkingSession.mockResolvedValue({
            sessionId: "ses-booking-ontime",
            lotId: "lot-1",
            plateNumber: "51F-782.91",
            vehicleType: "Car",
            customerType: "Booking",
            status: "Active",
            entryTime: "2026-11-20T10:00:00Z",
            scheduledArrivalTimeUtc: "2026-11-20T10:00:00Z",
            isAccountLinked: true,
            reservationId: "res-ontime-1234",
            qrToken: "QR-BOOK-456",
            availableMotorbikeSlots: 30,
            slotCode: "B-03",
        });

        render(<StaffEntryPage />);
        const categorySelect = document.getElementById(
            "staff-cust-category",
        ) as HTMLSelectElement;
        fireEvent.change(categorySelect, { target: { value: "Booking" } });

        fireEvent.click(
            screen.getByRole("button", {
                name: /Xác thực & Mở rào chắn/i,
            }),
        );

        await waitFor(() =>
            expect(screen.getByText(/Khách đặt trước/i)).toBeInTheDocument(),
        );
        expect(screen.queryByText(/Trễ.*phút/i)).not.toBeInTheDocument();
        expect(
            screen.queryByText(/Xe đặt trước tới muộn/i),
        ).not.toBeInTheDocument();
    });
});
