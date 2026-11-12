"use client";

import React, { useState } from "react";
import QRCode from "react-qr-code";
import {
    QrCode,
    Printer,
    Copy,
    Check,
    CheckCircle2,
    X,
    ShieldCheck,
    PhoneCall,
    Car,
    Bike,
    Clock,
    Hash,
} from "lucide-react";
import { Barcode1D } from "@/shared/ui/Barcode1D";
import type { PaymentStatus, SessionStatus, VehicleType } from "./types";
import { useLanguage } from "@/shared/i18n/LanguageContext";

export function formatVnd(amount: number): string {
    return `${amount.toLocaleString("vi-VN")} ₫`;
}

export function formatTime(iso: string): string {
    try {
        const d = new Date(iso);
        return d.toLocaleTimeString("vi-VN", {
            hour: "2-digit",
            minute: "2-digit",
            second: "2-digit",
        });
    } catch {
        return iso;
    }
}

export function formatDateTime(iso: string): string {
    try {
        const d = new Date(iso);
        return `${d.toLocaleDateString("vi-VN")} ${d.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })}`;
    } catch {
        return iso;
    }
}

export function FormattedTime({
    iso,
    dateTime = false,
}: {
    iso: string;
    dateTime?: boolean;
}) {
    return (
        <span suppressHydrationWarning>
            {dateTime ? formatDateTime(iso) : formatTime(iso)}
        </span>
    );
}

export function PlateBadge({
    plate,
    size = "normal",
}: {
    plate: string;
    size?: "normal" | "large";
}) {
    if (!plate || !plate.trim()) {
        return (
            <span
                className={`staff-plate-badge ${size === "large" ? "plate-large" : ""}`}
                style={{ opacity: 0.6, borderStyle: "dashed" }}
                title="Chưa nhận diện được biển số"
            >
                <span className="staff-plate-text font-mono">Chờ quét biển số...</span>
            </span>
        );
    }

    return (
        <span
            className={`staff-plate-badge ${size === "large" ? "plate-large" : ""}`}
            title={plate}
        >
            <span className="staff-plate-flag">
                <span className="staff-plate-vn">VN</span>
            </span>
            <span className="staff-plate-text">{plate}</span>
            <span className="staff-plate-bolt" />
        </span>
    );
}

export function StatusPill({
    status,
}: {
    status: SessionStatus | PaymentStatus;
}) {
    const { t } = useLanguage();
    const cls =
        status === "Active"
            ? "staff-pill-active"
            : status === "Completed" || status === "Paid"
              ? "staff-pill-success"
              : status === "Cancelled"
                ? "staff-pill-danger"
                : "staff-pill-warning";

    let label: string = status;
    if (status === "Active") label = t("statusActive");
    else if (status === "Paid") label = t("statusPaid");
    else if (status === "Pending") label = t("statusPending");
    else if (status === "Completed") label = t("statusCompleted");
    else if (status === "Cancelled") label = t("statusCancelled");

    return <span className={`staff-pill ${cls}`}>{label}</span>;
}

export function QrCodeModal({
    qrToken,
    plateNumber,
    vehicleType,
    entryTime,
    onClose,
}: {
    qrToken: string;
    plateNumber: string;
    vehicleType: VehicleType;
    entryTime: string;
    onClose: () => void;
}) {
    const { t } = useLanguage();
    const [copied, setCopied] = useState(false);

    const isValidTicket = Boolean(qrToken && qrToken.trim());

    function copy() {
        navigator.clipboard.writeText(qrToken);
        setCopied(true);
        setTimeout(() => setCopied(false), 2000);
    }

    function printTicket() {
        window.print();
    }

    return (
        <div className="staff-modal-backdrop" onClick={onClose}>
            <div
                className="staff-modal-card staff-qr-modal print:shadow-none"
                onClick={(e) => e.stopPropagation()}
                role="dialog"
                aria-modal="true"
                aria-labelledby="staff-qr-ticket-title"
            >
                {/* B. Header */}
                <div className="staff-ticket-modal-head no-print">
                    <div className="staff-ticket-head-left">
                        <span
                            className="staff-ticket-head-icon"
                            aria-hidden="true"
                        >
                            <QrCode size={18} strokeWidth={2.2} />
                        </span>
                        <div className="staff-ticket-head-titles">
                            <h3 id="staff-qr-ticket-title">
                                {t("qrTicketTitle") || "Vé giữ xe điện tử"}
                            </h3>
                            <span className="staff-ticket-head-sub">
                                ParkWise Digital Parking Pass
                            </span>
                        </div>
                    </div>

                    <div className="staff-ticket-head-right">
                        {isValidTicket && (
                            <span className="staff-ticket-valid-badge">
                                <ShieldCheck size={13} strokeWidth={2.4} />
                                VÉ HỢP LỆ
                            </span>
                        )}
                        <button
                            type="button"
                            className="staff-modal-close"
                            onClick={onClose}
                            title="Đóng modal (Esc)"
                            aria-label="Đóng vé giữ xe điện tử"
                        >
                            <X size={16} strokeWidth={2.2} />
                        </button>
                    </div>
                </div>

                {/* Scrollable Ticket Body */}
                <div className="staff-ticket-scroll-area">
                    <div id="thermal-receipt" className="staff-digital-ticket">
                        {/* C. Khu vực nhận diện ParkWise */}
                        <div className="staff-ticket-brand-head">
                            <div className="staff-ticket-brand-logo-row">
                                <span
                                    className="staff-ticket-brand-mark"
                                    aria-hidden="true"
                                >
                                    P
                                </span>
                                <span className="staff-ticket-brand-name">
                                    PARKWISE SMART PARKING
                                </span>
                            </div>
                            <div className="staff-ticket-gate-name">
                                TRẠM KIỂM SOÁT CỔNG VÀO (ENTRY GATE)
                            </div>
                            <div className="staff-ticket-station-meta">
                                Mã trạm: GATE-01 • TP. HỒ CHÍ MINH
                            </div>
                        </div>

                        {/* Perforated Ticket Divider */}
                        <div
                            className="staff-ticket-perforation"
                            aria-hidden="true"
                        >
                            <span className="notch notch-left" />
                            <span className="perforation-line" />
                            <span className="notch notch-right" />
                        </div>

                        {/* D. Khu vực QR CODE */}
                        <div className="staff-ticket-qr-section">
                            <div className="staff-ticket-qr-quietzone">
                                <QRCode
                                    value={qrToken}
                                    size={156}
                                    style={{
                                        height: "auto",
                                        maxWidth: "100%",
                                        width: "156px",
                                        display: "block",
                                    }}
                                />
                            </div>
                            {isValidTicket && (
                                <div className="staff-ticket-qr-caption">
                                    <CheckCircle2 size={13} strokeWidth={2.4} />
                                    <span>MÃ QR HỢP LỆ</span>
                                </div>
                            )}
                        </div>

                        {/* E. BARCODE */}
                        <div className="staff-ticket-barcode-section">
                            <Barcode1D
                                value={plateNumber || "PARKWISE"}
                                height={40}
                            />
                        </div>

                        {/* F. THÔNG TIN PHIÊN XE */}
                        <div className="staff-ticket-info-list">
                            <div className="staff-ticket-info-row row-highlight">
                                <span className="staff-ticket-info-label">
                                    Biển số xe
                                </span>
                                <span className="staff-ticket-plate-value">
                                    {plateNumber}
                                </span>
                            </div>

                            <div className="staff-ticket-info-row">
                                <span className="staff-ticket-info-label">
                                    {vehicleType === "Car" ? (
                                        <Car size={13} strokeWidth={2} />
                                    ) : (
                                        <Bike size={13} strokeWidth={2} />
                                    )}
                                    Loại xe
                                </span>
                                <strong className="staff-ticket-info-value">
                                    {vehicleType === "Car"
                                        ? "Ô TÔ (4-7 Chỗ)"
                                        : "XE MÁY"}
                                </strong>
                            </div>

                            <div className="staff-ticket-info-row">
                                <span className="staff-ticket-info-label">
                                    <Clock size={13} strokeWidth={2} />
                                    Giờ vào
                                </span>
                                <strong
                                    className="staff-ticket-info-value"
                                    suppressHydrationWarning
                                >
                                    {formatDateTime(entryTime)}
                                </strong>
                            </div>

                            <div className="staff-ticket-info-row">
                                <span className="staff-ticket-info-label">
                                    <Hash size={13} strokeWidth={2} />
                                    Mã phiên
                                </span>
                                <div className="staff-ticket-token-group">
                                    <code
                                        className="staff-ticket-token-value"
                                        title={qrToken}
                                    >
                                        {qrToken}
                                    </code>
                                    <button
                                        type="button"
                                        className="staff-ticket-inline-copy no-print"
                                        onClick={copy}
                                        title="Sao chép mã phiên"
                                        aria-label="Sao chép mã phiên"
                                    >
                                        {copied ? (
                                            <Check
                                                size={12}
                                                strokeWidth={2.5}
                                            />
                                        ) : (
                                            <Copy size={12} strokeWidth={2.2} />
                                        )}
                                    </button>
                                </div>
                            </div>
                        </div>

                        {/* G. FOOTER – HƯỚNG DẪN */}
                        <div className="staff-ticket-guide-foot">
                            <p className="guide-primary">
                                Quý khách vui lòng giữ vé để đối chiếu khi ra
                                bãi.
                            </p>
                            <p className="guide-secondary">
                                Quét mã QR để thanh toán phí và đổi xe tự động.
                            </p>
                            <p className="guide-hotline">
                                <PhoneCall size={11} strokeWidth={2.2} />
                                <span>Hotline hỗ trợ: 1900 8888 (24/7)</span>
                            </p>
                        </div>
                    </div>
                </div>

                {/* 3. KHU VỰC BUTTON – Sticky Action Footer */}
                <div className="staff-ticket-modal-actions no-print">
                    <button
                        type="button"
                        className="staff-ticket-btn btn-print"
                        onClick={printTicket}
                    >
                        <Printer size={16} strokeWidth={2.2} />
                        <span>In vé nhiệt (80mm)</span>
                    </button>
                    <button
                        type="button"
                        className="staff-ticket-btn btn-copy"
                        onClick={copy}
                    >
                        {copied ? (
                            <Check size={15} strokeWidth={2.4} />
                        ) : (
                            <Copy size={15} strokeWidth={2.2} />
                        )}
                        <span>{copied ? t("qrCopied") : t("qrCopy")}</span>
                    </button>
                    <button
                        type="button"
                        className="staff-ticket-btn btn-done"
                        onClick={onClose}
                    >
                        <CheckCircle2 size={16} strokeWidth={2.2} />
                        <span>{t("qrDone")}</span>
                    </button>
                </div>
            </div>
        </div>
    );
}

/** A count from Parking, or a dash when Parking could not give it. */
export function countText(value: number | null | undefined): string {
    return value === null || value === undefined ? "—" : String(value);
}

/** A fee from Payment, or a note when Payment could not give it. */
export function feeText(
    value: number | null | undefined,
    format: (vnd: number) => string,
): string {
    return value === null || value === undefined
        ? "Chưa tính được phí"
        : format(value);
}

export { RealCameraFeed } from "./RealCameraFeed";
export {
    IoTBarrierGateSimulator,
    type IoTBarrierGateSimulatorProps,
    type BarrierPhysicalState,
    type VehicleApproachState,
} from "./IoTBarrierGateSimulator";
