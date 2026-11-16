"use client";

import { useEffect, useState, useRef } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { AlertCircle, CheckCircle2, Clock, ShieldAlert } from "lucide-react";

export type PaymentPublicStatus =
    "Verifying" | "Pending" | "Succeeded" | "Failed" | "Invalid" | "Timeout";

export function ParkingFeeReturnScreen({
    searchParamsOverride,
}: {
    searchParamsOverride?:
        | Record<string, string | string[] | undefined>
        | URLSearchParams
        | string;
}) {
    const nextSearchParams = useSearchParams();

    // Resolve search params string
    let queryString = "";
    if (typeof searchParamsOverride === "string") {
        queryString = searchParamsOverride.startsWith("?")
            ? searchParamsOverride.slice(1)
            : searchParamsOverride;
    } else if (searchParamsOverride instanceof URLSearchParams) {
        queryString = searchParamsOverride.toString();
    } else if (
        searchParamsOverride &&
        typeof searchParamsOverride === "object"
    ) {
        const sp = new URLSearchParams();
        for (const [key, val] of Object.entries(searchParamsOverride)) {
            if (Array.isArray(val)) {
                for (const item of val) sp.append(key, item);
            } else if (val !== undefined) {
                sp.append(key, val);
            }
        }
        queryString = sp.toString();
    } else if (nextSearchParams) {
        queryString = nextSearchParams.toString();
    }

    const [state, setState] = useState<PaymentPublicStatus>(() =>
        !queryString ? "Invalid" : "Verifying",
    );
    const [txnRef, setTxnRef] = useState<string | null>(null);
    const [pollCount, setPollCount] = useState(0);
    const timerRef = useRef<NodeJS.Timeout | null>(null);

    useEffect(() => {
        if (!queryString) {
            return;
        }

        let isMounted = true;

        async function verifyReturn() {
            try {
                const res = await fetch(
                    `/api/payment/parking-sessions/vnpay-return-verify?${queryString}`,
                );
                if (!res.ok) {
                    if (isMounted) setState("Invalid");
                    return;
                }
                const data = (await res.json()) as {
                    isValid?: boolean;
                    txnRef?: string;
                };
                if (!data || !data.isValid) {
                    if (isMounted) setState("Invalid");
                    return;
                }

                const ref = data.txnRef || "";
                if (isMounted) {
                    setTxnRef(ref);
                    setState("Pending");
                }
            } catch {
                if (isMounted) setState("Invalid");
            }
        }

        void verifyReturn();

        return () => {
            isMounted = false;
        };
    }, [queryString]);

    useEffect(() => {
        if (state !== "Pending" || !txnRef) {
            if (timerRef.current) clearInterval(timerRef.current);
            return;
        }

        let isMounted = true;
        let count = 0;

        async function pollStatus() {
            count++;
            if (isMounted) setPollCount(count);

            try {
                const res = await fetch(
                    `/api/payment/parking-sessions/public-status?txnRef=${encodeURIComponent(txnRef!)}`,
                );
                if (res.ok) {
                    const data = (await res.json()) as { status?: string };
                    if (data?.status === "Succeeded") {
                        if (isMounted) setState("Succeeded");
                        return;
                    }
                    if (data?.status === "Failed") {
                        if (isMounted) setState("Failed");
                        return;
                    }
                }
            } catch {
                // Ignore transient network errors during polling
            }

            if (count >= 10) {
                if (isMounted) setState("Timeout");
            }
        }

        // Poll immediately once
        void pollStatus();

        // Then poll every 2.5s (2500ms)
        const interval = setInterval(() => {
            if (count < 10) {
                void pollStatus();
            } else {
                clearInterval(interval);
            }
        }, 2500);

        timerRef.current = interval;

        return () => {
            isMounted = false;
            clearInterval(interval);
        };
    }, [state, txnRef]);

    return (
        <div className="flex flex-col items-center justify-center min-h-[60vh] px-4 py-8 max-w-md mx-auto text-center font-sans">
            {state === "Verifying" && (
                <div
                    role="status"
                    className="flex flex-col items-center justify-center space-y-4"
                >
                    <div className="w-12 h-12 border-4 border-amber-500 border-t-transparent rounded-full animate-spin" />
                    <p className="text-slate-600 font-medium">
                        Đang xác thực thông tin giao dịch VNPay...
                    </p>
                </div>
            )}

            {state === "Invalid" && (
                <div
                    role="alert"
                    className="w-full bg-rose-50 border border-rose-200 rounded-2xl p-6 text-rose-900 shadow-sm"
                >
                    <div className="w-12 h-12 mx-auto mb-4 rounded-full bg-rose-100 flex items-center justify-center text-rose-600">
                        <ShieldAlert className="w-6 h-6" />
                    </div>
                    <h2 className="text-lg font-bold mb-2">Cảnh báo bảo mật</h2>
                    <p className="text-base font-semibold text-rose-700">
                        Dữ liệu phản hồi thanh toán không hợp lệ
                    </p>
                    <p className="text-xs text-rose-600 mt-2">
                        Chữ ký xác thực giao dịch từ cổng thanh toán không khớp
                        hoặc đã bị can thiệp.
                    </p>
                    <div className="mt-6">
                        <Link
                            href="/"
                            className="inline-flex items-center justify-center min-h-[44px] px-6 py-2.5 rounded-xl bg-rose-600 text-white font-medium hover:bg-rose-700 transition"
                        >
                            Về trang chủ
                        </Link>
                    </div>
                </div>
            )}

            {state === "Pending" && (
                <div
                    role="status"
                    className="w-full bg-amber-50 border border-amber-200 rounded-2xl p-6 text-amber-900 shadow-sm"
                >
                    <div className="w-12 h-12 mx-auto mb-4 rounded-full bg-amber-100 flex items-center justify-center text-amber-600 animate-pulse">
                        <Clock className="w-6 h-6" />
                    </div>
                    <h2 className="text-lg font-bold mb-2">
                        Đang xử lý giao dịch
                    </h2>
                    <p className="text-base font-semibold text-amber-800">
                        Đang xác nhận thanh toán với ngân hàng...
                    </p>
                    <p className="text-xs text-amber-700 mt-2">
                        Hệ thống đang chờ đối soát IPN từ VNPay (Lần kiểm tra{" "}
                        {pollCount}/10).
                    </p>
                </div>
            )}

            {state === "Succeeded" && (
                <div
                    role="status"
                    className="w-full bg-emerald-50 border border-emerald-200 rounded-2xl p-6 text-emerald-900 shadow-sm"
                >
                    <div className="w-12 h-12 mx-auto mb-4 rounded-full bg-emerald-100 flex items-center justify-center text-emerald-600">
                        <CheckCircle2 className="w-6 h-6" />
                    </div>
                    <h2 className="text-lg font-bold mb-2">
                        Thanh toán hoàn tất
                    </h2>
                    <p className="text-base font-semibold text-emerald-800">
                        Thanh toán thành công! Cổng kiểm soát sẽ mở cho xe của
                        bạn rời bãi.
                    </p>
                    {txnRef && (
                        <p className="text-xs text-emerald-700 mt-2">
                            Mã giao dịch:{" "}
                            <span className="font-mono font-medium">
                                {txnRef}
                            </span>
                        </p>
                    )}
                    <div className="mt-6">
                        <Link
                            href="/"
                            className="inline-flex items-center justify-center min-h-[44px] px-6 py-2.5 rounded-xl bg-emerald-600 text-white font-medium hover:bg-emerald-700 transition"
                        >
                            Về trang chủ
                        </Link>
                    </div>
                </div>
            )}

            {state === "Timeout" && (
                <div
                    role="alert"
                    className="w-full bg-amber-50 border border-amber-200 rounded-2xl p-6 text-amber-900 shadow-sm"
                >
                    <div className="w-12 h-12 mx-auto mb-4 rounded-full bg-amber-100 flex items-center justify-center text-amber-600">
                        <AlertCircle className="w-6 h-6" />
                    </div>
                    <h2 className="text-lg font-bold mb-2">
                        Đang chờ đối soát
                    </h2>
                    <p className="text-base font-semibold text-amber-800">
                        Giao dịch đang được ngân hàng đối soát. Nếu tài khoản đã
                        bị trừ tiền, vui lòng báo nhân viên trực cổng.
                    </p>
                    {txnRef && (
                        <p className="text-xs text-amber-700 mt-2">
                            Mã giao dịch:{" "}
                            <span className="font-mono font-medium">
                                {txnRef}
                            </span>
                        </p>
                    )}
                    <div className="mt-6 flex flex-col sm:flex-row gap-3 justify-center">
                        <button
                            type="button"
                            onClick={() => {
                                setState("Pending");
                            }}
                            className="inline-flex items-center justify-center min-h-[44px] px-6 py-2.5 rounded-xl bg-amber-600 text-white font-medium hover:bg-amber-700 transition"
                        >
                            Thử kiểm tra lại
                        </button>
                        <Link
                            href="/"
                            className="inline-flex items-center justify-center min-h-[44px] px-6 py-2.5 rounded-xl border border-amber-300 text-amber-800 font-medium hover:bg-amber-100 transition"
                        >
                            Về trang chủ
                        </Link>
                    </div>
                </div>
            )}

            {state === "Failed" && (
                <div
                    role="alert"
                    className="w-full bg-rose-50 border border-rose-200 rounded-2xl p-6 text-rose-900 shadow-sm"
                >
                    <div className="w-12 h-12 mx-auto mb-4 rounded-full bg-rose-100 flex items-center justify-center text-rose-600">
                        <AlertCircle className="w-6 h-6" />
                    </div>
                    <h2 className="text-lg font-bold mb-2">
                        Thanh toán không thành công
                    </h2>
                    <p className="text-base font-semibold text-rose-800">
                        Giao dịch đã bị từ chối hoặc quá hạn thanh toán.
                    </p>
                    <div className="mt-6">
                        <Link
                            href="/"
                            className="inline-flex items-center justify-center min-h-[44px] px-6 py-2.5 rounded-xl bg-rose-600 text-white font-medium hover:bg-rose-700 transition"
                        >
                            Về trang chủ
                        </Link>
                    </div>
                </div>
            )}
        </div>
    );
}
