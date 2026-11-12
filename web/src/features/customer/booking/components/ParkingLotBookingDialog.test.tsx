import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { ParkingLotBookingDialog } from "./ParkingLotBookingDialog";
import * as bookingApi from "../bookingApi";
import * as walletApi from "../../wallet/walletApi";
import type { ParkingLotDetailView } from "../../ParkingLotDetailExperience";
import type { RegisteredVehicle } from "../../myVehiclesApi";

afterEach(() => {
    vi.restoreAllMocks();
});

const mockLot: ParkingLotDetailView = {
    id: "10000000-0000-0000-0000-000000000001",
    name: "ParkWise Central Saigon",
    address: "72 Lê Thánh Tôn, Bến Nghé, Quận 1",
    type: "Indoor",
    isPreview: false,
    rating: 4.8,
    reviewCount: 120,
    hourlyPrice: 30000,
    spaces: 25,
    photos: ["/images/lot-1.jpg"],
    image: "/images/lot-1.jpg",
    amenities: ["Camera giám sát", "Mái che"],
    compatible: ["car", "ev"],
};

const mockVehicle: RegisteredVehicle = {
    vehicleId: "veh-car-01",
    licensePlate: "51H-999.88",
    vehicleType: "Car",
    nickName: "Xe đi làm",
    createdAtUtc: "2026-10-30T10:00:00Z",
    isDefault: true,
};

describe("ParkingLotBookingDialog", () => {
    it("renders review step with lot, vehicle, time and deposit amount", () => {
        vi.spyOn(walletApi, "getMyWallet").mockResolvedValue({
            walletId: "w-01",
            balance: 100000,
            currency: "VND",
            isDemo: false,
            demoNotice: "",
            limits: {
                minTopUp: 10000,
                maxTopUp: 5000000,
                maxBalance: 10000000,
            },
            gatewayMode: "Sandbox",
            pendingTopUps: [],
        });

        render(
            <ParkingLotBookingDialog
                isOpen={true}
                onClose={vi.fn()}
                lot={mockLot}
                vehicle={mockVehicle}
                date="2026-10-31"
                time="14:30"
                duration={2}
                selectedSpot="A-01"
            />,
        );

        expect(screen.getByText("Review booking details")).toBeInTheDocument();
        expect(screen.getByText("ParkWise Central Saigon")).toBeInTheDocument();
        expect(screen.getByText("51H-999.88")).toBeInTheDocument();
        expect(
            screen.getByText("Calculated after holding"),
        ).toBeInTheDocument();
        expect(screen.getByText("A-01")).toBeInTheDocument();
        expect(
            screen.getByRole("button", {
                name: "Confirm and hold spot",
            }),
        ).toBeInTheDocument();
    });

    it("creates hold, displays countdown timer, and confirms reservation with wallet", async () => {
        vi.spyOn(walletApi, "getMyWallet").mockResolvedValue({
            walletId: "w-01",
            balance: 150000,
            currency: "VND",
            isDemo: false,
            demoNotice: "",
            limits: {
                minTopUp: 10000,
                maxTopUp: 5000000,
                maxBalance: 10000000,
            },
            gatewayMode: "Sandbox",
            pendingTopUps: [],
        });

        const holdSpy = vi
            .spyOn(bookingApi, "createReservationHold")
            .mockResolvedValue({
                reservationId: "res-hold-001",
                lotId: mockLot.id,
                vehicleId: mockVehicle.vehicleId,
                plateNumber: mockVehicle.licensePlate,
                vehicleGroup: "Car",
                reservationType: "Scheduled",
                status: "TemporaryHold",
                appointmentTime: "2026-10-31T14:30:00.000Z",
                durationMinutes: 120,
                tempHoldExpiresAt: new Date(
                    Date.now() + 15 * 60 * 1000,
                ).toISOString(),
                windowStartAt: "2026-10-31T14:15:00.000Z",
                windowEndAt: "2026-10-31T16:30:00.000Z",
                prepaidAmount: 50000,
                priceSnapshot: null,
                createdAt: "2026-10-31T14:00:00.000Z",
            });

        const confirmSpy = vi
            .spyOn(bookingApi, "confirmReservation")
            .mockResolvedValue({
                reservationId: "res-hold-001",
                status: "Confirmed",
                confirmedAt: "2026-10-31T14:02:00.000Z",
                qrCodeToken: "RES-QR-HOLD001",
                prepaidAmount: 50000,
            });

        render(
            <ParkingLotBookingDialog
                isOpen={true}
                onClose={vi.fn()}
                lot={mockLot}
                vehicle={mockVehicle}
                date="2026-10-31"
                time="14:30"
                duration={2}
                selectedSpot={null}
            />,
        );

        // Bấm Giữ chỗ
        const holdBtn = screen.getByRole("button", {
            name: "Confirm and hold spot",
        });
        fireEvent.click(holdBtn);

        await waitFor(() => {
            expect(holdSpy).toHaveBeenCalledWith(
                expect.objectContaining({
                    lotId: mockLot.id,
                    plateNumber: mockVehicle.licensePlate,
                }),
            );
        });

        // Đã vào bước giữ chỗ với đồng hồ 15 phút
        expect(screen.getByText("Pay booking deposit")).toBeInTheDocument();
        expect(screen.getByRole("timer")).toBeInTheDocument();
        expect(screen.getByText("150.000 đ")).toBeInTheDocument();

        // Bấm trừ cọc ví
        const confirmBtn = screen.getByRole("button", {
            name: "Pay 50.000 đ from wallet and get pass",
        });
        fireEvent.click(confirmBtn);

        await waitFor(() => {
            expect(confirmSpy).toHaveBeenCalledWith(
                "res-hold-001",
                expect.objectContaining({
                    paymentMethod: "Wallet",
                    depositAmount: 50000,
                }),
            );
        });

        // Vé đặt chỗ BookingPassModal xuất hiện với mã QR
        expect(screen.getByText("Booking Pass")).toBeInTheDocument();
        expect(screen.getByText("BK-RES-HOLD")).toBeInTheDocument();
    });

    it("displays warning and topup link when wallet balance is insufficient", async () => {
        // Số dư ví chỉ có 20.000 đ (thiếu so với 50.000 đ cọc)
        vi.spyOn(walletApi, "getMyWallet").mockResolvedValue({
            walletId: "w-01",
            balance: 20000,
            currency: "VND",
            isDemo: false,
            demoNotice: "",
            limits: {
                minTopUp: 10000,
                maxTopUp: 5000000,
                maxBalance: 10000000,
            },
            gatewayMode: "Sandbox",
            pendingTopUps: [],
        });

        vi.spyOn(bookingApi, "createReservationHold").mockResolvedValue({
            reservationId: "res-hold-002",
            lotId: mockLot.id,
            vehicleId: mockVehicle.vehicleId,
            plateNumber: mockVehicle.licensePlate,
            vehicleGroup: "Car",
            reservationType: "Scheduled",
            status: "TemporaryHold",
            appointmentTime: "2026-10-31T14:30:00.000Z",
            durationMinutes: 120,
            tempHoldExpiresAt: new Date(
                Date.now() + 15 * 60 * 1000,
            ).toISOString(),
            windowStartAt: "2026-10-31T14:15:00.000Z",
            windowEndAt: "2026-10-31T16:30:00.000Z",
            prepaidAmount: 50000,
            priceSnapshot: null,
            createdAt: "2026-10-31T14:00:00.000Z",
        });

        render(
            <ParkingLotBookingDialog
                isOpen={true}
                onClose={vi.fn()}
                lot={mockLot}
                vehicle={mockVehicle}
                date="2026-10-31"
                time="14:30"
                duration={2}
                selectedSpot={null}
            />,
        );

        fireEvent.click(
            screen.getByRole("button", {
                name: "Confirm and hold spot",
            }),
        );

        await waitFor(() => {
            expect(
                screen.getByText(/Insufficient wallet balance/),
            ).toBeInTheDocument();
            expect(screen.getByText("Top up wallet →")).toBeInTheDocument();
        });
    });

    it("does not display fixed 50000 deposit or 15 minutes before creating hold", () => {
        vi.spyOn(walletApi, "getMyWallet").mockResolvedValue({
            walletId: "w-01",
            balance: 100000,
            currency: "VND",
            isDemo: false,
            demoNotice: "",
            limits: {
                minTopUp: 10000,
                maxTopUp: 5000000,
                maxBalance: 10000000,
            },
            gatewayMode: "Sandbox",
            pendingTopUps: [],
        });

        render(
            <ParkingLotBookingDialog
                isOpen={true}
                onClose={vi.fn()}
                lot={mockLot}
                vehicle={mockVehicle}
                date="2026-10-31"
                time="14:30"
                duration={2}
                selectedSpot={null}
            />,
        );

        expect(
            screen.getByText("Calculated after holding"),
        ).toBeInTheDocument();
        expect(screen.queryByText("50.000 đ")).not.toBeInTheDocument();
        expect(
            screen.queryByText(/15 minutes|15 phút/i),
        ).not.toBeInTheDocument();
    });

    it("uses prepaidAmount and tempHoldExpiresAt from hold response instead of 50000 or 15 minutes", async () => {
        vi.spyOn(walletApi, "getMyWallet").mockResolvedValue({
            walletId: "w-01",
            balance: 150000,
            currency: "VND",
            isDemo: false,
            demoNotice: "",
            limits: {
                minTopUp: 10000,
                maxTopUp: 5000000,
                maxBalance: 10000000,
            },
            gatewayMode: "Sandbox",
            pendingTopUps: [],
        });

        vi.spyOn(bookingApi, "createReservationHold").mockResolvedValue({
            reservationId: "res-hold-060",
            lotId: mockLot.id,
            vehicleId: mockVehicle.vehicleId,
            plateNumber: mockVehicle.licensePlate,
            vehicleGroup: "Car",
            reservationType: "Scheduled",
            status: "TemporaryHold",
            appointmentTime: "2026-10-31T14:30:00.000Z",
            durationMinutes: 120,
            tempHoldExpiresAt: new Date(
                Date.now() + 4 * 60 * 1000,
            ).toISOString(),
            windowStartAt: "2026-10-31T14:15:00.000Z",
            windowEndAt: "2026-10-31T16:30:00.000Z",
            prepaidAmount: 60000,
            priceSnapshot: null,
            createdAt: "2026-10-31T14:00:00.000Z",
        });

        const confirmSpy = vi
            .spyOn(bookingApi, "confirmReservation")
            .mockResolvedValue({
                reservationId: "res-hold-060",
                status: "Confirmed",
                confirmedAt: "2026-10-31T14:02:00.000Z",
                qrCodeToken: "RES-QR-HOLD060",
                prepaidAmount: 60000,
            });

        render(
            <ParkingLotBookingDialog
                isOpen={true}
                onClose={vi.fn()}
                lot={mockLot}
                vehicle={mockVehicle}
                date="2026-10-31"
                time="14:30"
                duration={2}
                selectedSpot={null}
            />,
        );

        const holdBtn = screen
            .getAllByRole("button")
            .find((b) => /Confirm and hold/i.test(b.textContent || ""));
        expect(holdBtn).toBeDefined();
        fireEvent.click(holdBtn!);

        await waitFor(() => {
            expect(screen.getByText("Pay booking deposit")).toBeInTheDocument();
        });

        expect(screen.getByText("60.000 đ")).toBeInTheDocument();
        const timer = screen.getByRole("timer");
        expect(timer.textContent).toMatch(/0[34]:\d{2}/);

        const payBtn = screen.getByRole("button", {
            name: "Pay 60.000 đ from wallet and get pass",
        });
        fireEvent.click(payBtn);

        await waitFor(() => {
            expect(confirmSpy).toHaveBeenCalledWith(
                "res-hold-060",
                expect.objectContaining({
                    paymentMethod: "Wallet",
                    depositAmount: 60000,
                }),
            );
        });

        expect(screen.getByText("Booking Pass")).toBeInTheDocument();
        expect(screen.getByText("60.000 đ")).toBeInTheDocument();
    });

    it("checks wallet sufficiency against actual prepaidAmount from hold response (55000 < 60000)", async () => {
        vi.spyOn(walletApi, "getMyWallet").mockResolvedValue({
            walletId: "w-01",
            balance: 55000,
            currency: "VND",
            isDemo: false,
            demoNotice: "",
            limits: {
                minTopUp: 10000,
                maxTopUp: 5000000,
                maxBalance: 10000000,
            },
            gatewayMode: "Sandbox",
            pendingTopUps: [],
        });

        vi.spyOn(bookingApi, "createReservationHold").mockResolvedValue({
            reservationId: "res-hold-061",
            lotId: mockLot.id,
            vehicleId: mockVehicle.vehicleId,
            plateNumber: mockVehicle.licensePlate,
            vehicleGroup: "Car",
            reservationType: "Scheduled",
            status: "TemporaryHold",
            appointmentTime: "2026-10-31T14:30:00.000Z",
            durationMinutes: 120,
            tempHoldExpiresAt: new Date(
                Date.now() + 4 * 60 * 1000,
            ).toISOString(),
            windowStartAt: "2026-10-31T14:15:00.000Z",
            windowEndAt: "2026-10-31T16:30:00.000Z",
            prepaidAmount: 60000,
            priceSnapshot: null,
            createdAt: "2026-10-31T14:00:00.000Z",
        });

        render(
            <ParkingLotBookingDialog
                isOpen={true}
                onClose={vi.fn()}
                lot={mockLot}
                vehicle={mockVehicle}
                date="2026-10-31"
                time="14:30"
                duration={2}
                selectedSpot={null}
            />,
        );

        const holdBtn = screen
            .getAllByRole("button")
            .find((b) => /Confirm and hold/i.test(b.textContent || ""));
        fireEvent.click(holdBtn!);

        await waitFor(() => {
            expect(
                screen.getByText(/Insufficient wallet balance/),
            ).toBeInTheDocument();
            expect(screen.getByText(/5\.000 VND/)).toBeInTheDocument();
        });
    });

    it("closes Pay booking deposit dialog when BookingPassModal opens so only one modal is visible", async () => {
        vi.spyOn(walletApi, "getMyWallet").mockResolvedValue({
            walletId: "w-01",
            balance: 150000,
            currency: "VND",
            isDemo: false,
            demoNotice: "",
            limits: {
                minTopUp: 10000,
                maxTopUp: 5000000,
                maxBalance: 10000000,
            },
            gatewayMode: "Sandbox",
            pendingTopUps: [],
        });

        vi.spyOn(bookingApi, "createReservationHold").mockResolvedValue({
            reservationId: "res-hold-062",
            lotId: mockLot.id,
            vehicleId: mockVehicle.vehicleId,
            plateNumber: mockVehicle.licensePlate,
            vehicleGroup: "Car",
            reservationType: "Scheduled",
            status: "TemporaryHold",
            appointmentTime: "2026-10-31T14:30:00.000Z",
            durationMinutes: 120,
            tempHoldExpiresAt: new Date(
                Date.now() + 4 * 60 * 1000,
            ).toISOString(),
            windowStartAt: "2026-10-31T14:15:00.000Z",
            windowEndAt: "2026-10-31T16:30:00.000Z",
            prepaidAmount: 60000,
            priceSnapshot: null,
            createdAt: "2026-10-31T14:00:00.000Z",
        });

        vi.spyOn(bookingApi, "confirmReservation").mockResolvedValue({
            reservationId: "res-hold-062",
            status: "Confirmed",
            confirmedAt: "2026-10-31T14:02:00.000Z",
            qrCodeToken: "RES-QR-HOLD062",
            prepaidAmount: 60000,
        });

        render(
            <ParkingLotBookingDialog
                isOpen={true}
                onClose={vi.fn()}
                lot={mockLot}
                vehicle={mockVehicle}
                date="2026-10-31"
                time="14:30"
                duration={2}
                selectedSpot={null}
            />,
        );

        const holdBtn = screen
            .getAllByRole("button")
            .find((b) => /Confirm and hold/i.test(b.textContent || ""));
        fireEvent.click(holdBtn!);

        await waitFor(() => {
            expect(screen.getByText("Pay booking deposit")).toBeInTheDocument();
        });

        const payBtn = screen
            .getAllByRole("button")
            .find((b) =>
                /Pay .* from wallet and get pass/i.test(b.textContent || ""),
            );
        fireEvent.click(payBtn!);

        await waitFor(() => {
            expect(screen.getByText("Booking Pass")).toBeInTheDocument();
        });

        expect(
            screen.queryByText("Pay booking deposit"),
        ).not.toBeInTheDocument();
        expect(screen.getAllByRole("dialog")).toHaveLength(1);
    });
});
