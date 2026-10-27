"use client";

import React, { useState } from "react";
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

    return (
        <div className="staff-modal-backdrop" onClick={onClose}>
            <div
                className="staff-modal-card staff-qr-modal"
                onClick={(e) => e.stopPropagation()}
            >
                <div className="staff-modal-head">
                    <h3>{t("qrTicketTitle")}</h3>
                    <button
                        type="button"
                        className="staff-modal-close"
                        onClick={onClose}
                    >
                        ✕
                    </button>
                </div>

                <div className="staff-qr-body">
                    <p className="staff-qr-desc">
                        {t("qrTicketDesc")}
                    </p>

                    <div className="staff-qr-box">
                        <svg
                            viewBox="0 0 100 100"
                            className="staff-qr-svg"
                            aria-label="QR Code"
                        >
                            <rect width="100" height="100" fill="#fff" />
                            {/* QR Finder Corners */}
                            <rect
                                x="10"
                                y="10"
                                width="26"
                                height="26"
                                fill="#171717"
                            />
                            <rect
                                x="14"
                                y="14"
                                width="18"
                                height="18"
                                fill="#fff"
                            />
                            <rect
                                x="18"
                                y="18"
                                width="10"
                                height="10"
                                fill="#171717"
                            />

                            <rect
                                x="64"
                                y="10"
                                width="26"
                                height="26"
                                fill="#171717"
                            />
                            <rect
                                x="68"
                                y="14"
                                width="18"
                                height="18"
                                fill="#fff"
                            />
                            <rect
                                x="72"
                                y="18"
                                width="10"
                                height="10"
                                fill="#171717"
                            />

                            <rect
                                x="10"
                                y="64"
                                width="26"
                                height="26"
                                fill="#171717"
                            />
                            <rect
                                x="14"
                                y="68"
                                width="18"
                                height="18"
                                fill="#fff"
                            />
                            <rect
                                x="18"
                                y="72"
                                width="10"
                                height="10"
                                fill="#171717"
                            />

                            {/* Data matrix patterns */}
                            <rect
                                x="42"
                                y="12"
                                width="6"
                                height="6"
                                fill="#171717"
                            />
                            <rect
                                x="52"
                                y="16"
                                width="6"
                                height="6"
                                fill="#171717"
                            />
                            <rect
                                x="42"
                                y="24"
                                width="6"
                                height="6"
                                fill="#171717"
                            />
                            <rect
                                x="48"
                                y="32"
                                width="6"
                                height="6"
                                fill="#171717"
                            />
                            <rect
                                x="14"
                                y="44"
                                width="6"
                                height="6"
                                fill="#171717"
                            />
                            <rect
                                x="26"
                                y="48"
                                width="6"
                                height="6"
                                fill="#171717"
                            />
                            <rect
                                x="36"
                                y="42"
                                width="6"
                                height="6"
                                fill="#171717"
                            />
                            <rect
                                x="44"
                                y="48"
                                width="6"
                                height="6"
                                fill="#171717"
                            />
                            <rect
                                x="56"
                                y="44"
                                width="6"
                                height="6"
                                fill="#171717"
                            />
                            <rect
                                x="68"
                                y="48"
                                width="6"
                                height="6"
                                fill="#171717"
                            />
                            <rect
                                x="80"
                                y="42"
                                width="6"
                                height="6"
                                fill="#171717"
                            />
                            <rect
                                x="42"
                                y="58"
                                width="6"
                                height="6"
                                fill="#171717"
                            />
                            <rect
                                x="54"
                                y="62"
                                width="6"
                                height="6"
                                fill="#171717"
                            />
                            <rect
                                x="64"
                                y="66"
                                width="6"
                                height="6"
                                fill="#171717"
                            />
                            <rect
                                x="76"
                                y="72"
                                width="6"
                                height="6"
                                fill="#171717"
                            />
                            <rect
                                x="46"
                                y="78"
                                width="6"
                                height="6"
                                fill="#171717"
                            />
                            <rect
                                x="62"
                                y="82"
                                width="6"
                                height="6"
                                fill="#171717"
                            />
                            <rect
                                x="84"
                                y="80"
                                width="6"
                                height="6"
                                fill="#171717"
                            />
                        </svg>
                    </div>

                    <div className="staff-qr-details">
                        <div className="staff-qr-row">
                            <span>{t("sessionsLicensePlate")}</span>
                            <PlateBadge plate={plateNumber} />
                        </div>
                        <div className="staff-qr-row">
                            <span>{t("sessionsVehicle")}</span>
                            <strong>
                                {vehicleType === "Car"
                                    ? t("staffCar")
                                    : t("staffMotorbike")}
                            </strong>
                        </div>
                        <div className="staff-qr-row">
                            <span>{t("sessionsEntryTime")}</span>
                            <strong>{formatDateTime(entryTime)}</strong>
                        </div>
                        <div className="staff-qr-row">
                            <span>QR Token</span>
                            <code className="staff-token-code">{qrToken}</code>
                        </div>
                    </div>
                </div>

                <div className="staff-modal-actions">
                    <button
                        type="button"
                        className="staff-btn staff-btn-secondary"
                        onClick={copy}
                    >
                        {copied ? t("qrCopied") : t("qrCopy")}
                    </button>
                    <button
                        type="button"
                        className="staff-btn staff-btn-primary"
                        onClick={onClose}
                    >
                        {t("qrDone")}
                    </button>
                </div>
            </div>
        </div>
    );
}
