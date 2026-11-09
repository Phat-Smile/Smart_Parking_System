"use client";

import React, { useState } from "react";
import { RealCameraFeed } from "./RealCameraFeed";
import {
    PlateBadge,
    feeText,
    formatDateTime,
    formatVnd,
    IoTBarrierGateSimulator,
} from "./StaffUi";
import {
    completeParkingSession,
    describeStaffError,
    findOpenSessionByPlate,
    findSessionByQr,
    getParkingSessionFee,
    manualVerifyExit,
    reissueParkingSessionQr,
} from "./staffApi";
import { useStaffData } from "./staffData";
import type { SessionByQrResponse, StaffSession } from "./types";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { useGateVoice } from "./audio";

export function StaffExitPage() {
    const { assignedLot, completeExit, activeSessions, fetchAssignedLot } =
        useStaffData();
    const { t, language } = useLanguage();
    const { announceCheckOut, announceWarning } = useGateVoice();

    // Fetch assigned lot and live active sessions on mount
    React.useEffect(() => {
        fetchAssignedLot().catch(() => {});
    }, [fetchAssignedLot]);

    // Inputs (start empty, no hardcoded mock data)
    const [qrInput, setQrInput] = useState("");
    const [detectedExitPlate, setDetectedExitPlate] = useState("");
    const [loading, setLoading] = useState(false);
    const [errorMsg, setErrorMsg] = useState("");

    // Scanned session data
    const [scannedSession, setScannedSession] = useState<StaffSession | null>(
        null,
    );

    // Payment modal & handling
    const [paymentModal, setPaymentModal] = useState<"Cash" | "VNPay" | null>(
        null,
    );
    const [cashGiven, setCashGiven] = useState<number>(100000);
    const [paymentSuccess, setPaymentSuccess] = useState(false);
    const [exitAllowed, setExitAllowed] = useState(false);

    // Verification check flags
    const qrValid =
        Boolean(scannedSession) &&
        (Boolean(scannedSession?.qrToken) || exitAllowed);
    const plateMatch =
        Boolean(scannedSession) &&
        Boolean(detectedExitPlate) &&
        scannedSession?.plateNumber.replace(/[^A-Z0-9]/gi, "") ===
            detectedExitPlate.replace(/[^A-Z0-9]/gi, "");
    const sessionActive =
        scannedSession?.status === "Active" ||
        scannedSession?.status === "Paid";
    const paymentCompleted =
        scannedSession?.paymentStatus === "Paid" || paymentSuccess;
    const amountDue = paymentCompleted
        ? 0
        : (scannedSession?.amountDueVnd ?? 0);

    const canAllowExit =
        plateMatch &&
        sessionActive &&
        paymentCompleted &&
        amountDue === 0 &&
        (qrValid || exitAllowed);

    async function handleVerifyQr(tokenOverride?: string) {
        const token = (
            typeof tokenOverride === "string" ? tokenOverride : qrInput || ""
        ).trim();
        if (!token) {
            setErrorMsg("Vui lòng nhập hoặc quét mã QR.");
            return;
        }

        setLoading(true);
        setErrorMsg("");
        setExitAllowed(false);
        setPaymentSuccess(false);

        try {
            // Call real endpoint GET /api/parking/sessions/by-qr/{qrToken}
            const queryToken = (token || qrInput || "").trim();
            const res: SessionByQrResponse = await findSessionByQr(queryToken);
            const fee = res.amountDueVnd ?? 30000;
            const isReg = res.customerType === "Registered";
            const isPaid = res.status === "Paid" || res.amountDueVnd === 0;
            const scanned: StaffSession = {
                id: res.sessionId,
                lotId: res.lotId,
                plateNumber: res.plateNumber,
                vehicleType: res.vehicleType,
                customerType: res.customerType,
                entryTime: res.entryTime,
                status: res.status,
                qrToken: res.qrToken,
                slotCode: res.vehicleType === "Car" ? "CAR-A" : "MOTO-B",
                paymentStatus: isPaid ? "Paid" : "Pending",
                totalFeeVnd: res.totalFeeVnd ?? fee,
                prepaidVnd: isPaid ? fee : 0,
                amountDueVnd: isPaid ? 0 : fee,
            };

            setScannedSession(scanned);
            setDetectedExitPlate(res.plateNumber);
            setQrInput(res.qrToken);
            if (scanned.amountDueVnd === 0 || isPaid) {
                setPaymentSuccess(true);
            }
        } catch (err) {
            setErrorMsg(describeStaffError(err));
            setScannedSession(null);
        } finally {
            setLoading(false);
        }
    }

    // Tra cứu phiên bằng biển số khi khách làm mất vé / QR
    async function handleLookupByPlate() {
        const plate = detectedExitPlate.trim();
        if (!plate) {
            setErrorMsg("Vui lòng nhập biển số xe để tra cứu.");
            return;
        }

        setLoading(true);
        setErrorMsg("");
        setExitAllowed(false);
        setPaymentSuccess(false);

        try {
            const lotId = assignedLot?.lotId || "";
            const res = await findOpenSessionByPlate(lotId, plate);
            setQrInput(res.qrToken || "");
            const paymentFee = await getParkingSessionFee(res.sessionId).catch(
                () => null,
            );
            const fallbackFee = res.vehicleType === "Car" ? 30000 : 6000;
            const totalFee = paymentFee?.totalFee ?? fallbackFee;
            const amountDue = paymentFee?.amountDue ?? fallbackFee;
            const isPaid = res.status === "Paid" || amountDue === 0;
            const scanned: StaffSession = {
                id: res.sessionId,
                lotId: res.lotId,
                plateNumber: res.plateNumber,
                vehicleType: res.vehicleType,
                customerType: "Guest",
                entryTime: res.entryTime,
                status: res.status,
                qrToken: res.qrToken || "",
                slotCode: res.vehicleType === "Car" ? "CAR-A" : "MOTO-B",
                paymentStatus: isPaid ? "Paid" : "Pending",
                totalFeeVnd: totalFee,
                prepaidVnd: isPaid
                    ? totalFee
                    : (paymentFee?.depositAmount ?? 0),
                amountDueVnd: isPaid ? 0 : amountDue,
            };
            setScannedSession(scanned);
            if (isPaid) setPaymentSuccess(true);
        } catch (err) {
            setErrorMsg(describeStaffError(err));
        } finally {
            setLoading(false);
        }
    }

    // Cấp lại vé QR mới
    async function handleReissueQr() {
        if (!scannedSession) return;
        setLoading(true);
        setErrorMsg("");
        try {
            const res = await reissueParkingSessionQr(
                scannedSession.id,
                "Khách làm mất vé QR tại cổng ra",
            );
            setQrInput(res.qrToken || "");
            setScannedSession({
                ...scannedSession,
                qrToken: res.qrToken || "",
            });
        } catch (err) {
            setErrorMsg(describeStaffError(err));
        } finally {
            setLoading(false);
        }
    }

    // Xác minh đối soát thủ công tại cổng ra
    async function handleManualVerify() {
        if (!scannedSession) return;
        setLoading(true);
        setErrorMsg("");
        try {
            const res = await manualVerifyExit(
                scannedSession.id,
                "Nhân viên đối soát giấy tờ xe thủ công tại quầy",
            );
            if (res.canExit) {
                setExitAllowed(true);
            }
        } catch (err) {
            setErrorMsg(describeStaffError(err));
        } finally {
            setLoading(false);
        }
    }

    // Exit payment is Sprint 3 (Demo B): Payment has no session payment yet, so the screen never marks a
    // session as paid and the gate stays closed.
    const EXIT_PAYMENT_NOT_READY =
        "Thanh toán phiên đỗ ở cổng ra chưa có trong Sprint 2. Chưa ghi nhận tiền, chưa mở cổng.";

    function handleCompleteCashPayment() {
        if (!scannedSession) return;
        setPaymentModal(null);
        setErrorMsg(EXIT_PAYMENT_NOT_READY);
    }

    function handleCompleteVnPayPayment() {
        if (!scannedSession) return;
        setPaymentModal(null);
        setErrorMsg(EXIT_PAYMENT_NOT_READY);
    }

    // 100% Real API: Close Parking Session
    async function handleAuthorizeExit() {
        if (!scannedSession || !canAllowExit) {
            if (scannedSession && !canAllowExit) {
                announceWarning(
                    "Xe chưa thanh toán phí đỗ. Không thể mở barrier.",
                );
            }
            return;
        }
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
            announceCheckOut({
                plateNumber: scannedSession.plateNumber,
                lotName: assignedLot?.lotName,
                amountPaid: scannedSession.totalFeeVnd,
            });
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
                    <h1 className="staff-title">{t("exitPageTitle")}</h1>
                    <p className="staff-subtitle">
                        {assignedLot?.lotName
                            ? `Bãi trực: ${assignedLot.lotName} · `
                            : ""}
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

            {/* Live active sessions quick selector from real DB */}
            {activeSessions.length > 0 && (
                <div className="staff-presets-bar">
                    <span className="staff-presets-label">
                        Xe đang đỗ trong CSDL:
                    </span>
                    {activeSessions.slice(0, 4).map((s) => (
                        <button
                            key={s.id}
                            type="button"
                            className="staff-preset-chip"
                            onClick={() => {
                                setQrInput(s.qrToken || "");
                                setDetectedExitPlate(s.plateNumber);
                                handleVerifyQr(s.qrToken);
                            }}
                        >
                            {s.vehicleType === "Car" ? "🚗" : "🛵"}{" "}
                            {s.plateNumber} ({(s.qrToken || "").slice(-8)})
                        </button>
                    ))}
                </div>
            )}

            <div className="staff-grid-split">
                {/* Left column: Real Camera ANPR & QR Scanner */}
                <div className="staff-col">
                    <RealCameraFeed
                        title={
                            t("exitCameraTitle") ||
                            "Camera Cổng Ra 02 (Gate Out 02)"
                        }
                        mode="exit"
                        detectedPlate={detectedExitPlate}
                        confidence={0.99}
                        isScanning={loading}
                        onQrCodeScanned={(code) => {
                            setQrInput(code);
                            handleVerifyQr(code);
                        }}
                    />

                    {/* QR Verification input */}
                    <div className="staff-card">
                        <div className="staff-card-header">
                            <h3>{t("exitQrVerifTitle")}</h3>
                            <span className="staff-pill staff-pill-active">
                                {t("exitScannerReady")}
                            </span>
                        </div>

                        <div className="staff-form-group">
                            <label htmlFor="staff-qr-input">
                                {t("exitScanQrLabel")} (Tự động nhận diện từ
                                Camera thật hoặc dán mã)
                            </label>
                            <div className="staff-input-group">
                                <input
                                    id="staff-qr-input"
                                    type="text"
                                    className="staff-input"
                                    value={qrInput || ""}
                                    onChange={(e) => setQrInput(e.target.value)}
                                    placeholder="Hướng camera vào mã QR hoặc dán mã QR-..."
                                />
                                <button
                                    type="button"
                                    className="staff-btn staff-btn-primary"
                                    disabled={loading || !qrInput?.trim()}
                                    onClick={() => handleVerifyQr()}
                                >
                                    {loading
                                        ? t("exitVerifying")
                                        : t("exitVerifyQrBtn")}
                                </button>
                            </div>
                        </div>

                        <div className="staff-form-group">
                            <label htmlFor="staff-exit-plate">
                                {t("exitCameraPlateLabel")}
                            </label>
                            <div className="staff-input-group">
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
                                    placeholder="Nhập biển số để đối soát"
                                />
                                <button
                                    type="button"
                                    className="staff-btn staff-btn-secondary"
                                    disabled={
                                        loading || !detectedExitPlate.trim()
                                    }
                                    title="Tra cứu phiên đỗ bằng biển số khi khách làm mất vé QR"
                                    onClick={handleLookupByPlate}
                                >
                                    🔍 Tra cứu mất vé
                                </button>
                            </div>
                        </div>

                        {/* Actions when QR is lost or manual check is needed */}
                        {scannedSession && (
                            <div
                                style={{
                                    marginTop: "12px",
                                    display: "flex",
                                    gap: "8px",
                                    flexWrap: "wrap",
                                }}
                            >
                                <button
                                    type="button"
                                    className="staff-btn staff-btn-secondary staff-btn-sm"
                                    disabled={loading}
                                    onClick={handleReissueQr}
                                    title="Hủy mã QR cũ và phát hành mã QR mới cho khách"
                                >
                                    🔄 Cấp lại vé QR
                                </button>
                                <button
                                    type="button"
                                    className="staff-btn staff-btn-secondary staff-btn-sm"
                                    disabled={loading}
                                    onClick={handleManualVerify}
                                    title="Nhân viên đối soát thủ công giấy tờ xe tại quầy"
                                >
                                    🛡️ Xác minh thủ công
                                </button>
                            </div>
                        )}
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
                            <div
                                className={`staff-check-item ${
                                    qrValid ? "passed" : "failed"
                                }`}
                            >
                                <div className="check-icon-wrap">
                                    <span className="check-icon">
                                        {qrValid ? "✓" : "✕"}
                                    </span>
                                </div>
                                <div className="check-meta">
                                    <div className="check-title">
                                        {t("exitCheckQr")}
                                    </div>
                                    <div className="check-desc">
                                        {qrValid ? (
                                            <>
                                                <span>
                                                    {t("exitCheckQrScanned")}
                                                </span>
                                                <code className="staff-code-pill">
                                                    {scannedSession?.qrToken ||
                                                        "Đã xác minh thủ công"}
                                                </code>
                                            </>
                                        ) : (
                                            <span>
                                                {scannedSession
                                                    ? "Khách mất vé QR — Vui lòng cấp lại vé hoặc xác minh thủ công"
                                                    : "Chưa quét hoặc chưa tìm thấy mã QR"}
                                            </span>
                                        )}
                                    </div>
                                </div>
                                <div className="check-badge-wrap">
                                    <span
                                        className={`staff-check-badge ${
                                            qrValid
                                                ? "badge-pass"
                                                : "badge-fail"
                                        }`}
                                    >
                                        {qrValid
                                            ? t("exitStatusPassed")
                                            : t("exitStatusFailed")}
                                    </span>
                                </div>
                            </div>

                            <div
                                className={`staff-check-item ${
                                    plateMatch ? "passed" : "failed"
                                }`}
                            >
                                <div className="check-icon-wrap">
                                    <span className="check-icon">
                                        {plateMatch ? "✓" : "✕"}
                                    </span>
                                </div>
                                <div className="check-meta">
                                    <div className="check-title">
                                        {t("exitCheckPlate")}
                                    </div>
                                    <div className="check-desc">
                                        <span>Cổng ra:</span>{" "}
                                        <code className="staff-code-pill">
                                            {detectedExitPlate || "—"}
                                        </code>{" "}
                                        <span className="check-desc-sep">
                                            vs
                                        </span>{" "}
                                        <span>Vé vào:</span>{" "}
                                        <code className="staff-code-pill">
                                            {scannedSession?.plateNumber || "—"}
                                        </code>
                                    </div>
                                </div>
                                <div className="check-badge-wrap">
                                    <span
                                        className={`staff-check-badge ${
                                            plateMatch
                                                ? "badge-pass"
                                                : "badge-fail"
                                        }`}
                                    >
                                        {plateMatch
                                            ? t("exitStatusPassed")
                                            : t("exitStatusFailed")}
                                    </span>
                                </div>
                            </div>

                            <div
                                className={`staff-check-item ${
                                    sessionActive ? "passed" : "failed"
                                }`}
                            >
                                <div className="check-icon-wrap">
                                    <span className="check-icon">
                                        {sessionActive ? "✓" : "✕"}
                                    </span>
                                </div>
                                <div className="check-meta">
                                    <div className="check-title">
                                        Trạng thái phiên xe
                                    </div>
                                    <div className="check-desc">
                                        <span>
                                            {scannedSession
                                                ? `Trạng thái: ${scannedSession.status}`
                                                : "Chờ dữ liệu..."}
                                        </span>
                                    </div>
                                </div>
                                <div className="check-badge-wrap">
                                    <span
                                        className={`staff-check-badge ${
                                            sessionActive
                                                ? "badge-pass"
                                                : "badge-fail"
                                        }`}
                                    >
                                        {sessionActive
                                            ? t("exitStatusPassed")
                                            : t("exitStatusFailed")}
                                    </span>
                                </div>
                            </div>

                            <div
                                className={`staff-check-item ${
                                    paymentCompleted ? "passed" : "failed"
                                }`}
                            >
                                <div className="check-icon-wrap">
                                    <span className="check-icon">
                                        {paymentCompleted ? "✓" : "✕"}
                                    </span>
                                </div>
                                <div className="check-meta">
                                    <div className="check-title">
                                        {t("exitCheckPayment")}
                                    </div>
                                    <div className="check-desc">
                                        {scannedSession ? (
                                            paymentCompleted ? (
                                                <span className="text-emerald">
                                                    ✓ Đã thanh toán đủ
                                                </span>
                                            ) : (
                                                <span className="text-danger">
                                                    Còn nợ:{" "}
                                                    {formatVnd(
                                                        scannedSession.amountDueVnd ??
                                                            0,
                                                    )}
                                                </span>
                                            )
                                        ) : (
                                            <span>Chờ phiên...</span>
                                        )}
                                    </div>
                                </div>
                                <div className="check-badge-wrap">
                                    <span
                                        className={`staff-check-badge ${
                                            paymentCompleted
                                                ? "badge-pass"
                                                : "badge-fail"
                                        }`}
                                    >
                                        {paymentCompleted
                                            ? t("exitStatusPassed")
                                            : t("exitStatusFailed")}
                                    </span>
                                </div>
                            </div>
                        </div>

                        {/* Fee Calculation Breakdown */}
                        {scannedSession && (
                            <div className="staff-fee-box">
                                <div className="staff-fee-header">
                                    <h3>{t("exitFeeTitle")}</h3>
                                    <PlateBadge
                                        plate={scannedSession.plateNumber}
                                    />
                                    <span>
                                        {t("sessionsSpot")}{" "}
                                        {scannedSession.slotCode ?? "—"}
                                    </span>
                                </div>

                                <div className="staff-fee-rows">
                                    <div className="staff-fee-row">
                                        <span>{t("sessionsVehicle")}</span>
                                        <strong>
                                            {scannedSession.vehicleType ===
                                            "Car"
                                                ? t("staffCar")
                                                : t("staffMotorbike")}
                                        </strong>
                                    </div>
                                    <div className="staff-fee-row">
                                        <span>{t("sessionsEntryTime")}</span>
                                        <strong>
                                            {formatDateTime(
                                                scannedSession.entryTime,
                                            )}
                                        </strong>
                                    </div>
                                    <div className="staff-fee-row">
                                        <span>{t("exitTotalFee")}</span>
                                        <strong>
                                            {feeText(
                                                scannedSession.totalFeeVnd,
                                                formatVnd,
                                            )}
                                        </strong>
                                    </div>
                                    <div className="fee-row">
                                        <span>{t("exitPrepaidWallet")}</span>
                                        <span className="text-emerald">
                                            -{" "}
                                            {feeText(
                                                scannedSession.prepaidVnd,
                                                formatVnd,
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

                                {/* Payment Actions */}
                                {amountDue > 0 && (
                                    <div className="staff-pay-buttons">
                                        <button
                                            type="button"
                                            className="staff-btn staff-btn-primary staff-btn-flex"
                                            onClick={() =>
                                                setPaymentModal("Cash")
                                            }
                                        >
                                            💵 {t("exitCollectCash")}
                                        </button>
                                        <button
                                            type="button"
                                            className="staff-btn staff-btn-secondary staff-btn-flex"
                                            onClick={() =>
                                                setPaymentModal("VNPay")
                                            }
                                        >
                                            📱 {t("exitVnPayQr")}
                                        </button>
                                    </div>
                                )}
                            </div>
                        )}

                        {/* Barrier opened alert with Interactive IoT Simulator */}
                        {exitAllowed && (
                            <div style={{ marginTop: "1rem" }}>
                                <IoTBarrierGateSimulator
                                    gateId="GATE-OUT-02"
                                    gateName="Cổng Ra Xe (Gate Out 02)"
                                    gateType="EXIT"
                                    isOpen={true}
                                    physicalState="OPEN"
                                    vehicleState="PASSING"
                                    detectedPlate={
                                        scannedSession?.plateNumber ||
                                        detectedExitPlate
                                    }
                                    vehicleType={
                                        scannedSession?.vehicleType || "Car"
                                    }
                                    compact={true}
                                    enableControls={false}
                                    enableEmergencyOverride={true}
                                />
                                <div
                                    className="staff-barrier-open-alert"
                                    style={{ marginTop: "8px" }}
                                >
                                    <div className="barrier-icon">🟢</div>
                                    <div className="barrier-content">
                                        <strong>
                                            {t("exitBarrierRaised")}
                                        </strong>
                                        <p>{t("exitBarrierHint")}</p>
                                    </div>
                                </div>
                            </div>
                        )}

                        {/* Final Authorize Exit Button */}
                        <div className="staff-exit-actions">
                            {!exitAllowed ? (
                                <button
                                    type="button"
                                    className="staff-btn staff-btn-primary staff-btn-lg"
                                    disabled={!canAllowExit || loading}
                                    onClick={handleAuthorizeExit}
                                >
                                    {loading
                                        ? t("exitVerifying")
                                        : t("exitAuthorizeBtn")}
                                </button>
                            ) : (
                                <button
                                    type="button"
                                    className="staff-btn staff-btn-primary"
                                    onClick={resetExitGate}
                                >
                                    {t("exitDoneBtn")}
                                </button>
                            )}
                        </div>
                    </div>
                </div>
            </div>

            {/* Payment Modal */}
            {paymentModal && scannedSession && (
                <div
                    className="staff-modal-backdrop"
                    onClick={() => setPaymentModal(null)}
                >
                    <div
                        className="staff-modal"
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div className="staff-modal-header">
                            <h2>
                                {paymentModal === "Cash"
                                    ? t("exitCashModalTitle")
                                    : t("exitVnPayModalTitle")}
                            </h2>
                            <button
                                type="button"
                                className="staff-modal-close"
                                onClick={() => setPaymentModal(null)}
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
                                        {t("sessionsSpot")}{" "}
                                        {scannedSession.slotCode ?? "—"}
                                    </span>
                                </div>
                                <div className="staff-summary-row staff-summary-due-row">
                                    <span className="staff-summary-label">
                                        {t("exitAmountDueLabel")}
                                    </span>
                                    <strong className="staff-modal-amount-due">
                                        {feeText(
                                            scannedSession.amountDueVnd,
                                            formatVnd,
                                        )}
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
                                                        scannedSession.amountDueVnd ??
                                                            0,
                                                    )
                                                }
                                            >
                                                {t("exitExactCash")} (
                                                {feeText(
                                                    scannedSession.amountDueVnd,
                                                    formatVnd,
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
                                                            language === "vi"
                                                                ? "vi-VN"
                                                                : "en-US",
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
                                                (scannedSession.amountDueVnd ??
                                                    0)
                                                    ? language === "vi"
                                                        ? "Khách đã trả đủ tiền mặt"
                                                        : "Sufficient cash received"
                                                    : language === "vi"
                                                      ? "⚠️ Chưa đủ số tiền cước"
                                                      : "⚠️ Insufficient cash"}
                                            </span>
                                        </div>
                                        <strong className="staff-change-val">
                                            {formatVnd(
                                                Math.max(
                                                    0,
                                                    cashGiven -
                                                        (scannedSession.amountDueVnd ??
                                                            0),
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
                                                (scannedSession.amountDueVnd ??
                                                    0)
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
                                            {language === "vi"
                                                ? "VNPay QR Cổng Ra"
                                                : "Gate Out VietQR / VNPay"}
                                        </div>
                                        <div className="staff-vnpay-code">
                                            🅿️ PARKWISE GATE 02 ·{" "}
                                            {feeText(
                                                scannedSession.amountDueVnd,
                                                formatVnd,
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
                                            Thanh toán VNPay thành công
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
