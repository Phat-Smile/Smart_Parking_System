"use client";

import {
    X,
    CheckCircle,
    Calendar,
    Clock,
    Car,
    MapPin,
    QrCode as QrIcon,
} from "lucide-react";
import QRCode from "react-qr-code";
import Link from "next/link";
import { useCustomerLanguage } from "../../i18n/customerLanguage";

interface BookingPassModalProps {
    isOpen: boolean;
    onClose: () => void;
    reservationId: string;
    lotName: string;
    lotAddress?: string;
    plateNumber: string;
    appointmentTime: string;
    durationMinutes?: number | null;
    qrCodeToken: string;
    prepaidAmount: number;
}

export function BookingPassModal({
    isOpen,
    onClose,
    reservationId,
    lotName,
    lotAddress,
    plateNumber,
    appointmentTime,
    durationMinutes,
    qrCodeToken,
    prepaidAmount,
}: BookingPassModalProps) {
    const { copy, language } = useCustomerLanguage();
    if (!isOpen) return null;

    const shortId = `BK-${reservationId.slice(0, 8).toUpperCase()}`;
    const arrivalDate = new Date(appointmentTime);
    const dateFormatted = arrivalDate.toLocaleDateString(language === "vi" ? "vi-VN" : "en-GB", {
        weekday: "short",
        day: "2-digit",
        month: "2-digit",
        year: "numeric",
    });
    const timeFormatted = arrivalDate.toLocaleTimeString("vi-VN", {
        hour: "2-digit",
        minute: "2-digit",
    });

    return (
        <div
            className="fixed inset-0 bg-black/85 backdrop-blur-md flex items-center justify-center z-[110] p-4 animate-in fade-in duration-200"
            role="dialog"
            aria-modal="true"
            aria-labelledby="booking-pass-title"
        >
            <div className="bg-[#0b0c17] border border-white/15 rounded-[2rem] max-w-lg w-full overflow-hidden shadow-2xl relative">
                {/* Header vé */}
                <div className="bg-gradient-to-r from-[#6246ea] to-[#7c5cff] p-6 text-white relative">
                    <button
                        type="button"
                        onClick={onClose}
                        className="absolute top-5 right-5 text-white/80 hover:text-white transition-colors p-1"
                        aria-label={copy("Close", "Đóng")}
                    >
                        <X size={22} />
                    </button>
                    <div className="flex items-center gap-2 mb-2">
                        <CheckCircle size={20} className="text-emerald-300" />
                        <span className="text-xs font-semibold uppercase tracking-wider text-emerald-200 bg-emerald-500/20 px-2.5 py-0.5 rounded-full border border-emerald-400/30">
                            {copy("Booking confirmed", "Đặt chỗ thành công")}
                        </span>
                    </div>
                    <h2
                        id="booking-pass-title"
                        className="text-2xl font-black tracking-tight"
                    >
                        {copy("Booking Pass", "Vé đặt chỗ")}
                    </h2>
                    <p className="text-white/80 text-sm mt-0.5 font-mono">
                        {copy("Pass ID:", "Mã vé:")}{" "}
                        <strong className="text-white font-bold">
                            {shortId}
                        </strong>
                    </p>
                </div>

                <div className="p-6 space-y-6">
                    {/* Khu vực mã QR */}
                    <div className="flex flex-col items-center justify-center p-5 bg-white rounded-2xl shadow-inner text-center">
                        <div className="p-2 bg-white rounded-xl">
                            <QRCode
                                value={qrCodeToken}
                                size={160}
                                style={{
                                    height: "auto",
                                    maxWidth: "100%",
                                    width: "100%",
                                }}
                                viewBox="0 0 160 160"
                            />
                        </div>
                        <p className="text-gray-600 text-xs mt-3 flex items-center gap-1.5 font-medium">
                            <QrIcon size={14} /> {copy("Scan this code at the gate or show it to the attendant when entering", "Quét mã tại cổng hoặc xuất trình cho nhân viên khi vào bãi")}
                        </p>
                    </div>

                    {/* Thông tin chi tiết */}
                    <div className="bg-[#101426] border border-white/10 rounded-2xl p-4.5 space-y-3 text-sm">
                        <div className="flex justify-between items-start gap-4">
                            <span className="text-[#8790a3] flex items-center gap-2 shrink-0">
                                <MapPin size={15} /> {copy("Parking lot", "Bãi đỗ xe")}
                            </span>
                            <div className="text-right">
                                <span className="text-white font-semibold block">
                                    {lotName}
                                </span>
                                {lotAddress && (
                                    <span className="text-xs text-[#8790a3] line-clamp-1">
                                        {lotAddress}
                                    </span>
                                )}
                            </div>
                        </div>

                        <div className="flex justify-between items-center">
                            <span className="text-[#8790a3] flex items-center gap-2">
                                <Car size={15} /> {copy("License plate", "Biển số xe")}
                            </span>
                            <span className="text-white font-mono font-bold text-base px-2.5 py-0.5 rounded bg-white/5 border border-white/10">
                                {plateNumber}
                            </span>
                        </div>

                        <div className="flex justify-between items-center">
                            <span className="text-[#8790a3] flex items-center gap-2">
                                <Calendar size={15} /> {copy("Entry date", "Ngày hẹn")}
                            </span>
                            <span className="text-white font-medium">
                                {dateFormatted}
                            </span>
                        </div>

                        <div className="flex justify-between items-center">
                            <span className="text-[#8790a3] flex items-center gap-2">
                                <Clock size={15} /> {copy("Entry time / Duration", "Giờ hẹn / Thời lượng")}
                            </span>
                            <span className="text-white font-medium">
                                {timeFormatted}{" "}
                                {durationMinutes
                                    ? `(${durationMinutes} ${copy("minutes", "phút")})`
                                    : ""}
                            </span>
                        </div>

                        <div className="border-t border-white/10 pt-3 flex justify-between items-center">
                            <span className="text-[#8790a3]">
                                {copy("Deposit paid", "Tiền cọc đã thanh toán")}
                            </span>
                            <span className="text-emerald-400 font-bold text-base">
                                {prepaidAmount.toLocaleString("vi-VN")} đ
                            </span>
                        </div>
                    </div>

                    {/* Actions */}
                    <div className="flex gap-3">
                        <Link
                            href="/customer/bookings"
                            className="flex-1 py-3 px-4 rounded-xl font-bold text-center bg-[#6246ea] hover:bg-[#7c5cff] text-white transition-colors text-sm shadow-glow-purple"
                            onClick={onClose}
                        >
                            {copy("Manage my bookings", "Quản lý đặt chỗ")}
                        </Link>
                        <button
                            type="button"
                            onClick={onClose}
                            className="py-3 px-5 rounded-xl font-semibold bg-white/10 hover:bg-white/15 text-white transition-colors text-sm"
                        >
                            {copy("Close", "Đóng")}
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
}
