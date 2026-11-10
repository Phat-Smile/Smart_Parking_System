"use client";

import React, { useEffect, useState } from "react";
import Link from "next/link";
import {
    Calendar,
    Clock,
    Car,
    MapPin,
    ArrowLeft,
    CheckCircle,
    Navigation,
    Printer,
    Share2,
} from "lucide-react";
import QRCode from "react-qr-code";
import { useCustomerLanguage } from "../i18n/customerLanguage";
import { getMyReservations, type ReservationSummaryDto } from "./bookingApi";
import { getApprovedParkingLot, type ParkingLotDetail } from "../parkingLotApi";
import { BookingBadge, mapStatusToBadge } from "./components/BookingBadge";

interface CustomerBookingPassPageProps {
    bookingId: string;
}

export function CustomerBookingPassPage({
    bookingId,
}: CustomerBookingPassPageProps) {
    const { copy, language } = useCustomerLanguage();
    const [reservation, setReservation] =
        useState<ReservationSummaryDto | null>(null);
    const [lot, setLot] = useState<ParkingLotDetail | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const [errorMessage, setErrorMessage] = useState("");
    const [copied, setCopied] = useState(false);

    useEffect(() => {
        let isMounted = true;

        getMyReservations()
            .then(async (reservations) => {
                if (!isMounted) return;
                const found = reservations.find(
                    (r) =>
                        r.reservationId.toLowerCase() ===
                        bookingId.toLowerCase(),
                );

                if (!found) {
                    setErrorMessage(
                        copy(
                            "Booking not found or you do not have permission to view it.",
                            "Không tìm thấy thông tin đặt chỗ hoặc bạn không có quyền truy cập.",
                        ),
                    );
                    setIsLoading(false);
                    return;
                }

                setReservation(found);
                try {
                    const lotDetail = await getApprovedParkingLot(found.lotId);
                    if (isMounted) setLot(lotDetail);
                } catch {
                    // Lot details optional for pass rendering
                } finally {
                    if (isMounted) setIsLoading(false);
                }
            })
            .catch(() => {
                if (isMounted) {
                    setErrorMessage(
                        copy(
                            "Unable to load booking pass. Please try again later.",
                            "Không thể tải thẻ đặt chỗ. Vui lòng thử lại sau.",
                        ),
                    );
                    setIsLoading(false);
                }
            });

        return () => {
            isMounted = false;
        };
    }, [bookingId, copy]);

    const handleCopyShare = async () => {
        if (typeof window !== "undefined") {
            try {
                await navigator.clipboard.writeText(window.location.href);
                setCopied(true);
                setTimeout(() => setCopied(false), 2000);
            } catch {
                // Clipboard fallback
            }
        }
    };

    if (isLoading) {
        return (
            <div className="max-w-md mx-auto py-32 text-center">
                <div className="inline-block animate-spin rounded-full h-12 w-12 border-4 border-slate-700 border-t-[#c4ed43] mb-4" />
                <p className="text-[#8790a3] font-semibold text-sm">
                    {copy("Loading Booking Pass…", "Đang tải thẻ đặt chỗ…")}
                </p>
            </div>
        );
    }

    if (errorMessage || !reservation) {
        return (
            <div className="max-w-md mx-auto py-24 px-4 text-center">
                <div className="bg-red-500/10 border border-red-500/20 text-red-400 p-8 rounded-3xl shadow-xl">
                    <p className="font-bold text-base mb-6">{errorMessage}</p>
                    <Link
                        href="/customer/bookings"
                        className="inline-flex items-center gap-2 px-6 py-3 bg-[#6246ea] text-white rounded-xl text-sm font-bold hover:bg-[#5235d9] transition"
                    >
                        <ArrowLeft size={16} />
                        {copy(
                            "Back to My Bookings",
                            "Quay lại danh sách đặt chỗ",
                        )}
                    </Link>
                </div>
            </div>
        );
    }

    const shortId = `BK-${reservation.reservationId.slice(0, 8).toUpperCase()}`;
    const arrivalDate = new Date(reservation.appointmentTime);
    const dateFormatted = arrivalDate.toLocaleDateString(
        language === "vi" ? "vi-VN" : "en-GB",
        {
            weekday: "long",
            day: "2-digit",
            month: "2-digit",
            year: "numeric",
        },
    );
    const timeFormatted = arrivalDate.toLocaleTimeString("vi-VN", {
        hour: "2-digit",
        minute: "2-digit",
    });

    const formatVnd = (val: number) =>
        `${new Intl.NumberFormat("vi-VN").format(val)} ₫`;

    const qrValue =
        reservation.qrCodeToken ||
        `RES:${reservation.reservationId}:${reservation.plateNumber}`;

    const navigationUrl = lot
        ? `https://www.google.com/maps/dir/?api=1&destination=${lot.latitude},${lot.longitude}`
        : null;

    return (
        <div className="max-w-lg mx-auto py-8 px-4">
            {/* Top Navigation */}
            <div className="flex items-center justify-between mb-6">
                <Link
                    href="/customer/bookings"
                    className="inline-flex items-center gap-2 text-sm font-bold text-[#8790a3] hover:text-white transition"
                >
                    <ArrowLeft size={16} />
                    {copy("My Bookings", "Danh sách đặt chỗ")}
                </Link>
                <div className="flex items-center gap-2">
                    <button
                        type="button"
                        onClick={handleCopyShare}
                        className="p-2 rounded-xl bg-white/5 border border-white/10 text-[#8790a3] hover:text-white transition"
                        title={copy("Share Link", "Chia sẻ")}
                    >
                        <Share2 size={16} />
                    </button>
                    <button
                        type="button"
                        onClick={() => window.print()}
                        className="p-2 rounded-xl bg-white/5 border border-white/10 text-[#8790a3] hover:text-white transition"
                        title={copy("Print Pass", "In thẻ")}
                    >
                        <Printer size={16} />
                    </button>
                </div>
            </div>

            {copied && (
                <div className="mb-4 text-center text-xs font-bold text-emerald-400 bg-emerald-500/10 border border-emerald-500/20 py-2 rounded-xl animate-in fade-in">
                    {copy(
                        "Pass link copied to clipboard!",
                        "Đã sao chép liên kết thẻ đặt chỗ!",
                    )}
                </div>
            )}

            {/* Pass Card */}
            <div className="bg-[#0b0c17] border border-white/15 rounded-[2rem] overflow-hidden shadow-2xl relative">
                {/* Header */}
                <div className="bg-gradient-to-r from-[#6246ea] to-[#7c5cff] p-6 text-white relative">
                    <div className="flex items-center justify-between mb-3">
                        <div className="flex items-center gap-2">
                            <CheckCircle
                                size={20}
                                className="text-emerald-300"
                            />
                            <span className="text-xs font-semibold uppercase tracking-wider text-emerald-200 bg-emerald-500/20 px-2.5 py-0.5 rounded-full border border-emerald-400/30">
                                {copy("Official Pass", "Vé Hợp Lệ")}
                            </span>
                        </div>
                        <BookingBadge
                            status={mapStatusToBadge(reservation.status)}
                        />
                    </div>
                    <h1 className="text-2xl font-black tracking-tight mb-1">
                        {copy("DRIVER BOOKING PASS", "THẺ ĐẶT CHỖ ĐỖ XE")}
                    </h1>
                    <p className="text-white/80 text-xs">
                        {copy("Reservation code", "Mã đặt chỗ")}:{" "}
                        <span className="font-mono font-bold text-white">
                            {shortId}
                        </span>
                    </p>
                </div>

                {/* QR Code Section */}
                <div className="p-6 flex flex-col items-center bg-white/[0.02] border-b border-white/10">
                    <div className="bg-white p-4 rounded-2xl shadow-inner mb-3">
                        <QRCode
                            value={qrValue}
                            size={200}
                            style={{
                                height: "auto",
                                maxWidth: "100%",
                                width: "100%",
                            }}
                            viewBox={`0 0 256 256`}
                        />
                    </div>
                    <p className="text-xs text-[#8790a3] text-center max-w-xs">
                        {copy(
                            "Scan this QR code at the entrance barrier or present to staff",
                            "Quét mã QR này trước camera barrier cổng vào hoặc xuất trình cho nhân viên",
                        )}
                    </p>
                </div>

                {/* Details Section */}
                <div className="p-6 space-y-4 text-white text-sm">
                    {/* Bãi xe */}
                    <div className="flex items-start gap-3">
                        <MapPin
                            size={18}
                            className="text-[#a28aff] shrink-0 mt-0.5"
                        />
                        <div>
                            <div className="text-xs text-[#8790a3]">
                                {copy("Parking Facility", "Bãi đỗ xe")}
                            </div>
                            <div className="font-bold text-base text-white">
                                {lot?.name ||
                                    copy(
                                        "ParkWise Facility",
                                        "Bãi xe ParkWise",
                                    )}
                            </div>
                            {lot?.address && (
                                <div className="text-xs text-[#8790a3] mt-0.5">
                                    {lot.address}
                                </div>
                            )}
                        </div>
                    </div>

                    {/* Biển số & Phương tiện */}
                    <div className="flex items-start gap-3">
                        <Car
                            size={18}
                            className="text-emerald-400 shrink-0 mt-0.5"
                        />
                        <div>
                            <div className="text-xs text-[#8790a3]">
                                {copy(
                                    "Vehicle & License Plate",
                                    "Phương tiện & Biển số",
                                )}
                            </div>
                            <div className="font-mono font-bold text-emerald-400 text-lg">
                                {reservation.plateNumber}
                            </div>
                            <div className="text-xs text-[#8790a3]">
                                {reservation.vehicleGroup === "Car"
                                    ? copy(
                                          "Automobile (Standard Slot)",
                                          "Ô tô (Ô đỗ tiêu chuẩn)",
                                      )
                                    : copy("Motorbike", "Xe máy")}
                            </div>
                        </div>
                    </div>

                    {/* Khung giờ hẹn */}
                    <div className="grid grid-cols-2 gap-3 bg-white/[0.03] border border-white/5 rounded-xl p-3">
                        <div className="flex items-start gap-2">
                            <Calendar
                                size={16}
                                className="text-[#a28aff] shrink-0 mt-0.5"
                            />
                            <div>
                                <div className="text-[11px] text-[#8790a3]">
                                    {copy("Appointment Date", "Ngày hẹn")}
                                </div>
                                <div className="font-semibold text-xs text-white">
                                    {dateFormatted}
                                </div>
                            </div>
                        </div>

                        <div className="flex items-start gap-2">
                            <Clock
                                size={16}
                                className="text-emerald-400 shrink-0 mt-0.5"
                            />
                            <div>
                                <div className="text-[11px] text-[#8790a3]">
                                    {copy("Arrival Time", "Giờ đến")}
                                </div>
                                <div className="font-semibold text-xs text-white">
                                    {timeFormatted}
                                    {reservation.durationMinutes && (
                                        <span className="text-[#8790a3] block text-[10px]">
                                            ({reservation.durationMinutes}{" "}
                                            {copy("minutes", "phút")})
                                        </span>
                                    )}
                                </div>
                            </div>
                        </div>
                    </div>

                    {/* Tiền cọc */}
                    <div className="flex items-center justify-between bg-emerald-500/10 border border-emerald-500/20 rounded-xl px-4 py-3 text-xs">
                        <span className="text-emerald-300 font-medium">
                            {copy(
                                "Prepaid Deposit (Wallet)",
                                "Tiền cọc giữ chỗ đã trả (Ví)",
                            )}
                        </span>
                        <span className="font-bold text-emerald-300 text-sm">
                            {formatVnd(reservation.prepaidAmount)}
                        </span>
                    </div>

                    {/* Navigation Button */}
                    {navigationUrl && (
                        <a
                            href={navigationUrl}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="w-full flex items-center justify-center gap-2 py-3 rounded-xl bg-white/10 hover:bg-white/15 text-white font-bold text-sm border border-white/15 transition mt-2"
                        >
                            <Navigation size={16} className="text-[#c4ed43]" />
                            {copy(
                                "Navigate to Parking Lot",
                                "Dẫn đường tới bãi đỗ",
                            )}
                        </a>
                    )}
                </div>
            </div>
        </div>
    );
}
