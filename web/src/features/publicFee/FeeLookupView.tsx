"use client";

import React, { useCallback, useEffect, useState, useTransition } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import {
    AlertCircle,
    ArrowLeft,
    Clock,
    Info,
    RefreshCw,
    Search,
    ShieldCheck,
    X,
} from "lucide-react";
import {
    fetchPublicParkingFee,
    FeeLookupError,
    formatDateTimeVi,
    formatDurationVi,
    formatVnd,
    mapCalculationMethodVi,
    mapStatusVi,
    type PublicParkingSessionFeeResponse,
} from "./feeLookupApi";

export function FeeLookupView() {
    const searchParams = useSearchParams();
    const urlQr = searchParams.get("qr") || "";

    const [inputQr, setInputQr] = useState(urlQr);
    const [prevUrlQr, setPrevUrlQr] = useState(urlQr);
    if (urlQr !== prevUrlQr) {
        setPrevUrlQr(urlQr);
        setInputQr(urlQr);
    }

    const [feeData, setFeeData] =
        useState<PublicParkingSessionFeeResponse | null>(null);
    const [isLoading, setIsLoading] = useState(false);
    const [errorMessage, setErrorMessage] = useState<string | null>(null);
    const [errorStatusCode, setErrorStatusCode] = useState<number | null>(null);
    const [, startTransition] = useTransition();

    const executeLookup = useCallback(
        async (token: string) => {
            const trimmed = token.trim();
            if (!trimmed) {
                setFeeData(null);
                setErrorMessage(
                    "Vui lòng nhập hoặc quét mã QR từ vé gửi xe của bạn.",
                );
                setErrorStatusCode(null);
                return;
            }

            setIsLoading(true);
            setErrorMessage(null);
            setErrorStatusCode(null);

            try {
                const data = await fetchPublicParkingFee(trimmed);
                setFeeData(data);
                // Sync URL query without full reload
                const nextUrl = `/fee-lookup?qr=${encodeURIComponent(trimmed)}`;
                if (typeof window !== "undefined") {
                    window.history.replaceState(null, "", nextUrl);
                }
            } catch (err: unknown) {
                setFeeData(null);
                if (err instanceof FeeLookupError) {
                    setErrorStatusCode(err.statusCode);
                    setErrorMessage(err.message);
                } else if (
                    err instanceof DOMException &&
                    err.name === "AbortError"
                ) {
                    // Ignore abort
                } else {
                    setErrorStatusCode(500);
                    setErrorMessage(
                        "Có lỗi xảy ra trong quá trình tra cứu. Vui lòng thử lại sau.",
                    );
                }
            } finally {
                setIsLoading(false);
            }
        },
        [],
    );

    // Initial search if query param qr is present on mount or changed externally
    useEffect(() => {
        if (urlQr && urlQr.trim()) {
            void executeLookup(urlQr);
        }
    }, [urlQr, executeLookup]);

    const handleSubmit = (e: React.FormEvent) => {
        e.preventDefault();
        startTransition(() => {
            void executeLookup(inputQr);
        });
    };

    const handleClear = () => {
        setInputQr("");
        setFeeData(null);
        setErrorMessage(null);
        setErrorStatusCode(null);
        if (typeof window !== "undefined") {
            window.history.replaceState(null, "", "/fee-lookup");
        }
    };

    const handleRefresh = () => {
        if (inputQr.trim()) {
            void executeLookup(inputQr.trim());
        }
    };

    const statusInfo = feeData ? mapStatusVi(feeData.status) : null;

    return (
        <main className="min-h-screen bg-slate-900 text-slate-100 px-4 py-6 sm:py-12 flex flex-col items-center">
            <div className="w-full max-w-lg mx-auto">
                {/* Header navigation & Brand */}
                <header className="mb-6">
                    <Link
                        href="/"
                        className="inline-flex items-center gap-2 text-xs sm:text-sm text-slate-400 hover:text-slate-200 transition-colors mb-4 focus:outline-none focus:ring-2 focus:ring-blue-500 rounded-md px-2 py-1 -ml-2"
                    >
                        <ArrowLeft size={16} aria-hidden="true" />
                        <span>Trang chủ ParkWise</span>
                    </Link>

                    <div className="flex items-center justify-between gap-3 flex-wrap">
                        <div>
                            <span className="text-xs uppercase tracking-wider font-semibold text-blue-400 bg-blue-950/70 border border-blue-800/60 px-2.5 py-0.5 rounded-full inline-block mb-1.5">
                                Cổng thông tin công khai
                            </span>
                            <h1 className="text-xl sm:text-2xl font-bold tracking-tight text-white">
                                Tra cứu phí đỗ xe
                            </h1>
                        </div>
                        <div
                            className="flex items-center gap-1.5 text-xs text-slate-400 bg-slate-800/80 px-2.5 py-1 rounded-lg border border-slate-700/80"
                            title="Thông tin được ẩn danh bảo mật"
                        >
                            <ShieldCheck
                                size={14}
                                className="text-emerald-400 shrink-0"
                                aria-hidden="true"
                            />
                            <span>Bảo mật Q-V2c</span>
                        </div>
                    </div>
                    <p className="text-xs sm:text-sm text-slate-400 mt-2">
                        Quét hoặc nhập mã QR từ vé đỗ xe để kiểm tra thời lượng
                        và phí gửi xe tạm tính theo thời gian thực.
                    </p>
                </header>

                {/* Search Form Card */}
                <section
                    className="bg-slate-800/90 border border-slate-700/80 rounded-2xl p-4 sm:p-5 shadow-xl shadow-slate-950/40 mb-6 backdrop-blur-sm"
                    aria-label="Khung tra cứu phí"
                >
                    <form onSubmit={handleSubmit} className="space-y-3">
                        <label
                            htmlFor="qr-input"
                            className="block text-xs sm:text-sm font-medium text-slate-300"
                        >
                            Mã vé xe / Token phiên đỗ
                        </label>
                        <div className="relative flex items-center">
                            <input
                                id="qr-input"
                                type="text"
                                value={inputQr}
                                onChange={(e) => setInputQr(e.target.value)}
                                placeholder="Nhập mã QR từ vé giấy (ví dụ: qr-token...)"
                                className="w-full bg-slate-900 border border-slate-700 rounded-xl px-3.5 py-2.5 pr-20 text-sm text-white placeholder-slate-500 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-all"
                                disabled={isLoading}
                                autoComplete="off"
                                autoCorrect="off"
                                spellCheck="false"
                            />
                            {inputQr && (
                                <button
                                    type="button"
                                    onClick={handleClear}
                                    className="absolute right-10 text-slate-400 hover:text-white p-1 rounded-md transition-colors"
                                    title="Xóa nội dung"
                                    aria-label="Xóa nội dung nhập"
                                >
                                    <X size={16} aria-hidden="true" />
                                </button>
                            )}
                            <button
                                type="submit"
                                disabled={isLoading || !inputQr.trim()}
                                className="absolute right-1.5 bg-blue-600 hover:bg-blue-500 disabled:bg-slate-700 disabled:text-slate-500 text-white p-2 rounded-lg transition-colors flex items-center justify-center cursor-pointer disabled:cursor-not-allowed"
                                title="Tra cứu phí"
                                aria-label="Bắt đầu tra cứu phí đỗ xe"
                            >
                                <Search size={16} aria-hidden="true" />
                            </button>
                        </div>
                        <div className="flex items-center justify-between text-xs text-slate-400 pt-1">
                            <span>Hỗ trợ quét qua camera hoặc dán mã</span>
                            <button
                                type="submit"
                                disabled={isLoading || !inputQr.trim()}
                                className="text-blue-400 hover:text-blue-300 font-medium underline underline-offset-2 transition-colors disabled:opacity-40"
                            >
                                {isLoading ? "Đang tra cứu…" : "Tra cứu ngay"}
                            </button>
                        </div>
                    </form>
                </section>

                {/* State 1: Loading Skeleton */}
                {isLoading && (
                    <section
                        className="bg-slate-800/80 border border-slate-700/80 rounded-2xl p-6 shadow-lg text-center animate-pulse"
                        role="status"
                        aria-live="polite"
                        aria-label="Đang tải dữ liệu"
                    >
                        <div className="inline-block animate-spin rounded-full h-9 w-9 border-3 border-slate-600 border-t-blue-500 mb-3" />
                        <h2 className="text-base font-semibold text-white">
                            Đang tra cứu dữ liệu phiên đỗ…
                        </h2>
                        <p className="text-xs text-slate-400 mt-1">
                            Vui lòng chờ trong giây lát trong khi hệ thống tính
                            toán cước phí.
                        </p>
                    </section>
                )}

                {/* State 2, 3, 4: Error States (404, 429, 503, Generic) */}
                {!isLoading && errorMessage && (
                    <section
                        className="bg-red-950/40 border border-red-800/70 rounded-2xl p-5 shadow-lg text-left"
                        role="alert"
                        aria-live="assertive"
                    >
                        <div className="flex items-start gap-3">
                            <div className="p-2 bg-red-900/50 rounded-lg text-red-300 shrink-0 mt-0.5">
                                <AlertCircle size={20} aria-hidden="true" />
                            </div>
                            <div className="flex-1 min-w-0">
                                <h2 className="text-sm font-bold text-red-200">
                                    {errorStatusCode === 404
                                        ? "Không tìm thấy phiên đỗ"
                                        : errorStatusCode === 429
                                          ? "Vượt giới hạn tra cứu"
                                          : errorStatusCode === 503
                                            ? "Dịch vụ tạm ngưng"
                                            : "Thông báo lỗi"}
                                </h2>
                                <p className="text-xs sm:text-sm text-red-300 mt-1 leading-relaxed">
                                    {errorMessage}
                                </p>
                                <div className="mt-3 text-xs text-red-400/90 space-y-1">
                                    {errorStatusCode === 404 && (
                                        <p>
                                            Gợi ý: Kiểm tra lại mã QR in trên vé
                                            hoặc yêu cầu nhân viên trực cổng hỗ
                                            trợ.
                                        </p>
                                    )}
                                    {errorStatusCode === 429 && (
                                        <p>
                                            Hệ thống giới hạn tần suất để chống
                                            tấn công giả mạo. Xin vui lòng chờ 1
                                            phút và bấm tra cứu lại.
                                        </p>
                                    )}
                                    {errorStatusCode === 503 && (
                                        <p>
                                            Hệ thống máy chủ tính phí đang được
                                            bảo trì hoặc cập nhật.
                                        </p>
                                    )}
                                </div>
                            </div>
                        </div>
                    </section>
                )}

                {/* State 5: Success State */}
                {!isLoading && feeData && (
                    <article
                        className="bg-slate-800/95 border border-slate-700/80 rounded-2xl overflow-hidden shadow-2xl shadow-slate-950/60"
                        aria-label="Thông tin phí gửi xe"
                    >
                        {/* Ticket Header & Plate */}
                        <div className="bg-gradient-to-r from-slate-900 via-slate-850 to-slate-900 p-5 border-b border-slate-700/80">
                            <div className="flex items-center justify-between gap-2 flex-wrap mb-3">
                                <span className="text-xs font-semibold text-slate-400 tracking-wider uppercase">
                                    Thông tin xe & trạng thái
                                </span>
                                {statusInfo && (
                                    <span
                                        className={`inline-flex items-center gap-1.5 text-xs font-bold px-2.5 py-0.5 rounded-full border ${
                                            statusInfo.tone === "active"
                                                ? "bg-emerald-950 text-emerald-300 border-emerald-700/80"
                                                : statusInfo.tone === "paid"
                                                  ? "bg-blue-950 text-blue-300 border-blue-700/80"
                                                  : statusInfo.tone ===
                                                      "completed"
                                                    ? "bg-slate-800 text-slate-300 border-slate-600"
                                                    : "bg-amber-950 text-amber-300 border-amber-700/80"
                                        }`}
                                    >
                                        <span
                                            className={`w-1.5 h-1.5 rounded-full ${
                                                statusInfo.tone === "active"
                                                    ? "bg-emerald-400 animate-pulse"
                                                    : statusInfo.tone === "paid"
                                                      ? "bg-blue-400"
                                                      : "bg-slate-400"
                                            }`}
                                            aria-hidden="true"
                                        />
                                        {statusInfo.label}
                                    </span>
                                )}
                            </div>

                            {/* Masked License Plate Badge */}
                            <div className="flex items-center justify-center my-2">
                                <div
                                    className="bg-white text-slate-900 border-2 border-slate-400 px-4 py-2 rounded-lg font-mono font-black text-lg sm:text-2xl tracking-wider shadow-inner flex items-center gap-2 select-all"
                                    aria-label={`Biển số xe: ${feeData.maskedPlateNumber}`}
                                >
                                    <span className="text-[10px] sm:text-xs font-sans font-bold bg-blue-700 text-white px-1.5 py-0.5 rounded leading-none">
                                        VN
                                    </span>
                                    <span>{feeData.maskedPlateNumber}</span>
                                </div>
                            </div>
                        </div>

                        {/* Amount Due Highlight */}
                        <div className="p-5 sm:p-6 bg-slate-850/60 border-b border-slate-700/60 text-center">
                            <span className="text-xs uppercase tracking-wider text-slate-400 font-medium block mb-1">
                                Số tiền cần thanh toán
                            </span>
                            <div
                                className="text-3xl sm:text-4xl font-extrabold text-emerald-400 tracking-tight"
                                aria-label={`Số tiền: ${formatVnd(feeData.amountDue)}`}
                            >
                                {formatVnd(feeData.amountDue)}
                            </div>
                            {feeData.paidAmount > 0 && (
                                <p className="text-xs text-slate-400 mt-1">
                                    Đã khấu trừ cọc trước:{" "}
                                    <span className="text-slate-200 font-semibold">
                                        {formatVnd(feeData.paidAmount)}
                                    </span>
                                </p>
                            )}
                        </div>

                        {/* Detailed Session Metrics Grid */}
                        <div className="p-5 sm:p-6 space-y-4">
                            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 text-xs sm:text-sm">
                                <div className="bg-slate-900/60 border border-slate-700/60 rounded-xl p-3">
                                    <span className="text-slate-400 block text-xs">
                                        Thời gian xe vào
                                    </span>
                                    <span className="font-semibold text-white mt-0.5 block">
                                        {formatDateTimeVi(
                                            feeData.entryTimeUtc,
                                        )}
                                    </span>
                                </div>

                                <div className="bg-slate-900/60 border border-slate-700/60 rounded-xl p-3">
                                    <span className="text-slate-400 block text-xs">
                                        Thời lượng đã đỗ
                                    </span>
                                    <span className="font-semibold text-white mt-0.5 flex items-center gap-1.5">
                                        <Clock
                                            size={14}
                                            className="text-amber-400"
                                            aria-hidden="true"
                                        />
                                        {formatDurationVi(
                                            feeData.durationMinutes,
                                        )}
                                    </span>
                                </div>

                                <div className="bg-slate-900/60 border border-slate-700/60 rounded-xl p-3">
                                    <span className="text-slate-400 block text-xs">
                                        Phương thức tính
                                    </span>
                                    <span className="font-medium text-slate-200 mt-0.5 block">
                                        {mapCalculationMethodVi(
                                            feeData.calculationMethod,
                                        )}
                                    </span>
                                </div>

                                <div className="bg-slate-900/60 border border-slate-700/60 rounded-xl p-3">
                                    <span className="text-slate-400 block text-xs">
                                        Tổng cước tạm tính
                                    </span>
                                    <span className="font-semibold text-slate-200 mt-0.5 block">
                                        {formatVnd(feeData.totalFee)}
                                    </span>
                                </div>
                            </div>

                            {/* Timestamp of Calculation */}
                            <div className="text-[11px] text-slate-400 flex items-center justify-between border-t border-slate-700/60 pt-3">
                                <span>
                                    Cập nhật lúc:{" "}
                                    <span className="text-slate-300">
                                        {formatDateTimeVi(
                                            feeData.calculatedAtUtc,
                                        )}
                                    </span>
                                </span>
                                <button
                                    type="button"
                                    onClick={handleRefresh}
                                    className="inline-flex items-center gap-1 text-blue-400 hover:text-blue-300 transition-colors p-1"
                                    title="Tính lại theo thời gian thực"
                                >
                                    <RefreshCw size={12} aria-hidden="true" />
                                    <span>Làm mới</span>
                                </button>
                            </div>

                            {/* Informational Guidance Note (NO PAY BUTTON) */}
                            <div className="bg-blue-950/40 border border-blue-900/60 rounded-xl p-3 text-xs text-blue-200 flex items-start gap-2.5">
                                <Info
                                    size={16}
                                    className="text-blue-400 shrink-0 mt-0.5"
                                    aria-hidden="true"
                                />
                                <p className="leading-relaxed">
                                    Số tiền trên là cước tạm tính đến thời điểm
                                    hiện tại. Phí đỗ xe chính xác sẽ được đối
                                    soát tại cổng kiểm soát khi xe ra. Quý khách
                                    vui lòng thanh toán trực tiếp tại bãi xe hoặc
                                    làm theo hướng dẫn của nhân viên trạm soát
                                    vé.
                                </p>
                            </div>
                        </div>

                        {/* Action buttons */}
                        <div className="bg-slate-900/80 p-4 border-t border-slate-700/80 flex items-center justify-between gap-3">
                            <button
                                type="button"
                                onClick={handleClear}
                                className="w-full text-center text-xs sm:text-sm font-medium text-slate-300 hover:text-white bg-slate-800 hover:bg-slate-700 py-2.5 px-4 rounded-xl transition-colors"
                            >
                                Tra cứu mã vé khác
                            </button>
                        </div>
                    </article>
                )}

                {/* State 6: Initial Idle State (when no search done yet and no data) */}
                {!isLoading && !feeData && !errorMessage && (
                    <section
                        className="bg-slate-850/50 border border-slate-700/50 rounded-2xl p-6 text-center"
                        aria-label="Hướng dẫn tra cứu"
                    >
                        <div className="w-12 h-12 rounded-full bg-blue-950/80 border border-blue-800/80 text-blue-400 flex items-center justify-center mx-auto mb-3">
                            <Search size={22} aria-hidden="true" />
                        </div>
                        <h2 className="text-sm sm:text-base font-semibold text-white">
                            Tra cứu phí nhanh chóng
                        </h2>
                        <p className="text-xs text-slate-400 mt-1 max-w-sm mx-auto leading-relaxed">
                            Nhập mã in dưới mã vạch trên vé giữ xe hoặc quét mã
                            QR bằng camera điện thoại để xem chi tiết tiền gửi
                            xe.
                        </p>
                    </section>
                )}

                {/* Footer disclaimer */}
                <footer className="mt-8 text-center text-[11px] text-slate-400 space-y-1">
                    <p>
                        ParkWise Smart Parking System • Nền tảng quản lý đỗ xe
                        thông minh
                    </p>
                    <p>
                        Mọi thắc mắc về cước phí xin liên hệ hotline trực ban tại
                        bãi xe.
                    </p>
                </footer>
            </div>
        </main>
    );
}
