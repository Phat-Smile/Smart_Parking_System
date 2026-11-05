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
    window.history.replaceState({}, "", "/");
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
        expect(api.describeWalletError(err)).toMatch(/used with a different amount/);
        expect(api.describeWalletError(new TypeError("x"))).toMatch(/server/);
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
        expect(
            await screen.findByText("Top-up", {
                selector: ".wallet-transaction-detail strong",
            }),
        ).toBeInTheDocument();
    });

    it("updates balance and history when a pending top-up completes", async () => {
        vi.useFakeTimers({ shouldAdvanceTime: true });
        const wallet = vi
            .spyOn(api, "getMyWallet")
            .mockResolvedValueOnce({
                ...WALLET,
                pendingTopUps: [
                    {
                        topupId: "T1",
                        amount: 50000,
                        createdAtUtc: "2026-10-30T02:00:00Z",
                    },
                ],
            })
            .mockResolvedValueOnce({
                ...WALLET,
                balance: 50000,
                pendingTopUps: [],
            });
        const history = vi
            .spyOn(api, "getWalletTransactions")
            .mockResolvedValue(HISTORY);

        render(
            <CustomerWallet
                onRedirect={vi.fn()}
                onOpenStatus={vi.fn()}
                newKey={() => "k"}
            />,
        );
        expect(
            await screen.findByText("Awaiting confirmation"),
        ).toBeInTheDocument();

        await act(async () => {
            await vi.advanceTimersByTimeAsync(5000);
        });

        expect(await screen.findByTestId("wallet-balance")).toHaveTextContent(
            "50.000",
        );
        expect(wallet).toHaveBeenCalledTimes(2);
        expect(history).toHaveBeenCalledTimes(2);
    });

    it("counts down and removes a pending top-up when its link expires", async () => {
        vi.useFakeTimers({ shouldAdvanceTime: true });
        vi.spyOn(api, "getMyWallet").mockResolvedValue({
            ...WALLET,
            pendingTopUps: [{
                topupId: "T1",
                amount: 50000,
                createdAtUtc: new Date().toISOString(),
                expiresAtUtc: new Date(Date.now() + 2000).toISOString(),
            }],
        });
        render(
            <CustomerWallet
                onRedirect={vi.fn()}
                onOpenStatus={vi.fn()}
                newKey={() => "k"}
            />,
        );

        expect(await screen.findByText(/00:02 remaining to pay/i)).toBeInTheDocument();
        await act(async () => {
            await vi.advanceTimersByTimeAsync(2000);
        });
        expect(screen.queryByRole("heading", { name: "Awaiting confirmation" })).not.toBeInTheDocument();
    });

    it("keeps expired top-up requests visible without adding them to the balance", async () => {
        vi.spyOn(api, "getMyWallet").mockResolvedValue({
            ...WALLET,
            expiredTopUps: [{
                topupId: "expired-1",
                amount: 50000,
                createdAtUtc: "2026-10-30T02:00:00Z",
                expiresAtUtc: "2026-10-30T02:15:00Z",
            }],
        });

        render(
            <CustomerWallet
                onRedirect={vi.fn()}
                onOpenStatus={vi.fn()}
                newKey={() => "k"}
            />,
        );

        const expiredHeading = await screen.findByRole("heading", { name: "Recent expired top-ups" });
        expect(expiredHeading.closest(".wallet-side-column")).toBeInTheDocument();
        expect(screen.getByTestId("wallet-balance")).toHaveTextContent("0 ₫");
        expect(screen.getByRole("link", { name: /View details/i })).toHaveAttribute(
            "href", "/payment/vnpay-return?vnp_TxnRef=expired-1",
        );
    });

    it("loads another history page when the customer selects Next", async () => {
        const user = userEvent.setup();
        vi.spyOn(api, "getMyWallet").mockResolvedValue(WALLET);
        const transactions = vi
            .spyOn(api, "getWalletTransactions")
            .mockImplementation(async (page = 1) => ({
                ...HISTORY,
                page,
                totalItems: 21,
                totalPages: 2,
                items:
                    page === 1
                        ? HISTORY.items
                        : [{ ...HISTORY.items[0], entryId: "e2" }],
            }));

        render(
            <CustomerWallet
                onRedirect={vi.fn()}
                onOpenStatus={vi.fn()}
                newKey={() => "k"}
            />,
        );
        await screen.findByText("Page 1 / 2");
        await user.click(screen.getByRole("button", { name: "Next" }));

        expect(await screen.findByText("Page 2 / 2")).toBeInTheDocument();
        expect(transactions).toHaveBeenCalledWith(2);
    });

    it("uses a suggested amount for the top-up request", async () => {
        const user = userEvent.setup();
        vi.spyOn(api, "getMyWallet").mockResolvedValue(WALLET);
        const create = vi.spyOn(api, "createWalletTopup").mockResolvedValue({
            topupId: "T2",
            amount: 100000,
            status: "Pending",
            paymentUrl: "https://sandbox.vnpayment.vn/x",
            createdAtUtc: "2026-10-30T02:00:00Z",
            expiresAtUtc: "2026-10-30T02:15:00Z",
            isReplay: false,
        });
        render(
            <CustomerWallet
                onRedirect={vi.fn()}
                onOpenStatus={vi.fn()}
                newKey={() => "key-2"}
            />,
        );

        await screen.findByTestId("wallet-balance");
        await user.click(screen.getByRole("button", { name: "100.000" }));
        await user.click(
            screen.getByRole("button", { name: /Top up with VNPay/i }),
        );

        expect(create).toHaveBeenCalledWith(100000, "key-2");
    });

    it("formats the amount while sending its numeric value to Payment", async () => {
        const user = userEvent.setup();
        vi.spyOn(api, "getMyWallet").mockResolvedValue(WALLET);
        const create = vi.spyOn(api, "createWalletTopup").mockResolvedValue({
            topupId: "T3",
            amount: 500000,
            status: "Pending",
            paymentUrl: "https://sandbox.vnpayment.vn/x",
            createdAtUtc: "2026-10-30T02:00:00Z",
            expiresAtUtc: "2026-10-30T02:15:00Z",
            isReplay: false,
        });
        render(
            <CustomerWallet
                onRedirect={vi.fn()}
                onOpenStatus={vi.fn()}
                newKey={() => "key-3"}
            />,
        );

        await screen.findByTestId("wallet-balance");
        const input = screen.getByLabelText(/Top-up amount/i);
        expect(input).toHaveValue("50.000");
        await user.clear(input);
        await user.type(input, "500000");
        expect(input).toHaveValue("500.000");
        await user.click(screen.getByRole("button", { name: /Top up with VNPay/i }));

        expect(create).toHaveBeenCalledWith(500000, "key-3");
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
            name: /Top up with VNPay/i,
        });

        await user.click(button);
        expect(await screen.findByRole("alert")).toHaveTextContent(/server/);
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
        const input = screen.getByLabelText(/Top-up amount/i);
        await user.clear(input);
        await user.type(input, "5000");
        await user.click(
            screen.getByRole("button", { name: /Top up with VNPay/i }),
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
            screen.getByRole("button", { name: /Top up with VNPay/i }),
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

        expect(await screen.findByText(/No balance change/i)).toBeInTheDocument();
    });
});

describe("VnpayReturnStatus", () => {
    it("shows the remaining payment time and then the expired result", async () => {
        vi.useFakeTimers({ shouldAdvanceTime: true });
        const expiresAtUtc = new Date(Date.now() + 3000).toISOString();
        vi.spyOn(api, "getWalletTopup")
            .mockResolvedValueOnce({
                topupId: "T1",
                amount: 50000,
                status: "Pending",
                createdAtUtc: "",
                completedAtUtc: null,
                expiresAtUtc,
            })
            .mockResolvedValue({
                topupId: "T1",
                amount: 50000,
                status: "Expired",
                createdAtUtc: "",
                completedAtUtc: null,
                expiresAtUtc,
            });

        render(<VnpayReturnStatus topupId="T1" />);
        expect(await screen.findByText(/Time remaining to pay: 00:03/i)).toBeInTheDocument();
        await act(async () => {
            await vi.advanceTimersByTimeAsync(3000);
        });
        expect(await screen.findByRole("heading", { name: /Payment link expired/i })).toBeInTheDocument();
    });

    it("relays a signed sandbox return on localhost, but not a status-only link", async () => {
        const fetchMock = vi.fn().mockResolvedValue(new Response('{"RspCode":"00"}'));
        vi.stubGlobal("fetch", fetchMock);
        vi.spyOn(api, "getWalletTopup").mockResolvedValue({
            topupId: "T1",
            amount: 50000,
            status: "Pending",
            createdAtUtc: "",
            completedAtUtc: null,
            expiresAtUtc: "",
        });

        window.history.replaceState({}, "", "/payment/vnpay-return?vnp_TxnRef=T1");
        const first = render(<VnpayReturnStatus topupId="T1" />);
        expect(fetchMock).not.toHaveBeenCalled();
        first.unmount();

        window.history.replaceState(
            {},
            "",
            "/payment/vnpay-return?vnp_TxnRef=T1&vnp_Amount=5000000&vnp_ResponseCode=00&vnp_TransactionStatus=00&vnp_SecureHash=signed",
        );
        render(<VnpayReturnStatus topupId="T1" />);
        await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1));
        expect(String(fetchMock.mock.calls[0][0])).toContain("/api/payment/vnpay/ipn?");
    });

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
            await screen.findByText(/Top-up successful/i),
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
        expect(
            await screen.findByRole("heading", { name: /expired/i }),
        ).toBeInTheDocument();
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
            await screen.findByText(/ParkWise is waiting for confirmation from VNPay/i),
        ).toBeInTheDocument();
        await act(async () => {
            await vi.advanceTimersByTimeAsync(3000);
        });
        expect(
            await screen.findByRole("heading", { name: /Top-up successful/i }),
        ).toBeInTheDocument();
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
            /This top-up could not be found/,
        );
    });

    it("a return without a transaction reference is reported", async () => {
        render(<VnpayReturnStatus topupId={null} />);
        expect(await screen.findByRole("alert")).toBeInTheDocument();
    });
});
