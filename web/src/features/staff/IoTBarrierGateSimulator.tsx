"use client";

import React, { useState, useEffect, useCallback, useId } from "react";
import {
    ShieldAlert,
    AlertTriangle,
    CheckCircle2,
    Radio,
    Zap,
    RotateCcw,
    Power,
    Car,
    Eye,
    Activity,
    Lock,
    Unlock,
    Info,
} from "lucide-react";
import { PlateBadge } from "./StaffUi";

export type BarrierPhysicalState =
    "CLOSED" | "OPENING" | "OPEN" | "CLOSING" | "EMERGENCY_OVERRIDE";

export type VehicleApproachState =
    "IDLE" | "APPROACHING" | "AT_STOP_LINE" | "PASSING" | "CLEARED";

export interface IoTBarrierGateSimulatorProps {
    gateId?: string;
    gateName?: string;
    gateType?: "ENTRY" | "EXIT";
    /** Controlled externally by API/parent */
    isOpen?: boolean;
    physicalState?: BarrierPhysicalState;
    vehicleState?: VehicleApproachState;
    detectedPlate?: string;
    vehicleType?: "Car" | "Motorbike";
    confidence?: number;
    /** Callbacks */
    onGateStateChange?: (state: BarrierPhysicalState) => void;
    onVehicleStateChange?: (state: VehicleApproachState) => void;
    onManualOverride?: (isOverride: boolean) => void;
    onVehicleCleared?: () => void;
    /** Feature flags */
    enableControls?: boolean;
    enableEmergencyOverride?: boolean;
    interactiveDemo?: boolean;
    compact?: boolean;
    className?: string;
}

export function IoTBarrierGateSimulator({
    gateId = "GATE-01",
    gateName = "Cổng Kiểm Soát Vào Ra Thông Minh",
    gateType = "ENTRY",
    isOpen: controlledIsOpen,
    physicalState: controlledPhysicalState,
    vehicleState: controlledVehicleState,
    detectedPlate = "51F-782.91",
    vehicleType = "Car",
    confidence = 0.98,
    onGateStateChange,
    onVehicleStateChange,
    onManualOverride,
    onVehicleCleared,
    enableControls = true,
    enableEmergencyOverride = true,
    interactiveDemo = true,
    compact = false,
    className = "",
}: IoTBarrierGateSimulatorProps) {
    // Unique ID for SVG pattern/filters
    const instanceId = useId().replace(/:/g, "");

    // Internal state when not fully controlled
    const [internalGateState, setInternalGateState] =
        useState<BarrierPhysicalState>("CLOSED");
    const [internalVehicleState, setInternalVehicleState] =
        useState<VehicleApproachState>("AT_STOP_LINE");
    const [isEmergencyOverride, setIsEmergencyOverride] = useState(false);
    const [isSafetyCoverOpen, setIsSafetyCoverOpen] = useState(false);
    const [radarDistanceMeters, setRadarDistanceMeters] = useState<number>(1.6);
    const [telemetryLogs, setTelemetryLogs] = useState<
        Array<{ time: string; text: string; type: "info" | "warn" | "success" }>
    >([]);

    // Derived states
    const gateState = controlledPhysicalState ?? internalGateState;
    const vehicleState = controlledVehicleState ?? internalVehicleState;

    // Boom pole angle: 0deg = horizontal (Closed), 90deg = vertical (Open)
    const boomAngle =
        gateState === "OPEN" || gateState === "EMERGENCY_OVERRIDE"
            ? 90
            : gateState === "OPENING"
              ? 85
              : gateState === "CLOSING"
                ? 15
                : 0;

    // Traffic light state
    const isRedLight =
        gateState === "CLOSED" ||
        gateState === "CLOSING" ||
        gateState === "EMERGENCY_OVERRIDE";
    const isGreenLight = gateState === "OPEN" || gateState === "OPENING";
    const isOverrideActive =
        gateState === "EMERGENCY_OVERRIDE" || isEmergencyOverride;

    // Add telemetry log
    const addLog = useCallback(
        (text: string, type: "info" | "warn" | "success" = "info") => {
            const time = new Date().toLocaleTimeString("vi-VN", {
                hour: "2-digit",
                minute: "2-digit",
                second: "2-digit",
            });
            setTelemetryLogs((prev) => [
                { time, text, type },
                ...prev.slice(0, 9),
            ]);
        },
        [],
    );

    // Sync with external isOpen prop
    useEffect(() => {
        if (controlledIsOpen !== undefined) {
            if (
                controlledIsOpen &&
                gateState !== "OPEN" &&
                !isEmergencyOverride
            ) {
                const openTimer = setTimeout(() => {
                    setInternalGateState("OPENING");
                    addLog(
                        `Lệnh MQTT 'OPEN_BARRIER' kích hoạt cổng ${gateId}`,
                        "success",
                    );
                }, 0);
                const completeTimer = setTimeout(() => {
                    setInternalGateState("OPEN");
                    onGateStateChange?.("OPEN");
                }, 1200);
                return () => {
                    clearTimeout(openTimer);
                    clearTimeout(completeTimer);
                };
            } else if (
                !controlledIsOpen &&
                gateState !== "CLOSED" &&
                !isEmergencyOverride
            ) {
                const closeTimer = setTimeout(() => {
                    setInternalGateState("CLOSING");
                    addLog(
                        `Lệnh MQTT 'CLOSE_BARRIER' hạ cần cổng ${gateId}`,
                        "info",
                    );
                }, 0);
                const completeTimer = setTimeout(() => {
                    setInternalGateState("CLOSED");
                    onGateStateChange?.("CLOSED");
                }, 1200);
                return () => {
                    clearTimeout(closeTimer);
                    clearTimeout(completeTimer);
                };
            }
        }
    }, [
        controlledIsOpen,
        gateId,
        gateState,
        isEmergencyOverride,
        onGateStateChange,
        addLog,
    ]);

    // Handle manual open
    const handleOpenGate = useCallback(() => {
        if (isEmergencyOverride) return;
        setInternalGateState("OPENING");
        addLog(
            `Động cơ Servo kích hoạt: Nâng cần lên 90° (${gateId})`,
            "success",
        );
        setTimeout(() => {
            setInternalGateState("OPEN");
            onGateStateChange?.("OPEN");
        }, 1200);
    }, [gateId, isEmergencyOverride, onGateStateChange, addLog]);

    // Handle manual close
    const handleCloseGate = useCallback(() => {
        if (isEmergencyOverride) return;
        setInternalGateState("CLOSING");
        addLog(
            `Cảm biến an toàn kiểm tra: Hạ cần xuống 0° (${gateId})`,
            "info",
        );
        setTimeout(() => {
            setInternalGateState("CLOSED");
            onGateStateChange?.("CLOSED");
        }, 1200);
    }, [gateId, isEmergencyOverride, onGateStateChange, addLog]);

    // Handle emergency manual override
    const handleToggleOverride = useCallback(() => {
        if (!isEmergencyOverride) {
            setIsEmergencyOverride(true);
            setInternalGateState("EMERGENCY_OVERRIDE");
            addLog(
                `🚨 CẢNH BÁO: BẢO VỆ KÍCH HOẠT MỞ CƯỠNG BỨC KHẨN CẤP (MANUAL OVERRIDE)`,
                "warn",
            );
            onManualOverride?.(true);
            onGateStateChange?.("EMERGENCY_OVERRIDE");
        } else {
            setIsEmergencyOverride(false);
            setInternalGateState("CLOSED");
            setIsSafetyCoverOpen(false);
            addLog(
                `Khôi phục chế độ tự động thông minh (IoT Auto Mode)`,
                "info",
            );
            onManualOverride?.(false);
            onGateStateChange?.("CLOSED");
        }
    }, [isEmergencyOverride, onManualOverride, onGateStateChange, addLog]);

    // Vehicle lifecycle simulation actions
    const handleSimulateApproach = useCallback(() => {
        setInternalVehicleState("APPROACHING");
        setRadarDistanceMeters(4.5);
        addLog(`Vòng từ 1 phát hiện xe tiếp cận vạch dừng...`, "info");

        setTimeout(() => {
            setInternalVehicleState("AT_STOP_LINE");
            setRadarDistanceMeters(1.2);
            addLog(
                `Xe dừng tại vạch kiểm soát. Camera ANPR quét biển số: ${detectedPlate}`,
                "success",
            );
            onVehicleStateChange?.("AT_STOP_LINE");
        }, 1000);
    }, [detectedPlate, onVehicleStateChange, addLog]);

    const handleSimulateVehicleCross = useCallback(() => {
        if (gateState !== "OPEN" && gateState !== "EMERGENCY_OVERRIDE") {
            addLog(`Chặn xe: Cần barrier chưa mở hoàn toàn!`, "warn");
            return;
        }

        setInternalVehicleState("PASSING");
        setRadarDistanceMeters(0.4);
        addLog(
            `Xe đang di chuyển qua barrier. Cảm biến quang điện Photocell kích hoạt bảo vệ.`,
            "info",
        );
        onVehicleStateChange?.("PASSING");

        setTimeout(() => {
            setInternalVehicleState("CLEARED");
            setRadarDistanceMeters(8.0);
            addLog(
                `Vòng từ 2: Xe đã qua cổng an toàn. Kích hoạt đếm ngược tự động đóng cần...`,
                "success",
            );
            onVehicleStateChange?.("CLEARED");
            onVehicleCleared?.();

            // Auto close after vehicle clears
            if (!isEmergencyOverride) {
                setTimeout(() => {
                    handleCloseGate();
                }, 1800);
            }
        }, 2200);
    }, [
        gateState,
        isEmergencyOverride,
        onVehicleStateChange,
        onVehicleCleared,
        handleCloseGate,
        addLog,
    ]);

    return (
        <div
            className={`iot-barrier-simulator-container ${compact ? "iot-compact" : ""} ${className}`}
            style={{
                background: "linear-gradient(180deg, #0d1117 0%, #161b22 100%)",
                borderRadius: "16px",
                border: isOverrideActive
                    ? "2px solid #ef4444"
                    : "1px solid rgba(255, 255, 255, 0.12)",
                boxShadow: isOverrideActive
                    ? "0 0 30px rgba(239, 68, 68, 0.35)"
                    : "0 16px 40px rgba(0, 0, 0, 0.4)",
                color: "#e6edf3",
                overflow: "hidden",
                fontFamily: "var(--pw-admin-font-sans, system-ui, sans-serif)",
            }}
        >
            {/* Top Status & Telemetry Bar */}
            <div
                style={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    padding: "12px 18px",
                    background: "rgba(0, 0, 0, 0.4)",
                    borderBottom: "1px solid rgba(255, 255, 255, 0.08)",
                }}
            >
                <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                    <div
                        style={{
                            width: 10,
                            height: 10,
                            borderRadius: "50%",
                            background: isOverrideActive
                                ? "#ef4444"
                                : isGreenLight
                                  ? "#10b981"
                                  : "#3b82f6",
                            boxShadow: isOverrideActive
                                ? "0 0 10px #ef4444"
                                : isGreenLight
                                  ? "0 0 10px #10b981"
                                  : "0 0 10px #3b82f6",
                            animation: isOverrideActive
                                ? "iotPulse 0.8s infinite"
                                : "none",
                        }}
                    />
                    <div>
                        <div
                            style={{
                                fontSize: "14px",
                                fontWeight: 800,
                                letterSpacing: "0.04em",
                                display: "flex",
                                alignItems: "center",
                                gap: 6,
                            }}
                        >
                            <span>{gateName}</span>
                            <span
                                style={{
                                    fontSize: "10px",
                                    padding: "2px 6px",
                                    borderRadius: "4px",
                                    background:
                                        gateType === "ENTRY"
                                            ? "rgba(16, 185, 129, 0.2)"
                                            : "rgba(59, 130, 246, 0.2)",
                                    color:
                                        gateType === "ENTRY"
                                            ? "#34d399"
                                            : "#60a5fa",
                                    border: `1px solid ${
                                        gateType === "ENTRY"
                                            ? "rgba(16, 185, 129, 0.4)"
                                            : "rgba(59, 130, 246, 0.4)"
                                    }`,
                                }}
                            >
                                {gateType === "ENTRY" ? "GATE IN" : "GATE OUT"}
                            </span>
                        </div>
                        <div
                            style={{
                                fontSize: "11px",
                                color: "#8b949e",
                                display: "flex",
                                gap: 12,
                                marginTop: 2,
                            }}
                        >
                            <span>
                                ID: <strong>{gateId}</strong>
                            </span>
                            <span>
                                Trục quay: <strong>{boomAngle}°</strong>
                            </span>
                            <span>
                                Mô tơ: <strong>24V Brushless DC</strong>
                            </span>
                        </div>
                    </div>
                </div>

                {/* Status Badges */}
                <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                    {isOverrideActive ? (
                        <div
                            style={{
                                display: "flex",
                                alignItems: "center",
                                gap: 6,
                                background: "rgba(239, 68, 68, 0.2)",
                                color: "#f87171",
                                border: "1px solid #ef4444",
                                padding: "4px 10px",
                                borderRadius: "6px",
                                fontSize: "12px",
                                fontWeight: 700,
                                animation: "iotStrobe 1s infinite alternate",
                            }}
                        >
                            <AlertTriangle size={14} />
                            <span>MỞ KHẨN CẤP (OVERRIDE)</span>
                        </div>
                    ) : gateState === "OPEN" ? (
                        <div
                            style={{
                                display: "flex",
                                alignItems: "center",
                                gap: 6,
                                background: "rgba(16, 185, 129, 0.2)",
                                color: "#34d399",
                                border: "1px solid #10b981",
                                padding: "4px 10px",
                                borderRadius: "6px",
                                fontSize: "12px",
                                fontWeight: 700,
                            }}
                        >
                            <Unlock size={14} />
                            <span>CỔNG ĐANG MỞ (90°)</span>
                        </div>
                    ) : gateState === "OPENING" ? (
                        <div
                            style={{
                                display: "flex",
                                alignItems: "center",
                                gap: 6,
                                background: "rgba(245, 158, 11, 0.2)",
                                color: "#fbbf24",
                                border: "1px solid #f59e0b",
                                padding: "4px 10px",
                                borderRadius: "6px",
                                fontSize: "12px",
                                fontWeight: 700,
                            }}
                        >
                            <Activity size={14} className="animate-spin" />
                            <span>ĐANG NÂNG CẦN...</span>
                        </div>
                    ) : gateState === "CLOSING" ? (
                        <div
                            style={{
                                display: "flex",
                                alignItems: "center",
                                gap: 6,
                                background: "rgba(245, 158, 11, 0.2)",
                                color: "#fbbf24",
                                border: "1px solid #f59e0b",
                                padding: "4px 10px",
                                borderRadius: "6px",
                                fontSize: "12px",
                                fontWeight: 700,
                            }}
                        >
                            <Activity size={14} className="animate-spin" />
                            <span>ĐANG HẠ CẦN...</span>
                        </div>
                    ) : (
                        <div
                            style={{
                                display: "flex",
                                alignItems: "center",
                                gap: 6,
                                background: "rgba(239, 68, 68, 0.15)",
                                color: "#f87171",
                                border: "1px solid rgba(239, 68, 68, 0.4)",
                                padding: "4px 10px",
                                borderRadius: "6px",
                                fontSize: "12px",
                                fontWeight: 700,
                            }}
                        >
                            <Lock size={14} />
                            <span>CỔNG ĐANG ĐÓNG (0°)</span>
                        </div>
                    )}
                </div>
            </div>

            {/* Main Interactive Stage / Graphic Canvas */}
            <div
                style={{
                    position: "relative",
                    minHeight: compact ? "220px" : "320px",
                    background:
                        "radial-gradient(ellipse at 50% 30%, #1c2333 0%, #0d1117 80%)",
                    overflow: "hidden",
                    borderBottom: "1px solid rgba(255, 255, 255, 0.08)",
                }}
            >
                {/* Overhead Digital LED Signboard */}
                <div
                    style={{
                        position: "absolute",
                        top: 14,
                        left: "50%",
                        transform: "translateX(-50%)",
                        background: "#05070a",
                        border: "2px solid #30363d",
                        borderRadius: "8px",
                        padding: "6px 20px",
                        boxShadow: "0 4px 20px rgba(0, 0, 0, 0.6)",
                        zIndex: 10,
                        display: "flex",
                        alignItems: "center",
                        gap: 16,
                    }}
                >
                    <div
                        style={{
                            display: "flex",
                            flexDirection: "column",
                            alignItems: "center",
                        }}
                    >
                        <span
                            style={{
                                fontSize: "10px",
                                letterSpacing: "0.15em",
                                color: "#8b949e",
                                fontWeight: 700,
                            }}
                        >
                            PARKWISE SMART GATE
                        </span>
                        <span
                            style={{
                                fontFamily: "monospace",
                                fontSize: "14px",
                                fontWeight: 900,
                                color: isOverrideActive
                                    ? "#ef4444"
                                    : isGreenLight
                                      ? "#10b981"
                                      : "#38bdf8",
                                letterSpacing: "0.08em",
                                textShadow: isOverrideActive
                                    ? "0 0 10px #ef4444"
                                    : isGreenLight
                                      ? "0 0 10px #10b981"
                                      : "0 0 10px #38bdf8",
                            }}
                        >
                            {isOverrideActive
                                ? "⚠ MỞ CƯỠNG BỨC KHẨN CẤP ⚠"
                                : gateState === "OPEN"
                                  ? "MỜI XE QUA CỔNG / PLEASE PASS"
                                  : vehicleState === "AT_STOP_LINE"
                                    ? `DỪNG CHỜ KIỂM SOÁT [${detectedPlate}]`
                                    : "SẴN SÀNG ĐÓN XE / GATE READY"}
                        </span>
                    </div>
                </div>

                {/* Asphalt Road Canvas with markings */}
                <div
                    style={{
                        position: "absolute",
                        bottom: 0,
                        left: 0,
                        right: 0,
                        height: "140px",
                        background: "#12171f",
                        borderTop: "3px solid #30363d",
                        boxShadow: "inset 0 10px 30px rgba(0, 0, 0, 0.7)",
                    }}
                >
                    {/* Road lane dashes */}
                    <div
                        style={{
                            position: "absolute",
                            top: "50%",
                            left: 0,
                            right: 0,
                            height: "4px",
                            borderTop: "3px dashed rgba(255, 255, 255, 0.25)",
                        }}
                    />

                    {/* White Stop Line */}
                    <div
                        style={{
                            position: "absolute",
                            top: 10,
                            bottom: 10,
                            left: "38%",
                            width: "8px",
                            background: "#ffffff",
                            borderRadius: "2px",
                            boxShadow: "0 0 12px rgba(255, 255, 255, 0.4)",
                        }}
                    >
                        <span
                            style={{
                                position: "absolute",
                                top: -20,
                                left: "50%",
                                transform: "translateX(-50%)",
                                fontSize: "9px",
                                fontWeight: 800,
                                color: "#f0f6fc",
                                letterSpacing: "0.08em",
                                whiteSpace: "nowrap",
                            }}
                        >
                            VACH DUNG XE
                        </span>
                    </div>

                    {/* Inductive Loop 1 (Approach Loop) */}
                    <div
                        style={{
                            position: "absolute",
                            top: 25,
                            bottom: 25,
                            left: "18%",
                            width: "16%",
                            border: `2px dashed ${
                                vehicleState === "AT_STOP_LINE" ||
                                vehicleState === "APPROACHING"
                                    ? "#f59e0b"
                                    : "rgba(245, 158, 11, 0.3)"
                            }`,
                            borderRadius: "6px",
                            background:
                                vehicleState === "AT_STOP_LINE" ||
                                vehicleState === "APPROACHING"
                                    ? "rgba(245, 158, 11, 0.15)"
                                    : "transparent",
                            boxShadow:
                                vehicleState === "AT_STOP_LINE" ||
                                vehicleState === "APPROACHING"
                                    ? "0 0 16px rgba(245, 158, 11, 0.4)"
                                    : "none",
                            transition: "all 0.3s ease",
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "center",
                        }}
                    >
                        <span
                            style={{
                                fontSize: "10px",
                                fontWeight: 700,
                                color:
                                    vehicleState === "AT_STOP_LINE" ||
                                    vehicleState === "APPROACHING"
                                        ? "#fbbf24"
                                        : "rgba(245, 158, 11, 0.5)",
                            }}
                        >
                            LOOP A (48kHz)
                        </span>
                    </div>

                    {/* Inductive Loop 2 (Safety & Departure Loop) */}
                    <div
                        style={{
                            position: "absolute",
                            top: 25,
                            bottom: 25,
                            left: "52%",
                            width: "18%",
                            border: `2px dashed ${
                                vehicleState === "PASSING"
                                    ? "#10b981"
                                    : "rgba(16, 185, 129, 0.3)"
                            }`,
                            borderRadius: "6px",
                            background:
                                vehicleState === "PASSING"
                                    ? "rgba(16, 185, 129, 0.15)"
                                    : "transparent",
                            boxShadow:
                                vehicleState === "PASSING"
                                    ? "0 0 16px rgba(16, 185, 129, 0.4)"
                                    : "none",
                            transition: "all 0.3s ease",
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "center",
                        }}
                    >
                        <span
                            style={{
                                fontSize: "10px",
                                fontWeight: 700,
                                color:
                                    vehicleState === "PASSING"
                                        ? "#34d399"
                                        : "rgba(16, 185, 129, 0.5)",
                            }}
                        >
                            LOOP B (ANTI-CRUSH)
                        </span>
                    </div>
                </div>

                {/* Traffic Light Post (Cột Đèn Giao Thông Đôi) */}
                <div
                    style={{
                        position: "absolute",
                        bottom: 90,
                        left: "34%",
                        zIndex: 25,
                        display: "flex",
                        flexDirection: "column",
                        alignItems: "center",
                    }}
                >
                    {/* Dual Fresnel Head */}
                    <div
                        style={{
                            background: "#1c2128",
                            border: "3px solid #30363d",
                            borderRadius: "14px",
                            padding: "8px 6px",
                            display: "flex",
                            flexDirection: "column",
                            gap: 10,
                            boxShadow: "0 8px 24px rgba(0, 0, 0, 0.7)",
                        }}
                    >
                        {/* RED LIGHT */}
                        <div
                            style={{
                                width: 26,
                                height: 26,
                                borderRadius: "50%",
                                background: isRedLight
                                    ? "radial-gradient(circle, #ff6b6b 0%, #ef4444 60%, #b91c1c 100%)"
                                    : "#2d1515",
                                border: isRedLight
                                    ? "2px solid #ffffff"
                                    : "2px solid #4a1d1d",
                                boxShadow: isRedLight
                                    ? "0 0 20px #ef4444, 0 0 35px rgba(239, 68, 68, 0.6)"
                                    : "none",
                                transition:
                                    "all 0.3s cubic-bezier(0.4, 0, 0.2, 1)",
                            }}
                        />

                        {/* GREEN LIGHT */}
                        <div
                            style={{
                                width: 26,
                                height: 26,
                                borderRadius: "50%",
                                background: isGreenLight
                                    ? "radial-gradient(circle, #6ee7b7 0%, #10b981 60%, #047857 100%)"
                                    : "#0d261e",
                                border: isGreenLight
                                    ? "2px solid #ffffff"
                                    : "2px solid #064e3b",
                                boxShadow: isGreenLight
                                    ? "0 0 24px #10b981, 0 0 45px rgba(16, 185, 129, 0.7)"
                                    : "none",
                                animation: isGreenLight
                                    ? "iotPulse 1s infinite alternate"
                                    : "none",
                                transition:
                                    "all 0.3s cubic-bezier(0.4, 0, 0.2, 1)",
                            }}
                        />
                    </div>

                    {/* Pole Stand */}
                    <div
                        style={{
                            width: "8px",
                            height: "50px",
                            background:
                                "linear-gradient(90deg, #484f58, #21262d)",
                            borderLeft: "1px solid #6e7681",
                        }}
                    />
                    {/* Heavy Steel Base Plate */}
                    <div
                        style={{
                            width: "26px",
                            height: "6px",
                            background: "#0d1117",
                            border: "1px solid #484f58",
                            borderRadius: "2px",
                        }}
                    />
                </div>

                {/* Industrial Barrier Housing Cabinet & Boom Pole */}
                <div
                    style={{
                        position: "absolute",
                        bottom: 90,
                        left: "45%",
                        zIndex: 30,
                    }}
                >
                    {/* The Barrier Cabinet Body */}
                    <div
                        style={{
                            position: "relative",
                            width: "56px",
                            height: "105px",
                            background:
                                "linear-gradient(135deg, #f97316 0%, #c2410c 100%)",
                            borderRadius: "8px 8px 3px 3px",
                            boxShadow:
                                "0 12px 30px rgba(0, 0, 0, 0.7), inset 0 2px 4px rgba(255, 255, 255, 0.4)",
                            border: "2px solid #ea580c",
                            display: "flex",
                            flexDirection: "column",
                            alignItems: "center",
                            padding: "6px 4px",
                        }}
                    >
                        {/* Industrial warning stripes at cabinet top */}
                        <div
                            style={{
                                width: "100%",
                                height: "8px",
                                borderRadius: "4px 4px 0 0",
                                background:
                                    "repeating-linear-gradient(45deg, #171717, #171717 6px, #facc15 6px, #facc15 12px)",
                                marginBottom: "4px",
                            }}
                        />

                        {/* Cabinet Digital Display */}
                        <div
                            style={{
                                width: "90%",
                                height: "20px",
                                background: "#05070a",
                                border: "1px solid #30363d",
                                borderRadius: "4px",
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "center",
                                fontFamily: "monospace",
                                fontSize: "9px",
                                fontWeight: 900,
                                color: isOverrideActive
                                    ? "#ef4444"
                                    : isGreenLight
                                      ? "#10b981"
                                      : "#fbbf24",
                                letterSpacing: "0.06em",
                            }}
                        >
                            {isOverrideActive
                                ? "OVRD"
                                : gateState === "OPEN"
                                  ? "PASS"
                                  : gateState === "OPENING" ||
                                      gateState === "CLOSING"
                                    ? "MOVE"
                                    : "WAIT"}
                        </div>

                        {/* Status LEDs on Cabinet */}
                        <div
                            style={{
                                display: "flex",
                                gap: 4,
                                marginTop: "8px",
                            }}
                        >
                            <div
                                title="Power 24V"
                                style={{
                                    width: 6,
                                    height: 6,
                                    borderRadius: "50%",
                                    background: "#10b981",
                                    boxShadow: "0 0 6px #10b981",
                                }}
                            />
                            <div
                                title="MQTT Network"
                                style={{
                                    width: 6,
                                    height: 6,
                                    borderRadius: "50%",
                                    background: "#38bdf8",
                                    boxShadow: "0 0 6px #38bdf8",
                                    animation: "iotPulse 1.5s infinite",
                                }}
                            />
                            <div
                                title="Fault Indicator"
                                style={{
                                    width: 6,
                                    height: 6,
                                    borderRadius: "50%",
                                    background: isOverrideActive
                                        ? "#ef4444"
                                        : "#451a1a",
                                    boxShadow: isOverrideActive
                                        ? "0 0 6px #ef4444"
                                        : "none",
                                }}
                            />
                        </div>

                        {/* Brand Logo text */}
                        <div
                            style={{
                                marginTop: "auto",
                                fontSize: "8px",
                                fontWeight: 900,
                                color: "#ffffff",
                                letterSpacing: "0.08em",
                                textShadow: "0 1px 2px rgba(0,0,0,0.8)",
                            }}
                        >
                            PARKWISE
                        </div>
                    </div>

                    {/* Pivot Arm Hub & Boom Pole (Thanh chắn nâng hạ 90 độ) */}
                    <div
                        style={{
                            position: "absolute",
                            top: "22px",
                            left: "22px",
                            zIndex: 40,
                        }}
                    >
                        {/* Metallic Pivot Hub Base */}
                        <div
                            style={{
                                position: "absolute",
                                top: "-12px",
                                left: "-12px",
                                width: "24px",
                                height: "24px",
                                borderRadius: "50%",
                                background:
                                    "radial-gradient(circle, #94a3b8 0%, #475569 60%, #1e293b 100%)",
                                border: "2px solid #cbd5e1",
                                boxShadow: "0 4px 10px rgba(0, 0, 0, 0.6)",
                                zIndex: 45,
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "center",
                            }}
                        >
                            <div
                                style={{
                                    width: 8,
                                    height: 8,
                                    borderRadius: "50%",
                                    background: "#0f172a",
                                    border: "1px solid #94a3b8",
                                }}
                            />
                        </div>

                        {/* The Boom Pole itself (GPU Accelerated CSS Transform) */}
                        <div
                            style={{
                                position: "absolute",
                                top: "-7px",
                                left: "0px",
                                width: compact ? "170px" : "240px",
                                height: "14px",
                                transformOrigin: "0px 7px",
                                transform: `rotate(-${boomAngle}deg)`,
                                transition:
                                    "transform 1.35s cubic-bezier(0.25, 1, 0.5, 1)",
                                willChange: "transform",
                                borderRadius: "0 6px 6px 0",
                                background:
                                    "repeating-linear-gradient(45deg, #ef4444, #ef4444 14px, #ffffff 14px, #ffffff 28px)",
                                border: "1.5px solid #b91c1c",
                                boxShadow: isOverrideActive
                                    ? "0 0 20px #f59e0b, 0 4px 12px rgba(0, 0, 0, 0.8)"
                                    : isGreenLight
                                      ? "0 0 18px #10b981, 0 4px 12px rgba(0, 0, 0, 0.8)"
                                      : "0 0 16px #ef4444, 0 4px 12px rgba(0, 0, 0, 0.8)",
                            }}
                        >
                            {/* Running LED light strip on Boom Pole */}
                            <div
                                style={{
                                    position: "absolute",
                                    top: "2px",
                                    bottom: "2px",
                                    left: "14px",
                                    right: "4px",
                                    borderRadius: "2px",
                                    background: isOverrideActive
                                        ? "#f59e0b"
                                        : isGreenLight
                                          ? "#10b981"
                                          : "#ef4444",
                                    boxShadow: isOverrideActive
                                        ? "0 0 10px #f59e0b, inset 0 0 4px #ffffff"
                                        : isGreenLight
                                          ? "0 0 10px #10b981, inset 0 0 4px #ffffff"
                                          : "0 0 10px #ef4444, inset 0 0 4px #ffffff",
                                    opacity: 0.9,
                                }}
                            />

                            {/* Soft rubber protection lip under boom */}
                            <div
                                style={{
                                    position: "absolute",
                                    bottom: "-3px",
                                    left: "8px",
                                    right: "0px",
                                    height: "3px",
                                    background: "#171717",
                                    borderRadius: "0 0 3px 0",
                                }}
                            />
                        </div>
                    </div>
                </div>

                {/* Ultrasonic / Radar Sonar Beam Wave */}
                {vehicleState === "AT_STOP_LINE" && (
                    <div
                        style={{
                            position: "absolute",
                            bottom: 120,
                            left: "37%",
                            zIndex: 20,
                            pointerEvents: "none",
                        }}
                    >
                        <svg width="60" height="40" viewBox="0 0 60 40">
                            <path
                                d="M 50 20 A 15 15 0 0 0 35 5"
                                fill="none"
                                stroke="#38bdf8"
                                strokeWidth="2"
                                opacity="0.8"
                                strokeDasharray="3,3"
                            >
                                <animate
                                    attributeName="opacity"
                                    values="0.2;1;0.2"
                                    dur="1.2s"
                                    repeatCount="indefinite"
                                />
                            </path>
                            <path
                                d="M 50 20 A 25 25 0 0 0 25 0"
                                fill="none"
                                stroke="#38bdf8"
                                strokeWidth="2"
                                opacity="0.5"
                                strokeDasharray="3,3"
                            >
                                <animate
                                    attributeName="opacity"
                                    values="0.1;0.8;0.1"
                                    dur="1.2s"
                                    begin="0.3s"
                                    repeatCount="indefinite"
                                />
                            </path>
                        </svg>
                    </div>
                )}

                {/* Vehicle Simulation Sprite / Avatar */}
                <div
                    style={{
                        position: "absolute",
                        bottom: 40,
                        left:
                            vehicleState === "IDLE"
                                ? "-120px"
                                : vehicleState === "APPROACHING"
                                  ? "6%"
                                  : vehicleState === "AT_STOP_LINE"
                                    ? "20%"
                                    : vehicleState === "PASSING"
                                      ? "48%"
                                      : "92%",
                        transition:
                            vehicleState === "PASSING"
                                ? "left 2.1s cubic-bezier(0.4, 0, 0.2, 1)"
                                : "left 1s cubic-bezier(0.2, 0.8, 0.2, 1)",
                        zIndex: 35,
                        display: "flex",
                        flexDirection: "column",
                        alignItems: "center",
                    }}
                >
                    {/* Floating License Plate Badge over car */}
                    <div
                        style={{
                            marginBottom: "6px",
                            transform: "scale(0.85)",
                            filter: "drop-shadow(0 4px 8px rgba(0, 0, 0, 0.8))",
                        }}
                    >
                        <PlateBadge plate={detectedPlate} />
                    </div>

                    {/* Vector Isometric Car Body */}
                    <div
                        style={{
                            position: "relative",
                            width: "128px",
                            height: "56px",
                        }}
                    >
                        {/* Headlight beam */}
                        <div
                            style={{
                                position: "absolute",
                                right: "-75px",
                                top: "26px",
                                width: "80px",
                                height: "24px",
                                background:
                                    "linear-gradient(90deg, rgba(254, 240, 138, 0.6) 0%, rgba(254, 240, 138, 0) 100%)",
                                clipPath:
                                    "polygon(0 30%, 100% 0, 100% 100%, 0 70%)",
                                pointerEvents: "none",
                            }}
                        />

                        {/* Car Silhouette & Windows */}
                        <div
                            style={{
                                width: "128px",
                                height: "46px",
                                background:
                                    vehicleType === "Car"
                                        ? "linear-gradient(180deg, #1e293b 0%, #0f172a 100%)"
                                        : "linear-gradient(180deg, #0284c7 0%, #0369a1 100%)",
                                borderRadius: "16px 24px 6px 6px",
                                border: "2px solid #38bdf8",
                                boxShadow:
                                    "0 8px 18px rgba(0,0,0,0.8), inset 0 2px 4px rgba(255,255,255,0.2)",
                                position: "relative",
                            }}
                        >
                            {/* Windshield */}
                            <div
                                style={{
                                    position: "absolute",
                                    top: "6px",
                                    right: "22px",
                                    width: "36px",
                                    height: "18px",
                                    background:
                                        "linear-gradient(135deg, #7dd3fc 0%, #0284c7 100%)",
                                    borderRadius: "4px 12px 2px 2px",
                                    opacity: 0.85,
                                }}
                            />

                            {/* Rear window */}
                            <div
                                style={{
                                    position: "absolute",
                                    top: "6px",
                                    left: "22px",
                                    width: "30px",
                                    height: "18px",
                                    background:
                                        "linear-gradient(135deg, #7dd3fc 0%, #0284c7 100%)",
                                    borderRadius: "10px 2px 2px 2px",
                                    opacity: 0.75,
                                }}
                            />

                            {/* Headlight bulb */}
                            <div
                                style={{
                                    position: "absolute",
                                    top: "26px",
                                    right: "2px",
                                    width: "8px",
                                    height: "10px",
                                    background: "#fef08a",
                                    borderRadius: "2px 6px 6px 2px",
                                    boxShadow: "0 0 10px #fef08a",
                                }}
                            />
                        </div>

                        {/* Front and Rear Wheels */}
                        <div
                            style={{
                                position: "absolute",
                                bottom: "0px",
                                left: "20px",
                                width: "20px",
                                height: "20px",
                                borderRadius: "50%",
                                background: "#000000",
                                border: "3px solid #64748b",
                                boxShadow: "0 2px 6px rgba(0,0,0,0.9)",
                            }}
                        />
                        <div
                            style={{
                                position: "absolute",
                                bottom: "0px",
                                right: "20px",
                                width: "20px",
                                height: "20px",
                                borderRadius: "50%",
                                background: "#000000",
                                border: "3px solid #64748b",
                                boxShadow: "0 2px 6px rgba(0,0,0,0.9)",
                            }}
                        />
                    </div>
                </div>
            </div>

            {/* Telemetry and Controls Grid */}
            <div
                style={{
                    padding: "16px 18px",
                    display: "grid",
                    gridTemplateColumns: compact ? "1fr" : "1.1fr 1.3fr 1.2fr",
                    gap: "16px",
                    background: "rgba(0, 0, 0, 0.25)",
                }}
            >
                {/* Column 1: Live Hardware Telemetry */}
                <div
                    style={{
                        background: "#0d1117",
                        border: "1px solid #30363d",
                        borderRadius: "10px",
                        padding: "12px 14px",
                    }}
                >
                    <div
                        style={{
                            fontSize: "12px",
                            fontWeight: 700,
                            color: "#8b949e",
                            marginBottom: "10px",
                            display: "flex",
                            alignItems: "center",
                            gap: 6,
                        }}
                    >
                        <Radio size={14} className="text-sky-400" />
                        <span>THÔNG SỐ PHẦN CỨNG IOT</span>
                    </div>

                    <div
                        style={{
                            display: "flex",
                            flexDirection: "column",
                            gap: 8,
                            fontSize: "12px",
                        }}
                    >
                        <div
                            style={{
                                display: "flex",
                                justifyContent: "space-between",
                            }}
                        >
                            <span style={{ color: "#8b949e" }}>
                                Góc mở thanh chắn:
                            </span>
                            <strong
                                style={{
                                    color: isGreenLight ? "#34d399" : "#f87171",
                                }}
                            >
                                {boomAngle}° ({gateState})
                            </strong>
                        </div>
                        <div
                            style={{
                                display: "flex",
                                justifyContent: "space-between",
                            }}
                        >
                            <span style={{ color: "#8b949e" }}>
                                Khoảng cách Radar:
                            </span>
                            <strong style={{ color: "#38bdf8" }}>
                                {radarDistanceMeters.toFixed(1)} m
                            </strong>
                        </div>
                        <div
                            style={{
                                display: "flex",
                                justifyContent: "space-between",
                            }}
                        >
                            <span style={{ color: "#8b949e" }}>
                                Vòng từ A (Tiếp cận):
                            </span>
                            <span
                                style={{
                                    color:
                                        vehicleState === "AT_STOP_LINE" ||
                                        vehicleState === "APPROACHING"
                                            ? "#fbbf24"
                                            : "#8b949e",
                                    fontWeight: 700,
                                }}
                            >
                                {vehicleState === "AT_STOP_LINE" ||
                                vehicleState === "APPROACHING"
                                    ? "PHÁT HIỆN XE"
                                    : "TRỐNG"}
                            </span>
                        </div>
                        <div
                            style={{
                                display: "flex",
                                justifyContent: "space-between",
                            }}
                        >
                            <span style={{ color: "#8b949e" }}>
                                Vòng từ B (Chống kẹt):
                            </span>
                            <span
                                style={{
                                    color:
                                        vehicleState === "PASSING"
                                            ? "#34d399"
                                            : "#8b949e",
                                    fontWeight: 700,
                                }}
                            >
                                {vehicleState === "PASSING"
                                    ? "XE ĐANG QUA"
                                    : "SẴN SÀNG"}
                            </span>
                        </div>
                        <div
                            style={{
                                display: "flex",
                                justifyContent: "space-between",
                            }}
                        >
                            <span style={{ color: "#8b949e" }}>
                                Giao thức kết nối:
                            </span>
                            <span
                                style={{
                                    color: "#a5b4fc",
                                    fontFamily: "monospace",
                                }}
                            >
                                MQTT / QoS 1 (18ms)
                            </span>
                        </div>
                    </div>
                </div>

                {/* Column 2: Simulation Workflow Buttons */}
                {enableControls && (
                    <div
                        style={{
                            background: "#0d1117",
                            border: "1px solid #30363d",
                            borderRadius: "10px",
                            padding: "12px 14px",
                            display: "flex",
                            flexDirection: "column",
                            justifyContent: "space-between",
                        }}
                    >
                        <div
                            style={{
                                fontSize: "12px",
                                fontWeight: 700,
                                color: "#8b949e",
                                marginBottom: "10px",
                                display: "flex",
                                alignItems: "center",
                                gap: 6,
                            }}
                        >
                            <Zap size={14} className="text-amber-400" />
                            <span>ĐIỀU KHIỂN & MÔ PHỎNG LUỒNG XE</span>
                        </div>

                        <div
                            style={{
                                display: "grid",
                                gridTemplateColumns: "1fr 1fr",
                                gap: 8,
                            }}
                        >
                            <button
                                type="button"
                                onClick={handleSimulateApproach}
                                disabled={isOverrideActive}
                                style={{
                                    background: "rgba(59, 130, 246, 0.15)",
                                    border: "1px solid rgba(59, 130, 246, 0.4)",
                                    color: "#60a5fa",
                                    padding: "8px 10px",
                                    borderRadius: "6px",
                                    fontSize: "12px",
                                    fontWeight: 600,
                                    cursor: isOverrideActive
                                        ? "not-allowed"
                                        : "pointer",
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: "center",
                                    gap: 6,
                                }}
                            >
                                <Car size={14} />
                                <span>1. Xe Đến Cổng</span>
                            </button>

                            <button
                                type="button"
                                onClick={handleOpenGate}
                                disabled={
                                    isOverrideActive || gateState === "OPEN"
                                }
                                style={{
                                    background: "rgba(16, 185, 129, 0.15)",
                                    border: "1px solid rgba(16, 185, 129, 0.4)",
                                    color: "#34d399",
                                    padding: "8px 10px",
                                    borderRadius: "6px",
                                    fontSize: "12px",
                                    fontWeight: 600,
                                    cursor:
                                        isOverrideActive || gateState === "OPEN"
                                            ? "not-allowed"
                                            : "pointer",
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: "center",
                                    gap: 6,
                                }}
                            >
                                <Unlock size={14} />
                                <span>2. Nâng Cần (90°)</span>
                            </button>

                            <button
                                type="button"
                                onClick={handleSimulateVehicleCross}
                                disabled={
                                    gateState !== "OPEN" && !isOverrideActive
                                }
                                style={{
                                    background: "rgba(168, 85, 247, 0.15)",
                                    border: "1px solid rgba(168, 85, 247, 0.4)",
                                    color: "#c084fc",
                                    padding: "8px 10px",
                                    borderRadius: "6px",
                                    fontSize: "12px",
                                    fontWeight: 600,
                                    cursor:
                                        gateState !== "OPEN" &&
                                        !isOverrideActive
                                            ? "not-allowed"
                                            : "pointer",
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: "center",
                                    gap: 6,
                                }}
                            >
                                <Activity size={14} />
                                <span>3. Xe Đi Qua Cổng</span>
                            </button>

                            <button
                                type="button"
                                onClick={handleCloseGate}
                                disabled={
                                    isOverrideActive || gateState === "CLOSED"
                                }
                                style={{
                                    background: "rgba(239, 68, 68, 0.15)",
                                    border: "1px solid rgba(239, 68, 68, 0.4)",
                                    color: "#f87171",
                                    padding: "8px 10px",
                                    borderRadius: "6px",
                                    fontSize: "12px",
                                    fontWeight: 600,
                                    cursor:
                                        isOverrideActive ||
                                        gateState === "CLOSED"
                                            ? "not-allowed"
                                            : "pointer",
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: "center",
                                    gap: 6,
                                }}
                            >
                                <Lock size={14} />
                                <span>4. Hạ Cần (0°)</span>
                            </button>
                        </div>
                    </div>
                )}

                {/* Column 3: Emergency Manual Override Control */}
                {enableEmergencyOverride && (
                    <div
                        style={{
                            background: isOverrideActive
                                ? "rgba(239, 68, 68, 0.1)"
                                : "#0d1117",
                            border: isOverrideActive
                                ? "1px solid #ef4444"
                                : "1px solid #30363d",
                            borderRadius: "10px",
                            padding: "12px 14px",
                            display: "flex",
                            flexDirection: "column",
                            justifyContent: "space-between",
                        }}
                    >
                        <div
                            style={{
                                fontSize: "12px",
                                fontWeight: 700,
                                color: isOverrideActive ? "#f87171" : "#8b949e",
                                marginBottom: "8px",
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "space-between",
                            }}
                        >
                            <div
                                style={{
                                    display: "flex",
                                    alignItems: "center",
                                    gap: 6,
                                }}
                            >
                                <ShieldAlert
                                    size={14}
                                    className={
                                        isOverrideActive ? "text-red-400" : ""
                                    }
                                />
                                <span>MỞ CƯỠNG BỨC KHẨN CẤP</span>
                            </div>
                            <span
                                style={{
                                    fontSize: "10px",
                                    background: "#21262d",
                                    padding: "2px 6px",
                                    borderRadius: "4px",
                                    color: "#8b949e",
                                }}
                            >
                                SAFETY SWITCH
                            </span>
                        </div>

                        <p
                            style={{
                                fontSize: "11px",
                                color: "#8b949e",
                                margin: "0 0 10px 0",
                                lineHeight: 1.4,
                            }}
                        >
                            Dành cho bảo vệ khi mất điện, sự cố hỏa hoạn PCCC
                            hoặc xe ưu tiên cấp cứu.
                        </p>

                        {!isEmergencyOverride ? (
                            <div
                                style={{
                                    display: "flex",
                                    flexDirection: "column",
                                    gap: 6,
                                }}
                            >
                                {!isSafetyCoverOpen ? (
                                    <button
                                        type="button"
                                        onClick={() =>
                                            setIsSafetyCoverOpen(true)
                                        }
                                        style={{
                                            background:
                                                "rgba(245, 158, 11, 0.15)",
                                            border: "1px dashed #f59e0b",
                                            color: "#fbbf24",
                                            padding: "8px 12px",
                                            borderRadius: "6px",
                                            fontSize: "12px",
                                            fontWeight: 700,
                                            cursor: "pointer",
                                            display: "flex",
                                            alignItems: "center",
                                            justifyContent: "center",
                                            gap: 6,
                                        }}
                                    >
                                        <span>🛡 Mở Nắp An Toàn Cần Gạt</span>
                                    </button>
                                ) : (
                                    <div style={{ display: "flex", gap: 6 }}>
                                        <button
                                            type="button"
                                            onClick={handleToggleOverride}
                                            style={{
                                                flex: 1,
                                                background: "#dc2626",
                                                border: "1px solid #ef4444",
                                                color: "#ffffff",
                                                padding: "9px 12px",
                                                borderRadius: "6px",
                                                fontSize: "12px",
                                                fontWeight: 800,
                                                cursor: "pointer",
                                                boxShadow:
                                                    "0 0 15px rgba(220, 38, 38, 0.6)",
                                                display: "flex",
                                                alignItems: "center",
                                                justifyContent: "center",
                                                gap: 6,
                                            }}
                                        >
                                            <Power size={14} />
                                            <span>KÍCH HOẠT MỞ CƯỠNG BỨC</span>
                                        </button>
                                        <button
                                            type="button"
                                            onClick={() =>
                                                setIsSafetyCoverOpen(false)
                                            }
                                            style={{
                                                background: "#21262d",
                                                border: "1px solid #30363d",
                                                color: "#8b949e",
                                                padding: "0 10px",
                                                borderRadius: "6px",
                                                fontSize: "11px",
                                                cursor: "pointer",
                                            }}
                                        >
                                            Hủy
                                        </button>
                                    </div>
                                )}
                            </div>
                        ) : (
                            <button
                                type="button"
                                onClick={handleToggleOverride}
                                style={{
                                    background: "#047857",
                                    border: "1px solid #10b981",
                                    color: "#ffffff",
                                    padding: "9px 12px",
                                    borderRadius: "6px",
                                    fontSize: "12px",
                                    fontWeight: 800,
                                    cursor: "pointer",
                                    boxShadow:
                                        "0 0 15px rgba(16, 185, 129, 0.4)",
                                    display: "flex",
                                    alignItems: "center",
                                    justifyContent: "center",
                                    gap: 6,
                                }}
                            >
                                <RotateCcw size={14} />
                                <span>ĐÓNG OVERRIDE · VỀ TỰ ĐỘNG</span>
                            </button>
                        )}
                    </div>
                )}
            </div>

            {/* Micro Telemetry Audit Logs */}
            {telemetryLogs.length > 0 && !compact && (
                <div
                    style={{
                        padding: "8px 18px",
                        background: "#090d13",
                        borderTop: "1px solid rgba(255, 255, 255, 0.05)",
                        display: "flex",
                        alignItems: "center",
                        gap: 12,
                        fontSize: "11px",
                        fontFamily: "monospace",
                        color: "#8b949e",
                        overflow: "hidden",
                        whiteSpace: "nowrap",
                    }}
                >
                    <span style={{ color: "#38bdf8", fontWeight: 700 }}>
                        [IoT MQTT EVENT]:
                    </span>
                    <span style={{ color: "#c9d1d9" }}>
                        {telemetryLogs[0].time} - {telemetryLogs[0].text}
                    </span>
                </div>
            )}
        </div>
    );
}
