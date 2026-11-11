import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import BookingsPage from "@/features/customer/booking/CustomerBookingsPage";
import { setCustomerLanguage } from "@/features/customer/i18n/customerLanguage";
import * as parkingLotApi from "@/features/customer/parkingLotApi";
import * as bookingApi from "../../../features/customer/booking/bookingApi";

afterEach(() => {
    vi.restoreAllMocks();
    setCustomerLanguage("en");
});

const mockReservations: bookingApi.ReservationSummaryDto[] = [
    {
        reservationId: "11111111-2222-3333-4444-555555555555",
        lotId: "lot-aaa-001",
        vehicleId: "veh-001",
        plateNumber: "51H-999.88",
        vehicleGroup: "Car",
        reservationType: "Scheduled",
        status: "Confirmed",
        appointmentTime: "2026-10-30T14:30:00Z",
        durationMinutes: 120,
        tempHoldExpiresAt: null,
        windowStartAt: "2026-10-30T14:15:00Z",
        windowEndAt: "2026-10-30T16:30:00Z",
        prepaidAmount: 50000,
        qrCodeToken: "RES-QR-11111111",
        priceSnapshot: null,
        createdAt: "2026-10-30T14:00:00Z",
        confirmedAt: "2026-10-30T14:02:00Z",
    },
    {
        reservationId: "22222222-3333-4444-5555-666666666666",
        lotId: "lot-bbb-002",
        vehicleId: "veh-002",
        plateNumber: "30A-123.45",
        vehicleGroup: "Car",
        reservationType: "Scheduled",
        status: "TemporaryHold",
        appointmentTime: "2026-10-30T16:00:00Z",
        durationMinutes: 60,
        tempHoldExpiresAt: "2026-10-30T16:15:00Z",
        windowStartAt: "2026-10-30T15:45:00Z",
        windowEndAt: "2026-10-30T17:00:00Z",
        prepaidAmount: 50000,
        qrCodeToken: null,
        priceSnapshot: null,
        createdAt: "2026-10-30T15:45:00Z",
        confirmedAt: null,
    },
    {
        reservationId: "33333333-4444-5555-6666-777777777777",
        lotId: "lot-ccc-003",
        vehicleId: "veh-003",
        plateNumber: "29A-888.88",
        vehicleGroup: "Car",
        reservationType: "Scheduled",
        status: "Cancelled",
        appointmentTime: "2026-10-29T10:00:00Z",
        durationMinutes: 120,
        tempHoldExpiresAt: null,
        windowStartAt: "2026-10-29T09:45:00Z",
        windowEndAt: "2026-10-29T12:00:00Z",
        prepaidAmount: 50000,
        qrCodeToken: null,
        priceSnapshot: null,
        createdAt: "2026-10-29T09:00:00Z",
        confirmedAt: null,
    },
];

describe("BookingsPage", () => {
    it("loads and displays real reservations from booking API", async () => {
        vi.spyOn(bookingApi, "getMyReservations").mockResolvedValue(
            mockReservations,
        );

        render(<BookingsPage />);

        // Chờ tải xong
        await waitFor(() => {
            expect(screen.getByText("51H-999.88")).toBeInTheDocument();
            expect(screen.getByText("30A-123.45")).toBeInTheDocument();
            expect(screen.getByText("29A-888.88")).toBeInTheDocument();
        });

        // 2 lượt đang có hiệu lực (Confirmed + TemporaryHold)
        expect(screen.getByText("2/3")).toBeInTheDocument();
        expect(screen.getByText("All (3)")).toBeInTheDocument();
        expect(screen.getByText("Active (2)")).toBeInTheDocument();
    });

    it("filters reservations by tab correctly", async () => {
        vi.spyOn(bookingApi, "getMyReservations").mockResolvedValue(
            mockReservations,
        );

        render(<BookingsPage />);

        await waitFor(() => {
            expect(screen.getByText("51H-999.88")).toBeInTheDocument();
        });

        // Chuyển sang Tab "Đang có hiệu lực"
        fireEvent.click(screen.getByText("Active (2)"));
        expect(screen.getByText("51H-999.88")).toBeInTheDocument();
        expect(screen.getByText("30A-123.45")).toBeInTheDocument();
        expect(screen.queryByText("29A-888.88")).not.toBeInTheDocument();

        // Chuyển sang Tab "Đã hủy / Hết hạn"
        fireEvent.click(screen.getByText("Cancelled / Expired"));
        expect(screen.getByText("29A-888.88")).toBeInTheDocument();
        expect(screen.queryByText("51H-999.88")).not.toBeInTheDocument();
    });

    it("allows canceling a reservation and refreshes list", async () => {
        vi.spyOn(bookingApi, "getMyReservations").mockResolvedValue(
            mockReservations,
        );
        const cancelSpy = vi
            .spyOn(bookingApi, "cancelReservation")
            .mockResolvedValue({
                reservationId: "11111111-2222-3333-4444-555555555555",
                status: "Cancelled",
                refundAmount: 50000,
                message: "Hủy đặt chỗ thành công. Tiền cọc đã hoàn về ví.",
            });

        render(<BookingsPage />);

        await waitFor(() => {
            expect(screen.getByText("51H-999.88")).toBeInTheDocument();
        });

        // Bấm nút Hủy đặt chỗ đầu tiên
        const cancelButtons = screen.getAllByRole("button", {
            name: "Cancel booking",
        });
        fireEvent.click(cancelButtons[0]);

        // Modal xác nhận xuất hiện
        expect(screen.getByText("Confirm cancellation")).toBeInTheDocument();
        expect(screen.getAllByText(/50.000 đ/).length).toBeGreaterThan(0);

        // Bấm xác nhận hủy
        const confirmCancelBtn = screen.getByRole("button", {
            name: "Cancel and refund deposit",
        });
        fireEvent.click(confirmCancelBtn);

        await waitFor(() => {
            expect(cancelSpy).toHaveBeenCalledWith(
                "11111111-2222-3333-4444-555555555555",
                expect.objectContaining({ reason: "Change of plans" }),
            );
        });
    });

    it("opens BookingPassModal when clicking Xem vé / QR", async () => {
        vi.spyOn(bookingApi, "getMyReservations").mockResolvedValue(
            mockReservations,
        );

        render(<BookingsPage />);

        await waitFor(() => {
            expect(screen.getByText("51H-999.88")).toBeInTheDocument();
        });

        const viewQrBtn = screen.getByRole("button", {
            name: /View pass \/ QR/,
        });
        fireEvent.click(viewQrBtn);

        expect(screen.getByText("Booking Pass")).toBeInTheDocument();
        expect(
            screen.getAllByText("BK-11111111").length,
        ).toBeGreaterThanOrEqual(2);
    });

    it("displays empty state when no reservations exist", async () => {
        vi.spyOn(bookingApi, "getMyReservations").mockResolvedValue([]);

        render(<BookingsPage />);

        await waitFor(() => {
            expect(screen.getByText("No bookings yet")).toBeInTheDocument();
        });
        expect(screen.getByText("0/3")).toBeInTheDocument();
    });

    it("fetches real lot name and address once per unique lotId, falls back cleanly, and passes details to BookingPassModal", async () => {
        const duplicateLotReservations: bookingApi.ReservationSummaryDto[] = [
            mockReservations[0],
            {
                ...mockReservations[2],
                reservationId: "44444444-5555-6666-7777-888888888888",
                lotId: "lot-aaa-001",
                plateNumber: "51G-444.44",
            },
            mockReservations[1],
        ];
        vi.spyOn(bookingApi, "getMyReservations").mockResolvedValue(
            duplicateLotReservations,
        );
        const lotSpy = vi
            .spyOn(parkingLotApi, "getApprovedParkingLot")
            .mockImplementation(async (lotId: string) => {
                if (lotId === "lot-aaa-001") {
                    return {
                        id: "lot-aaa-001",
                        name: "Bãi Xe Bitexco",
                        address: "2 Hải Triều, Quận 1",
                        latitude: 10.77,
                        longitude: 106.7,
                        type: "Indoor",
                        openingHours: null,
                    };
                }
                throw new Error("Parking lot unavailable");
            });

        render(<BookingsPage />);

        await waitFor(() => {
            expect(screen.getAllByText("Bãi Xe Bitexco")).toHaveLength(2);
        });
        expect(screen.getAllByText("2 Hải Triều, Quận 1")).toHaveLength(2);
        expect(lotSpy).toHaveBeenCalledTimes(2);
        expect(lotSpy).toHaveBeenCalledWith(
            "lot-aaa-001",
            expect.any(AbortSignal),
        );
        expect(lotSpy).toHaveBeenCalledWith(
            "lot-bbb-002",
            expect.any(AbortSignal),
        );
        expect(screen.getByText("Parking lot #lot-bbb-")).toBeInTheDocument();
        expect(screen.getByText("Lot ID: #lot-bbb-")).toBeInTheDocument();
        expect(
            screen.queryByText("ParkWise Smart Lot"),
        ).not.toBeInTheDocument();

        fireEvent.click(
            screen.getByRole("button", { name: /View pass \/ QR/i }),
        );
        expect(
            screen.getAllByText("Bãi Xe Bitexco").length,
        ).toBeGreaterThanOrEqual(3);
        expect(
            screen.getAllByText("2 Hải Triều, Quận 1").length,
        ).toBeGreaterThanOrEqual(3);
    });

    it("formats booking duration by selected language (min vs phút) instead of 120p", async () => {
        vi.spyOn(bookingApi, "getMyReservations").mockResolvedValue([
            mockReservations[0],
        ]);
        vi.spyOn(parkingLotApi, "getApprovedParkingLot").mockResolvedValue({
            id: "lot-aaa-001",
            name: "Bãi Xe Bitexco",
            address: "2 Hải Triều, Quận 1",
            latitude: 10.77,
            longitude: 106.7,
            type: "Indoor",
            openingHours: null,
        });

        setCustomerLanguage("en");
        const { rerender } = render(<BookingsPage />);

        await waitFor(() => {
            expect(screen.getByText(/\(120 min\)/)).toBeInTheDocument();
        });
        expect(screen.queryByText(/\(120p\)/)).not.toBeInTheDocument();

        setCustomerLanguage("vi");
        rerender(<BookingsPage />);

        await waitFor(() => {
            expect(screen.getByText(/\(120 phút\)/)).toBeInTheDocument();
        });
        expect(screen.queryByText(/\(120p\)/)).not.toBeInTheDocument();
    });
});
