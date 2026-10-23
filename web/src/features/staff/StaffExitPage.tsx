"use client";

import React, { useState } from "react";
import {
    CameraFeedMock,
    PlateBadge,
    formatDateTime,
    formatVnd,
} from "./StaffUi";
import { describeStaffError, findSessionByQr } from "./staffApi";
import { useStaffData } from "./staffData";
import type { SessionByQrResponse, StaffSession } from "./types";

export function StaffExitPage() {
    const { activeSessions, completeExit } = useStaffData();

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
            // First look in live local sessions
            const local = activeSessions.find(
                (s) => s.qrToken.toLowerCase() === qrInput.trim().toLowerCase(),
            );

            if (local) {
                setScannedSession(local);
                setDetectedExitPlate(local.plateNumber);
                if (
                    local.amountDueVnd === 0 &&
                    local.paymentStatus === "Paid"
                ) {
                    setPaymentSuccess(true);
                }
            } else {
                // Call real endpoint GET /api/parking/sessions/by-qr/{qrToken}
                const res: SessionByQrResponse = await findSessionByQr(
                    qrInput.trim(),
                );
                const simulated: StaffSession = {
                    id: res.sessionId,
                    lotId: res.lotId,
                    plateNumber: res.plateNumber,
                    vehicleType: res.vehicleType,
                    customerType: res.customerType,
                    entryTime: res.entryTime,
                    status: res.status,
                    qrToken: res.qrToken,
                    slotCode: "B1-B10",
                    paymentStatus: "Pending",
                    totalFeeVnd: 60000,
                    prepaidVnd: 0,
                    amountDueVnd: 60000,
                };
                setScannedSession(simulated);
                setDetectedExitPlate(res.plateNumber);
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

    // 3.18 Close Parking Session
    function handleAuthorizeExit() {
        if (!scannedSession || !canAllowExit) return;
        completeExit(scannedSession.id, scannedSession.paymentMethod || "Cash");
        setExitAllowed(true);
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
                        Gate Out — Exit Verification & Payment
                    </h1>
                    <p className="staff-subtitle">
                        Scan QR, cross-check license plate, process parking fee,
                        and release spot.
                    </p>
                </div>

                <div className="staff-header-actions">
                    <button
                        type="button"
                        className="staff-btn staff-btn-secondary"
                        onClick={resetExitGate}
                    >
                        🔄 Clear Exit Gate
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
                    Simulate vehicle at exit:
                </span>
                <button
                    type="button"
                    className="staff-preset-chip"
                    onClick={() => {
                        setQrInput("QR-GUEST-51K-89241");
                        setDetectedExitPlate("51K-892.41");
                    }}
                >
                    Guest Car (Unpaid: 60,000₫)
                </button>
                <button
                    type="button"
                    className="staff-preset-chip"
                    onClick={() => {
                        setQrInput("QR-PARKWISE-REG-101");
                        setDetectedExitPlate("51F-782.91");
                    }}
                >
                    Registered Car (Prepaid: 0₫ Due)
                </button>
                <button
                    type="button"
                    className="staff-preset-chip chip-warning"
                    onClick={() => {
                        setQrInput("QR-GUEST-59T1-88312");
                        setDetectedExitPlate("51A-000.00"); // mismatched plate
                    }}
                >
                    Plate Mismatch Test
                </button>
            </div>

            <div className="staff-grid-split">
                {/* Left column: ANPR Camera & QR Scanner input */}
                <div className="staff-col">
                    <CameraFeedMock
                        title="Gate Out 02 · Exit ANPR Camera"
                        detectedPlate={detectedExitPlate}
                        confidence={0.99}
                        isScanning={loading}
                    />

                    {/* Guest QR Verification input */}
                    <div className="staff-card">
                        <div className="staff-card-header">
                            <h3>Guest QR / Ticket Verification</h3>
                            <span className="staff-pill staff-pill-active">
                                Scanner Ready
                            </span>
                        </div>

                        <div className="staff-form-group">
                            <label htmlFor="staff-qr-input">
                                Scan QR Barcode or Enter QR Token
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
                                    {loading ? "Verifying..." : "Verify QR"}
                                </button>
                            </div>
                        </div>

                        <div className="staff-form-group">
                            <label htmlFor="staff-exit-plate">
                                Exit Camera Detected Plate
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
                            <h2>Exit Verification Checklist</h2>
                            {canAllowExit ? (
                                <span className="staff-pill staff-pill-success">
                                    EXIT AUTHORIZED
                                </span>
                            ) : (
                                <span className="staff-pill staff-pill-danger">
                                    EXIT DENIED
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
                                    <strong>Valid Session QR</strong>
                                    <span>
                                        {qrValid
                                            ? `Scanned: ${scannedSession?.qrToken}`
                                            : "No QR verified yet"}
                                    </span>
                                </div>
                            </div>

                            <div className="staff-check-item">
                                <span className="check-icon">
                                    {plateMatch ? "✅" : "❌"}
                                </span>
                                <div className="check-meta">
                                    <strong>License Plate Match</strong>
                                    <span>
                                        Camera:{" "}
                                        <code>{detectedExitPlate || "—"}</code>{" "}
                                        vs Session:{" "}
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
                                    <strong>Session Status</strong>
                                    <span>
                                        {sessionActive
                                            ? "Session is Active"
                                            : "Invalid / already closed"}
                                    </span>
                                </div>
                            </div>

                            <div className="staff-check-item">
                                <span className="check-icon">
                                    {paymentCompleted ? "✅" : "⚠️"}
                                </span>
                                <div className="check-meta">
                                    <strong>Payment Completed</strong>
                                    <span>
                                        {paymentCompleted
                                            ? "Payment settled in full"
                                            : `Outstanding amount: ${formatVnd(amountDue)}`}
                                    </span>
                                </div>
                            </div>
                        </div>

                        {/* Parking Fee at Exit */}
                        {scannedSession && (
                            <div className="staff-fee-box">
                                <div className="staff-fee-header">
                                    <h3>Parking Fee Calculation</h3>
                                    <span>Slot {scannedSession.slotCode}</span>
                                </div>

                                <div className="staff-fee-rows">
                                    <div className="fee-row">
                                        <span>Entry Time:</span>
                                        <strong>
                                            {formatDateTime(
                                                scannedSession.entryTime,
                                            )}
                                        </strong>
                                    </div>
                                    <div className="fee-row">
                                        <span>Total Parking Fee:</span>
                                        <strong>
                                            {formatVnd(
                                                scannedSession.totalFeeVnd,
                                            )}
                                        </strong>
                                    </div>
                                    <div className="fee-row">
                                        <span>Prepaid via App Wallet:</span>
                                        <span className="text-emerald">
                                            -{" "}
                                            {formatVnd(
                                                scannedSession.prepaidVnd,
                                            )}
                                        </span>
                                    </div>
                                    <div className="fee-row fee-total">
                                        <span>Amount Due at Exit:</span>
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
                                            ⚠️ Amount Due &gt; 0. Collect
                                            payment before allowing vehicle
                                            exit.
                                        </p>
                                        <div className="staff-payment-buttons">
                                            <button
                                                type="button"
                                                className="staff-btn staff-btn-primary"
                                                onClick={() =>
                                                    setPaymentModal("Cash")
                                                }
                                            >
                                                💵 Collect Cash (
                                                {formatVnd(amountDue)})
                                            </button>
                                            <button
                                                type="button"
                                                className="staff-btn staff-btn-secondary"
                                                onClick={() =>
                                                    setPaymentModal("VNPay")
                                                }
                                            >
                                                📱 VNPay QR Payment
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
                                        SESSION CLOSED &amp; BARRIER RAISED
                                    </strong>
                                    <p>
                                        Slot {scannedSession?.slotCode} is now
                                        released. Capacity updated.
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
                                        ? "Authorize Exit & Raise Barrier"
                                        : "Exit Blocked (Requirements Unmet)"}
                                </button>
                            ) : (
                                <button
                                    type="button"
                                    className="staff-btn staff-btn-primary staff-btn-lg"
                                    onClick={resetExitGate}
                                >
                                    Done / Process Next Vehicle
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
                                        ? "💵 Thu Phí Tiền Mặt (Cash Payment)"
                                        : "📱 Thanh Toán VNPay QR"}
                                </h3>
                                <p className="staff-modal-head-sub">
                                    Xác nhận giao dịch thanh toán tại bốt trước
                                    khi mở barrier
                                </p>
                            </div>
                            <button
                                type="button"
                                className="staff-modal-close"
                                onClick={() => setPaymentModal(null)}
                                title="Đóng cửa sổ"
                            >
                                ✕
                            </button>
                        </div>

                        <div className="staff-payment-modal-body">
                            {/* Receipt Summary Card */}
                            <div className="staff-modal-receipt-card">
                                <div className="staff-summary-row">
                                    <span className="staff-summary-label">
                                        Biển số phương tiện:
                                    </span>
                                    <PlateBadge
                                        plate={scannedSession.plateNumber}
                                        size="normal"
                                    />
                                </div>
                                <div className="staff-summary-row">
                                    <span className="staff-summary-label">
                                        Vị trí đỗ:
                                    </span>
                                    <span className="staff-slot-code">
                                        Slot {scannedSession.slotCode}
                                    </span>
                                </div>
                                <div className="staff-summary-row staff-summary-due-row">
                                    <span className="staff-summary-label">
                                        Tổng cước còn lại:
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
                                                Tiền khách đưa (VND)
                                            </label>
                                            <span className="staff-form-hint">
                                                Nhập hoặc chọn nhanh
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
                                                placeholder="Ví dụ: 100000"
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
                                                Đúng số tiền (
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
                                                            "vi-VN",
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
                                                Tiền thối lại cho khách:
                                            </span>
                                            <span className="staff-change-sub">
                                                {cashGiven >=
                                                scannedSession.amountDueVnd
                                                    ? "Khách đã trả đủ tiền mặt"
                                                    : "⚠️ Chưa đủ số tiền cước"}
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
                                            Hủy bỏ
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
                                            ✓ Xác nhận thu tiền &amp; Đã thanh
                                            toán
                                        </button>
                                    </div>
                                </div>
                            ) : (
                                <div className="staff-vnpay-flow">
                                    <p className="staff-vnpay-desc">
                                        Hướng dẫn tài xế mở app Ngân hàng /
                                        VNPay / MoMo quét mã QR tĩnh của bốt
                                        xuất hoặc mã hiển thị bên dưới.
                                    </p>
                                    <div className="staff-vnpay-qr">
                                        <div className="staff-vnpay-badge">
                                            VNPay QR Cổng Ra
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
                                            Hủy bỏ
                                        </button>
                                        <button
                                            type="button"
                                            className="staff-btn staff-btn-primary staff-btn-flex"
                                            onClick={handleCompleteVnPayPayment}
                                        >
                                            ✓ Xác nhận giao dịch VNPay thành
                                            công
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
