"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";
import { Printer } from "lucide-react";
import { CustomerIcon } from "./CustomerShell";
import { useCustomerLanguage } from "./i18n/customerLanguage";
import { getParkingFeeReceiptApi, type ParkingFeeReceipt } from "./receiptApi";
import "./receipt.css";

interface CustomerReceiptPageProps {
    sessionId: string;
}

export function CustomerReceiptPage({ sessionId }: CustomerReceiptPageProps) {
    const { copy, language } = useCustomerLanguage();
    const [receipt, setReceipt] = useState<ParkingFeeReceipt | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const [errorMessage, setErrorMessage] = useState("");

    useEffect(() => {
        let isMounted = true;

        getParkingFeeReceiptApi(sessionId)
            .then((data) => {
                if (isMounted) {
                    setReceipt(data);
                    setIsLoading(false);
                }
            })
            .catch(() => {
                if (isMounted) {
                    setErrorMessage(
                        copy(
                            "Unable to load electronic receipt. Please check session ID or try again later.",
                            "Không thể tải hóa đơn điện tử. Vui lòng kiểm tra mã phiên hoặc thử lại sau.",
                        ),
                    );
                    setIsLoading(false);
                }
            });

        return () => {
            isMounted = false;
        };
    }, [sessionId, copy]);

    const formatVnd = (amount: number) =>
        `${new Intl.NumberFormat(language === "vi" ? "vi-VN" : "en-US").format(amount)} ₫`;

    const formatDateTime = (isoString?: string | null) => {
        if (!isoString) return "—";
        const date = new Date(isoString);
        return date.toLocaleString(language === "vi" ? "vi-VN" : "en-GB", {
            day: "2-digit",
            month: "2-digit",
            year: "numeric",
            hour: "2-digit",
            minute: "2-digit",
            second: "2-digit",
        });
    };

    if (isLoading) {
        return (
            <div className="receipt-container text-center py-24">
                <div className="inline-block animate-spin rounded-full h-10 w-10 border-4 border-slate-300 border-t-emerald-500 mb-4" />
                <p className="text-slate-500 font-semibold text-sm">
                    {copy(
                        "Loading electronic receipt…",
                        "Đang tải hóa đơn điện tử…",
                    )}
                </p>
            </div>
        );
    }

    if (errorMessage || !receipt) {
        return (
            <div className="receipt-container py-16 text-center">
                <div className="bg-red-50 border border-red-200 text-red-700 px-6 py-8 rounded-2xl max-w-md mx-auto shadow-sm">
                    <p className="font-bold text-base mb-4">{errorMessage}</p>
                    <Link
                        href="/customer/parking-history"
                        className="inline-flex items-center gap-2 px-5 py-2.5 bg-slate-900 text-white rounded-xl text-sm font-bold hover:bg-slate-800 transition"
                    >
                        <CustomerIcon name="arrow" size={16} />
                        {copy(
                            "Back to parking history",
                            "Quay lại lịch sử gửi xe",
                        )}
                    </Link>
                </div>
            </div>
        );
    }

    const shortReceiptCode = `E-RCPT-${receipt.receiptId.slice(0, 8).toUpperCase()}`;

    return (
        <div className="receipt-container">
            <div className="receipt-card">
                {/* Header */}
                <header className="receipt-header">
                    <div className="receipt-brand-badge">
                        <span>⚡ PARKWAISE PARKING RECEIPT</span>
                    </div>
                    <h1 className="receipt-title">
                        {copy(
                            "ELECTRONIC PARKING RECEIPT",
                            "HÓA ĐƠN ĐIỆN TỬ DỊCH VỤ TRÔNG GIỮ XE",
                        )}
                    </h1>
                    <p className="receipt-subtitle">
                        {copy("Receipt No.", "Mã hóa đơn:")}{" "}
                        <b>{shortReceiptCode}</b>
                    </p>
                </header>

                {/* Body */}
                <div className="receipt-body">
                    {/* Parking info grid */}
                    <div className="receipt-grid">
                        <div className="receipt-field">
                            <span className="receipt-label">
                                {copy("Parking Facility", "Bãi đỗ xe")}
                            </span>
                            <span className="receipt-value">
                                {receipt.parkingLotName}
                            </span>
                        </div>
                        <div className="receipt-field">
                            <span className="receipt-label">
                                {copy("License Plate", "Biển số xe")}
                            </span>
                            <span className="receipt-value font-mono tracking-wide">
                                {receipt.licensePlate} ({receipt.vehicleType})
                            </span>
                        </div>
                        <div className="receipt-field">
                            <span className="receipt-label">
                                {copy("Check-In Time", "Giờ vào")}
                            </span>
                            <span className="receipt-value text-slate-700">
                                {formatDateTime(receipt.checkInTimeUtc)}
                            </span>
                        </div>
                        <div className="receipt-field">
                            <span className="receipt-label">
                                {copy("Check-Out Time", "Giờ ra")}
                            </span>
                            <span className="receipt-value text-slate-700">
                                {formatDateTime(receipt.checkOutTimeUtc)}
                            </span>
                        </div>
                        <div className="receipt-field">
                            <span className="receipt-label">
                                {copy("Total Duration", "Thời lượng đỗ")}
                            </span>
                            <span className="receipt-value">
                                {receipt.totalDurationHours.toFixed(1)}{" "}
                                {copy("hours", "giờ")}
                            </span>
                        </div>
                        <div className="receipt-field">
                            <span className="receipt-label">
                                {copy("Payment Method", "Phương thức")}
                            </span>
                            <span className="receipt-value">
                                {receipt.paymentMethod === "Wallet"
                                    ? copy(
                                          "ParkWise Wallet",
                                          "Ví điện tử ParkWise",
                                      )
                                    : copy(
                                          "Cash / Counter",
                                          "Tiền mặt tại bãi",
                                      )}
                            </span>
                        </div>
                    </div>

                    {/* Breakdown table */}
                    <div className="receipt-table">
                        <div className="receipt-row">
                            <span className="receipt-row-title">
                                {copy(
                                    "Standard Parking Fee",
                                    "Cước đỗ xe tiêu chuẩn",
                                )}
                            </span>
                            <span className="receipt-row-amount">
                                {formatVnd(receipt.standardFee)}
                            </span>
                        </div>

                        {receipt.surgeMultiplier > 1.0 && (
                            <div className="receipt-row">
                                <span className="receipt-row-title">
                                    {copy(
                                        "Peak Surge Rate",
                                        "Phụ thu cao điểm",
                                    )}{" "}
                                    <span className="text-xs bg-amber-100 text-amber-800 font-bold px-1.5 py-0.5 rounded ml-1">
                                        x{receipt.surgeMultiplier.toFixed(1)}
                                    </span>
                                </span>
                                <span className="receipt-row-amount text-amber-700">
                                    {formatVnd(
                                        receipt.standardFee *
                                            (receipt.surgeMultiplier - 1.0),
                                    )}
                                </span>
                            </div>
                        )}

                        {receipt.prepaidDepositOffset > 0 && (
                            <div className="receipt-row">
                                <span className="receipt-row-title receipt-row-discount">
                                    {copy(
                                        "Prepaid Reservation Deposit (Offset)",
                                        "Cấn trừ cọc đặt chỗ trước",
                                    )}
                                </span>
                                <span className="receipt-row-amount receipt-row-discount">
                                    - {formatVnd(receipt.prepaidDepositOffset)}
                                </span>
                            </div>
                        )}
                    </div>

                    {/* Total Amount Box */}
                    <div className="receipt-total-box">
                        <span className="receipt-total-label">
                            {copy("TOTAL NET CHARGE", "TỔNG TIỀN THỰC THU")}
                        </span>
                        <span className="receipt-total-amount">
                            {formatVnd(receipt.netPaidAmount)}
                        </span>
                    </div>

                    {/* Audit meta */}
                    <div className="receipt-meta">
                        <div>
                            <b>
                                {copy("Payment Time", "Thời gian thanh toán")}:
                            </b>{" "}
                            {formatDateTime(receipt.paidAtUtc)}
                        </div>
                        {receipt.ledgerTransactionReference && (
                            <div>
                                <b>
                                    {copy(
                                        "Ledger Reference",
                                        "Mã đối soát sổ cái",
                                    )}
                                    :
                                </b>{" "}
                                <span className="font-mono text-xs">
                                    {receipt.ledgerTransactionReference}
                                </span>
                            </div>
                        )}
                        <div className="mt-2 text-slate-400 text-[11px]">
                            {copy(
                                "This is a valid electronic parking receipt generated by ParkWise Platform. Complies with Ministry of Finance electronic invoicing standards.",
                                "Hóa đơn điện tử hợp lệ được khởi tạo tự động từ Nền tảng Đỗ xe Thông minh ParkWise.",
                            )}
                        </div>
                    </div>

                    {/* Interactive buttons */}
                    <div className="receipt-actions">
                        <Link
                            href="/customer/parking-history"
                            className="inline-flex items-center gap-2 px-4 py-2.5 rounded-xl border border-slate-300 text-slate-700 font-bold text-sm hover:bg-slate-50 transition"
                        >
                            <CustomerIcon name="arrow" size={15} />
                            {copy("Back to History", "Lịch sử gửi xe")}
                        </Link>
                        <button
                            type="button"
                            onClick={() => window.print()}
                            className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl bg-slate-900 text-white font-bold text-sm hover:bg-slate-800 transition shadow-sm"
                        >
                            <Printer size={16} />
                            {copy("Print / Save PDF", "In / Lưu Hóa Đơn PDF")}
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
}
