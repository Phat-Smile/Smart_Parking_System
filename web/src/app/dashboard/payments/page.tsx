"use client";

import { vi } from "../../../features/customer/i18n/vi";
import { Wallet, AlertCircle, Plus, Info, Loader2 } from "lucide-react";
import { useEffect, useState } from "react";
import { gatewayFetch } from "@/shared/api/client";

interface WalletBalance {
    walletId: string;
    balance: number;
    currency: string;
    isDemo: boolean;
    demoNotice?: string;
    limits: {
        minTopUp: number;
        maxTopUp: number;
        maxBalance: number;
    };
    gatewayMode: string;
    pendingTopUps?: Array<{
        topupId: string;
        amount: number;
        createdAtUtc: string;
    }>;
}

interface TopupResult {
    topupId: string;
    amount: number;
    status: string;
    paymentUrl: string;
    createdAtUtc: string;
    expiresAtUtc: string;
    isReplay: boolean;
}

export default function WalletPage() {
    const [amount, setAmount] = useState<string>("50000");
    const [wallet, setWallet] = useState<WalletBalance | null>(null);
    const [isLoading, setIsLoading] = useState<boolean>(true);
    const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
    const [errorMessage, setErrorMessage] = useState<string | null>(null);

    useEffect(() => {
        let active = true;
        gatewayFetch<WalletBalance>("/api/payment/wallets/me")
            .then((data) => {
                if (active) {
                    setWallet(data);
                }
            })
            .catch((err: unknown) => {
                console.error("Failed to load wallet balance:", err);
                if (active) {
                    setErrorMessage(
                        "Không thể tải số dư ví. Vui lòng thử lại sau.",
                    );
                }
            })
            .finally(() => {
                if (active) {
                    setIsLoading(false);
                }
            });

        return () => {
            active = false;
        };
    }, []);

    const balance = wallet?.balance ?? 0;
    const isLowBalance = balance < 50000;

    const handleTopUp = async () => {
        const numAmount = Number(amount);
        if (isNaN(numAmount) || numAmount < 10000) {
            setErrorMessage("Số tiền nạp tối thiểu là 10.000 VND.");
            return;
        }

        try {
            setIsSubmitting(true);
            setErrorMessage(null);
            const idempotencyKey = crypto.randomUUID();
            const result = await gatewayFetch<TopupResult>(
                "/api/payment/wallets/top-up",
                {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                        "Idempotency-Key": idempotencyKey,
                    },
                    body: JSON.stringify({ amount: numAmount }),
                },
            );

            if (result.paymentUrl) {
                window.location.href = result.paymentUrl;
            } else {
                setErrorMessage(
                    "Không nhận được liên kết thanh toán từ cổng VNPay.",
                );
            }
        } catch (err: unknown) {
            console.error("Top-up request failed:", err);
            setErrorMessage(
                err instanceof Error
                    ? err.message
                    : "Giao dịch nạp tiền thất bại. Vui lòng thử lại.",
            );
        } finally {
            setIsSubmitting(false);
        }
    };

    return (
        <div className="animate-in fade-in duration-500 space-y-6 max-w-2xl mx-auto">
            <h1 className="text-2xl font-bold text-white mb-6">
                {vi.dashboard.wallet}
            </h1>

            {/* Thẻ Ví */}
            <div className="bg-gradient-to-br from-[#6246ea] to-[#4d36d0] rounded-3xl p-8 shadow-glow-purple relative overflow-hidden">
                <div className="absolute top-0 right-0 w-64 h-64 bg-white opacity-5 blur-[100px] rounded-full pointer-events-none"></div>

                <div className="relative z-10 flex flex-col items-center text-center">
                    <Wallet size={32} className="text-white mb-4 opacity-80" />
                    <span className="text-white/80 font-medium mb-1">
                        {vi.wallet.balance}
                    </span>
                    <h2 className="text-4xl md:text-5xl font-bold text-white mb-6">
                        {isLoading ? (
                            <span className="inline-flex items-center gap-2 text-2xl opacity-75">
                                <Loader2 className="animate-spin" size={28} />{" "}
                                Đang tải...
                            </span>
                        ) : (
                            new Intl.NumberFormat("vi-VN", {
                                style: "currency",
                                currency: "VND",
                            }).format(balance)
                        )}
                    </h2>

                    {!isLoading && isLowBalance && (
                        <div className="bg-[#f59e0b]/20 text-[#fbbf24] px-4 py-2 rounded-xl text-sm font-medium flex items-center gap-2 mb-6">
                            <AlertCircle size={16} />
                            {vi.wallet.lowBalance}
                        </div>
                    )}

                    <div className="bg-black/20 backdrop-blur-sm rounded-xl p-3 inline-flex items-center gap-2 text-white/70 text-xs">
                        <Info size={14} />
                        {vi.wallet.policy}
                    </div>
                </div>
            </div>

            {/* Form Nạp Tiền */}
            <div className="bg-[#0b0c17] border border-white/10 rounded-3xl p-6 md:p-8 shadow-card-light mt-8">
                <h3 className="text-lg font-bold text-white mb-4">
                    {vi.wallet.topUp}
                </h3>

                {errorMessage && (
                    <div className="bg-red-500/10 border border-red-500/20 text-red-400 p-4 rounded-xl text-sm mb-6 flex items-center gap-2">
                        <AlertCircle size={18} className="shrink-0" />
                        <span>{errorMessage}</span>
                    </div>
                )}

                <div className="grid grid-cols-3 gap-3 mb-6">
                    {[
                        "50000",
                        "100000",
                        "200000",
                        "500000",
                        "1000000",
                        "2000000",
                    ].map((val) => (
                        <button
                            key={val}
                            onClick={() => {
                                setAmount(val);
                                setErrorMessage(null);
                            }}
                            className={`py-3 rounded-xl font-medium transition-colors border ${
                                amount === val
                                    ? "bg-[#6246ea]/20 border-[#6246ea] text-white"
                                    : "bg-white/5 border-white/10 text-[#8790a3] hover:bg-white/10 hover:text-white"
                            }`}
                        >
                            {new Intl.NumberFormat("vi-VN").format(Number(val))}
                        </button>
                    ))}
                </div>

                <div className="relative mb-6">
                    <input
                        type="number"
                        value={amount}
                        onChange={(e) => {
                            setAmount(e.target.value);
                            setErrorMessage(null);
                        }}
                        className="w-full bg-[#101426] border border-white/10 rounded-xl px-4 py-4 text-xl font-bold text-white outline-none focus:border-[#6246ea] transition-colors"
                        placeholder="Nhập số tiền khác..."
                        min="10000"
                        step="10000"
                    />
                    <span className="absolute right-4 top-1/2 -translate-y-1/2 text-[#8790a3] font-medium">
                        VND
                    </span>
                </div>

                <button
                    onClick={handleTopUp}
                    disabled={isSubmitting}
                    className="w-full bg-[#6246ea] text-white py-4 rounded-xl font-bold hover:bg-[#7c5cff] disabled:opacity-50 disabled:cursor-not-allowed transition-colors flex items-center justify-center gap-2 shadow-glow-purple"
                >
                    {isSubmitting ? (
                        <>
                            <Loader2 className="animate-spin" size={20} />
                            Đang kết nối cổng VNPay...
                        </>
                    ) : (
                        <>
                            <Plus size={20} />
                            Thanh toán qua VNPay
                        </>
                    )}
                </button>
            </div>
        </div>
    );
}
