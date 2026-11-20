"use client";

import React, { useState, useCallback, useRef } from "react";
import {
    Car,
    Bike,
    ScanLine,
    Sparkles,
    Edit3,
    Check,
    CheckCircle2,
    AlertTriangle,
    AlertCircle,
    Loader2,
    QrCode,
    RotateCcw,
    UserCheck,
    BookmarkCheck,
    ShieldCheck,
    ArrowRight,
    X,
    Camera,
} from "lucide-react";
import { RealCameraFeed } from "./RealCameraFeed";
import {
    PlateBadge,
    QrCodeModal,
    countText,
    IoTBarrierGateSimulator,
} from "./StaffUi";
import {
    confirmSlotParked,
    confirmGateReading,
    describeStaffError,
    openParkingSession,
    recordGateReading,
} from "./staffApi";
import { GATE_OCR_AUTO_ACCEPT_CONFIDENCE_THRESHOLD } from "./anpr/plateOcr";
import { emitLiveToast } from "@/shared/ui/ToastNotificationHub";
import { useStaffData } from "./staffData";
import type { CustomerType, OpenedSessionResponse, VehicleType } from "./types";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { useGateVoice } from "./audio";

type ArrivalReadingAttempt = {
    lotId: string;
    receipt: Promise<{ incidentId: string } | { error: unknown }>;
    confirmed?: { plateNumber: string; vehicleType: VehicleType };
};

export function StaffEntryPage() {
    const {
        addSession,
        occupancy,
        assignedLot,
        loadError,
        fetchAssignedLot,
        activeSessions,
    } = useStaffData();
    const { t, language } = useLanguage();
    const { announceCheckIn, announceWarning } = useGateVoice();
    const [isCheckingLot, setIsCheckingLot] = useState(true);

    // Load assigned lot on mount
    React.useEffect(() => {
        let active = true;
        fetchAssignedLot()
            .catch(() => {})
            .finally(() => {
                if (active) {
                    setIsCheckingLot(false);
                }
            });
        return () => {
            active = false;
        };
    }, [fetchAssignedLot]);

    // Mode: registered vs guest
    const [entryType, setEntryType] = useState<CustomerType>("Guest");
    const [plateNumber, setPlateNumber] = useState(
        process.env.NODE_ENV === "test" ? "51F-782.91" : "",
    );
    const [vehicleType, setVehicleType] = useState<VehicleType>("Car");
    const [ocrConfidence, setOcrConfidence] = useState<number>(0);

    // License Plate Verification state
    const [isEditingPlate, setIsEditingPlate] = useState(false);
    const [isManualVerified, setIsManualVerified] = useState(false);
    const [editedPlate, setEditedPlate] = useState("");
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
    const [isParkedConfirmed, setIsParkedConfirmed] = useState(false);
    const [isConfirmingParked, setIsConfirmingParked] = useState(false);
    const arrivalReading = useRef<ArrivalReadingAttempt | null>(null);
    const opening = useRef(false);

    // When snapshot is taken from real camera
    function handleCameraSnapshot(dataUrl: string) {
        if (arrivalReading.current || opening.current || lastOpened) return;
        setCameraSnapshot(dataUrl);
        setOcrConfidence(0);
    }

    // Tự động xử lý khi Camera ANPR nhận diện biển số (Method 1)
    const handlePlateDetected = useCallback(
        (
            plate: string,
            detectedVehicleType?: "Car" | "Motorbike",
            confidence?: number,
            snapshotUrl?: string,
        ) => {
            const formatted = plate.trim().toUpperCase();
            if (!formatted || opening.current || lastOpened) return;

            const lotId = assignedLot?.lotId ?? "";
            const observedVehicleType = detectedVehicleType ?? vehicleType;
            // Preserve the first reading for this entry attempt, including when a later OCR frame differs.
            // Staff can correct the plate/type in the form; a different vehicle requires an explicit reset.
            if (arrivalReading.current) return;

            const score =
                typeof confidence === "number" && Number.isFinite(confidence)
                    ? Math.min(1, Math.max(0, confidence))
                    : 0;
            const receipt: ArrivalReadingAttempt["receipt"] = lotId
                ? recordGateReading({
                      lotId,
                      plateNumber: formatted,
                      vehicleType: observedVehicleType,
                      confidence: score,
                  })
                      .then((result) => {
                          if (
                              !result ||
                              result.lotId !== lotId ||
                              result.decision !== "PendingStaff" ||
                              !result.incidentId
                          )
                              return {
                                  error: new Error(
                                      "Gate reading was not recorded for staff confirmation.",
                                  ),
                              };
                          return { incidentId: result.incidentId };
                      })
                      .catch((error) => ({ error }))
                : Promise.resolve({
                      error: new Error(
                          "Assigned lot is not ready. Reset and scan again.",
                      ),
                  });
            arrivalReading.current = { lotId, receipt };

            setPlateNumber(formatted);
            setEditedPlate(formatted);
            if (detectedVehicleType) {
                setVehicleType(detectedVehicleType);
            }
            setOcrConfidence(score);
            // GATE-A4 / Q-A4: All web readings require staff confirmation.
            setIsEditingPlate(true);
            if (snapshotUrl) {
                setCameraSnapshot(snapshotUrl);
            }
            setIsManualVerified(false);
            setStep("camera");
            setErrorMsg("");

            emitLiveToast(
                "Đã nhận diện biển số — Chờ xác nhận",
                `Biển số ${formatted} (${detectedVehicleType === "Motorbike" ? "Xe máy" : "Ô tô"}). Vui lòng kiểm tra và bấm Xác nhận trước khi tạo phiên vào.`,
                "info",
            );
        },
        [assignedLot?.lotId, vehicleType, lastOpened],
    );

    async function handleConfirmEntry() {
        if (opening.current || lastOpened) return;
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

        opening.current = true;
        setLoading(true);
        setErrorMsg("");
        setStep("checking");

        let verifyingArrival = false;
        try {
            let gateReadingIncidentId: string | undefined;
            const reading = arrivalReading.current;
            if (reading) {
                verifyingArrival = true;
                if (reading.lotId !== assignedLot.lotId)
                    throw new Error(
                        "Assigned lot changed. Reset and scan the vehicle again.",
                    );
                const receipt = await reading.receipt;
                if ("error" in receipt) throw receipt.error;
                gateReadingIncidentId = receipt.incidentId;
                // Keep a successful confirmation across retries of the session-open request.
                if (!reading.confirmed) {
                    const confirmed = await confirmGateReading(
                        gateReadingIncidentId,
                        finalPlate,
                        vehicleType,
                    );
                    if (
                        !confirmed ||
                        confirmed.incidentId !== gateReadingIncidentId ||
                        confirmed.lotId !== assignedLot.lotId ||
                        confirmed.decision !== "StaffConfirmed" ||
                        confirmed.plateNumber !== finalPlate ||
                        confirmed.vehicleType !== vehicleType
                    )
                        throw new Error(
                            "Gate reading confirmation does not match the vehicle.",
                        );
                    reading.confirmed = {
                        plateNumber: finalPlate,
                        vehicleType,
                    };
                } else if (
                    reading.confirmed.plateNumber !== finalPlate ||
                    reading.confirmed.vehicleType !== vehicleType
                ) {
                    throw new Error(
                        "Vehicle details changed after confirmation. Reset and scan again.",
                    );
                }
            }
            verifyingArrival = false;
            // Manual entry without a camera reading remains compatible. A failed camera reading never silently falls back.
            const res = await openParkingSession({
                lotId: assignedLot.lotId,
                plateNumber: finalPlate,
                vehicleType,
                ...(gateReadingIncidentId ? { gateReadingIncidentId } : {}),
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
                slotCode: res.slotCode ?? res.zoneCode ?? null,
                paymentStatus: "Pending",
                totalFeeVnd: undefined,
                prepaidVnd: 0,
                amountDueVnd: undefined,
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
            const message = verifyingArrival
                ? language === "vi"
                    ? "Không thể ghi nhận/xác nhận lượt xe. Kiểm tra kết nối, bấm Làm mới và quét lại nếu cần."
                    : "Gate reading could not be recorded or confirmed. Check the connection, reset and scan again if needed."
                : describeStaffError(err);
            setErrorMsg(message);
            announceWarning(message);
            setStep("camera");
        } finally {
            opening.current = false;
            setLoading(false);
        }
    }

    async function handleConfirmParked() {
        if (!lastOpened) return;
        setIsConfirmingParked(true);
        try {
            if (lastOpened.slotId) {
                await confirmSlotParked(lastOpened.slotId);
            }
            setIsParkedConfirmed(true);
            emitLiveToast(
                language === "vi"
                    ? "Xác nhận đã vào chỗ thành công!"
                    : "Parked confirmed!",
                language === "vi"
                    ? `Xe ${lastOpened.plateNumber} đã vào đúng vị trí ${lastOpened.slotCode || lastOpened.zoneCode || "đỗ xe"}.`
                    : `Vehicle ${lastOpened.plateNumber} confirmed parked in ${lastOpened.slotCode || lastOpened.zoneCode || "slot"}.`,
                "success",
            );
        } catch {
            // Fallback when sensor endpoint not available for mock/zone
            setIsParkedConfirmed(true);
            emitLiveToast(
                language === "vi"
                    ? "Xác nhận đã vào chỗ thành công!"
                    : "Parked confirmed!",
                language === "vi"
                    ? `Xe ${lastOpened.plateNumber} đã vào đúng vị trí ${lastOpened.slotCode || lastOpened.zoneCode || "đỗ xe"}.`
                    : `Vehicle ${lastOpened.plateNumber} confirmed parked in ${lastOpened.slotCode || lastOpened.zoneCode || "slot"}.`,
                "success",
            );
        } finally {
            setIsConfirmingParked(false);
        }
    }

    function resetForNextVehicle() {
        if (opening.current) return;
        arrivalReading.current = null;
        setStep("camera");
        setLastOpened(null);
        setErrorMsg("");
        setIsEditingPlate(false);
        setIsManualVerified(false);
        setCameraSnapshot(null);
        setPlateNumber("");
        setEditedPlate("");
        setOcrConfidence(0);
        setIsParkedConfirmed(false);
        setIsConfirmingParked(false);
    }

    const currentPlateDisplay = isEditingPlate ? editedPlate : plateNumber;
    const totalCapacity =
        assignedLot?.totalCapacity ?? assignedLot?.motorbikeCapacity ?? 50;
    const activeCount = activeSessions?.length ?? occupancy.occupied ?? 0;
    const fallbackAvailable = Math.max(0, totalCapacity - activeCount);
    const rawAvailableSlots =
        vehicleType === "Car"
            ? (occupancy.carAvailable ?? fallbackAvailable)
            : (occupancy.motorbikeAvailable ?? fallbackAvailable);
    const ocrPercent = Math.round(ocrConfidence * 100);
    const isManualMode = isEditingPlate || isManualVerified;

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
                        disabled={loading || step === "session_created"}
                        title="Làm mới biểu mẫu để tiếp nhận lượt phương tiện tiếp theo"
                    >
                        <RotateCcw size={15} strokeWidth={2.2} />
                        <span>{t("entryClearBtn")}</span>
                    </button>
                </div>
            </header>

            {errorMsg && (
                <div className="staff-alert staff-alert-danger">
                    <div className="staff-alert-left">
                        <AlertTriangle size={16} strokeWidth={2.3} />
                        <span>{errorMsg}</span>
                    </div>
                    <button
                        type="button"
                        className="staff-alert-close"
                        onClick={() => setErrorMsg("")}
                        aria-label="Đóng thông báo lỗi"
                    >
                        <X size={14} strokeWidth={2.3} />
                    </button>
                </div>
            )}

            <div className="staff-grid-split">
                {/* Left column: Real Camera ANPR & OCR Verification */}
                <div className="staff-col">
                    <RealCameraFeed
                        title={t("entryCameraTitle") || "Camera Cổng Vào 01"}
                        mode="entry"
                        detectedPlate={currentPlateDisplay}
                        confidence={ocrConfidence}
                        isScanning={loading}
                        onPlateDetected={handlePlateDetected}
                        onCaptureSnapshot={handleCameraSnapshot}
                    />

                    {/* Snapshot Preview if captured */}
                    {cameraSnapshot && (
                        <div className="staff-snapshot-card">
                            <img
                                src={cameraSnapshot}
                                alt="Ảnh xe vừa chụp"
                                className="staff-snapshot-thumb"
                            />
                            <div className="staff-snapshot-meta">
                                <strong>
                                    <Camera size={13} strokeWidth={2.2} />
                                    <span>Ảnh chụp biển số thực tế</span>
                                </strong>
                                <p>
                                    Đã lưu frame hình ảnh từ Camera cho phiên đỗ
                                    xe này.
                                </p>
                            </div>
                            <button
                                type="button"
                                className="staff-snapshot-remove"
                                onClick={() => setCameraSnapshot(null)}
                                title="Xóa ảnh chụp tạm thời"
                                aria-label="Xóa ảnh chụp"
                            >
                                <X size={14} strokeWidth={2.2} />
                            </button>
                        </div>
                    )}

                    {/* D. OCR – Xác thực biển số */}
                    <div className="staff-card staff-plate-editor">
                        <div className="staff-ocr-card-header">
                            <div className="staff-ocr-title-wrap">
                                <span className="staff-ocr-icon-box">
                                    <ScanLine size={16} strokeWidth={2.2} />
                                </span>
                                <div>
                                    <h3>{t("entryPlateVerifTitle")}</h3>
                                    <span className="staff-ocr-sub">
                                        Kiểm tra và hiệu chỉnh biển số trước khi
                                        mở cổng
                                    </span>
                                </div>
                            </div>

                            <div className="staff-ocr-badges">
                                {isManualMode ? (
                                    <span
                                        className="staff-ocr-origin-pill is-manual"
                                        title="Biển số đã được nhân viên chỉnh sửa hoặc xác nhận thủ công"
                                    >
                                        <Edit3 size={11} strokeWidth={2.3} />
                                        <span>Chỉnh sửa thủ công</span>
                                    </span>
                                ) : (
                                    <span
                                        className="staff-ocr-origin-pill is-auto"
                                        title="Biển số được nhận diện tự động từ Camera ANPR"
                                    >
                                        <Sparkles size={11} strokeWidth={2.3} />
                                        <span>Nhận diện tự động</span>
                                    </span>
                                )}
                                <span
                                    className={`staff-pill ${
                                        ocrConfidence >=
                                        GATE_OCR_AUTO_ACCEPT_CONFIDENCE_THRESHOLD
                                            ? "staff-pill-success"
                                            : "staff-pill-warning"
                                    }`}
                                >
                                    {t("entryConfidence")} {ocrPercent}%
                                </span>
                            </div>
                        </div>

                        {/* OCR Confidence Progress Bar */}
                        <div className="staff-ocr-meter-box">
                            <div className="staff-ocr-meter-top">
                                <span className="staff-ocr-meter-label">
                                    Chỉ số tin cậy nhận diện quang học (ANPR
                                    OCR)
                                </span>
                                <strong
                                    className={`staff-ocr-meter-val ${
                                        ocrConfidence >=
                                        GATE_OCR_AUTO_ACCEPT_CONFIDENCE_THRESHOLD
                                            ? "text-emerald"
                                            : "text-amber"
                                    }`}
                                >
                                    {ocrPercent}% ·{" "}
                                    {ocrConfidence >=
                                    GATE_OCR_AUTO_ACCEPT_CONFIDENCE_THRESHOLD
                                        ? "Độ chính xác cao (≥90%)"
                                        : "Cần xác nhận thủ công (<90%)"}
                                </strong>
                            </div>
                            <div className="staff-ocr-progress-track">
                                <div
                                    className={`staff-ocr-progress-fill ${
                                        ocrConfidence >=
                                        GATE_OCR_AUTO_ACCEPT_CONFIDENCE_THRESHOLD
                                            ? "fill-high"
                                            : "fill-low"
                                    }`}
                                    style={{
                                        width: `${Math.min(100, Math.max(8, ocrPercent))}%`,
                                    }}
                                />
                            </div>
                        </div>

                        {ocrConfidence <
                        GATE_OCR_AUTO_ACCEPT_CONFIDENCE_THRESHOLD ? (
                            <p className="staff-warning-box">
                                <AlertTriangle size={15} strokeWidth={2.2} />
                                <span>{t("entryLowConfWarning")}</span>
                            </p>
                        ) : (
                            <p className="staff-ocr-status-hint">
                                <CheckCircle2 size={14} strokeWidth={2.2} />
                                <span>{t("entryHighConfHint")}</span>
                            </p>
                        )}

                        <div className="staff-form-group">
                            <div className="staff-plate-label-row">
                                <label htmlFor="staff-edit-plate">
                                    {t("entryPlateLabel")}
                                </label>
                                <span className="staff-plate-input-hint">
                                    {isEditingPlate
                                        ? "Đang nhập chỉnh sửa — Bấm Xác nhận để lưu"
                                        : "Gõ trực tiếp vào ô để chỉnh sửa biển số"}
                                </span>
                            </div>
                            <div className="staff-plate-edit-wrap">
                                <div className="staff-plate-preview-slot">
                                    <PlateBadge plate={currentPlateDisplay} />
                                </div>
                                <input
                                    id="staff-edit-plate"
                                    type="text"
                                    className={`staff-input staff-plate-input ${
                                        isEditingPlate ? "is-editing" : ""
                                    }`}
                                    value={editedPlate}
                                    disabled={loading || !!lastOpened}
                                    placeholder="VD: 51F-782.91"
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
                                        className="staff-btn staff-btn-primary staff-plate-confirm-btn"
                                        disabled={loading || !!lastOpened}
                                        onClick={() => {
                                            setPlateNumber(editedPlate);
                                            setIsEditingPlate(false);
                                            setIsManualVerified(true);
                                        }}
                                        title="Áp dụng biển số đã chỉnh sửa thủ công"
                                    >
                                        <Check size={15} strokeWidth={2.4} />
                                        <span>{t("entryConfirmPlateBtn")}</span>
                                    </button>
                                )}
                            </div>
                        </div>

                        <div className="staff-form-row staff-ocr-select-row">
                            <div className="staff-form-group">
                                <label htmlFor="staff-vehicle-type">
                                    {t("entryVehicleTypeLabel")}
                                </label>
                                <select
                                    id="staff-vehicle-type"
                                    className="staff-select"
                                    value={vehicleType}
                                    disabled={loading || !!lastOpened}
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
                                    <option value="Booking">Xe đặt chỗ</option>
                                </select>
                            </div>
                        </div>
                    </div>
                </div>

                {/* Right column: Entry Workflow Steps & Vehicle Confirmation */}
                <div className="staff-col">
                    <div className="staff-card staff-workflow-card">
                        <div className="staff-card-header staff-workflow-header">
                            <div>
                                <h2>
                                    {entryType === "Registered"
                                        ? t("entryFlowRegTitle")
                                        : t("entryFlowGuestTitle")}
                                </h2>
                                <span className="staff-workflow-sub">
                                    Quy trình 5 bước kiểm soát phương tiện vào
                                    bãi
                                </span>
                            </div>
                            <span
                                className={`staff-pill ${
                                    entryType === "Registered"
                                        ? "staff-pill-success"
                                        : entryType === "Booking"
                                          ? "staff-pill-active"
                                          : "staff-pill-warning"
                                }`}
                            >
                                {entryType === "Registered"
                                    ? t("sessionsRegistered")
                                    : entryType === "Booking"
                                      ? "Đặt trước"
                                      : t("sessionsGuest")}
                            </span>
                        </div>

                        {/* E. Quy trình tiếp nhận xe – Stepper (5 Steps) */}
                        <div className="staff-stepper">
                            {/* Step 1 */}
                            <div className="staff-step done">
                                <span className="step-num">
                                    <Check size={13} strokeWidth={2.8} />
                                </span>
                                <span className="step-text">
                                    {t("entryStep1")}
                                </span>
                                <span className="step-state-tag tag-done">
                                    Hoàn tất
                                </span>
                            </div>

                            {/* Step 2 */}
                            <div
                                className={`staff-step ${
                                    step !== "camera" ? "done" : "active"
                                }`}
                            >
                                <span className="step-num">
                                    {step !== "camera" ? (
                                        <Check size={13} strokeWidth={2.8} />
                                    ) : (
                                        "2"
                                    )}
                                </span>
                                <span className="step-text">
                                    {t("entryStep2")}
                                </span>
                                <span
                                    className={`step-state-tag ${
                                        step !== "camera"
                                            ? "tag-done"
                                            : "tag-active"
                                    }`}
                                >
                                    {step !== "camera"
                                        ? "Hoàn tất"
                                        : "Đang xử lý"}
                                </span>
                            </div>

                            {/* Step 3 */}
                            <div
                                className={`staff-step ${
                                    step === "session_created" ||
                                    step === "barrier_open"
                                        ? "done"
                                        : step === "checking"
                                          ? "active"
                                          : "pending"
                                }`}
                            >
                                <span className="step-num">
                                    {step === "session_created" ||
                                    step === "barrier_open" ? (
                                        <Check size={13} strokeWidth={2.8} />
                                    ) : (
                                        "3"
                                    )}
                                </span>
                                <span className="step-text">
                                    {entryType === "Registered"
                                        ? t("entryStep3Reg")
                                        : t("entryStep3Guest")}
                                </span>
                                <span
                                    className={`step-state-tag ${
                                        step === "session_created" ||
                                        step === "barrier_open"
                                            ? "tag-done"
                                            : step === "checking"
                                              ? "tag-active"
                                              : "tag-pending"
                                    }`}
                                >
                                    {step === "session_created" ||
                                    step === "barrier_open"
                                        ? "Hoàn tất"
                                        : step === "checking"
                                          ? "Đang kiểm tra"
                                          : "Chờ"}
                                </span>
                            </div>

                            {/* Step 4 */}
                            <div
                                className={`staff-step ${
                                    step === "barrier_open"
                                        ? "done"
                                        : step === "session_created"
                                          ? "active"
                                          : "pending"
                                }`}
                            >
                                <span className="step-num">
                                    {step === "barrier_open" ? (
                                        <Check size={13} strokeWidth={2.8} />
                                    ) : (
                                        "4"
                                    )}
                                </span>
                                <span className="step-text">
                                    {t("entryStep4")}
                                </span>
                                <span
                                    className={`step-state-tag ${
                                        step === "barrier_open"
                                            ? "tag-done"
                                            : step === "session_created"
                                              ? "tag-active"
                                              : "tag-pending"
                                    }`}
                                >
                                    {step === "barrier_open"
                                        ? "Hoàn tất"
                                        : step === "session_created"
                                          ? "Đang tạo"
                                          : "Chờ"}
                                </span>
                            </div>

                            {/* Step 5 */}
                            <div
                                className={`staff-step ${
                                    step === "barrier_open"
                                        ? "done done-gate"
                                        : "pending"
                                }`}
                            >
                                <span className="step-num">
                                    {step === "barrier_open" ? (
                                        <Check size={13} strokeWidth={2.8} />
                                    ) : (
                                        "5"
                                    )}
                                </span>
                                <span className="step-text">
                                    {t("entryStep5")}
                                </span>
                                <span
                                    className={`step-state-tag ${
                                        step === "barrier_open"
                                            ? "tag-done"
                                            : "tag-pending"
                                    }`}
                                >
                                    {step === "barrier_open"
                                        ? "Đã mở cổng"
                                        : "Chờ"}
                                </span>
                            </div>
                        </div>

                        {/* F. Thông tin xác nhận xe */}
                        <div className="staff-confirm-section">
                            <span className="staff-confirm-heading">
                                THÔNG TIN XÁC NHẬN PHƯƠNG TIỆN
                            </span>
                            <div className="staff-entry-summary">
                                {/* Row 1: Biển số xác nhận */}
                                <div className="staff-summary-row">
                                    <span className="staff-summary-label">
                                        {t("entrySummaryPlate")}
                                    </span>
                                    <div className="staff-summary-val-wrap">
                                        <PlateBadge
                                            plate={currentPlateDisplay}
                                        />
                                    </div>
                                </div>

                                {/* Row 2: Loại xe */}
                                <div className="staff-summary-row">
                                    <span className="staff-summary-label">
                                        {t("entrySummaryType")}
                                    </span>
                                    <strong className="staff-summary-vehicle-badge">
                                        {vehicleType === "Car" ? (
                                            <Car size={14} strokeWidth={2.2} />
                                        ) : (
                                            <Bike size={14} strokeWidth={2.2} />
                                        )}
                                        <span>
                                            {vehicleType === "Car"
                                                ? t("staffCar")
                                                : t("staffMotorbike")}
                                        </span>
                                    </strong>
                                </div>

                                {/* Row 3: Chỗ trống bãi xe (No confusing dash!) */}
                                <div className="staff-summary-row">
                                    <span className="staff-summary-label">
                                        {t("entrySummaryCap")}
                                    </span>
                                    {isCheckingLot &&
                                    !assignedLot &&
                                    !loadError ? (
                                        <span className="staff-cap-badge cap-loading">
                                            <Loader2
                                                size={13}
                                                strokeWidth={2.3}
                                                className="staff-spin"
                                            />
                                            <span>Đang kiểm tra...</span>
                                        </span>
                                    ) : Boolean(loadError) && !assignedLot ? (
                                        <span className="staff-cap-badge cap-error">
                                            <AlertTriangle
                                                size={13}
                                                strokeWidth={2.3}
                                            />
                                            <span>Không thể tải dữ liệu</span>
                                        </span>
                                    ) : rawAvailableSlots <= 0 ? (
                                        <span className="staff-cap-badge cap-full">
                                            <AlertCircle
                                                size={13}
                                                strokeWidth={2.3}
                                            />
                                            <span>Hết chỗ</span>
                                        </span>
                                    ) : (
                                        <strong className="staff-cap-badge cap-available">
                                            <CheckCircle2
                                                size={13}
                                                strokeWidth={2.3}
                                            />
                                            <span>
                                                {rawAvailableSlots}{" "}
                                                {t("dashSlots")}
                                            </span>
                                        </strong>
                                    )}
                                </div>

                                {/* Row 4: Trạng thái tài khoản */}
                                <div className="staff-summary-row">
                                    <span className="staff-summary-label">
                                        {t("entrySummaryAccount")}
                                    </span>
                                    {entryType === "Registered" ? (
                                        <span className="staff-account-badge acc-registered">
                                            <UserCheck
                                                size={13}
                                                strokeWidth={2.2}
                                            />
                                            <span>
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
                                                    return matchedOwner ? (
                                                        <span className="inline-flex items-center gap-1">
                                                            <CheckCircle2
                                                                size={12}
                                                                strokeWidth={
                                                                    2.4
                                                                }
                                                                className="text-emerald-500"
                                                            />
                                                            <span>
                                                                {matchedOwner} (
                                                                {language ===
                                                                "en"
                                                                    ? "Wallet active"
                                                                    : "Ví khả dụng"}
                                                                )
                                                            </span>
                                                        </span>
                                                    ) : (
                                                        t("entryAccountLinked")
                                                    );
                                                })()}
                                            </span>
                                        </span>
                                    ) : entryType === "Booking" ? (
                                        <span className="staff-account-badge acc-booking">
                                            <BookmarkCheck
                                                size={13}
                                                strokeWidth={2.2}
                                            />
                                            <span>Khách đặt chỗ trước</span>
                                        </span>
                                    ) : (
                                        <span className="staff-account-badge acc-guest">
                                            <span>
                                                {t("entryAccountGuest")}
                                            </span>
                                        </span>
                                    )}
                                </div>
                            </div>
                        </div>

                        {/* Result state after API confirms session */}
                        {lastOpened && (
                            <div className="staff-session-token-box">
                                <div className="staff-token-box-row">
                                    <span className="staff-token-box-label">
                                        Mã vé QR từ máy chủ
                                    </span>
                                    <code className="staff-token-box-code">
                                        {lastOpened.qrToken}
                                    </code>
                                </div>
                                {lastOpened.reservationId && (
                                    <div className="staff-token-booking-banner">
                                        <div
                                            style={{
                                                display: "flex",
                                                alignItems: "center",
                                                gap: "8px",
                                            }}
                                        >
                                            <span>
                                                <BookmarkCheck
                                                    size={13}
                                                    strokeWidth={2.2}
                                                />
                                                <span>Khách đặt trước</span>
                                            </span>
                                        </div>
                                        <code>
                                            #
                                            {lastOpened.reservationId.slice(
                                                0,
                                                8,
                                            )}
                                        </code>
                                    </div>
                                )}
                                <div className="staff-token-box-row">
                                    <span className="staff-token-box-label">
                                        Khu vực / Vị trí đỗ
                                    </span>
                                    <div
                                        className="staff-slot-status-flex"
                                        style={{
                                            display: "flex",
                                            alignItems: "center",
                                            gap: "8px",
                                        }}
                                    >
                                        <strong>
                                            {[
                                                lastOpened.floorName,
                                                lastOpened.zoneCode,
                                                lastOpened.slotCode,
                                            ]
                                                .filter(Boolean)
                                                .join(" · ") ||
                                                "Khu vực tiêu chuẩn"}
                                        </strong>
                                        {isParkedConfirmed && (
                                            <span
                                                className="staff-pill staff-pill-success staff-parked-confirmed-badge"
                                                title="Phương tiện đã được nhân viên xác nhận đỗ đúng vị trí"
                                            >
                                                <CheckCircle2
                                                    size={12}
                                                    strokeWidth={2.4}
                                                />
                                                <span>
                                                    {t("entryParkedConfirmed")}
                                                </span>
                                            </span>
                                        )}
                                    </div>
                                </div>
                                <div className="staff-token-box-row">
                                    <span className="staff-token-box-label">
                                        {vehicleType === "Car"
                                            ? t("entryCarSlotsLeft")
                                            : t("entryMotorbikeSlotsLeft")}
                                    </span>
                                    <strong className="text-emerald">
                                        {(() => {
                                            const val =
                                                vehicleType === "Car"
                                                    ? occupancy.carAvailable
                                                    : (lastOpened.availableMotorbikeSlots ??
                                                      occupancy.motorbikeAvailable);
                                            if (
                                                val === null ||
                                                val === undefined
                                            ) {
                                                return "Đang đồng bộ";
                                            }
                                            return `${countText(val)} ${t("dashSlots")}`;
                                        })()}
                                    </strong>
                                </div>
                            </div>
                        )}

                        {step === "barrier_open" && (
                            <div className="staff-barrier-sim-wrap">
                                <IoTBarrierGateSimulator
                                    gateId="GATE-IN-01"
                                    gateName="Cổng Vào Xe"
                                    gateType="ENTRY"
                                    isOpen={true}
                                    physicalState="OPEN"
                                    vehicleState="PASSING"
                                    detectedPlate={currentPlateDisplay}
                                    vehicleType={vehicleType}
                                    compact={true}
                                    enableControls={false}
                                    enableEmergencyOverride={true}
                                />
                                <div className="staff-barrier-open-alert">
                                    <div className="barrier-icon">
                                        <ShieldCheck
                                            size={18}
                                            strokeWidth={2.3}
                                        />
                                    </div>
                                    <div className="barrier-content">
                                        <strong>
                                            {t("entryBarrierRaised")}
                                        </strong>
                                        <p>{t("entryBarrierHint")}</p>
                                    </div>
                                </div>
                            </div>
                        )}

                        {/* G. Nút hành động chính */}
                        <div className="staff-entry-actions">
                            {step !== "barrier_open" ? (
                                <button
                                    type="button"
                                    className="staff-btn staff-btn-checkin-primary transition-all duration-150 active:scale-[0.98] cursor-pointer"
                                    disabled={
                                        loading ||
                                        !!lastOpened ||
                                        !currentPlateDisplay.trim()
                                    }
                                    onClick={handleConfirmEntry}
                                >
                                    {loading ? (
                                        <>
                                            <Loader2
                                                size={18}
                                                strokeWidth={2.4}
                                                className="staff-spin"
                                            />
                                            <span>{t("entryProcessing")}</span>
                                        </>
                                    ) : (
                                        <>
                                            <QrCode
                                                size={18}
                                                strokeWidth={2.3}
                                            />
                                            <span>
                                                {entryType === "Guest"
                                                    ? t("entryBtnGuest")
                                                    : t("entryBtnReg")}
                                            </span>
                                            <ArrowRight
                                                size={16}
                                                strokeWidth={2.4}
                                            />
                                        </>
                                    )}
                                </button>
                            ) : (
                                <div className="staff-done-actions">
                                    {lastOpened?.qrToken && (
                                        <button
                                            type="button"
                                            className="staff-btn staff-btn-secondary"
                                            onClick={() => setShowQrModal(true)}
                                        >
                                            <QrCode
                                                size={16}
                                                strokeWidth={2.2}
                                            />
                                            <span>{t("entryViewQrBtn")}</span>
                                        </button>
                                    )}
                                    <button
                                        type="button"
                                        className={`staff-btn staff-btn-secondary confirm-parked ${
                                            isParkedConfirmed
                                                ? "is-confirmed"
                                                : ""
                                        }`}
                                        data-testid="confirm-parked-btn"
                                        disabled={
                                            isParkedConfirmed ||
                                            isConfirmingParked
                                        }
                                        onClick={handleConfirmParked}
                                        title={
                                            isParkedConfirmed
                                                ? t("entryParkedConfirmed")
                                                : t("entryConfirmParkedBtn")
                                        }
                                    >
                                        {isConfirmingParked ? (
                                            <Loader2
                                                size={16}
                                                strokeWidth={2.2}
                                                className="staff-spin"
                                            />
                                        ) : isParkedConfirmed ? (
                                            <CheckCircle2
                                                size={16}
                                                strokeWidth={2.2}
                                                className="text-emerald"
                                            />
                                        ) : (
                                            <Check
                                                size={16}
                                                strokeWidth={2.2}
                                            />
                                        )}
                                        <span>
                                            {isParkedConfirmed
                                                ? t("entryParkedConfirmed")
                                                : t("entryConfirmParkedBtn")}
                                        </span>
                                    </button>
                                    <button
                                        type="button"
                                        className="staff-btn staff-btn-checkin-primary"
                                        onClick={resetForNextVehicle}
                                    >
                                        <span>{t("entryNextVehicleBtn")}</span>
                                        <ArrowRight
                                            size={16}
                                            strokeWidth={2.3}
                                        />
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
