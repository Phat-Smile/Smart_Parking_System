"use client";

import React, { useState } from "react";
import QRCode from "react-qr-code";
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

export function CameraFeedMock({
    title = "Gate ANPR Camera 01",
    detectedPlate,
    confidence,
    isScanning = false,
}: {
    title?: string;
    detectedPlate?: string;
    confidence?: number;
    isScanning?: boolean;
}) {
    return (
        <div className="staff-camera-feed">
            <div className="staff-camera-header">
                <span className="staff-rec-indicator">
                    <span className="staff-rec-dot" /> REC LIVE
                </span>
                <span className="staff-camera-title">{title}</span>
                <span className="staff-camera-fps">30 FPS · 1080p</span>
            </div>

            <div className="staff-camera-viewport">
                <div className="staff-camera-grid" />
                {isScanning && <div className="staff-camera-scanline" />}

                {/* Target box */}
                <div className="staff-camera-target">
                    <div className="staff-target-corner top-left" />
                    <div className="staff-target-corner top-right" />
                    <div className="staff-target-corner bottom-left" />
                    <div className="staff-target-corner bottom-right" />

                    {detectedPlate && (
                        <div className="staff-camera-overlay-info">
                            <PlateBadge plate={detectedPlate} />
                            {typeof confidence === "number" && (
                                <span
                                    className={`staff-camera-conf ${
                                        confidence >= 0.8
                                            ? "conf-high"
                                            : "conf-low"
                                    }`}
                                >
                                    {(confidence * 100).toFixed(0)}% OCR
                                </span>
                            )}
                        </div>
                    )}
                </div>
            </div>
        </div>
    );
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
                style={{ maxWidth: 420 }}
            >
                <div className="staff-modal-head no-print">
                    <div
                        style={{
                            display: "flex",
                            alignItems: "center",
                            gap: 8,
                        }}
                    >
                        <span style={{ fontSize: 18 }}>🎫</span>
                        <h3>
                            {t("qrTicketTitle") ||
                                "Phiếu Gửi Xe Thông Minh (Thermal Ticket)"}
                        </h3>
                    </div>
                    <button
                        type="button"
                        className="staff-modal-close"
                        onClick={onClose}
                    >
                        ✕
                    </button>
                </div>

                {/* Print-ready Thermal Ticket Layout */}
                <div
                    id="thermal-receipt"
                    style={{
                        background: "#ffffff",
                        color: "#171717",
                        padding: "16px 20px",
                        borderRadius: 8,
                        boxShadow: "inset 0 0 0 1px #e5e5e5",
                        fontFamily: "monospace",
                    }}
                >
                    <div
                        style={{
                            textAlign: "center",
                            borderBottom: "1px dashed #737373",
                            paddingBottom: 8,
                            marginBottom: 12,
                        }}
                    >
                        <div
                            style={{
                                fontSize: 14,
                                fontWeight: 900,
                                letterSpacing: "0.08em",
                            }}
                        >
                            PARKWISE SMART PARKING
                        </div>
                        <div
                            style={{
                                fontSize: 11,
                                color: "#525252",
                                fontWeight: 700,
                            }}
                        >
                            TRẠM KIỂM SOÁT CỔNG VÀO (ENTRY GATE)
                        </div>
                        <div style={{ fontSize: 10, color: "#737373" }}>
                            Mã trạm: GATE-01 • TP. HỒ CHÍ MINH
                        </div>
                    </div>

                    <div
                        style={{
                            display: "flex",
                            flexDirection: "column",
                            alignItems: "center",
                            justifyContent: "center",
                            margin: "10px 0",
                            padding: 8,
                            background: "#ffffff",
                            borderRadius: 4,
                            border: "1px solid #e5e5e5",
                        }}
                    >
                        <QRCode value={qrToken} size={130} />
                        <span
                            style={{
                                fontSize: 10,
                                fontFamily: "monospace",
                                color: "#737373",
                                marginTop: 4,
                            }}
                        >
                            MÃ PHIÊN QR HỢP LỆ
                        </span>
                    </div>

                    <div
                        style={{
                            margin: "10px 0",
                            padding: "6px 0",
                            borderTop: "1px dashed #a3a3a3",
                            borderBottom: "1px dashed #a3a3a3",
                        }}
                    >
                        <Barcode1D
                            value={plateNumber || "PARKWISE"}
                            height={38}
                        />
                    </div>

                    <div
                        style={{
                            display: "flex",
                            flexDirection: "column",
                            gap: 6,
                            fontSize: 12,
                            color: "#262626",
                            marginTop: 10,
                        }}
                    >
                        <div
                            style={{
                                display: "flex",
                                justifyContent: "space-between",
                                alignItems: "center",
                            }}
                        >
                            <span style={{ color: "#737373" }}>
                                Biển số xe:
                            </span>
                            <span
                                style={{
                                    fontWeight: 800,
                                    fontSize: 13,
                                    background: "#f5f5f5",
                                    padding: "2px 8px",
                                    borderRadius: 4,
                                    border: "1px solid #d4d4d4",
                                    color: "#171717",
                                }}
                            >
                                {plateNumber}
                            </span>
                        </div>
                        <div
                            style={{
                                display: "flex",
                                justifyContent: "space-between",
                                alignItems: "center",
                            }}
                        >
                            <span style={{ color: "#737373" }}>Loại xe:</span>
                            <strong style={{ color: "#171717" }}>
                                {vehicleType === "Car"
                                    ? "Ô TÔ (4-7 Chỗ)"
                                    : "XE MÁY"}
                            </strong>
                        </div>
                        <div
                            style={{
                                display: "flex",
                                justifyContent: "space-between",
                                alignItems: "center",
                            }}
                        >
                            <span style={{ color: "#737373" }}>Giờ vào:</span>
                            <strong style={{ color: "#171717" }}>
                                {formatDateTime(entryTime)}
                            </strong>
                        </div>
                        <div
                            style={{
                                display: "flex",
                                justifyContent: "space-between",
                                alignItems: "center",
                                fontSize: 10,
                                color: "#737373",
                            }}
                        >
                            <span>Mã phiên:</span>
                            <span
                                style={{
                                    maxWidth: 160,
                                    overflow: "hidden",
                                    textOverflow: "ellipsis",
                                    whiteSpace: "nowrap",
                                }}
                            >
                                {qrToken.slice(0, 16)}...
                            </span>
                        </div>
                    </div>

                    <div
                        style={{
                            borderTop: "1px dashed #737373",
                            paddingTop: 8,
                            marginTop: 12,
                            textAlign: "center",
                            fontSize: 10,
                            color: "#737373",
                            lineHeight: 1.4,
                        }}
                    >
                        <p style={{ fontWeight: 700, margin: 0 }}>
                            * Quý khách vui lòng giữ vé để đối chiếu khi ra bãi
                            *
                        </p>
                        <p style={{ margin: 0 }}>
                            Quét mã QR để thanh toán phí đỗ xe tự động
                        </p>
                        <p
                            style={{
                                fontWeight: 800,
                                color: "#262626",
                                margin: "2px 0 0 0",
                            }}
                        >
                            Hotline hỗ trợ: 1900 8888 (24/7)
                        </p>
                    </div>
                </div>

                <div
                    className="staff-modal-actions no-print"
                    style={{ display: "flex", gap: 8, marginTop: 16 }}
                >
                    <button
                        type="button"
                        className="staff-btn staff-btn-primary"
                        onClick={printTicket}
                        style={{
                            display: "flex",
                            alignItems: "center",
                            gap: 6,
                            background: "#059669",
                        }}
                    >
                        <span>🖨️</span>
                        <span>In vé nhiệt (80mm)</span>
                    </button>
                    <button
                        type="button"
                        className="staff-btn staff-btn-secondary"
                        onClick={copy}
                    >
                        {copied ? t("qrCopied") : t("qrCopy")}
                    </button>
                    <button
                        type="button"
                        className="staff-btn staff-btn-secondary"
                        onClick={onClose}
                    >
                        {t("qrDone")}
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
