"use client";

import { useState, useEffect } from "react";
import Link from "next/link";
import { X, AlertCircle, Wallet } from "lucide-react";
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
import type { ParkingLotDetailView } from "../../ParkingLotDetailExperience";
import type { RegisteredVehicle } from "../../myVehiclesApi";
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
    depositRequired = 50000,
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
                prepaidAmount: depositRequired,
            });
            const normalizedHold: ReservationHoldResponse = {
                ...hold,
                tempHoldExpiresAt:
                    hold.tempHoldExpiresAt ||
                    new Date(Date.now() + 15 * 60 * 1000).toISOString(),
            };
            setBookingHold(normalizedHold);

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
                    depositAmount: depositRequired,
                },
            );
            setBookingConfirmed(confirmed);
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

    const estimatedTotal =
        lot.hourlyPrice === undefined
            ? "Pricing unavailable"
            : `${(lot.hourlyPrice * duration).toLocaleString("vi-VN")} đ`;

    return (
        <>
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
                        <h2 id="lot-detail-dialog-title" style={{ margin: 0 }}>
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
                            <HoldCountdownTimer
                                expiresAt={bookingHold.tempHoldExpiresAt || ""}
                                onExpire={() => {
                                    setErrorMessage(
                                        copy(
                                            "Your 15-minute hold has expired. Please book again.",
                                            "Thời gian giữ chỗ 15 phút đã hết. Vui lòng đặt lại.",
                                        ),
                                    );
                                }}
                            />

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
                                        {copy("License plate", "Biển số xe")}
                                    </dt>
                                    <dd>{bookingHold.plateNumber}</dd>
                                </div>
                                <div>
                                    <dt>
                                        {copy("Entry time", "Thời gian hẹn")}
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
                                        {depositRequired.toLocaleString(
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
                                            : copy("Loading...", "Đang tải...")}
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

                            {walletBalance !== null &&
                            walletBalance < depositRequired ? (
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
                                    <p style={{ margin: 0, fontWeight: "600" }}>
                                        ⚠️{" "}
                                        {copy(
                                            "Insufficient wallet balance (short by",
                                            "Số dư ví không đủ (thiếu",
                                        )}{" "}
                                        {(
                                            depositRequired - walletBalance
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
                                        {copy("Top up wallet", "Nạp ví ngay")} →
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
                                        : `${copy("Pay", "Trừ ví")} ${depositRequired.toLocaleString("vi-VN")} đ ${copy("from wallet and get pass", "và nhận vé")}`}
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
                                    <dt>{copy("Parking lot", "Bãi đỗ xe")}</dt>
                                    <dd>{lot.name}</dd>
                                </div>
                                <div>
                                    <dt>{copy("Vehicle", "Xe đăng ký")}</dt>
                                    <dd>{vehicle.licensePlate}</dd>
                                </div>
                                <div>
                                    <dt>
                                        {copy("Entry time", "Thời gian hẹn")}
                                    </dt>
                                    <dd>
                                        {date} · {time}
                                    </dd>
                                </div>
                                <div>
                                    <dt>{copy("Duration", "Thời lượng")}</dt>
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
                                            color: "#168844",
                                            fontWeight: "bold",
                                        }}
                                    >
                                        {depositRequired.toLocaleString(
                                            "vi-VN",
                                        )}{" "}
                                        đ
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
                                    ? copy("Holding spot...", "Đang giữ chỗ...")
                                    : copy(
                                          "Confirm and hold for 15 minutes",
                                          "Xác nhận và giữ chỗ 15 phút",
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
                    prepaidAmount={depositRequired}
                />
            )}
        </>
    );
}
