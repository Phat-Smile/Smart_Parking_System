"use client";

import React, { useState, useEffect, useRef } from "react";
import {
    IoTBarrierGateSimulator,
    type BarrierPhysicalState,
    type VehicleApproachState,
} from "./IoTBarrierGateSimulator";
import { PlateBadge } from "./StaffUi";
import { useStaffData } from "./staffData";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import {
    Activity,
    AlertTriangle,
    CheckCircle2,
    Play,
    RotateCcw,
    ShieldAlert,
    Sliders,
    Terminal,
    Wifi,
    Cpu,
    Radio,
    Zap,
    Car,
} from "lucide-react";

interface VehiclePreset {
    id: string;
    plate: string;
    type: "Car" | "Motorbike";
    label: string;
    isEmergency?: boolean;
}

const VEHICLE_PRESETS: VehiclePreset[] = [
    {
        id: "p1",
        plate: "51F-782.91",
        type: "Car",
        label: "Ô tô Sedan (Khách vãng lai)",
    },
    {
        id: "p2",
        plate: "51A-999.88",
        type: "Car",
        label: "Ô tô SUV (Khách đặt chỗ)",
    },
    {
        id: "p3",
        plate: "59X1-234.56",
        type: "Motorbike",
        label: "Xe máy Honda SH (Thẻ tháng)",
    },
    {
        id: "p4",
        plate: "51B-001.15",
        type: "Car",
        label: "🚑 Xe Cứu Thương (Ưu tiên khẩn cấp)",
        isEmergency: true,
    },
];

export function StaffSimulatorPage() {
    const { assignedLot } = useStaffData();
    const { language } = useLanguage();

    const [selectedGate, setSelectedGate] = useState<"ENTRY" | "EXIT">("ENTRY");
    const [selectedVehicle, setSelectedVehicle] = useState<VehiclePreset>(
        VEHICLE_PRESETS[0],
    );
    const [physicalState, setPhysicalState] =
        useState<BarrierPhysicalState>("CLOSED");
    const [vehicleState, setVehicleState] =
        useState<VehicleApproachState>("AT_STOP_LINE");
    const [isAutoLoopRunning, setIsAutoLoopRunning] = useState(false);
    const [autoStepText, setAutoStepText] = useState("");
    const [mqttLogs, setMqttLogs] = useState<
        Array<{ time: string; topic: string; payload: string; level: string }>
    >([]);

    const loopTimeoutRef = useRef<NodeJS.Timeout | null>(null);

    // Push MQTT log helper
    const pushMqttLog = (
        topic: string,
        payload: Record<string, unknown>,
        level: "INFO" | "WARN" | "SUCCESS" = "INFO",
    ) => {
        const time = new Date().toLocaleTimeString("vi-VN", {
            hour: "2-digit",
            minute: "2-digit",
            second: "2-digit",
        });
        setMqttLogs((prev) => [
            {
                time,
                topic,
                payload: JSON.stringify(payload),
                level,
            },
            ...prev.slice(0, 19),
        ]);
    };

    // Initial boot log
    useEffect(() => {
        const timer = setTimeout(() => {
            pushMqttLog(
                `parkwise/iot/${selectedGate.toLowerCase()}/status`,
                {
                    gatewayId: "GW-RPi4-01",
                    firmware: "v2.4.1-rc3",
                    boomAngle: 0,
                    servoStatus: "READY",
                    sensors: { loopA: false, loopB: false, photocell: "CLEAR" },
                },
                "SUCCESS",
            );
        }, 0);
        return () => clearTimeout(timer);
    }, [selectedGate]);

    // Handle full automatic end-to-end simulation sequence
    const startAutoSimulationLoop = () => {
        if (isAutoLoopRunning) return;
        setIsAutoLoopRunning(true);
        setAutoStepText("1/5: Xe đang tiếp cận vạch dừng cổng bãi...");

        // Step 1: Vehicle Approaches (0s)
        setVehicleState("APPROACHING");
        setPhysicalState("CLOSED");
        pushMqttLog(
            `parkwise/iot/${selectedGate.toLowerCase()}/events`,
            { event: "VEHICLE_APPROACHING", loopA: true, distance: "3.2m" },
            "INFO",
        );

        // Step 2: At Stop line & ANPR Scan (1.2s)
        loopTimeoutRef.current = setTimeout(() => {
            setVehicleState("AT_STOP_LINE");
            setAutoStepText(
                `2/5: Camera ANPR quét biển số [${selectedVehicle.plate}] - Độ tin cậy 99.2%`,
            );
            pushMqttLog(
                `parkwise/iot/${selectedGate.toLowerCase()}/anpr`,
                {
                    plate: selectedVehicle.plate,
                    vehicleType: selectedVehicle.type,
                    confidence: 0.992,
                    snapshotUrl: "s3://parkwise/captures/live_gate.jpg",
                },
                "SUCCESS",
            );

            // Step 3: Backend Approval & Barrier Open Command (2.5s)
            loopTimeoutRef.current = setTimeout(() => {
                setAutoStepText(
                    "3/5: Xác nhận phiên hợp lệ! Động cơ Servo nâng cần barrier lên 90°...",
                );
                setPhysicalState("OPENING");
                pushMqttLog(
                    `parkwise/iot/${selectedGate.toLowerCase()}/cmd`,
                    {
                        cmd: "OPEN_BARRIER",
                        targetAngle: 90,
                        durationMs: 1200,
                        authBy: "API_GATE_AUTO",
                    },
                    "SUCCESS",
                );

                loopTimeoutRef.current = setTimeout(() => {
                    setPhysicalState("OPEN");
                    setAutoStepText(
                        "4/5: Barrier đã mở 90° - Đèn tín hiệu XANH - Mời xe qua cổng!",
                    );

                    // Step 4: Vehicle Passes Under Gate (4.0s)
                    loopTimeoutRef.current = setTimeout(() => {
                        setVehicleState("PASSING");
                        setAutoStepText(
                            "5/5: Xe đang di chuyển qua barrier. Vòng từ 2 & Photocell bảo vệ kích hoạt...",
                        );
                        pushMqttLog(
                            `parkwise/iot/${selectedGate.toLowerCase()}/safety`,
                            {
                                loopB: true,
                                photocell: "BLOCKED",
                                antiCrushActive: true,
                            },
                            "WARN",
                        );

                        // Step 5: Vehicle Clears & Auto-close Gate (6.5s)
                        loopTimeoutRef.current = setTimeout(() => {
                            setVehicleState("CLEARED");
                            setAutoStepText(
                                "Hoàn tất! Xe đã qua an toàn. Đóng cần barrier về 0°.",
                            );
                            pushMqttLog(
                                `parkwise/iot/${selectedGate.toLowerCase()}/safety`,
                                {
                                    loopB: false,
                                    photocell: "CLEAR",
                                    allClear: true,
                                },
                                "INFO",
                            );

                            loopTimeoutRef.current = setTimeout(() => {
                                setPhysicalState("CLOSING");
                                pushMqttLog(
                                    `parkwise/iot/${selectedGate.toLowerCase()}/cmd`,
                                    { cmd: "CLOSE_BARRIER", targetAngle: 0 },
                                    "INFO",
                                );

                                loopTimeoutRef.current = setTimeout(() => {
                                    setPhysicalState("CLOSED");
                                    setIsAutoLoopRunning(false);
                                    setAutoStepText("");
                                    pushMqttLog(
                                        `parkwise/iot/${selectedGate.toLowerCase()}/status`,
                                        {
                                            boomAngle: 0,
                                            trafficLight: "RED",
                                            state: "CLOSED",
                                        },
                                        "SUCCESS",
                                    );
                                }, 1200);
                            }, 800);
                        }, 2200);
                    }, 1500);
                }, 1200);
            }, 1400);
        }, 1200);
    };

    const stopSimulation = () => {
        if (loopTimeoutRef.current) {
            clearTimeout(loopTimeoutRef.current);
            loopTimeoutRef.current = null;
        }
        setIsAutoLoopRunning(false);
        setAutoStepText("");
        setPhysicalState("CLOSED");
        setVehicleState("AT_STOP_LINE");
        pushMqttLog(
            `parkwise/iot/${selectedGate.toLowerCase()}/reset`,
            { action: "MANUAL_STOP_REQUESTED" },
            "WARN",
        );
    };

    return (
        <div className="staff-page" style={{ paddingBottom: 60 }}>
            {/* Header */}
            <header className="staff-header" style={{ marginBottom: 20 }}>
                <div>
                    <div
                        style={{
                            display: "flex",
                            alignItems: "center",
                            gap: 10,
                            marginBottom: 4,
                        }}
                    >
                        <span style={{ fontSize: 24 }}>🚧</span>
                        <h1 className="staff-title" style={{ margin: 0 }}>
                            {language === "en"
                                ? "IoT Barrier Gate & Traffic Light Simulator"
                                : "Mô Phỏng Cổng Barrier IoT & Đèn Giao Thông"}
                        </h1>
                        <span
                            style={{
                                background: "rgba(16, 185, 129, 0.15)",
                                color: "#10b981",
                                border: "1px solid rgba(16, 185, 129, 0.3)",
                                padding: "2px 8px",
                                borderRadius: "4px",
                                fontSize: "11px",
                                fontWeight: 700,
                            }}
                        >
                            HARDWARE LAB v2.4
                        </span>
                    </div>
                    <p className="staff-subtitle" style={{ margin: 0 }}>
                        {assignedLot?.lotName
                            ? `Bãi trực: ${assignedLot.lotName} · `
                            : ""}
                        Mô phỏng trục quay servo 90°, đèn LED Fresnell Xanh/Đỏ,
                        vòng từ Loop A/B, sóng radar ANPR và công tắc mở cưỡng
                        bức khẩn cấp.
                    </p>
                </div>

                {/* Gate Selector Mode */}
                <div style={{ display: "flex", gap: 8 }}>
                    <button
                        type="button"
                        onClick={() => setSelectedGate("ENTRY")}
                        className={`staff-btn ${
                            selectedGate === "ENTRY"
                                ? "staff-btn-primary"
                                : "staff-btn-secondary"
                        }`}
                        style={{
                            display: "flex",
                            alignItems: "center",
                            gap: 6,
                        }}
                    >
                        <span>🚗 Cổng Vào (Gate IN-01)</span>
                    </button>
                    <button
                        type="button"
                        onClick={() => setSelectedGate("EXIT")}
                        className={`staff-btn ${
                            selectedGate === "EXIT"
                                ? "staff-btn-primary"
                                : "staff-btn-secondary"
                        }`}
                        style={{
                            display: "flex",
                            alignItems: "center",
                            gap: 6,
                        }}
                    >
                        <span>💳 Cổng Ra (Gate OUT-02)</span>
                    </button>
                </div>
            </header>

            {/* Quick Vehicle Presets Bar */}
            <div
                style={{
                    background: "#ffffff",
                    border: "1px solid var(--pw-admin-line, #e8eaef)",
                    borderRadius: "12px",
                    padding: "12px 16px",
                    marginBottom: 20,
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "space-between",
                    flexWrap: "wrap",
                    gap: 12,
                }}
            >
                <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                    <span
                        style={{
                            fontSize: "12px",
                            fontWeight: 700,
                            color: "var(--pw-admin-muted, #5d6474)",
                        }}
                    >
                        Chọn mẫu xe thử nghiệm:
                    </span>
                    <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
                        {VEHICLE_PRESETS.map((p) => (
                            <button
                                key={p.id}
                                type="button"
                                onClick={() => setSelectedVehicle(p)}
                                style={{
                                    background:
                                        selectedVehicle.id === p.id
                                            ? "var(--pw-admin-ink, #171717)"
                                            : "#f1f5f9",
                                    color:
                                        selectedVehicle.id === p.id
                                            ? "var(--pw-admin-lime, #eafe89)"
                                            : "#1e293b",
                                    border: p.isEmergency
                                        ? "1px solid #ef4444"
                                        : "1px solid transparent",
                                    padding: "6px 12px",
                                    borderRadius: "8px",
                                    fontSize: "12px",
                                    fontWeight: 700,
                                    cursor: "pointer",
                                    display: "flex",
                                    alignItems: "center",
                                    gap: 6,
                                }}
                            >
                                <span>{p.label}</span>
                                <code
                                    style={{
                                        fontSize: "11px",
                                        background: "rgba(0,0,0,0.15)",
                                        padding: "1px 5px",
                                        borderRadius: "3px",
                                    }}
                                >
                                    {p.plate}
                                </code>
                            </button>
                        ))}
                    </div>
                </div>

                {/* Automated Full Test Runner */}
                <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                    {!isAutoLoopRunning ? (
                        <button
                            type="button"
                            onClick={startAutoSimulationLoop}
                            style={{
                                background: "#059669",
                                color: "#ffffff",
                                border: "none",
                                padding: "8px 16px",
                                borderRadius: "8px",
                                fontSize: "13px",
                                fontWeight: 800,
                                cursor: "pointer",
                                display: "flex",
                                alignItems: "center",
                                gap: 6,
                                boxShadow: "0 2px 8px rgba(5, 150, 105, 0.3)",
                            }}
                        >
                            <Play size={15} />
                            <span>Chạy Kịch Bản Tự Động (Auto Test Flow)</span>
                        </button>
                    ) : (
                        <button
                            type="button"
                            onClick={stopSimulation}
                            style={{
                                background: "#dc2626",
                                color: "#ffffff",
                                border: "none",
                                padding: "8px 16px",
                                borderRadius: "8px",
                                fontSize: "13px",
                                fontWeight: 800,
                                cursor: "pointer",
                                display: "flex",
                                alignItems: "center",
                                gap: 6,
                            }}
                        >
                            <RotateCcw size={15} />
                            <span>Dừng Mô Phỏng</span>
                        </button>
                    )}
                </div>
            </div>

            {/* Auto step indicator */}
            {isAutoLoopRunning && (
                <div
                    style={{
                        background: "rgba(16, 185, 129, 0.1)",
                        border: "1px solid #10b981",
                        borderRadius: "8px",
                        padding: "10px 16px",
                        marginBottom: "16px",
                        display: "flex",
                        alignItems: "center",
                        gap: 10,
                        color: "#065f46",
                        fontWeight: 700,
                        fontSize: "13px",
                    }}
                >
                    <Activity
                        size={18}
                        className="animate-spin text-emerald-600"
                    />
                    <span>{autoStepText}</span>
                </div>
            )}

            {/* The Main Visual Simulator Component */}
            <div style={{ marginBottom: 24 }}>
                <IoTBarrierGateSimulator
                    gateId={
                        selectedGate === "ENTRY" ? "GATE-IN-01" : "GATE-OUT-02"
                    }
                    gateName={
                        selectedGate === "ENTRY"
                            ? "Cổng Vào Xe Cơ Giới (Lane 01)"
                            : "Cổng Ra Kiểm Soát Phí (Lane 02)"
                    }
                    gateType={selectedGate}
                    physicalState={physicalState}
                    vehicleState={vehicleState}
                    detectedPlate={selectedVehicle.plate}
                    vehicleType={selectedVehicle.type}
                    onGateStateChange={(st) => {
                        setPhysicalState(st);
                        pushMqttLog(
                            `parkwise/iot/${selectedGate.toLowerCase()}/state_changed`,
                            { newState: st },
                            st === "EMERGENCY_OVERRIDE" ? "WARN" : "INFO",
                        );
                    }}
                    onVehicleStateChange={(vs) => {
                        setVehicleState(vs);
                    }}
                    onManualOverride={(isOv) => {
                        pushMqttLog(
                            `parkwise/iot/${selectedGate.toLowerCase()}/override`,
                            {
                                operatorOverride: isOv,
                                timestamp: new Date().toISOString(),
                                reason: "GUARD_EMERGENCY_BUTTON_TRIGGERED",
                            },
                            "WARN",
                        );
                    }}
                    enableControls={!isAutoLoopRunning}
                    enableEmergencyOverride={true}
                    interactiveDemo={true}
                />
            </div>

            {/* Split Lower Grid: Hardware Diagnostics & Realtime MQTT Stream */}
            <div
                style={{
                    display: "grid",
                    gridTemplateColumns: "1.1fr 1fr",
                    gap: 20,
                }}
            >
                {/* Hardware Architecture & Sensor Specs */}
                <div className="staff-card">
                    <div className="staff-card-header">
                        <div
                            style={{
                                display: "flex",
                                alignItems: "center",
                                gap: 8,
                            }}
                        >
                            <Cpu size={18} className="text-blue-600" />
                            <h2 style={{ fontSize: "16px", margin: 0 }}>
                                Kiến Trúc Phần Cứng IoT & Cảm Biến Cổng
                            </h2>
                        </div>
                        <span className="staff-pill staff-pill-active">
                            READY (24V DC)
                        </span>
                    </div>

                    <div
                        style={{
                            display: "flex",
                            flexDirection: "column",
                            gap: 14,
                        }}
                    >
                        {/* Spec 1: Boom Pole & Servo */}
                        <div
                            style={{
                                display: "flex",
                                justifyContent: "space-between",
                                alignItems: "center",
                                padding: "10px 12px",
                                background: "#f8fafc",
                                borderRadius: "8px",
                                border: "1px solid #e2e8f0",
                            }}
                        >
                            <div>
                                <strong
                                    style={{
                                        fontSize: "13px",
                                        color: "#1e293b",
                                    }}
                                >
                                    Động cơ Servo Trục Giảm Tốc (24V DC)
                                </strong>
                                <p
                                    style={{
                                        fontSize: "11px",
                                        color: "#64748b",
                                        margin: "2px 0 0 0",
                                    }}
                                >
                                    Mô-men xoắn 120 Nm · Thời gian nâng hạ 1.2s
                                    · Góc quay 0° - 90°
                                </p>
                            </div>
                            <span
                                style={{
                                    fontFamily: "monospace",
                                    fontSize: "12px",
                                    color: "#059669",
                                    fontWeight: 700,
                                }}
                            >
                                90° ROTATION OK
                            </span>
                        </div>

                        {/* Spec 2: Inductive Loops */}
                        <div
                            style={{
                                display: "flex",
                                justifyContent: "space-between",
                                alignItems: "center",
                                padding: "10px 12px",
                                background: "#f8fafc",
                                borderRadius: "8px",
                                border: "1px solid #e2e8f0",
                            }}
                        >
                            <div>
                                <strong
                                    style={{
                                        fontSize: "13px",
                                        color: "#1e293b",
                                    }}
                                >
                                    Vòng Từ Cảm Ứng (Inductive Loop A & B)
                                </strong>
                                <p
                                    style={{
                                        fontSize: "11px",
                                        color: "#64748b",
                                        margin: "2px 0 0 0",
                                    }}
                                >
                                    Tần số 48.2 kHz · Phát hiện kim loại gầm xe
                                    · Chống kẹp xe (Anti-crush)
                                </p>
                            </div>
                            <span
                                style={{
                                    fontFamily: "monospace",
                                    fontSize: "12px",
                                    color: "#2563eb",
                                    fontWeight: 700,
                                }}
                            >
                                DUAL-CHANNEL OK
                            </span>
                        </div>

                        {/* Spec 3: Photocell & Traffic Light */}
                        <div
                            style={{
                                display: "flex",
                                justifyContent: "space-between",
                                alignItems: "center",
                                padding: "10px 12px",
                                background: "#f8fafc",
                                borderRadius: "8px",
                                border: "1px solid #e2e8f0",
                            }}
                        >
                            <div>
                                <strong
                                    style={{
                                        fontSize: "13px",
                                        color: "#1e293b",
                                    }}
                                >
                                    Cảm Biến Quang Điện & Đèn Tín Hiệu Kép
                                </strong>
                                <p
                                    style={{
                                        fontSize: "11px",
                                        color: "#64748b",
                                        margin: "2px 0 0 0",
                                    }}
                                >
                                    Tia hồng ngoại 15m · Thấu kính Fresnel LED
                                    Xanh/Đỏ 200mm
                                </p>
                            </div>
                            <span
                                style={{
                                    fontFamily: "monospace",
                                    fontSize: "12px",
                                    color: "#059669",
                                    fontWeight: 700,
                                }}
                            >
                                ACTIVE
                            </span>
                        </div>

                        {/* Spec 4: Emergency Manual Override */}
                        <div
                            style={{
                                display: "flex",
                                justifyContent: "space-between",
                                alignItems: "center",
                                padding: "10px 12px",
                                background: "rgba(239, 68, 68, 0.05)",
                                borderRadius: "8px",
                                border: "1px solid rgba(239, 68, 68, 0.2)",
                            }}
                        >
                            <div>
                                <strong
                                    style={{
                                        fontSize: "13px",
                                        color: "#b91c1c",
                                    }}
                                >
                                    Công Tắc Mở Cưỡng Bức Khẩn Cấp (Manual
                                    Override)
                                </strong>
                                <p
                                    style={{
                                        fontSize: "11px",
                                        color: "#7f1d1d",
                                        margin: "2px 0 0 0",
                                    }}
                                >
                                    Bypass rơ-le cứng · Nắp gập an toàn chống
                                    cấn tay · Còi hú báo động
                                </p>
                            </div>
                            <span
                                style={{
                                    fontFamily: "monospace",
                                    fontSize: "12px",
                                    color: "#dc2626",
                                    fontWeight: 700,
                                }}
                            >
                                SAFETY ARMED
                            </span>
                        </div>
                    </div>
                </div>

                {/* Realtime MQTT Hardware Bus Stream */}
                <div className="staff-card">
                    <div className="staff-card-header">
                        <div
                            style={{
                                display: "flex",
                                alignItems: "center",
                                gap: 8,
                            }}
                        >
                            <Terminal size={18} className="text-emerald-600" />
                            <h2 style={{ fontSize: "16px", margin: 0 }}>
                                Nhật Ký Giao Thức MQTT / Bus Sự Kiện IoT
                            </h2>
                        </div>
                        <span
                            style={{
                                fontSize: "11px",
                                fontFamily: "monospace",
                                background: "#0f172a",
                                color: "#38bdf8",
                                padding: "2px 6px",
                                borderRadius: "4px",
                            }}
                        >
                            QoS 1 · 18ms
                        </span>
                    </div>

                    <div
                        style={{
                            background: "#090d13",
                            borderRadius: "8px",
                            padding: "12px",
                            fontFamily: "monospace",
                            fontSize: "11px",
                            minHeight: "260px",
                            maxHeight: "320px",
                            overflowY: "auto",
                            display: "flex",
                            flexDirection: "column",
                            gap: 8,
                        }}
                    >
                        {mqttLogs.map((log, idx) => (
                            <div
                                key={idx}
                                style={{
                                    lineHeight: 1.4,
                                    borderBottom:
                                        "1px solid rgba(255, 255, 255, 0.05)",
                                    paddingBottom: 6,
                                }}
                            >
                                <div
                                    style={{
                                        display: "flex",
                                        justifyContent: "space-between",
                                        color: "#8b949e",
                                    }}
                                >
                                    <span>[{log.time}]</span>
                                    <span
                                        style={{
                                            color:
                                                log.level === "SUCCESS"
                                                    ? "#34d399"
                                                    : log.level === "WARN"
                                                      ? "#fbbf24"
                                                      : "#60a5fa",
                                            fontWeight: 700,
                                        }}
                                    >
                                        {log.level}
                                    </span>
                                </div>
                                <div
                                    style={{
                                        color: "#38bdf8",
                                        wordBreak: "break-all",
                                    }}
                                >
                                    TOPIC: {log.topic}
                                </div>
                                <div
                                    style={{
                                        color: "#e2e8f0",
                                        wordBreak: "break-all",
                                    }}
                                >
                                    PAYLOAD: {log.payload}
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
            </div>
        </div>
    );
}
