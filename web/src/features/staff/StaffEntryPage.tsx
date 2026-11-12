"use client";

import React, { useState } from "react";
import { RealCameraFeed } from "./RealCameraFeed";
import {
    CameraFeedMock,
    PlateBadge,
    QrCodeModal,
    countText,
    IoTBarrierGateSimulator,
} from "./StaffUi";
import { describeStaffError, openParkingSession } from "./staffApi";
import { emitLiveToast } from "@/shared/ui/ToastNotificationHub";
import { useStaffData } from "./staffData";
import type { CustomerType, OpenedSessionResponse, VehicleType } from "./types";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { useGateVoice } from "./audio";

export function StaffEntryPage() {
    const { addSession, occupancy, assignedLot, fetchAssignedLot } =
        useStaffData();
    const { t, language } = useLanguage();
    const { announceCheckIn, announceWarning } = useGateVoice();

    // Load assigned lot on mount
    React.useEffect(() => {
        fetchAssignedLot().catch(() => {});
    }, [fetchAssignedLot]);

    // Mode: registered vs guest
    const [entryType, setEntryType] = useState<CustomerType>("Guest");
    const [plateNumber, setPlateNumber] = useState("51F-782.91");
    const [vehicleType, setVehicleType] = useState<VehicleType>("Car");
    const [ocrConfidence, setOcrConfidence] = useState<number>(0.96);

    // License Plate Verification state
    const [isEditingPlate, setIsEditingPlate] = useState(false);
    const [editedPlate, setEditedPlate] = useState("51F-782.91");
    const [cameraSnapshot, setCameraSnapshot] = useState<string | null>(null);

    // Stepper state
    const [step, setStep] = useState<
        "camera" | "checking" | "session_created" | "barrier_open"
    >("camera");
    const [loading, setLoading] = useState(false);
    const [errorMsg, setErrorMsg] = useState("");
    const [lastOpened, setLastOpened] = useState<OpenedSessionResponse | null>(
        null,
    );
    const [showQrModal, setShowQrModal] = useState(false);

    // Load presets for quick entry
    function loadPreset(
        plate: string,
        type: VehicleType,
        cust: CustomerType,
        conf: number,
    ) {
        setPlateNumber(plate);
        setEditedPlate(plate);
        setVehicleType(type);
        setEntryType(cust);
        setOcrConfidence(conf);
        setIsEditingPlate(conf < 0.8);
        setStep("camera");
        setErrorMsg("");
    }

    // When snapshot is taken from real camera
    function handleCameraSnapshot(dataUrl: string) {
        setCameraSnapshot(dataUrl);
        setOcrConfidence(0.97);
    }

    async function handleConfirmEntry() {
        const finalPlate = (isEditingPlate ? editedPlate : plateNumber)
            .trim()
            .toUpperCase();
        if (!finalPlate) {
            setErrorMsg(
                language === "vi"
                    ? "Vui lòng nhập biển số xe."
                    : "Enter the vehicle license plate.",
            );
            return;
        }

        if (!assignedLot?.lotId) {
            setErrorMsg(
                language === "vi"
                    ? "Chưa tải được thông tin bãi xe được phân công. Vui lòng kiểm tra lại tài khoản nhân viên."
                    : "Could not load your assigned parking lot. Check your staff account and try again.",
            );
            return;
        }

        setLoading(true);
        setErrorMsg("");
        setStep("checking");

        try {
            // 100% Real API: Call POST /api/parking/sessions
            const res = await openParkingSession({
                lotId: assignedLot.lotId,
                plateNumber: finalPlate,
                vehicleType,
            });

            setLastOpened(res);
            setStep("session_created");
            emitLiveToast(
                language === "vi"
                    ? "Xác nhận xe vào cổng thành công!"
                    : "Vehicle check-in successful!",
                language === "vi"
                    ? `Biển số ${res.plateNumber} đã qua barrier. Phiếu gửi xe đã sẵn sàng.`
                    : `License plate ${res.plateNumber} passed the gate. The parking ticket is ready.`,
                "success",
            );

            // Trợ lý giọng nói phát âm đón xe vào cổng
            announceCheckIn({
                plateNumber: res.plateNumber,
                slotCode: (res.slotCode ?? res.zoneCode) || undefined,
                lotName: assignedLot?.lotName,
            });

            // Add real session into live staff state (Parking assigns customer type, zone, and slot)
            addSession({
                id: res.sessionId,
                lotId: res.lotId,
                plateNumber: res.plateNumber,
                vehicleType: res.vehicleType,
                customerType: res.customerType || entryType,
                driverName:
                    res.driverName?.trim() ||
                    res.customerName?.trim() ||
                    res.accountOwnerName?.trim() ||
                    (entryType === "Registered"
                        ? "Khách đã đăng ký"
                        : "Khách vãng lai"),
                entryTime: res.entryTime,
                status: "Active",
                qrToken: res.qrToken,
                slotCode:
                    res.slotCode ??
                    res.zoneCode ??
                    (vehicleType === "Car" ? "CAR-A" : "MOTO-B"),
                paymentStatus: "Pending",
                totalFeeVnd: vehicleType === "Car" ? 30000 : 6000,
                prepaidVnd: 0,
                amountDueVnd: vehicleType === "Car" ? 30000 : 6000,
            });

            // If Guest or UnregisteredWalkIn, auto prompt QR ticket modal
            if (
                res.customerType === "Guest" ||
                res.customerType === "UnregisteredWalkIn"
            ) {
                setShowQrModal(true);
            }

            // Barrier opening indicator
            setTimeout(() => {
                setStep("barrier_open");
            }, 1000);
        } catch (err) {
            setErrorMsg(describeStaffError(err));
            announceWarning(describeStaffError(err));
            setStep("camera");
        } finally {
            setLoading(false);
        }
    }

    function resetForNextVehicle() {
        setStep("camera");
        setLastOpened(null);
        setErrorMsg("");
        setIsEditingPlate(false);
        setCameraSnapshot(null);
        const randomPlate = `51K-${Math.floor(100 + Math.random() * 899)}.${Math.floor(10 + Math.random() * 89)}`;
        setPlateNumber(randomPlate);
        setEditedPlate(randomPlate);
        setVehicleType("Car");
        setEntryType("Guest");
        setOcrConfidence(0.98);
    }

    return (
        <div className="staff-page">
            <header className="staff-header">
                <div>
                    <h1 className="staff-title">{t("entryPageTitle")}</h1>
                    <p className="staff-subtitle">
                        {assignedLot?.lotName
                            ? `Bãi trực: ${assignedLot.lotName} · `
                            : ""}
                        {t("entryPageSubtitle")}
                    </p>
                </div>

                <div className="staff-header-actions">
                    <button
                        type="button"
                        className="staff-btn staff-btn-secondary"
                        onClick={resetForNextVehicle}
                    >
                        {t("entryClearBtn")}
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

            {/* Quick Presets Bar */}
            <div className="staff-presets-bar">
                <span className="staff-presets-label">Biển số mẫu:</span>
                <button
                    type="button"
                    className="staff-preset-chip"
                    onClick={() =>
                        loadPreset("59X1-234.56", "Motorbike", "Guest", 0.99)
                    }
                >
                    🛵 {t("staffMotorbike")}: 59X1-234.56
                </button>
                <button
                    type="button"
                    className="staff-preset-chip"
                    onClick={() =>
                        loadPreset("51A-123.45", "Car", "Guest", 0.98)
                    }
                >
                    🚗 {t("staffCar")}: 51A-123.45
                </button>
                <button
                    type="button"
                    className="staff-preset-chip"
                    onClick={() =>
                        loadPreset("51F-994.28", "Car", "Registered", 0.97)
                    }
                >
                    🚗 {t("staffCar")}: 51F-994.28 (Tài khoản)
                </button>
                <button
                    type="button"
                    className="staff-preset-chip chip-warning"
                    onClick={() =>
                        loadPreset("51K-892.4?", "Car", "Guest", 0.62)
                    }
                >
                    ⚠️ Biển số mờ (Cần sửa tay)
                </button>
                <button
                    type="button"
                    className="staff-preset-chip"
                    style={{
                        borderColor: "rgba(59, 130, 246, 0.4)",
                        color: "#60a5fa",
                    }}
                    onClick={() =>
                        loadPreset("51H-999.88", "Car", "Booking", 0.99)
                    }
                >
                    ⭐ Xe đặt trước: 51H-999.88
                </button>
            </div>

            <div className="staff-grid-split">
                {/* Left column: Real Camera ANPR & Detection Feed */}
                <div className="staff-col">
                    <RealCameraFeed
                        title={
                            t("entryCameraTitle") ||
                            "Camera Cổng Vào 01 (Gate In 01)"
                        }
                        mode="entry"
                        detectedPlate={
                            isEditingPlate ? editedPlate : plateNumber
                        }
                        confidence={ocrConfidence}
                        isScanning={loading}
                        onCaptureSnapshot={handleCameraSnapshot}
                    />

                    {/* Snapshot Preview if captured */}
                    {cameraSnapshot && (
                        <div
                            className="staff-card"
                            style={{
                                marginTop: "12px",
                                padding: "10px 14px",
                                display: "flex",
                                alignItems: "center",
                                gap: "12px",
                                background: "#0f172a",
                                border: "1px solid rgba(255, 255, 255, 0.1)",
                            }}
                        >
                            <img
                                src={cameraSnapshot}
                                alt="Ảnh xe vừa chụp"
                                style={{
                                    width: "80px",
                                    height: "55px",
                                    objectFit: "cover",
                                    borderRadius: "4px",
                                    border: "1px solid #10b981",
                                }}
                            />
                            <div
                                style={{
                                    flex: 1,
                                    fontSize: "12px",
                                    color: "#e2e8f0",
                                }}
                            >
                                <strong>Ảnh chụp biển số thực tế</strong>
                                <p
                                    style={{
                                        fontSize: "11px",
                                        color: "#94a3b8",
                                        margin: "2px 0 0",
                                    }}
                                >
                                    Đã lưu frame camera cho phiên đỗ này.
                                </p>
                            </div>
                            <button
                                type="button"
                                className="staff-btn staff-btn-ghost staff-btn-sm"
                                onClick={() => setCameraSnapshot(null)}
                                style={{ color: "#94a3b8" }}
                            >
                                ✕
                            </button>
                        </div>
                    )}

                    {/* License Plate Verification (When confidence is low or staff edits) */}
                    <div className="staff-card staff-plate-editor">
                        <div className="staff-card-header">
                            <h3>{t("entryPlateVerifTitle")}</h3>
                            <span
                                className={`staff-pill ${
                                    ocrConfidence >= 0.8
                                        ? "staff-pill-success"
                                        : "staff-pill-warning"
                                }`}
                            >
                                {t("entryConfidence")}{" "}
                                {(ocrConfidence * 100).toFixed(0)}%
                            </span>
                        </div>

                        {ocrConfidence < 0.8 ? (
                            <p className="staff-warning-box">
                                {t("entryLowConfWarning")}
                            </p>
                        ) : (
                            <p className="staff-card-hint">
                                {t("entryHighConfHint")}
                            </p>
                        )}

                        <div className="staff-form-group">
                            <label htmlFor="staff-edit-plate">
                                {t("entryPlateLabel")}
                            </label>
                            <div className="staff-plate-edit-wrap">
                                <input
                                    id="staff-edit-plate"
                                    type="text"
                                    className="staff-input staff-plate-input"
                                    value={editedPlate}
                                    onChange={(e) => {
                                        setEditedPlate(
                                            e.target.value.toUpperCase(),
                                        );
                                        setIsEditingPlate(true);
                                    }}
                                />
                                {isEditingPlate && (
                                    <button
                                        type="button"
                                        className="staff-btn staff-btn-secondary"
                                        onClick={() => {
                                            setPlateNumber(editedPlate);
                                            setIsEditingPlate(false);
                                        }}
                                    >
                                        {t("entryConfirmPlateBtn")}
                                    </button>
                                )}
                            </div>
                        </div>

                        <div className="staff-form-row">
                            <div className="staff-form-group">
                                <label htmlFor="staff-vehicle-type">
                                    {t("entryVehicleTypeLabel")}
                                </label>
                                <select
                                    id="staff-vehicle-type"
                                    className="staff-select"
                                    value={vehicleType}
                                    onChange={(e) =>
                                        setVehicleType(
                                            e.target.value as VehicleType,
                                        )
                                    }
                                >
                                    <option value="Car">{t("staffCar")}</option>
                                    <option value="Motorbike">
                                        {t("staffMotorbike")}
                                    </option>
                                </select>
                            </div>

                            <div className="staff-form-group">
                                <label htmlFor="staff-cust-category">
                                    {t("entryCustCategoryLabel")}
                                </label>
                                <select
                                    id="staff-cust-category"
                                    className="staff-select"
                                    value={entryType}
                                    onChange={(e) =>
                                        setEntryType(
                                            e.target.value as CustomerType,
                                        )
                                    }
                                >
                                    <option value="Guest">
                                        {t("entryGuestOption")}
                                    </option>
                                    <option value="Registered">
                                        {t("entryRegOption")}
                                    </option>
                                    <option value="Booking">
                                        Xe đặt chỗ (Booking)
                                    </option>
                                </select>
                            </div>
                        </div>
                    </div>
                </div>

                {/* Right column: Entry Workflow Steps */}
                <div className="staff-col">
                    <div className="staff-card">
                        <div className="staff-card-header">
                            <h2>
                                {entryType === "Registered"
                                    ? t("entryFlowRegTitle")
                                    : t("entryFlowGuestTitle")}
                            </h2>
                            <span className="staff-pill staff-pill-active">
                                {entryType === "Registered"
                                    ? t("sessionsRegistered")
                                    : t("sessionsGuest")}
                            </span>
                        </div>

                        {/* Step-by-step indicator */}
                        <div className="staff-stepper">
                            <div className="staff-step done">
                                <span className="step-num">1</span>
                                <span className="step-text">
                                    {t("entryStep1")}
                                </span>
                            </div>
                            <div
                                className={`staff-step ${
                                    step !== "camera" ? "done" : "active"
                                }`}
                            >
                                <span className="step-num">2</span>
                                <span className="step-text">
                                    {t("entryStep2")}
                                </span>
                            </div>
                            <div
                                className={`staff-step ${
                                    step === "session_created" ||
                                    step === "barrier_open"
                                        ? "done"
                                        : step === "checking"
                                          ? "active"
                                          : ""
                                }`}
                            >
                                <span className="step-num">3</span>
                                <span className="step-text">
                                    {entryType === "Registered"
                                        ? t("entryStep3Reg")
                                        : t("entryStep3Guest")}
                                </span>
                            </div>
                            <div
                                className={`staff-step ${
                                    step === "barrier_open"
                                        ? "done"
                                        : step === "session_created"
                                          ? "active"
                                          : ""
                                }`}
                            >
                                <span className="step-num">4</span>
                                <span className="step-text">
                                    {t("entryStep4")}
                                </span>
                            </div>
                            <div
                                className={`staff-step ${
                                    step === "barrier_open"
                                        ? "active done-gate"
                                        : ""
                                }`}
                            >
                                <span className="step-num">5</span>
                                <span className="step-text">
                                    {t("entryStep5")}
                                </span>
                            </div>
                        </div>

                        {/* Verification details summary */}
                        <div className="staff-entry-summary">
                            <div className="staff-summary-row">
                                <span>{t("entrySummaryPlate")}</span>
                                <PlateBadge
                                    plate={
                                        isEditingPlate
                                            ? editedPlate
                                            : plateNumber
                                    }
                                />
                            </div>
                            <div className="staff-summary-row">
                                <span>{t("entrySummaryType")}</span>
                                <strong>
                                    {vehicleType === "Car"
                                        ? t("staffCar")
                                        : t("staffMotorbike")}
                                </strong>
                            </div>
                            <div className="staff-summary-row">
                                <span>{t("entrySummaryCap")}</span>
                                <strong className="text-emerald">
                                    {`${countText(
                                        vehicleType === "Car"
                                            ? occupancy.carAvailable
                                            : occupancy.motorbikeAvailable,
                                    )} ${t("dashSlots")}`}
                                </strong>
                            </div>
                            <div className="staff-summary-row">
                                <span>{t("entrySummaryAccount")}</span>
                                {entryType === "Registered" ? (
                                    <span className="text-emerald">
                                        {(() => {
                                            const activePlate = (
                                                isEditingPlate
                                                    ? editedPlate
                                                    : plateNumber
                                            )
                                                .trim()
                                                .toUpperCase();
                                            const matchedOwner =
                                                lastOpened?.plateNumber?.toUpperCase() ===
                                                activePlate
                                                    ? lastOpened?.driverName?.trim() ||
                                                      lastOpened?.customerName?.trim() ||
                                                      lastOpened?.accountOwnerName?.trim()
                                                    : "";
                                            return matchedOwner
                                                ? `✓ ${matchedOwner} (${language === "en" ? "Wallet active" : "Ví khả dụng"})`
                                                : t("entryAccountLinked");
                                        })()}
                                    </span>
                                ) : (
                                    <span className="text-amber">
                                        {t("entryAccountGuest")}
                                    </span>
                                )}
                            </div>
                        </div>

                        {/* Result state */}
                        {lastOpened && (
                            <div
                                className="staff-card staff-session-token-box"
                                style={{
                                    marginTop: "1rem",
                                    background: "rgba(16, 185, 129, 0.08)",
                                    border: "1px solid rgba(16, 185, 129, 0.3)",
                                    padding: "12px 16px",
                                    borderRadius: "8px",
                                }}
                            >
                                <div
                                    style={{
                                        display: "flex",
                                        justifyContent: "space-between",
                                        alignItems: "center",
                                        marginBottom: "8px",
                                    }}
                                >
                                    <span
                                        style={{
                                            fontSize: "12px",
                                            color: "var(--staff-text-muted)",
                                        }}
                                    >
                                        Mã vé QR từ Server (100% Real API):
                                    </span>
                                    <code
                                        style={{
                                            fontSize: "13px",
                                            fontWeight: "bold",
                                            color: "#10b981",
                                            background: "rgba(0,0,0,0.2)",
                                            padding: "2px 8px",
                                            borderRadius: "4px",
                                        }}
                                    >
                                        {lastOpened.qrToken}
                                    </code>
                                </div>
                                {lastOpened.reservationId && (
                                    <div
                                        style={{
                                            display: "flex",
                                            justifyContent: "space-between",
                                            alignItems: "center",
                                            marginBottom: "8px",
                                            padding: "6px 10px",
                                            background:
                                                "rgba(59, 130, 246, 0.15)",
                                            borderRadius: "4px",
                                            border: "1px solid rgba(59, 130, 246, 0.3)",
                                        }}
                                    >
                                        <span
                                            style={{
                                                fontSize: "12px",
                                                color: "#60a5fa",
                                                fontWeight: 600,
                                            }}
                                        >
                                            ✨ Khách đặt trước (Booking)
                                        </span>
                                        <code
                                            style={{
                                                fontSize: "12px",
                                                color: "#93c5fd",
                                            }}
                                        >
                                            #
                                            {lastOpened.reservationId.slice(
                                                0,
                                                8,
                                            )}
                                        </code>
                                    </div>
                                )}
                                <div
                                    style={{
                                        display: "flex",
                                        justifyContent: "space-between",
                                        alignItems: "center",
                                    }}
                                >
                                    <span
                                        style={{
                                            fontSize: "12px",
                                            color: "var(--staff-text-muted)",
                                        }}
                                    >
                                        Khu / chỗ
                                    </span>
                                    <strong style={{ fontSize: "14px" }}>
                                        {[
                                            lastOpened.floorName,
                                            lastOpened.zoneCode,
                                            lastOpened.slotCode,
                                        ]
                                            .filter(Boolean)
                                            .join(" · ") || "—"}
                                    </strong>
                                </div>
                                <div
                                    style={{
                                        display: "flex",
                                        justifyContent: "space-between",
                                        alignItems: "center",
                                    }}
                                >
                                    <span
                                        style={{
                                            fontSize: "12px",
                                            color: "var(--staff-text-muted)",
                                        }}
                                    >
                                        {vehicleType === "Car"
                                            ? t("entryCarSlotsLeft")
                                            : t("entryMotorbikeSlotsLeft")}
                                    </span>
                                    <strong
                                        style={{
                                            fontSize: "14px",
                                            color: "#10b981",
                                        }}
                                    >
                                        {countText(
                                            vehicleType === "Car"
                                                ? occupancy.carAvailable
                                                : (lastOpened.availableMotorbikeSlots ??
                                                      occupancy.motorbikeAvailable),
                                        )}{" "}
                                        {t("dashSlots")}
                                    </strong>
                                </div>
                            </div>
                        )}

                        {step === "barrier_open" && (
                            <div style={{ marginTop: "1rem" }}>
                                <IoTBarrierGateSimulator
                                    gateId="GATE-IN-01"
                                    gateName="Cổng Vào Xe (Gate In 01)"
                                    gateType="ENTRY"
                                    isOpen={true}
                                    physicalState="OPEN"
                                    vehicleState="PASSING"
                                    detectedPlate={
                                        isEditingPlate
                                            ? editedPlate
                                            : plateNumber
                                    }
                                    vehicleType={vehicleType}
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
                                            {t("entryBarrierRaised")}
                                        </strong>
                                        <p>{t("entryBarrierHint")}</p>
                                    </div>
                                </div>
                            </div>
                        )}

                        {/* Action buttons */}
                        <div className="staff-entry-actions">
                            {step !== "barrier_open" ? (
                                <button
                                    type="button"
                                    className="staff-btn staff-btn-primary staff-btn-lg"
                                    disabled={loading || !plateNumber.trim()}
                                    onClick={handleConfirmEntry}
                                >
                                    {loading
                                        ? t("entryProcessing")
                                        : entryType === "Guest"
                                          ? t("entryBtnGuest")
                                          : t("entryBtnReg")}
                                </button>
                            ) : (
                                <div className="staff-done-actions">
                                    {lastOpened?.qrToken && (
                                        <button
                                            type="button"
                                            className="staff-btn staff-btn-secondary"
                                            onClick={() => setShowQrModal(true)}
                                        >
                                            {t("entryViewQrBtn")}
                                        </button>
                                    )}
                                    <button
                                        type="button"
                                        className="staff-btn staff-btn-primary"
                                        onClick={resetForNextVehicle}
                                    >
                                        {t("entryNextVehicleBtn")}
                                    </button>
                                </div>
                            )}
                        </div>
                    </div>
                </div>
            </div>

            {/* Guest QR Code Modal */}
            {showQrModal && lastOpened && (
                <QrCodeModal
                    qrToken={lastOpened.qrToken}
                    plateNumber={lastOpened.plateNumber}
                    vehicleType={lastOpened.vehicleType}
                    entryTime={lastOpened.entryTime}
                    onClose={() => setShowQrModal(false)}
                />
            )}
        </div>
    );
}
