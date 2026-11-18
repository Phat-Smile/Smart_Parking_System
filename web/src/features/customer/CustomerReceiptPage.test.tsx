import React from "react";
import { render, screen, waitFor, fireEvent } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { CustomerReceiptPage } from "./CustomerReceiptPage";
import { CustomerDataShell } from "./CustomerDataShell";
import { clearAuthSession } from "@/features/auth/authSession";
import type { ParkingFeeReceipt } from "./receiptApi";

const { replace } = vi.hoisted(() => ({ replace: vi.fn() }));
vi.mock("next/navigation", () => ({
    useRouter: () => ({ replace }),
    usePathname: () => "/customer/receipts/sess-2222",
}));
vi.mock("next/image", () => ({ default: () => null }));

const mockGetParkingFeeReceiptApi = vi.fn();
vi.mock("./receiptApi", async (importOriginal) => {
    const actual = await importOriginal<typeof import("./receiptApi")>();
    return {
        ...actual,
        getParkingFeeReceiptApi: (...args: unknown[]) =>
            mockGetParkingFeeReceiptApi(...args),
    };
});

describe("CustomerReceiptPage & CustomerDataShell", () => {
    const fakeReceipt: ParkingFeeReceipt = {
        receiptId: "rcpt-1111",
        sessionId: "sess-2222",
        paymentId: "pay-3333",
        parkingLotId: "lot-4444",
        parkingLotName: "Bãi đỗ xe Landmark 81",
        licensePlate: "51K-999.88",
        vehicleType: "Car",
        checkInTimeUtc: "2026-11-10T08:00:00Z",
        checkOutTimeUtc: "2026-11-10T10:30:00Z",
        totalDurationHours: 2.5,
        standardFee: 30000,
        surgeMultiplier: 1.0,
        prepaidDepositOffset: 0,
        netPaidAmount: 30000,
        paymentMethod: "Cash",
        paidAtUtc: "2026-11-10T10:31:00Z",
        ledgerTransactionReference: "TX-REF-12345",
    };

    beforeEach(() => {
        clearAuthSession();
        replace.mockClear();
        mockGetParkingFeeReceiptApi.mockReset();
    });

    afterEach(() => {
        clearAuthSession();
    });

    it("renders electronic receipt without authentication for anonymous walk-in guests", async () => {
        mockGetParkingFeeReceiptApi.mockResolvedValue(fakeReceipt);

        render(<CustomerReceiptPage sessionId="sess-2222" />);

        expect(
            screen.getByText(/(Loading electronic receipt|Đang tải hóa đơn điện tử)/i),
        ).toBeInTheDocument();

        await waitFor(() => {
            expect(
                screen.getByRole("heading", {
                    level: 1,
                    name: /(ELECTRONIC PARKING RECEIPT|HÓA ĐƠN ĐIỆN TỬ)/i,
                }),
            ).toBeInTheDocument();
        });

        expect(
            screen.getByText("Bãi đỗ xe Landmark 81"),
        ).toBeInTheDocument();
        expect(screen.getByText(/51K-999.88/i)).toBeInTheDocument();
        expect(screen.getByText("TX-REF-12345")).toBeInTheDocument();
        expect(screen.getByText(/(Cash \/ Counter|Tiền mặt tại bãi)/i)).toBeInTheDocument();
    });

    it("renders error state when receipt cannot be loaded", async () => {
        mockGetParkingFeeReceiptApi.mockRejectedValue(
            new Error("Session not found"),
        );

        render(<CustomerReceiptPage sessionId="invalid-session" />);

        await waitFor(() => {
            expect(
                screen.getByText(
                    /(Unable to load electronic receipt|Không thể tải hóa đơn điện tử)/i,
                ),
            ).toBeInTheDocument();
        });

        expect(
            screen.getByRole("link", { name: /(Back to parking history|Quay lại lịch sử gửi xe)/i }),
        ).toBeInTheDocument();
    });

    it("triggers window.print when Print / Save PDF button is clicked", async () => {
        const printSpy = vi.spyOn(window, "print").mockImplementation(() => {});
        mockGetParkingFeeReceiptApi.mockResolvedValue(fakeReceipt);

        render(<CustomerReceiptPage sessionId="sess-2222" />);

        await waitFor(() => {
            expect(
                screen.getByRole("button", { name: /(Print \/ Save PDF|In \/ Lưu Hóa Đơn PDF)/i }),
            ).toBeInTheDocument();
        });

        fireEvent.click(
            screen.getByRole("button", { name: /(Print \/ Save PDF|In \/ Lưu Hóa Đơn PDF)/i }),
        );
        expect(printSpy).toHaveBeenCalledTimes(1);
        printSpy.mockRestore();
    });

    it("allows unauthenticated access when requireAuth={false} in CustomerDataShell", () => {
        render(
            <CustomerDataShell
                active="history"
                fontClassName="font-test"
                requireAuth={false}
            >
                <div data-testid="guest-receipt-content">Guest Receipt</div>
            </CustomerDataShell>,
        );

        expect(
            screen.getByTestId("guest-receipt-content"),
        ).toBeInTheDocument();
        expect(replace).not.toHaveBeenCalled();
    });

    it("redirects to /login when requireAuth={true} by default in CustomerDataShell", () => {
        render(
            <CustomerDataShell active="history" fontClassName="font-test">
                <div data-testid="protected-content">Protected Content</div>
            </CustomerDataShell>,
        );

        expect(
            screen.queryByTestId("protected-content"),
        ).not.toBeInTheDocument();
        expect(replace).toHaveBeenCalledWith("/login");
    });
});
