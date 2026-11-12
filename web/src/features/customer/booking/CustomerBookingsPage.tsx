"use client";

import { useState, useEffect, useCallback } from "react";
import Link from "next/link";
import { en } from "@/features/customer/i18n/en";
import { vi as viCopy } from "@/features/customer/i18n/vi";
import { useCustomerLanguage } from "@/features/customer/i18n/customerLanguage";
import { BookingBadge, mapStatusToBadge } from "./components/BookingBadge";
import { BookingPassModal } from "./components/BookingPassModal";
import {
    getMyReservations,
    cancelReservation,
    type ReservationSummaryDto,
} from "./bookingApi";
import {
    Plus,
    Calendar,
    MapPin,
    Clock,
    Car,
    QrCode,
    X,
    AlertCircle,
    CheckCircle,
    RefreshCw,
    ExternalLink,
} from "lucide-react";
import { ApiError } from "@/shared/errors/api-error";

type TabFilter = "all" | "active" | "used" | "cancelled";

export default function BookingsPage() {
    const { copy, language } = useCustomerLanguage();
    const dictionary = language === "vi" ? viCopy : en;
    const [reservations, setReservations] = useState<ReservationSummaryDto[]>(
        [],
    );
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [activeTab, setActiveTab] = useState<TabFilter>("all");

    // Cancel modal state
    const [cancelTarget, setCancelTarget] =
        useState<ReservationSummaryDto | null>(null);
    const [cancelReason, setCancelReason] = useState("Change of plans");
    const [isCancelling, setIsCancelling] = useState(false);
    const [cancelError, setCancelError] = useState<string | null>(null);
    const [successMessage, setSuccessMessage] = useState<string | null>(null);

    // QR Pass Modal state
    const [passTarget, setPassTarget] = useState<ReservationSummaryDto | null>(
        null,
    );

    const loadReservations = useCallback(async () => {
        setLoading(true);
        setError(null);
        try {
            const data = await getMyReservations();
            setReservations(data);
        } catch (err: unknown) {
            if (err instanceof ApiError) {
                setError(
                    err.status === 401
                        ? copy(
                              "Sign in to view your bookings.",
                              "Vui lòng đăng nhập để xem đặt chỗ.",
                          )
                        : err.status === 403
                          ? copy(
                                "You do not have access to these bookings.",
                                "Bạn không có quyền xem các đặt chỗ này.",
                            )
                          : copy(
                                "Could not load your bookings. Please try again.",
                                "Không thể tải đặt chỗ. Vui lòng thử lại.",
                            ),
                );
            } else if (err instanceof TypeError) {
                setError(
                    copy(
                        "Could not connect to load your bookings. Please try again later.",
                        "Không thể kết nối để tải đặt chỗ. Vui lòng thử lại sau.",
                    ),
                );
            } else {
                setError(
                    copy(
                        "Something went wrong while loading your bookings.",
                        "Đã xảy ra lỗi khi tải đặt chỗ.",
                    ),
                );
            }
        } finally {
            setLoading(false);
        }
    }, [copy]);

    useEffect(() => {
        let active = true;
        queueMicrotask(() => {
            if (active) void loadReservations();
        });
        return () => {
            active = false;
        };
    }, [loadReservations]);

    const activeCount = reservations.filter((b) => {
        const badge = mapStatusToBadge(b.status);
        return badge === "confirmed" || badge === "holding";
    }).length;

    const filteredReservations = reservations.filter((b) => {
        const badge = mapStatusToBadge(b.status);
        if (activeTab === "active")
            return badge === "confirmed" || badge === "holding";
        if (activeTab === "used") return badge === "used";
        if (activeTab === "cancelled")
            return badge === "cancelled" || badge === "expired";
        return true;
    });

    async function handleConfirmCancel() {
        if (!cancelTarget) return;
        setIsCancelling(true);
        setCancelError(null);
        try {
            const result = await cancelReservation(cancelTarget.reservationId, {
                reason: cancelReason,
            });
            const refund = new Intl.NumberFormat(
                language === "vi" ? "vi-VN" : "en-US",
            ).format(result.refundAmount);
            setSuccessMessage(
                copy(
                    `Booking cancelled. ${refund} VND was returned to your wallet.`,
                    `Đã hủy đặt chỗ. ${refund} VND đã được hoàn vào ví của bạn.`,
                ),
            );
            setCancelTarget(null);
            await loadReservations();
        } catch (err: unknown) {
            if (err instanceof ApiError) {
                setCancelError(
                        err.status === 401
                            ? copy(
                                  "Sign in to cancel this booking.",
                                  "Vui lòng đăng nhập để hủy đặt chỗ này.",
                              )
                            : err.status === 403
                              ? copy(
                                    "You do not have permission to cancel this booking.",
                                    "Bạn không có quyền hủy đặt chỗ này.",
                                )
                              : copy(
                                    "Could not cancel the booking. Please try again.",
                                    "Không thể hủy đặt chỗ. Vui lòng thử lại.",
                                ),
                );
            } else {
                setCancelError(
                        copy(
                            "Something went wrong while cancelling the booking.",
                            "Đã xảy ra lỗi khi hủy đặt chỗ.",
                        ),
                );
            }
        } finally {
            setIsCancelling(false);
        }
    }

    return (
        <div className="animate-in fade-in duration-500 space-y-6">
            {/* Header */}
            <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
                <div>
                    <h1 className="text-2xl font-bold text-white mb-1">
                        {dictionary.bookingsPage.title}
                    </h1>
                    <p className="text-[#8790a3] text-sm">
                        <strong className="text-white">{activeCount}/3</strong>{" "}
                        {dictionary.bookingsPage.activeCount}
                    </p>
                </div>
                <div className="flex items-center gap-3">
                    <button
                        type="button"
                        onClick={loadReservations}
                        className="customer-data-refresh"
                        title={copy("Reload bookings", "Tải lại đặt chỗ")}
                        aria-label={copy("Reload bookings", "Tải lại đặt chỗ")}
                        aria-busy={loading}
                        disabled={loading}
                    >
                        <RefreshCw
                            size={18}
                            className={loading ? "animate-spin" : ""}
                        />
                    </button>
                    <Link
                        href="/search"
                        className="bg-[#6246ea] text-white px-6 py-3 rounded-xl font-bold flex items-center justify-center gap-2 hover:bg-[#7c5cff] transition-colors shadow-glow-purple text-sm"
                    >
                        <Plus size={18} />
                        {dictionary.bookingsPage.newBooking}
                    </Link>
                </div>
            </div>

            {/* Thông báo thành công */}
            {successMessage && (
                <div className="flex items-center justify-between p-4 rounded-xl bg-emerald-500/15 border border-emerald-500/30 text-emerald-300 text-sm">
                    <div className="flex items-center gap-2.5">
                        <CheckCircle size={18} className="shrink-0" />
                        <span>{successMessage}</span>
                    </div>
                    <button
                        type="button"
                        onClick={() => setSuccessMessage(null)}
                        className="text-emerald-400 hover:text-white p-1"
                    >
                        <X size={16} />
                    </button>
                </div>
            )}

            {/* Tab lọc */}
            <div className="customer-bookings-tabs flex gap-2 border-b border-white/10 pb-2 overflow-x-auto">
                <button
                    type="button"
                    onClick={() => setActiveTab("all")}
                    className={`shrink-0 whitespace-nowrap px-4 py-2 rounded-xl text-sm font-semibold transition-colors ${
                        activeTab === "all"
                            ? "bg-[#6246ea] text-white"
                            : "text-[#8790a3] hover:text-white hover:bg-white/5"
                    }`}
                >
                    {copy("All", "Tất cả")} ({reservations.length})
                </button>
                <button
                    type="button"
                    onClick={() => setActiveTab("active")}
                    className={`shrink-0 whitespace-nowrap px-4 py-2 rounded-xl text-sm font-semibold transition-colors ${
                        activeTab === "active"
                            ? "bg-[#6246ea] text-white"
                            : "text-[#8790a3] hover:text-white hover:bg-white/5"
                    }`}
                >
                    {copy("Active", "Đang có hiệu lực")} ({activeCount})
                </button>
                <button
                    type="button"
                    onClick={() => setActiveTab("used")}
                    className={`shrink-0 whitespace-nowrap px-4 py-2 rounded-xl text-sm font-semibold transition-colors ${
                        activeTab === "used"
                            ? "bg-[#6246ea] text-white"
                            : "text-[#8790a3] hover:text-white hover:bg-white/5"
                    }`}
                >
                    {copy("Completed", "Đã hoàn thành")}
                </button>
                <button
                    type="button"
                    onClick={() => setActiveTab("cancelled")}
                    className={`shrink-0 whitespace-nowrap px-4 py-2 rounded-xl text-sm font-semibold transition-colors ${
                        activeTab === "cancelled"
                            ? "bg-[#6246ea] text-white"
                            : "text-[#8790a3] hover:text-white hover:bg-white/5"
                    }`}
                >
                    {copy("Cancelled / Expired", "Đã hủy / Hết hạn")}
                </button>
            </div>

            {/* Trạng thái Loading */}
            {loading && (
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                    {[1, 2, 3].map((i) => (
                        <div
                            key={i}
                            className="bg-[#0b0c17] border border-white/10 rounded-2xl p-6 h-64 animate-pulse flex flex-col justify-between"
                        >
                            <div className="space-y-3">
                                <div className="h-6 w-24 bg-white/10 rounded-md" />
                                <div className="h-5 w-40 bg-white/10 rounded-md" />
                                <div className="h-4 w-52 bg-white/5 rounded-md" />
                            </div>
                            <div className="h-12 w-full bg-white/5 rounded-xl" />
                        </div>
                    ))}
                </div>
            )}

            {/* Trạng thái Lỗi */}
            {!loading && error && (
                <div className="p-6 pb-3 bg-red-500/10 border border-red-500/30 rounded-2xl text-center space-y-3" role="alert">
                    <AlertCircle size={32} className="mx-auto text-red-400" />
                    <p className="text-red-300 text-sm font-medium">{error}</p>
                    <button
                        type="button"
                        onClick={loadReservations}
                        className="px-4 py-2 bg-white/10 hover:bg-white/15 text-white rounded-xl text-xs font-semibold transition-colors"
                    >
                        {copy("Try again", "Thử lại")}
                    </button>
                </div>
            )}

            {/* Trạng thái Trống (Empty) */}
            {!loading && !error && filteredReservations.length === 0 && (
                <div className="bg-[#0b0c17] border border-white/10 rounded-2xl p-12 text-center space-y-4 shadow-card-light">
                    <div className="w-16 h-16 rounded-full bg-[#6246ea]/10 border border-[#6246ea]/20 flex items-center justify-center mx-auto text-[#a28aff]">
                        <Calendar size={28} />
                    </div>
                    <div>
                        <h3 className="text-lg font-bold text-white mb-1">
                            {copy(
                                "No bookings yet",
                                "Chưa có lượt đặt chỗ nào",
                            )}
                        </h3>
                        <p className="text-[#8790a3] text-sm max-w-md mx-auto">
                            {activeTab === "active"
                                ? copy(
                                      "You have no active holds or confirmed bookings.",
                                      "Bạn không có lượt giữ chỗ hoặc đặt chỗ nào đang hiệu lực.",
                                  )
                                : copy(
                                      "Find a parking lot and book ahead to secure a spot.",
                                      "Tìm bãi đỗ và đặt trước để giữ chỗ.",
                                  )}
                        </p>
                    </div>
                    <Link
                        href="/search"
                        className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl bg-[#6246ea] hover:bg-[#7c5cff] text-white font-bold text-sm transition-colors shadow-glow-purple"
                    >
                        <Plus size={16} /> {copy("Find parking", "Tìm bãi đỗ")}
                    </Link>
                </div>
            )}

            {/* Danh sách Đặt chỗ Thật */}
            {!loading && !error && filteredReservations.length > 0 && (
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                    {filteredReservations.map((booking) => {
                        const badgeStatus = mapStatusToBadge(booking.status);
                        const shortId = `BK-${booking.reservationId.slice(0, 8).toUpperCase()}`;
                        const appointmentDate = new Date(
                            booking.appointmentTime,
                        );
                        const dateStr = appointmentDate.toLocaleDateString(
                            language === "vi" ? "vi-VN" : "en-GB",
                            {
                                day: "2-digit",
                                month: "2-digit",
                                year: "numeric",
                            },
                        );
                        const timeStr = appointmentDate.toLocaleTimeString(
                            language === "vi" ? "vi-VN" : "en-GB",
                            {
                                hour: "2-digit",
                                minute: "2-digit",
                            },
                        );

                        return (
                            <div
                                key={booking.reservationId}
                                className="bg-[#0b0c17] border border-white/10 rounded-2xl p-6 shadow-card-light hover:border-white/20 transition-colors flex flex-col h-full"
                            >
                                <div className="flex justify-between items-start mb-4">
                                    <BookingBadge status={badgeStatus} />
                                    <span className="text-[#8790a3] text-xs font-mono">
                                        {shortId}
                                    </span>
                                </div>

                                <h3 className="text-lg font-bold text-white mb-1 truncate">
                                    {copy("Parking lot", "Bãi đỗ xe")} #
                                    {booking.lotId.slice(0, 8)}
                                </h3>
                                <p className="text-[#8790a3] text-sm flex items-center gap-2 mb-5 truncate">
                                    <MapPin
                                        size={14}
                                        className="shrink-0 text-[#6246ea]"
                                    />
                                    <span>ParkWise Smart Lot</span>
                                </p>

                                <div className="mt-auto space-y-2.5 bg-[#101426] rounded-xl p-4 border border-white/5 text-sm">
                                    <div className="flex justify-between">
                                        <span className="text-[#8790a3] flex items-center gap-2">
                                            <Car size={14} />{" "}
                                            {copy(
                                                "License plate",
                                                "Biển số xe",
                                            )}
                                        </span>
                                        <span className="text-white font-mono font-bold">
                                            {booking.plateNumber}
                                        </span>
                                    </div>
                                    <div className="flex justify-between">
                                        <span className="text-[#8790a3] flex items-center gap-2">
                                            <Calendar size={14} />{" "}
                                            {copy("Entry date", "Ngày hẹn")}
                                        </span>
                                        <span className="text-white font-medium">
                                            {dateStr}
                                        </span>
                                    </div>
                                    <div className="flex justify-between">
                                        <span className="text-[#8790a3] flex items-center gap-2">
                                            <Clock size={14} />{" "}
                                            {copy("Entry time", "Giờ hẹn")}
                                        </span>
                                        <span className="text-white font-medium">
                                            {timeStr}{" "}
                                            {booking.durationMinutes
                                                ? `(${booking.durationMinutes}p)`
                                                : ""}
                                        </span>
                                    </div>
                                    <div className="flex justify-between border-t border-white/5 pt-2">
                                        <span className="text-[#8790a3]">
                                            {copy("Deposit", "Tiền cọc")}
                                        </span>
                                        <span className="text-emerald-400 font-bold">
                                            {booking.prepaidAmount.toLocaleString(
                                                "vi-VN",
                                            )}{" "}
                                            đ
                                        </span>
                                    </div>
                                </div>

                                <div className="mt-4 flex gap-2">
                                    {/* Nút Xem Vé / QR Code */}
                                    {badgeStatus === "confirmed" &&
                                        booking.qrCodeToken && (
                                            <>
                                                <button
                                                    type="button"
                                                    onClick={() =>
                                                        setPassTarget(booking)
                                                    }
                                                    className="flex-1 py-2.5 px-3 bg-[#6246ea]/20 hover:bg-[#6246ea]/30 text-[#a28aff] border border-[#6246ea]/30 rounded-xl font-bold flex items-center justify-center gap-1.5 transition-colors text-xs"
                                                >
                                                    <QrCode size={15} />{" "}
                                                    {copy(
                                                        "View pass / QR",
                                                        "Xem vé / QR",
                                                    )}
                                                </button>
                                                <Link
                                                    href={`/customer/bookings/${booking.reservationId}`}
                                                    className="p-2.5 bg-white/5 hover:bg-white/10 text-[#8790a3] hover:text-white border border-white/10 rounded-xl transition-colors flex items-center justify-center"
                                                    title={copy(
                                                        "Open full-screen pass",
                                                        "Mở toàn màn hình",
                                                    )}
                                                >
                                                    <ExternalLink size={15} />
                                                </Link>
                                            </>
                                        )}

                                    {/* Nút Cancel booking */}
                                    {(badgeStatus === "confirmed" ||
                                        badgeStatus === "holding") && (
                                        <button
                                            type="button"
                                            onClick={() => {
                                                setCancelTarget(booking);
                                                setCancelReason(
                                                    "Change of plans",
                                                );
                                                setCancelError(null);
                                            }}
                                            className="flex-1 py-2.5 px-3 border border-[#ff9388]/30 text-[#ff9388] rounded-xl font-bold hover:bg-[#ff9388]/10 transition-colors text-xs"
                                        >
                                            {copy(
                                                "Cancel booking",
                                                "Hủy đặt chỗ",
                                            )}
                                        </button>
                                    )}
                                </div>
                            </div>
                        );
                    })}
                </div>
            )}

            {/* Modal Xác nhận Hủy Đặt Chỗ */}
            {cancelTarget && (
                <div
                    className="fixed inset-0 bg-black/80 backdrop-blur-sm flex items-center justify-center z-[110] p-4"
                    role="dialog"
                    aria-modal="true"
                    aria-labelledby="cancel-booking-title"
                >
                    <div className="bg-[#0b0c17] border border-white/10 rounded-[2rem] p-6 max-w-md w-full relative shadow-card-dark animate-in fade-in zoom-in duration-200">
                        <button
                            type="button"
                            onClick={() => setCancelTarget(null)}
                            className="absolute top-6 right-6 text-[#8790a3] hover:text-white transition-colors"
                        >
                            <X size={20} />
                        </button>

                        <h2
                            id="cancel-booking-title"
                            className="text-xl font-bold text-white mb-2"
                        >
                            {copy(
                                "Confirm cancellation",
                                "Xác nhận hủy đặt chỗ",
                            )}
                        </h2>
                        <p className="text-[#8790a3] text-sm mb-4">
                            {copy("Booking ID:", "Mã đặt chỗ:")}{" "}
                            <strong className="text-white font-mono">
                                #
                                {cancelTarget.reservationId
                                    .slice(0, 8)
                                    .toUpperCase()}
                            </strong>{" "}
                            ({cancelTarget.plateNumber}).{" "}
                            {copy("Deposit", "Tiền cọc")}{" "}
                            <strong className="text-emerald-400">
                                {cancelTarget.prepaidAmount.toLocaleString(
                                    "vi-VN",
                                )}{" "}
                                đ
                            </strong>{" "}
                            {copy(
                                "will be refunded to your ParkWise Wallet.",
                                "sẽ được hoàn về Ví ParkWise của bạn.",
                            )}
                        </p>

                        <div className="space-y-4">
                            <div>
                                <label className="block text-sm text-[#8790a3] mb-1.5">
                                    {copy(
                                        "Reason for cancellation",
                                        "Lý do hủy đặt chỗ",
                                    )}
                                </label>
                                <select
                                    value={cancelReason}
                                    onChange={(e) =>
                                        setCancelReason(e.target.value)
                                    }
                                    className="w-full bg-[#101426] border border-white/10 rounded-xl px-4 py-3 text-white outline-none focus:border-[#6246ea] transition-colors text-sm"
                                >
                                    <option value="Change of plans">
                                        {copy(
                                            "Change of plans",
                                            "Thay đổi lịch trình",
                                        )}
                                    </option>
                                    <option value="Found another parking spot">
                                        {copy(
                                            "Found another parking spot",
                                            "Đã tìm được chỗ đỗ khác",
                                        )}
                                    </option>
                                    <option value="Wait time is too long">
                                        {copy(
                                            "Wait time is too long",
                                            "Thời gian chờ quá lâu",
                                        )}
                                    </option>
                                    <option value="Other personal reason">
                                        {copy(
                                            "Other personal reason",
                                            "Lý do cá nhân khác",
                                        )}
                                    </option>
                                </select>
                            </div>

                            {cancelError && (
                                <p className="text-red-400 text-xs p-3 bg-red-500/10 rounded-xl border border-red-500/20">
                                    {cancelError}
                                </p>
                            )}

                            <div className="flex gap-3 pt-2">
                                <button
                                    type="button"
                                    disabled={isCancelling}
                                    onClick={handleConfirmCancel}
                                    className="flex-1 py-3 px-4 rounded-xl font-bold text-center bg-red-600 hover:bg-red-500 text-white transition-colors text-sm disabled:opacity-50"
                                >
                                    {isCancelling
                                        ? copy("Cancelling...", "Đang hủy...")
                                        : copy(
                                              "Cancel and refund deposit",
                                              "Hủy và hoàn tiền cọc",
                                          )}
                                </button>
                                <button
                                    type="button"
                                    onClick={() => setCancelTarget(null)}
                                    className="py-3 px-5 rounded-xl font-semibold bg-white/10 hover:bg-white/15 text-white transition-colors text-sm"
                                >
                                    {copy("Keep booking", "Giữ đặt chỗ")}
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            )}

            {/* Modal Hiển thị Vé Booking Pass & QR Code */}
            {passTarget && passTarget.qrCodeToken && (
                <BookingPassModal
                    isOpen={Boolean(passTarget)}
                    onClose={() => setPassTarget(null)}
                    reservationId={passTarget.reservationId}
                    lotName={`${copy("Parking lot", "Bãi đỗ xe")} #${passTarget.lotId.slice(0, 8)}`}
                    plateNumber={passTarget.plateNumber}
                    appointmentTime={passTarget.appointmentTime}
                    durationMinutes={passTarget.durationMinutes}
                    qrCodeToken={passTarget.qrCodeToken}
                    prepaidAmount={passTarget.prepaidAmount}
                />
            )}
        </div>
    );
}
