import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import BookingsPage from "./page";
import * as bookingApi from "../../../features/customer/booking/bookingApi";

afterEach(() => {
    vi.restoreAllMocks();
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
        expect(screen.getByText("Tất cả (3)")).toBeInTheDocument();
        expect(screen.getByText("Đang có hiệu lực (2)")).toBeInTheDocument();
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
        fireEvent.click(screen.getByText("Đang có hiệu lực (2)"));
        expect(screen.getByText("51H-999.88")).toBeInTheDocument();
        expect(screen.getByText("30A-123.45")).toBeInTheDocument();
        expect(screen.queryByText("29A-888.88")).not.toBeInTheDocument();

        // Chuyển sang Tab "Đã hủy / Hết hạn"
        fireEvent.click(screen.getByText("Đã hủy / Hết hạn"));
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
            name: "Hủy đặt chỗ",
        });
        fireEvent.click(cancelButtons[0]);

        // Modal xác nhận xuất hiện
        expect(screen.getByText("Xác nhận hủy đặt chỗ")).toBeInTheDocument();
        expect(screen.getAllByText(/50.000 đ/).length).toBeGreaterThan(0);

        // Bấm xác nhận hủy
        const confirmCancelBtn = screen.getByRole("button", {
            name: "Xác nhận hủy & Hoàn cọc",
        });
        fireEvent.click(confirmCancelBtn);

        await waitFor(() => {
            expect(cancelSpy).toHaveBeenCalledWith(
                "11111111-2222-3333-4444-555555555555",
                expect.objectContaining({ reason: "Thay đổi lịch trình" }),
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

        const viewQrBtn = screen.getByRole("button", { name: /Xem vé \/ QR/ });
        fireEvent.click(viewQrBtn);

        expect(
            screen.getByText("Vé Đặt Chỗ (Booking Pass)"),
        ).toBeInTheDocument();
        expect(
            screen.getAllByText("BK-11111111").length,
        ).toBeGreaterThanOrEqual(2);
    });

    it("displays empty state when no reservations exist", async () => {
        vi.spyOn(bookingApi, "getMyReservations").mockResolvedValue([]);

        render(<BookingsPage />);

        await waitFor(() => {
            expect(
                screen.getByText("Chưa có lượt đặt chỗ nào"),
            ).toBeInTheDocument();
        });
        expect(screen.getByText("0/3")).toBeInTheDocument();
    });
});
