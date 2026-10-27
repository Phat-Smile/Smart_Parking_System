"use client";

import React, { useState } from "react";
import { CameraFeedMock, PlateBadge, QrCodeModal } from "./StaffUi";
import { describeStaffError, openParkingSession } from "./staffApi";
import { useStaffData } from "./staffData";
import type { CustomerType, OpenedSessionResponse, VehicleType } from "./types";

export function StaffEntryPage() {
    const { addSession, occupancy, assignedLot, fetchAssignedLot } =
        useStaffData();

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

        try {
            // Call the real POST /api/parking/sessions endpoint with real assigned lotId
            const res = await openParkingSession({
                lotId: assignedLot.lotId,
                plateNumber: finalPlate,
                vehicleType,
            });

            setLastOpened(res);
            setStep("session_created");

            // Add to live staff state
            addSession({
                id: res.sessionId,
                lotId: res.lotId,
                plateNumber: res.plateNumber,
                vehicleType: res.vehicleType,
                customerType: entryType,
                driverName:
                    entryType === "Registered"
                        ? "Nguyễn Văn An"
                        : "Guest Driver",
                entryTime: res.entryTime,
                status: "Active",
                qrToken: res.qrToken,
                slotCode: vehicleType === "Car" ? "B1-A15" : "B2-M28",
                paymentStatus: "Pending",
                totalFeeVnd: vehicleType === "Car" ? 30000 : 6000,
                prepaidVnd: entryType === "Registered" ? 30000 : 0,
                amountDueVnd:
                    entryType === "Registered"
                        ? 0
                        : vehicleType === "Car"
                          ? 30000
                          : 6000,
            });

            // If Guest, auto prompt QR ticket modal
            if (entryType === "Guest") {
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
                    <h1 className="staff-title">
                        Gate In — Vehicle Entry Control
                    </h1>
                    <p className="staff-subtitle">
                        Automated ANPR camera detection, plate correction, and
                        session opening.
                    </p>
                </div>

                <div className="staff-header-actions">
                    <button
                        type="button"
                        className="staff-btn staff-btn-secondary"
                        onClick={resetForNextVehicle}
                    >
                        🔄 Clear / Scan Next Vehicle
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
                    Simulate incoming vehicle:
                </span>
                <button
                    type="button"
                    className="staff-preset-chip"
                    onClick={() =>
                        loadPreset("51F-994.28", "Car", "Registered", 0.97)
                    }
                >
                    🚙 Registered Car (97% OCR)
                </button>
                <button
                    type="button"
                    className="staff-preset-chip"
                    onClick={() =>
                        loadPreset("59T2-771.04", "Motorbike", "Guest", 0.99)
                    }
                >
                    🛵 Guest Motorbike (99% OCR)
                </button>
                <button
                    type="button"
                    className="staff-preset-chip chip-warning"
                    onClick={() =>
                        loadPreset("51K-892.4?", "Car", "Guest", 0.62)
                    }
                >
                    ⚠️ Low Confidence OCR (62%)
                </button>
            </div>

            <div className="staff-grid-split">
                {/* Left column: ANPR Camera & Detection Feed */}
                <div className="staff-col">
                    <CameraFeedMock
                        title="Gate In 01 · HD ANPR Camera"
                        detectedPlate={
                            isEditingPlate ? editedPlate : plateNumber
                        }
                        confidence={ocrConfidence}
                        isScanning={loading}
                    />

                    {/* License Plate Verification (When confidence is low) */}
                    <div className="staff-card staff-plate-editor">
                        <div className="staff-card-header">
                            <h3>License Plate Verification</h3>
                            <span
                                className={`staff-pill ${
                                    ocrConfidence >= 0.8
                                        ? "staff-pill-success"
                                        : "staff-pill-warning"
                                }`}
                            >
                                Confidence: {(ocrConfidence * 100).toFixed(0)}%
                            </span>
                        </div>

                        {ocrConfidence < 0.8 ? (
                            <p className="staff-warning-box">
                                ⚠️ Camera OCR confidence is below threshold
                                (80%). Please review and correct the license
                                plate before opening the gate.
                            </p>
                        ) : (
                            <p className="staff-card-hint">
                                High confidence match detected. Staff can still
                                edit manually if needed.
                            </p>
                        )}

                        <div className="staff-form-group">
                            <label htmlFor="staff-edit-plate">
                                License plate
                            </label>
                            <div className="staff-input-group">
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
                                        Confirm Plate
                                    </button>
                                )}
                            </div>
                        </div>

                        <div className="staff-form-row">
                            <div className="staff-form-group">
                                <label>Vehicle Type</label>
                                <select
                                    className="staff-select"
                                    value={vehicleType}
                                    onChange={(e) =>
                                        setVehicleType(
                                            e.target.value as VehicleType,
                                        )
                                    }
                                >
                                    <option value="Car">Car (Ô tô)</option>
                                    <option value="Motorbike">
                                        Motorbike (Xe máy)
                                    </option>
                                </select>
                            </div>

                            <div className="staff-form-group">
                                <label>Customer Category</label>
                                <select
                                    className="staff-select"
                                    value={entryType}
                                    onChange={(e) =>
                                        setEntryType(
                                            e.target.value as CustomerType,
                                        )
                                    }
                                >
                                    <option value="Registered">
                                        Registered (App account)
                                    </option>
                                    <option value="Guest">
                                        Guest (Vãng lai)
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
                                    ? "Registered Vehicle Entry Flow"
                                    : "Guest Vehicle Entry Flow"}
                            </h2>
                            <span className="staff-pill staff-pill-active">
                                {entryType}
                            </span>
                        </div>

                        {/* Step-by-step indicator */}
                        <div className="staff-stepper">
                            <div className="staff-step done">
                                <span className="step-num">1</span>
                                <span className="step-text">Camera Detect</span>
                            </div>
                            <div
                                className={`staff-step ${
                                    step !== "camera" ? "done" : "active"
                                }`}
                            >
                                <span className="step-num">2</span>
                                <span className="step-text">Plate & Type</span>
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
                                        ? "Account / Reservation"
                                        : "Capacity Check"}
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
                                <span className="step-text">Open Session</span>
                            </div>
                            <div
                                className={`staff-step ${
                                    step === "barrier_open"
                                        ? "active done-gate"
                                        : ""
                                }`}
                            >
                                <span className="step-num">5</span>
                                <span className="step-text">Allow Entry</span>
                            </div>
                        </div>

                        {/* Verification details summary */}
                        <div className="staff-entry-summary">
                            <div className="staff-summary-row">
                                <span>Plate to Authorize:</span>
                                <PlateBadge
                                    plate={
                                        isEditingPlate
                                            ? editedPlate
                                            : plateNumber
                                    }
                                />
                            </div>
                            <div className="staff-summary-row">
                                <span>Vehicle Type:</span>
                                <strong>{vehicleType}</strong>
                            </div>
                            <div className="staff-summary-row">
                                <span>Lot Capacity Available:</span>
                                <strong className="text-emerald">
                                    {vehicleType === "Car"
                                        ? `${occupancy.carAvailable} car slots`
                                        : `${occupancy.motorbikeAvailable} bike slots`}
                                </strong>
                            </div>
                            <div className="staff-summary-row">
                                <span>Account Status:</span>
                                {entryType === "Registered" ? (
                                    <span className="text-emerald">
                                        ✓ Linked: Nguyễn Văn An (Wallet Active)
                                    </span>
                                ) : (
                                    <span className="text-amber">
                                        Guest (No account needed · QR Issued)
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
                                        QR Token phiên gửi xe:
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
                                        Số chỗ xe máy còn trống:
                                    </span>
                                    <strong
                                        style={{
                                            fontSize: "14px",
                                            color: "#10b981",
                                        }}
                                    >
                                        {lastOpened.availableMotorbikeSlots ??
                                            occupancy.motorbikeAvailable}{" "}
                                        chỗ
                                    </strong>
                                </div>
                            </div>
                        )}

                        {step === "barrier_open" && (
                            <div className="staff-barrier-open-alert">
                                <div className="barrier-icon">🟢</div>
                                <div className="barrier-content">
                                    <strong>
                                        BARRIER RAISED — ALLOW ENTRY
                                    </strong>
                                    <p>
                                        Vehicle is clear to enter zone and park.
                                    </p>
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
                                        ? "Processing Entry..."
                                        : entryType === "Guest"
                                          ? "Create Session & Issue QR (Allow Entry)"
                                          : "Verify & Allow Entry"}
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
                                                View Guest QR Ticket
                                            </button>
                                        )}
                                    <button
                                        type="button"
                                        className="staff-btn staff-btn-primary"
                                        onClick={resetForNextVehicle}
                                    >
                                        Process Next Vehicle
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
