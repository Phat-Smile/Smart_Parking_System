"use client";

import { useState, useEffect, useMemo } from "react";
import Link from "next/link";
import { X, Wallet } from "lucide-react";
import {
    createReservationHold,
    confirmReservation,
    type ReservationHoldResponse,
    type ConfirmedReservationDto,
} from "../bookingApi";
import { HoldCountdownTimer } from "./HoldCountdownTimer";
import { BookingPassModal } from "./BookingPassModal";
import { getMyWallet } from "../../wallet/walletApi";
import { ApiError } from "@/shared/errors/api-error";
import { emitLiveToast } from "@/shared/ui/ToastNotificationHub";
import type { ParkingLotDetailView } from "../../ParkingLotDetailExperience";
import type { RegisteredVehicle } from "../../myVehiclesApi";
import { estimateLotBookingPricing } from "../../parkingLotApi";
import { useCustomerLanguage } from "../../i18n/customerLanguage";

interface ParkingLotBookingDialogProps {
    isOpen: boolean;
    onClose: () => void;
    lot: ParkingLotDetailView;
    vehicle: RegisteredVehicle;
    date: string;
    time: string;
    duration: number;
    selectedSpot: string | null;
    depositRequired?: number;
}

export function ParkingLotBookingDialog({
    isOpen,
    onClose,
    lot,
    vehicle,
    date,
    time,
    duration,
    selectedSpot,
    depositRequired,
}: ParkingLotBookingDialogProps) {
    const { copy } = useCustomerLanguage();
    const [bookingHold, setBookingHold] =
        useState<ReservationHoldResponse | null>(null);
    const [bookingConfirmed, setBookingConfirmed] =
        useState<ConfirmedReservationDto | null>(null);
    const [isHolding, setIsHolding] = useState(false);
    const [isConfirming, setIsConfirming] = useState(false);
    const [errorMessage, setErrorMessage] = useState("");
    const [walletBalance, setWalletBalance] = useState<number | null>(null);
    const [isPassModalOpen, setIsPassModalOpen] = useState(false);

    const isPeakHour = useMemo(() => {
        try {
            if (!time) return false;
            const [h, m] = time.split(":").map(Number);
            const mins = h * 60 + m;
            return (
                (mins >= 450 && mins <= 540) || (mins >= 1020 && mins <= 1140)
            );
        } catch {
            return false;
        }
    }, [time]);

    useEffect(() => {
        if (isOpen) {
            getMyWallet()
                .then((w) => setWalletBalance(w.balance))
                .catch(() => setWalletBalance(null));
        }
    }, [isOpen]);

    if (!isOpen) return null;

    async function handleCreateHold() {
        setIsHolding(true);
        setErrorMessage("");
        try {
            const appointmentIso = new Date(`${date}T${time}:00`).toISOString();
            const hold = await createReservationHold({
                lotId: lot.id,
                vehicleId: vehicle.vehicleId,
                plateNumber: vehicle.licensePlate,
                vehicleGroup: "Car",
                reservationType: "Scheduled",
                appointmentTime: appointmentIso,
                durationMinutes: duration * 60,
                prepaidAmount: depositRequired ?? 0,
            });
            setBookingHold(hold);

            // Cập nhật lại số dư ví sau khi giữ chỗ
            try {
                const wallet = await getMyWallet();
                setWalletBalance(wallet.balance);
            } catch {
                // Ignore
            }
        } catch (err: unknown) {
            if (err instanceof ApiError) {
                setErrorMessage(
                    err.problem?.detail ||
                        err.message ||
                        "Could not hold a spot for this time slot.",
                );
            } else if (err instanceof Error) {
                setErrorMessage(err.message);
            } else {
                setErrorMessage(
                    "Something went wrong while holding your spot.",
                );
            }
        } finally {
            setIsHolding(false);
        }
    }

    async function handleConfirmDeposit() {
        if (!bookingHold) return;
        setIsConfirming(true);
        setErrorMessage("");
        try {
            const confirmed = await confirmReservation(
                bookingHold.reservationId,
                {
                    paymentMethod: "Wallet",
                    depositAmount: bookingHold.prepaidAmount,
                },
            );
            setBookingConfirmed(confirmed);
            emitLiveToast(
                copy("Booking successful!", "Đặt chỗ thành công!"),
                copy(
                    `Your Booking Pass is ready${selectedSpot ? ` for space ${selectedSpot}` : ""}.`,
                    `Vé Booking Pass đã được cấp${selectedSpot ? ` cho ô đỗ ${selectedSpot}` : ""}.`,
                ),
                "success",
            );
            setIsPassModalOpen(true);
        } catch (err: unknown) {
            if (err instanceof ApiError) {
                setErrorMessage(
                    err.problem?.detail ||
                        err.message ||
                        "Could not confirm the deposit.",
                );
            } else if (err instanceof Error) {
                setErrorMessage(err.message);
            } else {
                setErrorMessage(
                    "Something went wrong while confirming the deposit.",
                );
            }
        } finally {
            setIsConfirming(false);
        }
    }

    const bookingPricing = estimateLotBookingPricing(
        lot.pricing,
        duration,
        vehicle.vehicleType,
        lot.hourlyPrice,
    );
    const estimatedTotal = bookingPricing
        ? `${bookingPricing.estimatedTotal.toLocaleString("vi-VN")} đ`
        : "Pricing unavailable";
    const actualDepositAmount = bookingHold?.prepaidAmount ?? 0;

    return (
        <>
            {!isPassModalOpen && (
                <div
                    className="lot-detail-dialog-backdrop"
                    onClick={onClose}
                    role="presentation"
                >
                    <div
                        className="lot-detail-dialog"
                        role="dialog"
                        aria-modal="true"
                        aria-labelledby="lot-detail-dialog-title"
                        onClick={(event) => event.stopPropagation()}
                        style={{ maxWidth: "460px" }}
                    >
                        <div
                            style={{
                                display: "flex",
                                justifyContent: "space-between",
                                alignItems: "center",
                            }}
                        >
                            <h2
                                id="lot-detail-dialog-title"
                                style={{ margin: 0 }}
                            >
                                {bookingHold
                                    ? copy(
                                          "Pay booking deposit",
                                          "Thanh toán cọc giữ chỗ",
                                      )
                                    : copy(
                                          "Review booking details",
                                          "Xác nhận thông tin đặt chỗ",
                                      )}
                            </h2>
                            <button
                                type="button"
                                onClick={onClose}
                                style={{
                                    background: "transparent",
                                    border: 0,
                                    cursor: "pointer",
                                    color: "#8790a3",
                                    padding: "4px",
                                }}
                                aria-label={copy("Close", "Đóng")}
                            >
                                <X size={20} />
                            </button>
                        </div>

                        {bookingHold ? (
                            <div
                                className="lot-detail-holding-step"
                                style={{ marginTop: "14px" }}
                            >
                                {bookingHold.tempHoldExpiresAt && (
                                    <HoldCountdownTimer
                                        expiresAt={
                                            bookingHold.tempHoldExpiresAt
                                        }
                                        onExpire={() => {
                                            setErrorMessage(
                                                copy(
                                                    "Your temporary hold has expired. Please book again.",
                                                    "Thời gian giữ chỗ tạm thời đã hết. Vui lòng đặt lại.",
                                                ),
                                            );
                                        }}
                                    />
                                )}

                                <dl style={{ marginTop: "14px" }}>
                                    <div>
                                        <dt>{copy("Hold ID", "Mã giữ chỗ")}</dt>
                                        <dd className="font-mono">
                                            #
                                            {bookingHold.reservationId
                                                .slice(0, 8)
                                                .toUpperCase()}
                                        </dd>
                                    </div>
                                    <div>
                                        <dt>
                                            {copy(
                                                "License plate",
                                                "Biển số xe",
                                            )}
                                        </dt>
                                        <dd>{bookingHold.plateNumber}</dd>
                                    </div>
                                    <div>
                                        <dt>
                                            {copy(
                                                "Entry time",
                                                "Thời gian hẹn",
                                            )}
                                        </dt>
                                        <dd>
                                            {date} · {time}
                                        </dd>
                                    </div>
                                    <div>
                                        <dt>
                                            {copy(
                                                "Booking deposit",
                                                "Tiền cọc giữ chỗ",
                                            )}
                                        </dt>
                                        <dd
                                            style={{
                                                color: "#168844",
                                                fontWeight: "bold",
                                            }}
                                        >
                                            {actualDepositAmount.toLocaleString(
                                                "vi-VN",
                                            )}{" "}
                                            đ
                                        </dd>
                                    </div>
                                    <div>
                                        <dt>
                                            {copy(
                                                "ParkWise Wallet balance",
                                                "Số dư Ví ParkWise",
                                            )}
                                        </dt>
                                        <dd style={{ fontWeight: "bold" }}>
                                            {walletBalance !== null
                                                ? `${walletBalance.toLocaleString("vi-VN")} đ`
                                                : copy(
                                                      "Loading...",
                                                      "Đang tải...",
                                                  )}
                                        </dd>
                                    </div>
                                </dl>

                                {isPeakHour && (
                                    <div
                                        style={{
                                            margin: "12px 0",
                                            padding: "8px 12px",
                                            background:
                                                "rgba(245, 158, 11, 0.12)",
                                            border: "1px solid rgba(245, 158, 11, 0.35)",
                                            borderRadius: "8px",
                                            color: "#d97706",
                                            fontSize: "12px",
                                            display: "flex",
                                            alignItems: "center",
                                            gap: "8px",
                                        }}
                                    >
                                        <span style={{ fontSize: "16px" }}>
                                            🔥
                                        </span>
                                        <div>
                                            <strong
                                                style={{ display: "block" }}
                                            >
                                                {copy(
                                                    "Peak Hours Surge Pricing (+20%)",
                                                    "Áp dụng Giá Động Giờ Cao Điểm (+20%)",
                                                )}
                                            </strong>
                                            <span
                                                style={{
                                                    color: "#b45309",
                                                    fontSize: "11px",
                                                }}
                                            >
                                                {copy(
                                                    "High demand window (07:30-09:00 / 17:00-19:00). Guaranteed spot priority.",
                                                    "Khung giờ cao điểm đô thị (07:30-09:00 / 17:00-19:00). Ưu tiên giữ slot.",
                                                )}
                                            </span>
                                        </div>
                                    </div>
                                )}

                                {errorMessage && (
                                    <p
                                        role="alert"
                                        style={{
                                            marginTop: "12px",
                                            padding: "8px 12px",
                                            borderRadius: "8px",
                                            background: "#fee2e2",
                                            color: "#b91c1c",
                                            fontSize: "13px",
                                        }}
                                    >
                                        {errorMessage}
                                    </p>
                                )}

                                {walletBalance !== null &&
                                walletBalance < actualDepositAmount ? (
                                    <div
                                        style={{
                                            marginTop: "14px",
                                            padding: "12px",
                                            background: "#fef3c7",
                                            borderRadius: "10px",
                                            color: "#92400e",
                                            fontSize: "13px",
                                        }}
                                    >
                                        <p
                                            style={{
                                                margin: 0,
                                                fontWeight: "600",
                                            }}
                                        >
                                            ⚠️{" "}
                                            {copy(
                                                "Insufficient wallet balance (short by",
                                                "Số dư ví không đủ (thiếu",
                                            )}{" "}
                                            {(
                                                actualDepositAmount -
                                                walletBalance
                                            ).toLocaleString("vi-VN")}{" "}
                                            VND).
                                        </p>
                                        <p style={{ margin: "4px 0 8px 0" }}>
                                            {copy(
                                                "Please top up your wallet to complete your booking.",
                                                "Vui lòng nạp thêm tiền để hoàn tất đặt chỗ.",
                                            )}
                                        </p>
                                        <Link
                                            href="/customer/wallet"
                                            target="_blank"
                                            style={{
                                                display: "inline-flex",
                                                alignItems: "center",
                                                gap: "6px",
                                                padding: "6px 12px",
                                                background: "#6246ea",
                                                color: "#fff",
                                                borderRadius: "6px",
                                                textDecoration: "none",
                                                fontWeight: "bold",
                                                fontSize: "12px",
                                            }}
                                        >
                                            <Wallet size={14} />{" "}
                                            {copy(
                                                "Top up wallet",
                                                "Nạp ví ngay",
                                            )}{" "}
                                            →
                                        </Link>
                                    </div>
                                ) : (
                                    <button
                                        type="button"
                                        disabled={isConfirming}
                                        onClick={handleConfirmDeposit}
                                        style={{
                                            width: "100%",
                                            minHeight: "42px",
                                            marginTop: "16px",
                                            borderRadius: "9px",
                                            border: 0,
                                            background: "#6246ea",
                                            color: "#fff",
                                            fontWeight: "800",
                                            cursor: isConfirming
                                                ? "not-allowed"
                                                : "pointer",
                                        }}
                                    >
                                        {isConfirming
                                            ? copy(
                                                  "Confirming deposit...",
                                                  "Đang xác nhận cọc...",
                                              )
                                            : `${copy("Pay", "Trừ ví")} ${actualDepositAmount.toLocaleString("vi-VN")} đ ${copy("from wallet and get pass", "và nhận vé")}`}
                                    </button>
                                )}

                                <button
                                    type="button"
                                    onClick={onClose}
                                    style={{
                                        width: "100%",
                                        minHeight: "38px",
                                        marginTop: "8px",
                                        borderRadius: "9px",
                                        border: "1px solid #ccc",
                                        background: "transparent",
                                        color: "#666",
                                        fontWeight: "600",
                                        cursor: "pointer",
                                    }}
                                >
                                    {copy("Close", "Đóng")}
                                </button>
                            </div>
                        ) : (
                            <div
                                className="lot-detail-review-step"
                                style={{ marginTop: "14px" }}
                            >
                                <dl>
                                    <div>
                                        <dt>
                                            {copy("Parking lot", "Bãi đỗ xe")}
                                        </dt>
                                        <dd>{lot.name}</dd>
                                    </div>
                                    <div>
                                        <dt>{copy("Vehicle", "Xe đăng ký")}</dt>
                                        <dd>{vehicle.licensePlate}</dd>
                                    </div>
                                    <div>
                                        <dt>
                                            {copy(
                                                "Entry time",
                                                "Thời gian hẹn",
                                            )}
                                        </dt>
                                        <dd>
                                            {date} · {time}
                                        </dd>
                                    </div>
                                    <div>
                                        <dt>
                                            {copy("Duration", "Thời lượng")}
                                        </dt>
                                        <dd>
                                            {duration} {copy("hours", "giờ")}
                                        </dd>
                                    </div>
                                    {selectedSpot && (
                                        <div>
                                            <dt>
                                                {copy(
                                                    "Preferred spot",
                                                    "Vị trí ưa thích",
                                                )}
                                            </dt>
                                            <dd>{selectedSpot}</dd>
                                        </div>
                                    )}
                                    <div>
                                        <dt>
                                            {copy(
                                                "Estimated parking fee",
                                                "Ước tính phí đỗ",
                                            )}
                                        </dt>
                                        <dd>{estimatedTotal}</dd>
                                    </div>
                                    <div>
                                        <dt>
                                            {copy(
                                                "Booking deposit",
                                                "Tiền cọc giữ chỗ",
                                            )}
                                        </dt>
                                        <dd
                                            style={{
                                                color: "#657892",
                                                fontWeight: 600,
                                            }}
                                        >
                                            {copy(
                                                "Calculated after holding",
                                                "Tính sau khi giữ chỗ",
                                            )}
                                        </dd>
                                    </div>
                                </dl>

                                {errorMessage && (
                                    <p
                                        role="alert"
                                        style={{
                                            marginTop: "12px",
                                            padding: "8px 12px",
                                            borderRadius: "8px",
                                            background: "#fee2e2",
                                            color: "#b91c1c",
                                            fontSize: "13px",
                                        }}
                                    >
                                        {errorMessage}
                                    </p>
                                )}

                                <button
                                    type="button"
                                    disabled={isHolding}
                                    onClick={handleCreateHold}
                                    style={{
                                        width: "100%",
                                        minHeight: "42px",
                                        marginTop: "18px",
                                        borderRadius: "9px",
                                        border: 0,
                                        background: "#dcfc5c",
                                        color: "#091829",
                                        fontWeight: "850",
                                        cursor: isHolding
                                            ? "not-allowed"
                                            : "pointer",
                                    }}
                                >
                                    {isHolding
                                        ? copy(
                                              "Holding spot...",
                                              "Đang giữ chỗ...",
                                          )
                                        : copy(
                                              "Confirm and hold spot",
                                              "Xác nhận và giữ chỗ",
                                          )}
                                </button>

                                <button
                                    type="button"
                                    onClick={onClose}
                                    style={{
                                        width: "100%",
                                        minHeight: "38px",
                                        marginTop: "8px",
                                        borderRadius: "9px",
                                        border: "1px solid #ccc",
                                        background: "transparent",
                                        color: "#666",
                                        fontWeight: "600",
                                        cursor: "pointer",
                                    }}
                                >
                                    {copy("Close", "Đóng")}
                                </button>
                            </div>
                        )}
                    </div>
                </div>
            )}

            {bookingHold && bookingConfirmed && (
                <BookingPassModal
                    isOpen={isPassModalOpen}
                    onClose={() => {
                        setIsPassModalOpen(false);
                        onClose();
                    }}
                    reservationId={bookingHold.reservationId}
                    lotName={lot.name}
                    lotAddress={lot.address}
                    plateNumber={bookingHold.plateNumber}
                    appointmentTime={bookingHold.appointmentTime}
                    durationMinutes={bookingHold.durationMinutes}
                    qrCodeToken={bookingConfirmed.qrCodeToken}
                    prepaidAmount={
                        bookingConfirmed.prepaidAmount ??
                        bookingHold.prepaidAmount
                    }
                />
            )}
        </>
    );
}
