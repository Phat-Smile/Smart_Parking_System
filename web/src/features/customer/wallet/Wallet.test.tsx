import { act, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "@/shared/errors/api-error";
import * as api from "./walletApi";
import { CustomerWallet } from "./CustomerWallet";
import { VnpayReturnStatus } from "./VnpayReturnStatus";

// Demo 3 steps D03.2–D03.6: balance, top-up through VNPay, the page VNPay returns to, and history,
// all as Payment returns them (docs/api/Payment-Wallet-Contract.md).
const WALLET: api.WalletBalance = {
    walletId: "w1",
    balance: 0,
    currency: "VND",
    isDemo: true,
    demoNotice: "Sandbox",
    limits: { minTopUp: 10000, maxTopUp: 5000000, maxBalance: 10000000 },
    gatewayMode: "Sandbox",
    pendingTopUps: [],
};

const HISTORY: api.WalletTransactionsPage = {
    items: [
        {
            entryId: "e1",
            createdAtUtc: "2026-10-30T02:00:00Z",
            entryType: "TopUp",
            amount: 50000,
            balanceDelta: 50000,
            affectsBalance: true,
            balanceAfter: 50000,
            reason: null,
            referenceType: "VnpayTopup",
            referenceId: "T1",
        },
    ],
    page: 1,
    pageSize: 20,
    totalItems: 1,
    totalPages: 1,
};

afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
    vi.useRealTimers();
});

describe("walletApi", () => {
    it("sends the Idempotency-Key header and reads the DTO", async () => {
        const fetchMock = vi.fn().mockResolvedValue(
            new Response(
                JSON.stringify({
                    topupId: "T1",
                    amount: 50000,
                    status: "Pending",
                    paymentUrl: "https://sandbox.vnpayment.vn/x",
                    isReplay: false,
                }),
                {
                    status: 201,
                    headers: { "Content-Type": "application/json" },
                },
            ),
        );
        vi.stubGlobal("fetch", fetchMock);

        const result = await api.createWalletTopup(50000, "key-1");

        expect(result.paymentUrl).toBe("https://sandbox.vnpayment.vn/x");
        const [url, init] = fetchMock.mock.calls[0];
        expect(String(url)).toContain("/api/payment/wallets/top-up");
        expect(new Headers(init.headers).get("Idempotency-Key")).toBe("key-1");
        expect(JSON.parse(init.body)).toEqual({ amount: 50000 });
    });

    it("explains Payment error codes", () => {
        const err = new ApiError(409, {
            status: 409,
            title: "t",
            errorCode: "payment.idempotency_key_reused",
        } as never);
        expect(api.describeWalletError(err)).toMatch(/đã được dùng/);
        expect(api.describeWalletError(new TypeError("x"))).toMatch(/máy chủ/);
    });
});

describe("CustomerWallet", () => {
    beforeEach(() => {
        vi.spyOn(api, "getWalletTransactions").mockResolvedValue(HISTORY);
    });

    it("shows the balance and the history from Payment", async () => {
        vi.spyOn(api, "getMyWallet").mockResolvedValue({
            ...WALLET,
            balance: 50000,
        });

        render(
            <CustomerWallet
                onRedirect={vi.fn()}
                onOpenStatus={vi.fn()}
                newKey={() => "k"}
            />,
        );

        expect(await screen.findByTestId("wallet-balance")).toHaveTextContent(
            "50.000",
        );
        expect(await screen.findByText("TopUp")).toBeInTheDocument();
    });

    it("a balance that cannot be read is an error, not 0", async () => {
        vi.spyOn(api, "getMyWallet").mockRejectedValue(new ApiError(500));

        render(
            <CustomerWallet
                onRedirect={vi.fn()}
                onOpenStatus={vi.fn()}
                newKey={() => "k"}
            />,
        );

        expect(await screen.findByRole("alert")).toBeInTheDocument();
        expect(screen.queryByTestId("wallet-balance")).not.toBeInTheDocument();
    });

    it("retrying after a network error reuses the same Idempotency-Key, then opens VNPay", async () => {
        const user = userEvent.setup();
        vi.spyOn(api, "getMyWallet").mockResolvedValue(WALLET);
        const create = vi
            .spyOn(api, "createWalletTopup")
            .mockRejectedValueOnce(new TypeError("Failed to fetch"))
            .mockResolvedValueOnce({
                topupId: "T1",
                amount: 50000,
                status: "Pending",
                paymentUrl: "https://sandbox.vnpayment.vn/x",
                createdAtUtc: "2026-10-30T02:00:00Z",
                expiresAtUtc: "2026-10-30T02:15:00Z",
                isReplay: false,
            });
        const onRedirect = vi.fn();
        let n = 0;

        render(
            <CustomerWallet
                onRedirect={onRedirect}
                onOpenStatus={vi.fn()}
                newKey={() => `key-${++n}`}
            />,
        );
        await screen.findByTestId("wallet-balance");
        const button = screen.getByRole("button", {
            name: /Nạp tiền qua VNPay/i,
        });

        await user.click(button);
        expect(await screen.findByRole("alert")).toHaveTextContent(/máy chủ/);
        await user.click(button);

        await waitFor(() =>
            expect(onRedirect).toHaveBeenCalledWith(
                "https://sandbox.vnpayment.vn/x",
            ),
        );
        expect(create).toHaveBeenNthCalledWith(1, 50000, "key-1");
        expect(create).toHaveBeenNthCalledWith(2, 50000, "key-1");
    });

    it("an amount below the wallet limit is refused before calling Payment", async () => {
        const user = userEvent.setup();
        vi.spyOn(api, "getMyWallet").mockResolvedValue(WALLET);
        const create = vi.spyOn(api, "createWalletTopup");

        render(
            <CustomerWallet
                onRedirect={vi.fn()}
                onOpenStatus={vi.fn()}
                newKey={() => "k"}
            />,
        );
        await screen.findByTestId("wallet-balance");
        const input = screen.getByLabelText(/Số tiền nạp/i);
        await user.clear(input);
        await user.type(input, "5000");
        await user.click(
            screen.getByRole("button", { name: /Nạp tiền qua VNPay/i }),
        );

        expect(await screen.findByRole("alert")).toHaveTextContent(/10\.000/);
        expect(create).not.toHaveBeenCalled();
    });
});

describe("wallet replay and history", () => {
    it("a replayed top-up that already succeeded opens its status page instead of an error", async () => {
        const user = userEvent.setup();
        vi.spyOn(api, "getMyWallet").mockResolvedValue(WALLET);
        vi.spyOn(api, "getWalletTransactions").mockResolvedValue(HISTORY);
        vi.spyOn(api, "createWalletTopup").mockResolvedValue({
            topupId: "T1",
            amount: 50000,
            status: "Succeeded",
            paymentUrl: null,
            createdAtUtc: "2026-10-30T02:00:00Z",
            expiresAtUtc: "2026-10-30T02:15:00Z",
            isReplay: true,
        });
        const onRedirect = vi.fn();
        const onOpenStatus = vi.fn();

        render(
            <CustomerWallet
                onRedirect={onRedirect}
                onOpenStatus={onOpenStatus}
                newKey={() => "k"}
            />,
        );
        await screen.findByTestId("wallet-balance");
        await user.click(
            screen.getByRole("button", { name: /Nạp tiền qua VNPay/i }),
        );

        await waitFor(() => expect(onOpenStatus).toHaveBeenCalledWith("T1"));
        expect(onRedirect).not.toHaveBeenCalled();
        expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    });

    it("an entry that does not change the balance is labelled, not shown as money moved", async () => {
        vi.spyOn(api, "getMyWallet").mockResolvedValue(WALLET);
        vi.spyOn(api, "getWalletTransactions").mockResolvedValue({
            ...HISTORY,
            items: [
                {
                    ...HISTORY.items[0],
                    entryId: "e2",
                    entryType: "Adjustment",
                    amount: 20000,
                    balanceDelta: 0,
                    affectsBalance: false,
                },
            ],
        });

        render(
            <CustomerWallet
                onRedirect={vi.fn()}
                onOpenStatus={vi.fn()}
                newKey={() => "k"}
            />,
        );

        expect(await screen.findByText(/Không đổi số dư/i)).toBeInTheDocument();
    });
});

describe("VnpayReturnStatus", () => {
    it("after a credited top-up it reads the new balance from Payment", async () => {
        vi.spyOn(api, "getWalletTopup").mockResolvedValue({
            topupId: "T1",
            amount: 50000,
            status: "Succeeded",
            createdAtUtc: "",
            completedAtUtc: "",
            expiresAtUtc: "",
        });
        const wallet = vi
            .spyOn(api, "getMyWallet")
            .mockResolvedValue({ ...WALLET, balance: 50000 });

        render(<VnpayReturnStatus topupId="T1" />);

        expect(
            await screen.findByText(/Nạp tiền thành công/i),
        ).toBeInTheDocument();
        expect(await screen.findByTestId("return-balance")).toHaveTextContent(
            "50.000",
        );
        expect(wallet).toHaveBeenCalledTimes(1);
    });

    it("an expired top-up is explained", async () => {
        vi.spyOn(api, "getWalletTopup").mockResolvedValue({
            topupId: "T2",
            amount: 50000,
            status: "Expired",
            createdAtUtc: "",
            completedAtUtc: null,
            expiresAtUtc: "",
        });
        render(<VnpayReturnStatus topupId="T2" />);
        expect(await screen.findByText(/hết hạn/i)).toBeInTheDocument();
    });

    it("asks Payment until the top-up is no longer pending", async () => {
        vi.useFakeTimers({ shouldAdvanceTime: true });
        const status = vi
            .spyOn(api, "getWalletTopup")
            .mockResolvedValueOnce({
                topupId: "T1",
                amount: 50000,
                status: "Pending",
                createdAtUtc: "",
                completedAtUtc: null,
                expiresAtUtc: "",
            })
            .mockResolvedValueOnce({
                topupId: "T1",
                amount: 50000,
                status: "Succeeded",
                createdAtUtc: "",
                completedAtUtc: "",
                expiresAtUtc: "",
            });

        render(<VnpayReturnStatus topupId="T1" />);

        expect(
            await screen.findByText(/đang chờ xác nhận/i),
        ).toBeInTheDocument();
        await act(async () => {
            await vi.advanceTimersByTimeAsync(3000);
        });
        expect(await screen.findByText(/thành công/i)).toBeInTheDocument();
        expect(status).toHaveBeenCalledTimes(2);
        await act(async () => {
            await vi.advanceTimersByTimeAsync(9000);
        });
        expect(status).toHaveBeenCalledTimes(2);
    });

    it("an unknown top-up is reported", async () => {
        vi.spyOn(api, "getWalletTopup").mockRejectedValue(
            new ApiError(404, {
                status: 404,
                title: "t",
                errorCode: "payment.topup_not_found",
            } as never),
        );

        render(<VnpayReturnStatus topupId="T404" />);

        expect(await screen.findByRole("alert")).toHaveTextContent(
            /Không tìm thấy giao dịch/,
        );
    });

    it("a return without a transaction reference is reported", async () => {
        render(<VnpayReturnStatus topupId={null} />);
        expect(await screen.findByRole("alert")).toBeInTheDocument();
    });
});
