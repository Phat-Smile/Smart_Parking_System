"use client";

import React, { useState } from "react";
import {
    CameraFeedMock,
    PlateBadge,
    formatDateTime,
    formatVnd,
} from "./StaffUi";
import {
    completeParkingSession,
    describeStaffError,
    findSessionByQr,
} from "./staffApi";
import { useStaffData } from "./staffData";
import type { SessionByQrResponse, StaffSession } from "./types";
import { useLanguage } from "@/shared/i18n/LanguageContext";

export function StaffExitPage() {
    const { completeExit } = useStaffData();
    const { t, language } = useLanguage();

    // Inputs
    const [qrInput, setQrInput] = useState("QR-GUEST-51K-89241");
    const [detectedExitPlate, setDetectedExitPlate] = useState("51K-892.41");
    const [loading, setLoading] = useState(false);
    const [errorMsg, setErrorMsg] = useState("");

    // Scanned session data
    const [scannedSession, setScannedSession] = useState<StaffSession | null>(
        null,
    );

    // 3.17 Payment modal & handling
    const [paymentModal, setPaymentModal] = useState<"Cash" | "VNPay" | null>(
        null,
    );
    const [cashGiven, setCashGiven] = useState<number>(100000);
    const [paymentSuccess, setPaymentSuccess] = useState(false);
    const [exitAllowed, setExitAllowed] = useState(false);

    // Verification check flags for 3.14
    const qrValid = Boolean(scannedSession);
    const plateMatch =
        Boolean(scannedSession) &&
        scannedSession?.plateNumber.replace(/[^A-Z0-9]/gi, "") ===
            detectedExitPlate.replace(/[^A-Z0-9]/gi, "");
    const sessionActive = scannedSession?.status === "Active";
    const paymentCompleted =
        scannedSession?.paymentStatus === "Paid" || paymentSuccess;
    const amountDue = paymentCompleted
        ? 0
        : (scannedSession?.amountDueVnd ?? 0);

    const canAllowExit =
        qrValid &&
        plateMatch &&
        sessionActive &&
        paymentCompleted &&
        amountDue === 0;

    async function handleVerifyQr() {
        if (!qrInput.trim()) return;
        setLoading(true);
        setErrorMsg("");
        setExitAllowed(false);
        setPaymentSuccess(false);

        try {
            // Call real endpoint GET /api/parking/sessions/by-qr/{qrToken}
            const res: SessionByQrResponse = await findSessionByQr(
                qrInput.trim(),
            );
            const fee = res.amountDueVnd ?? 60000;
            const isReg = res.customerType === "Registered";
            const scanned: StaffSession = {
                id: res.sessionId,
                lotId: res.lotId,
                plateNumber: res.plateNumber,
                vehicleType: res.vehicleType,
                customerType: res.customerType,
                entryTime: res.entryTime,
                status: res.status,
                qrToken: res.qrToken,
                slotCode: res.vehicleType === "Car" ? "B1-B10" : "B2-M12",
                paymentStatus: isReg ? "Paid" : fee === 0 ? "Paid" : "Pending",
                totalFeeVnd: res.totalFeeVnd ?? fee,
                prepaidVnd: isReg ? fee : 0,
                amountDueVnd: isReg ? 0 : fee,
            };
            setScannedSession(scanned);
            setDetectedExitPlate(res.plateNumber);
            if (scanned.amountDueVnd === 0) {
                setPaymentSuccess(true);
            }
        } catch (err) {
            setErrorMsg(describeStaffError(err));
            setScannedSession(null);
        } finally {
            setLoading(false);
        }
    }

    function handleCompleteCashPayment() {
        if (!scannedSession) return;
        setPaymentSuccess(true);
        setPaymentModal(null);
    }

    function handleCompleteVnPayPayment() {
        if (!scannedSession) return;
        setPaymentSuccess(true);
        setPaymentModal(null);
    }

    // 3.18 Close Parking Session: Call real POST /api/parking/sessions/{id}/complete
    async function handleAuthorizeExit() {
        if (!scannedSession || !canAllowExit) return;
        setLoading(true);
        setErrorMsg("");
        try {
            await completeParkingSession(scannedSession.id, {
                paymentMethod: scannedSession.paymentMethod || "Cash",
                amountPaidVnd: scannedSession.totalFeeVnd,
            });
            completeExit(
                scannedSession.id,
                scannedSession.paymentMethod || "Cash",
            );
            setExitAllowed(true);
        } catch (err) {
            setErrorMsg(describeStaffError(err));
        } finally {
            setLoading(false);
        }
    }

    function resetExitGate() {
        setScannedSession(null);
        setQrInput("");
        setDetectedExitPlate("");
        setErrorMsg("");
        setPaymentSuccess(false);
        setExitAllowed(false);
        setPaymentModal(null);
    }

    return (
        <div className="staff-page">
            <header className="staff-header">
                <div>
                    <h1 className="staff-title">
                        {t("exitPageTitle")}
                    </h1>
                    <p className="staff-subtitle">
                        {t("exitPageSubtitle")}
                    </p>
                </div>

                <div className="staff-header-actions">
                    <button
                        type="button"
                        className="staff-btn staff-btn-secondary"
                        onClick={resetExitGate}
                    >
                        {t("exitClearBtn")}
                    </button>
                </div>
            </header>

            {errorMsg && (
                <div className="staff-alert staff-alert-danger">
                    <span>⚠️ {errorMsg}</span>
                    <button type="button" onClick={() => setErrorMsg("")}>
                        ✕
                    </button>
                </div>
            )}

            {/* Quick preset selector */}
            <div className="staff-presets-bar">
                <span className="staff-presets-label">
                    {t("exitSimulateLabel")}
                </span>
                <button
                    type="button"
                    className="staff-preset-chip"
                    onClick={() => {
                        setQrInput("QR-GUEST-51K-89241");
                        setDetectedExitPlate("51K-892.41");
                    }}
                >
                    {t("exitPresetGuestUnpaid")}
                </button>
                <button
                    type="button"
                    className="staff-preset-chip"
                    onClick={() => {
                        setQrInput("QR-PARKWISE-REG-101");
                        setDetectedExitPlate("51F-782.91");
                    }}
                >
                    {t("exitPresetRegPrepaid")}
                </button>
                <button
                    type="button"
                    className="staff-preset-chip chip-warning"
                    onClick={() => {
                        setQrInput("QR-GUEST-59T1-88312");
                        setDetectedExitPlate("51A-000.00");
                    }}
                >
                    {t("exitPresetMismatch")}
                </button>
            </div>

            <div className="staff-grid-split">
                {/* Left column: ANPR Camera & QR Scanner input */}
                <div className="staff-col">
                    <CameraFeedMock
                        title={t("exitCameraTitle")}
                        detectedPlate={detectedExitPlate}
                        confidence={0.99}
                        isScanning={loading}
                    />

                    {/* Guest QR Verification input */}
                    <div className="staff-card">
                        <div className="staff-card-header">
                            <h3>{t("exitQrVerifTitle")}</h3>
                            <span className="staff-pill staff-pill-active">
                                {t("exitScannerReady")}
                            </span>
                        </div>

                        <div className="staff-form-group">
                            <label htmlFor="staff-qr-input">
                                {t("exitScanQrLabel")}
                            </label>
                            <div className="staff-input-group">
                                <input
                                    id="staff-qr-input"
                                    type="text"
                                    className="staff-input"
                                    value={qrInput}
                                    onChange={(e) => setQrInput(e.target.value)}
                                    placeholder="e.g. QR-GUEST-51K-89241"
                                />
                                <button
                                    type="button"
                                    className="staff-btn staff-btn-primary"
                                    disabled={loading || !qrInput.trim()}
                                    onClick={handleVerifyQr}
                                >
                                    {loading ? t("exitVerifying") : t("exitVerifyQrBtn")}
                                </button>
                            </div>
                        </div>

                        <div className="staff-form-group">
                            <label htmlFor="staff-exit-plate">
                                {t("exitCameraPlateLabel")}
                            </label>
                            <input
                                id="staff-exit-plate"
                                type="text"
                                className="staff-input staff-plate-input"
                                value={detectedExitPlate}
                                onChange={(e) =>
                                    setDetectedExitPlate(
                                        e.target.value.toUpperCase(),
                                    )
                                }
                            />
                        </div>
                    </div>
                </div>

                {/* Right column: Exit Verification checklist & Parking Fee */}
                <div className="staff-col">
                    <div className="staff-card">
                        <div className="staff-card-header">
                            <h2>{t("exitChecklistTitle")}</h2>
                            {canAllowExit ? (
                                <span className="staff-pill staff-pill-success">
                                    {t("exitAuthorized")}
                                </span>
                            ) : (
                                <span className="staff-pill staff-pill-danger">
                                    {t("exitDenied")}
                                </span>
                            )}
                        </div>

                        {/* Checklist items */}
                        <div className="staff-checklist">
                            <div className="staff-check-item">
                                <span className="check-icon">
                                    {qrValid ? "✅" : "❌"}
                                </span>
                                <div className="check-meta">
                                    <strong>{t("exitCheckQr")}</strong>
                                    <span>
                                        {qrValid
                                            ? `${t("exitCheckQrScanned")} ${scannedSession?.qrToken}`
                                            : t("exitCheckQrNone")}
                                    </span>
                                </div>
                            </div>

                            <div className="staff-check-item">
                                <span className="check-icon">
                                    {plateMatch ? "✅" : "❌"}
                                </span>
                                <div className="check-meta">
                                    <strong>{t("exitCheckPlate")}</strong>
                                    <span>
                                        {t("exitCheckPlateCam")}{" "}
                                        <code>{detectedExitPlate || "—"}</code>{" "}
                                        vs {t("exitCheckPlateSession")}{" "}
                                        <code>
                                            {scannedSession?.plateNumber || "—"}
                                        </code>
                                    </span>
                                </div>
                            </div>

                            <div className="staff-check-item">
                                <span className="check-icon">
                                    {sessionActive ? "✅" : "❌"}
                                </span>
                                <div className="check-meta">
                                    <strong>{t("exitCheckSession")}</strong>
                                    <span>
                                        {sessionActive
                                            ? t("exitCheckSessionActive")
                                            : t("exitCheckSessionClosed")}
                                    </span>
                                </div>
                            </div>

                            <div className="staff-check-item">
                                <span className="check-icon">
                                    {paymentCompleted ? "✅" : "⚠️"}
                                </span>
                                <div className="check-meta">
                                    <strong>{t("exitCheckPayment")}</strong>
                                    <span>
                                        {paymentCompleted
                                            ? t("exitCheckPaymentSettled")
                                            : `${t("exitCheckPaymentPending")} ${formatVnd(amountDue)}`}
                                    </span>
                                </div>
                            </div>
                        </div>

                        {/* Parking Fee at Exit */}
                        {scannedSession && (
                            <div className="staff-fee-box">
                                <div className="staff-fee-header">
                                    <h3>{t("exitFeeTitle")}</h3>
                                    <span>{t("sessionsSpot")} {scannedSession.slotCode}</span>
                                </div>

                                <div className="staff-fee-rows">
                                    <div className="fee-row">
                                        <span>{t("sessionsEntryTime")}:</span>
                                        <strong>
                                            {formatDateTime(
                                                scannedSession.entryTime,
                                            )}
                                        </strong>
                                    </div>
                                    <div className="fee-row">
                                        <span>{t("exitTotalFee")}</span>
                                        <strong>
                                            {formatVnd(
                                                scannedSession.totalFeeVnd,
                                            )}
                                        </strong>
                                    </div>
                                    <div className="fee-row">
                                        <span>{t("exitPrepaidWallet")}</span>
                                        <span className="text-emerald">
                                            -{" "}
                                            {formatVnd(
                                                scannedSession.prepaidVnd,
                                            )}
                                        </span>
                                    </div>
                                    <div className="fee-row fee-total">
                                        <span>{t("exitAmountDue")}</span>
                                        <strong
                                            className={
                                                amountDue > 0
                                                    ? "text-danger"
                                                    : "text-emerald"
                                            }
                                        >
                                            {formatVnd(amountDue)}
                                        </strong>
                                    </div>
                                </div>

                                {/* Staff Payment Handling buttons if amountDue > 0 */}
                                {amountDue > 0 && (
                                    <div className="staff-payment-prompt">
                                        <p className="staff-warning-box">
                                            {t("exitCollectWarning")}
                                        </p>
                                        <div className="staff-payment-buttons">
                                            <button
                                                type="button"
                                                className="staff-btn staff-btn-primary"
                                                onClick={() =>
                                                    setPaymentModal("Cash")
                                                }
                                            >
                                                {t("exitCollectCash")} (
                                                {formatVnd(amountDue)})
                                            </button>
                                            <button
                                                type="button"
                                                className="staff-btn staff-btn-secondary"
                                                onClick={() =>
                                                    setPaymentModal("VNPay")
                                                }
                                            >
                                                {t("exitVnPayQr")}
                                            </button>
                                        </div>
                                    </div>
                                )}
                            </div>
                        )}

                        {/* Barrier status / close session indicator */}
                        {exitAllowed && (
                            <div className="staff-barrier-open-alert">
                                <div className="barrier-icon">🟢</div>
                                <div className="barrier-content">
                                    <strong>
                                        {t("exitBarrierRaised")}
                                    </strong>
                                    <p>
                                        {t("exitBarrierHint")}
                                    </p>
                                </div>
                            </div>
                        )}

                        {/* Action button */}
                        <div className="staff-exit-actions">
                            {!exitAllowed ? (
                                <button
                                    type="button"
                                    className="staff-btn staff-btn-primary staff-btn-lg"
                                    disabled={!canAllowExit}
                                    onClick={handleAuthorizeExit}
                                >
                                    {canAllowExit
                                        ? t("exitAuthorizeBtn")
                                        : t("exitBlockedBtn")}
                                </button>
                            ) : (
                                <button
                                    type="button"
                                    className="staff-btn staff-btn-primary staff-btn-lg"
                                    onClick={resetExitGate}
                                >
                                    {t("exitDoneBtn")}
                                </button>
                            )}
                        </div>
                    </div>
                </div>
            </div>

            {/* 3.17 Payment Modal (Cash or VNPay) */}
            {paymentModal && scannedSession && (
                <div
                    className="staff-modal-backdrop"
                    onClick={() => setPaymentModal(null)}
                >
                    <div
                        className="staff-modal-card"
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div className="staff-modal-head">
                            <div className="staff-modal-head-title">
                                <h3>
                                    {paymentModal === "Cash"
                                        ? t("exitCashModalTitle")
                                        : t("exitVnPayModalTitle")}
                                </h3>
                                <p className="staff-modal-head-sub">
                                    {t("exitModalSub")}
                                </p>
                            </div>
                            <button
                                type="button"
                                className="staff-modal-close"
                                onClick={() => setPaymentModal(null)}
                                title={t("close")}
                            >
                                ✕
                            </button>
                        </div>

                        <div className="staff-payment-modal-body">
                            {/* Receipt Summary Card */}
                            <div className="staff-modal-receipt-card">
                                <div className="staff-summary-row">
                                    <span className="staff-summary-label">
                                        {t("exitPlateLabel")}
                                    </span>
                                    <PlateBadge
                                        plate={scannedSession.plateNumber}
                                        size="normal"
                                    />
                                </div>
                                <div className="staff-summary-row">
                                    <span className="staff-summary-label">
                                        {t("exitSpotLabel")}
                                    </span>
                                    <span className="staff-slot-code">
                                        {t("sessionsSpot")} {scannedSession.slotCode}
                                    </span>
                                </div>
                                <div className="staff-summary-row staff-summary-due-row">
                                    <span className="staff-summary-label">
                                        {t("exitAmountDueLabel")}
                                    </span>
                                    <strong className="staff-modal-amount-due">
                                        {formatVnd(scannedSession.amountDueVnd)}
                                    </strong>
                                </div>
                            </div>

                            {paymentModal === "Cash" ? (
                                <div className="staff-cash-flow">
                                    <div className="staff-form-group">
                                        <div className="staff-form-label-row">
                                            <label htmlFor="staff-cash-received">
                                                {t("exitCashReceivedLabel")}
                                            </label>
                                            <span className="staff-form-hint">
                                                {t("exitCashHint")}
                                            </span>
                                        </div>

                                        <div className="staff-input-cash-wrap">
                                            <input
                                                id="staff-cash-received"
                                                type="number"
                                                step="1000"
                                                className="staff-input staff-cash-input"
                                                value={cashGiven || ""}
                                                onChange={(e) =>
                                                    setCashGiven(
                                                        Number(e.target.value),
                                                    )
                                                }
                                                placeholder="100000"
                                            />
                                            <span className="staff-cash-unit">
                                                ₫
                                            </span>
                                        </div>

                                        {/* Quick tap cash presets */}
                                        <div className="staff-quick-cash-row">
                                            <button
                                                type="button"
                                                className="staff-quick-cash-btn"
                                                onClick={() =>
                                                    setCashGiven(
                                                        scannedSession.amountDueVnd,
                                                    )
                                                }
                                            >
                                                {t("exitExactCash")} (
                                                {formatVnd(
                                                    scannedSession.amountDueVnd,
                                                )}
                                                )
                                            </button>
                                            {[100000, 200000, 500000].map(
                                                (preset) => (
                                                    <button
                                                        key={preset}
                                                        type="button"
                                                        className={`staff-quick-cash-btn ${
                                                            cashGiven === preset
                                                                ? "active"
                                                                : ""
                                                        }`}
                                                        onClick={() =>
                                                            setCashGiven(preset)
                                                        }
                                                    >
                                                        {preset.toLocaleString(
                                                            language === "vi" ? "vi-VN" : "en-US",
                                                        )}
                                                        ₫
                                                    </button>
                                                ),
                                            )}
                                        </div>
                                    </div>

                                    <div className="staff-change-box">
                                        <div className="staff-change-info">
                                            <span className="staff-change-label">
                                                {t("exitChangeDue")}
                                            </span>
                                            <span className="staff-change-sub">
                                                {cashGiven >=
                                                scannedSession.amountDueVnd
                                                    ? (language === "vi" ? "Khách đã trả đủ tiền mặt" : "Sufficient cash received")
                                                    : (language === "vi" ? "⚠️ Chưa đủ số tiền cước" : "⚠️ Insufficient cash")}
                                            </span>
                                        </div>
                                        <strong className="staff-change-val">
                                            {formatVnd(
                                                Math.max(
                                                    0,
                                                    cashGiven -
                                                        scannedSession.amountDueVnd,
                                                ),
                                            )}
                                        </strong>
                                    </div>

                                    <div className="staff-modal-action-bar">
                                        <button
                                            type="button"
                                            className="staff-btn staff-btn-secondary"
                                            onClick={() =>
                                                setPaymentModal(null)
                                            }
                                        >
                                            {t("cancel")}
                                        </button>
                                        <button
                                            type="button"
                                            className="staff-btn staff-btn-primary staff-btn-flex"
                                            disabled={
                                                cashGiven <
                                                scannedSession.amountDueVnd
                                            }
                                            onClick={handleCompleteCashPayment}
                                        >
                                            {t("exitConfirmCashPayment")}
                                        </button>
                                    </div>
                                </div>
                            ) : (
                                <div className="staff-vnpay-flow">
                                    <p className="staff-vnpay-desc">
                                        {language === "vi"
                                            ? "Hướng dẫn tài xế mở app Ngân hàng / VNPay / MoMo quét mã QR tĩnh của bốt xuất hoặc mã hiển thị bên dưới."
                                            : "Instruct the driver to scan the QR code below using any mobile banking or e-wallet app."}
                                    </p>
                                    <div className="staff-vnpay-qr">
                                        <div className="staff-vnpay-badge">
                                            {language === "vi" ? "VNPay QR Cổng Ra" : "Gate Out VietQR / VNPay"}
                                        </div>
                                        <div className="staff-vnpay-code">
                                            🅿️ PARKWISE GATE 02 ·{" "}
                                            {formatVnd(
                                                scannedSession.amountDueVnd,
                                            )}
                                        </div>
                                    </div>
                                    <div className="staff-modal-action-bar">
                                        <button
                                            type="button"
                                            className="staff-btn staff-btn-secondary"
                                            onClick={() =>
                                                setPaymentModal(null)
                                            }
                                        >
                                            {t("cancel")}
                                        </button>
                                        <button
                                            type="button"
                                            className="staff-btn staff-btn-primary staff-btn-flex"
                                            onClick={handleCompleteVnPayPayment}
                                        >
                                            {t("exitSimulateVnPaySuccess")}
                                        </button>
                                    </div>
                                </div>
                            )}
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
