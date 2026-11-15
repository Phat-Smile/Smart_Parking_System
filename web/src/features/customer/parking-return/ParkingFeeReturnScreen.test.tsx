import { act, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ParkingFeeReturnScreen } from "./ParkingFeeReturnScreen";

describe("ParkingFeeReturnScreen", () => {
    afterEach(() => {
        vi.restoreAllMocks();
        vi.unstubAllGlobals();
        vi.useRealTimers();
    });

    it("displays clear security warning when query parameters are missing", async () => {
        render(<ParkingFeeReturnScreen searchParamsOverride="" />);

        expect(
            await screen.findByText("Dữ liệu phản hồi thanh toán không hợp lệ"),
        ).toBeInTheDocument();
        expect(screen.getByRole("alert")).toBeInTheDocument();
    });

    it("displays clear security warning when signature verification fails", async () => {
        const fetchMock = vi.fn().mockResolvedValue({
            ok: false,
            status: 400,
            json: async () => ({
                error: "payment.vnpay_signature_invalid",
                message: "Invalid signature",
            }),
        });
        vi.stubGlobal("fetch", fetchMock);

        render(
            <ParkingFeeReturnScreen searchParamsOverride="vnp_Amount=1000000&vnp_SecureHash=invalid" />,
        );

        expect(
            await screen.findByText("Dữ liệu phản hồi thanh toán không hợp lệ"),
        ).toBeInTheDocument();
        expect(screen.getByRole("alert")).toBeInTheDocument();
        expect(fetchMock).toHaveBeenCalledWith(
            expect.stringContaining(
                "/api/payment/parking-sessions/vnpay-return-verify?",
            ),
        );
    });

    it("shows amber pending status and then green success status upon confirmation", async () => {
        vi.useFakeTimers({ shouldAdvanceTime: true });

        const fetchMock = vi
            .fn()
            // 1. Return verify endpoint
            .mockImplementation((url: string) => {
                if (url.includes("vnpay-return-verify")) {
                    return Promise.resolve({
                        ok: true,
                        json: async () => ({
                            isValid: true,
                            txnRef: "PARK_20261115_TX01",
                        }),
                    });
                }
                if (url.includes("public-status")) {
                    return Promise.resolve({
                        ok: true,
                        json: async () => ({
                            status: "Succeeded",
                        }),
                    });
                }
                return Promise.reject(new Error("Unknown URL"));
            });
        vi.stubGlobal("fetch", fetchMock);

        render(
            <ParkingFeeReturnScreen searchParamsOverride="vnp_Amount=1000000&vnp_TxnRef=PARK_20261115_TX01&vnp_SecureHash=valid" />,
        );

        // Verification happens, then status becomes Succeeded
        expect(
            await screen.findByText(
                "Thanh toán thành công! Cổng kiểm soát sẽ mở cho xe của bạn rời bãi.",
            ),
        ).toBeInTheDocument();
        expect(screen.getByText("PARK_20261115_TX01")).toBeInTheDocument();
    });

    it("shows amber pending status while polling and timeout fallback after 10 polls", async () => {
        vi.useFakeTimers({ shouldAdvanceTime: true });

        const fetchMock = vi.fn().mockImplementation((url: string) => {
            if (url.includes("vnpay-return-verify")) {
                return Promise.resolve({
                    ok: true,
                    json: async () => ({
                        isValid: true,
                        txnRef: "PARK_20261115_TX02",
                    }),
                });
            }
            if (url.includes("public-status")) {
                return Promise.resolve({
                    ok: true,
                    json: async () => ({
                        status: "Pending",
                    }),
                });
            }
            return Promise.reject(new Error("Unknown URL"));
        });
        vi.stubGlobal("fetch", fetchMock);

        render(
            <ParkingFeeReturnScreen searchParamsOverride="vnp_Amount=1000000&vnp_TxnRef=PARK_20261115_TX02&vnp_SecureHash=valid" />,
        );

        // First verification should show amber status
        expect(
            await screen.findByText(
                "Đang xác nhận thanh toán với ngân hàng...",
            ),
        ).toBeInTheDocument();

        // Advance timers for 10 polling intervals of 2500ms
        for (let i = 0; i < 10; i++) {
            await act(async () => {
                await vi.advanceTimersByTimeAsync(2500);
            });
        }

        // After 10 intervals, it should show fallback instructions
        expect(
            await screen.findByText(
                "Giao dịch đang được ngân hàng đối soát. Nếu tài khoản đã bị trừ tiền, vui lòng báo nhân viên trực cổng.",
            ),
        ).toBeInTheDocument();
    });
});
