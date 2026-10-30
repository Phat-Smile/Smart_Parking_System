"use client";

import React, { useState } from "react";
import { CameraFeedMock, PlateBadge, QrCodeModal, countText } from "./StaffUi";
import { describeStaffError, openParkingSession } from "./staffApi";
import { useStaffData } from "./staffData";
import type { CustomerType, OpenedSessionResponse, VehicleType } from "./types";
import { useLanguage } from "@/shared/i18n/LanguageContext";

export function StaffEntryPage() {
    const { addSession, occupancy, assignedLot, fetchAssignedLot } =
        useStaffData();
    const { t } = useLanguage();

    // Load assigned lot on mount
    React.useEffect(() => {
        fetchAssignedLot().catch(() => {});
    }, [fetchAssignedLot]);

    // Mode: registered vs guest
    const [entryType, setEntryType] = useState<CustomerType>("Registered");
    const [plateNumber, setPlateNumber] = useState("51F-994.28");
    const [vehicleType, setVehicleType] = useState<VehicleType>("Car");
    const [ocrConfidence, setOcrConfidence] = useState<number>(0.96);

    // 3.11 License Plate Verification state
    const [isEditingPlate, setIsEditingPlate] = useState(false);
    const [editedPlate, setEditedPlate] = useState("51F-994.28");

    // Stepper state for 3.9 & 3.10
    const [step, setStep] = useState<
        "camera" | "checking" | "session_created" | "barrier_open"
    >("camera");
    const [loading, setLoading] = useState(false);
    const [errorMsg, setErrorMsg] = useState("");
    const [lastOpened, setLastOpened] = useState<OpenedSessionResponse | null>(
        null,
    );
    const [showQrModal, setShowQrModal] = useState(false);

    // Mock presets for testing
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

    async function handleConfirmEntry() {
        setLoading(true);
        setErrorMsg("");
        setStep("checking");

        const finalPlate = isEditingPlate
            ? editedPlate.trim()
            : plateNumber.trim();

        if (!assignedLot) {
            setLoading(false);
            setStep("camera");
            setErrorMsg(
                "Tài khoản chưa được phân công bãi nào, nên chưa mở được phiên.",
            );
            return;
        }

        try {
            // Call the real POST /api/parking/sessions endpoint with real assigned lotId
            const res = await openParkingSession({
                lotId: assignedLot.lotId,
                plateNumber: finalPlate,
                vehicleType,
            });

            setLastOpened(res);
            setStep("session_created");

            // Parking decides the customer type (plate linked to an account or not), the zone and the slot.
            addSession({
                id: res.sessionId,
                lotId: res.lotId,
                plateNumber: res.plateNumber,
                vehicleType: res.vehicleType,
                customerType: res.customerType,
                entryTime: res.entryTime,
                status: "Active",
                qrToken: res.qrToken,
                slotCode: res.slotCode ?? res.zoneCode ?? null,
                paymentStatus: "Pending",
            });

            // If Guest, auto prompt QR ticket modal
            if (res.customerType === "Guest") {
                setShowQrModal(true);
            }

            // Simulate barrier opening
            setTimeout(() => {
                setStep("barrier_open");
            }, 1200);
        } catch (err) {
            setErrorMsg(describeStaffError(err));
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
        setPlateNumber("59L2-349.12");
        setEditedPlate("59L2-349.12");
        setVehicleType("Motorbike");
        setEntryType("Guest");
        setOcrConfidence(0.98);
    }

    return (
        <div className="staff-page">
            <header className="staff-header">
                <div>
                    <h1 className="staff-title">{t("entryPageTitle")}</h1>
                    <p className="staff-subtitle">{t("entryPageSubtitle")}</p>
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

            {/* Top quick simulation bar */}
            <div className="staff-presets-bar">
                <span className="staff-presets-label">
                    {t("entrySimulateLabel")}
                </span>
                <button
                    type="button"
                    className="staff-preset-chip"
                    onClick={() =>
                        loadPreset(
                            "59X1-234.56",
                            "Motorbike",
                            "Registered",
                            0.99,
                        )
                    }
                >
                    🛵 {t("staffMotorbike")}: 59X1-234.56 (Demo A)
                </button>
                <button
                    type="button"
                    className="staff-preset-chip"
                    onClick={() =>
                        loadPreset("51A-123.45", "Car", "Registered", 0.98)
                    }
                >
                    🚗 {t("staffCar")}: 51A-123.45 (Demo A)
                </button>
                <button
                    type="button"
                    className="staff-preset-chip"
                    onClick={() =>
                        loadPreset("51F-994.28", "Car", "Registered", 0.97)
                    }
                >
                    {t("entryPresetRegCar")}
                </button>
                <button
                    type="button"
                    className="staff-preset-chip chip-warning"
                    onClick={() =>
                        loadPreset("51K-892.4?", "Car", "Guest", 0.62)
                    }
                >
                    {t("entryPresetLowConf")}
                </button>
            </div>

            <div className="staff-grid-split">
                {/* Left column: ANPR Camera & Detection Feed */}
                <div className="staff-col">
                    <CameraFeedMock
                        title={t("entryCameraTitle")}
                        detectedPlate={
                            isEditingPlate ? editedPlate : plateNumber
                        }
                        confidence={ocrConfidence}
                        isScanning={loading}
                    />

                    {/* License Plate Verification (When confidence is low) */}
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
                                    <option value="Registered">
                                        {t("entryRegOption")}
                                    </option>
                                    <option value="Guest">
                                        {t("entryGuestOption")}
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
                                        {t("entryAccountLinked")}
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
                                        {t("entryQrTokenLabel")}
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
                            <div className="staff-barrier-open-alert">
                                <div className="barrier-icon">🟢</div>
                                <div className="barrier-content">
                                    <strong>{t("entryBarrierRaised")}</strong>
                                    <p>{t("entryBarrierHint")}</p>
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
                                    {lastOpened?.qrToken &&
                                        entryType === "Guest" && (
                                            <button
                                                type="button"
                                                className="staff-btn staff-btn-secondary"
                                                onClick={() =>
                                                    setShowQrModal(true)
                                                }
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

            {/* 3.10 Guest QR Code Modal */}
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
