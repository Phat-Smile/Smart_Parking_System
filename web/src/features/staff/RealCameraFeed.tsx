"use client";

import React, { useEffect, useRef, useState, useCallback } from "react";
import { PlateBadge } from "./StaffUi";

export interface RealCameraFeedProps {
    title?: string;
    mode?: "entry" | "exit";
    detectedPlate?: string;
    confidence?: number;
    isScanning?: boolean;
    onPlateDetected?: (plate: string, snapshotUrl?: string) => void;
    onQrCodeScanned?: (qrCode: string) => void;
    onCaptureSnapshot?: (dataUrl: string) => void;
}

export function RealCameraFeed({
    title = "Gate ANPR Camera 01",
    mode = "entry",
    detectedPlate,
    confidence,
    isScanning = false,
    onPlateDetected,
    onQrCodeScanned,
    onCaptureSnapshot,
}: RealCameraFeedProps) {
    const videoRef = useRef<HTMLVideoElement | null>(null);
    const canvasRef = useRef<HTMLCanvasElement | null>(null);

    const [isActive, setIsActive] = useState<boolean>(true);
    const [devices, setDevices] = useState<MediaDeviceInfo[]>([]);
    const [selectedDeviceId, setSelectedDeviceId] = useState<string>("");
    const [stream, setStream] = useState<MediaStream | null>(null);
    const [errorMsg, setErrorMsg] = useState<string>("");
    const [resolution, setResolution] = useState<string>("1080p");
    const [flash, setFlash] = useState<boolean>(false);
    const [qrDetectedNotice, setQrDetectedNotice] = useState<string>("");
    const [lastSnapshot, setLastSnapshot] = useState<string | null>(null);

    // Enumerate video devices
    useEffect(() => {
        if (!navigator?.mediaDevices?.enumerateDevices) return;
        navigator.mediaDevices
            .enumerateDevices()
            .then((devs) => {
                const videoDevs = devs.filter((d) => d.kind === "videoinput");
                setDevices(videoDevs);
                if (videoDevs.length > 0 && !selectedDeviceId) {
                    setSelectedDeviceId(videoDevs[0].deviceId);
                }
            })
            .catch(() => {});
    }, [selectedDeviceId]);

    // Start / stop camera stream
    useEffect(() => {
        if (!isActive) {
            if (stream) {
                stream.getTracks().forEach((track) => track.stop());
                queueMicrotask(() => setStream(null));
            }
            return;
        }

        let isCancelled = false;

        const constraints: MediaStreamConstraints = {
            video: selectedDeviceId
                ? { deviceId: { exact: selectedDeviceId } }
                : {
                      facingMode: { ideal: "environment" },
                      width: { ideal: 1280 },
                      height: { ideal: 720 },
                  },
            audio: false,
        };

        navigator.mediaDevices
            ?.getUserMedia(constraints)
            .then((newStream) => {
                if (isCancelled) {
                    newStream.getTracks().forEach((t) => t.stop());
                    return;
                }
                setStream(newStream);
                if (videoRef.current) {
                    videoRef.current.srcObject = newStream;
                    videoRef.current.play().catch(() => {});
                }

                const track = newStream.getVideoTracks()[0];
                if (track) {
                    const settings = track.getSettings();
                    if (settings.width && settings.height) {
                        setResolution(`${settings.width}x${settings.height}`);
                    }
                }
            })
            .catch((err) => {
                if (isCancelled) return;
                const errText =
                    err.name === "NotAllowedError" ||
                    err.name === "PermissionDeniedError"
                        ? "Quyền truy cập Camera bị từ chối. Vui lòng cho phép quyền Camera trên trình duyệt."
                        : err.name === "NotFoundError" ||
                            err.name === "DevicesNotFoundError"
                          ? "Không tìm thấy thiết bị Camera trên máy này."
                          : `Không thể mở Camera: ${err.message || err.name}`;
                setErrorMsg(errText);
            });

        return () => {
            isCancelled = true;
            if (stream) {
                stream.getTracks().forEach((track) => track.stop());
            }
        };
    }, [isActive, selectedDeviceId]);

    // QR Code scanner loop for Check-out
    useEffect(() => {
        if (mode !== "exit" || !isActive || !stream || !onQrCodeScanned) return;

        let active = true;
        let detector: {
            detect: (
                s: HTMLVideoElement | HTMLCanvasElement,
            ) => Promise<Array<{ rawValue?: string }>>;
        } | null = null;

        // Check if BarcodeDetector is available in window
        if (typeof window !== "undefined" && "BarcodeDetector" in window) {
            try {
                detector = new (
                    window as unknown as {
                        BarcodeDetector: new (opts: {
                            formats: string[];
                        }) => typeof detector;
                    }
                ).BarcodeDetector({
                    formats: ["qr_code"],
                });
            } catch {
                detector = null;
            }
        }

        if (!detector) return;

        let lastScannedTime = 0;

        const scanInterval = setInterval(async () => {
            if (!active || !videoRef.current || videoRef.current.readyState < 2)
                return;
            const now = Date.now();
            if (now - lastScannedTime < 2000) return; // avoid flood

            try {
                const barcodes = await detector.detect(videoRef.current);
                if (barcodes && barcodes.length > 0) {
                    const code = barcodes[0].rawValue;
                    if (code && active) {
                        lastScannedTime = now;
                        setQrDetectedNotice(code);
                        setFlash(true);
                        setTimeout(() => setFlash(false), 300);
                        setTimeout(() => setQrDetectedNotice(""), 3000);
                        onQrCodeScanned(code);
                    }
                }
            } catch {
                // Ignore detection frame error
            }
        }, 300);

        return () => {
            active = false;
            clearInterval(scanInterval);
        };
    }, [mode, isActive, stream, onQrCodeScanned]);

    // Capture snapshot from video canvas
    const handleCapture = useCallback(() => {
        if (!videoRef.current) return;
        const video = videoRef.current;
        const canvas = canvasRef.current || document.createElement("canvas");
        canvas.width = video.videoWidth || 640;
        canvas.height = video.videoHeight || 480;

        const ctx = canvas.getContext("2d");
        if (!ctx) return;
        ctx.drawImage(video, 0, 0, canvas.width, canvas.height);

        const dataUrl = canvas.toDataURL("image/jpeg", 0.9);
        setLastSnapshot(dataUrl);
        setFlash(true);
        setTimeout(() => setFlash(false), 250);

        if (onCaptureSnapshot) {
            onCaptureSnapshot(dataUrl);
        }

        // Try BarcodeDetector on the captured image as well
        if (
            mode === "exit" &&
            onQrCodeScanned &&
            typeof window !== "undefined" &&
            "BarcodeDetector" in window
        ) {
            try {
                const detector = new (
                    window as unknown as {
                        BarcodeDetector: new (opts: { formats: string[] }) => {
                            detect: (
                                s: HTMLCanvasElement,
                            ) => Promise<Array<{ rawValue?: string }>>;
                        };
                    }
                ).BarcodeDetector({
                    formats: ["qr_code"],
                });
                detector
                    .detect(canvas)
                    .then((barcodes: Array<{ rawValue?: string }>) => {
                        if (barcodes && barcodes.length > 0) {
                            const code = barcodes[0].rawValue;
                            if (code) {
                                setQrDetectedNotice(code);
                                onQrCodeScanned(code);
                            }
                        }
                    })
                    .catch(() => {});
            } catch {
                // Ignore
            }
        }
    }, [mode, onCaptureSnapshot, onQrCodeScanned]);

    // Switch between available cameras
    const handleSwitchCamera = useCallback(() => {
        if (devices.length <= 1) return;
        const currentIndex = devices.findIndex(
            (d) => d.deviceId === selectedDeviceId,
        );
        const nextIndex = (currentIndex + 1) % devices.length;
        setSelectedDeviceId(devices[nextIndex].deviceId);
    }, [devices, selectedDeviceId]);

    return (
        <div className="staff-camera-feed">
            <canvas ref={canvasRef} style={{ display: "none" }} />

            {/* Header with Camera status & Controls */}
            <div className="staff-camera-header">
                <div
                    style={{
                        display: "flex",
                        alignItems: "center",
                        gap: "8px",
                    }}
                >
                    <span className="staff-rec-indicator">
                        <span
                            className={`staff-rec-dot ${!isActive ? "bg-slate-500" : ""}`}
                        />
                        {isActive && stream
                            ? "REC LIVE (CAMERA THẬT)"
                            : "OFFLINE"}
                    </span>
                    <span className="staff-camera-title">{title}</span>
                </div>

                <div
                    style={{
                        display: "flex",
                        alignItems: "center",
                        gap: "6px",
                    }}
                >
                    {devices.length > 1 && (
                        <button
                            type="button"
                            onClick={handleSwitchCamera}
                            className="staff-btn staff-btn-ghost staff-btn-sm"
                            style={{
                                color: "#ffffff",
                                padding: "2px 8px",
                                fontSize: "11px",
                            }}
                            title="Đổi camera khác"
                        >
                            🔄 Đổi cam ({devices.length})
                        </button>
                    )}

                    <button
                        type="button"
                        onClick={() => setIsActive(!isActive)}
                        className={`staff-btn staff-btn-sm ${isActive ? "staff-btn-ghost" : "staff-btn-primary"}`}
                        style={{ padding: "2px 8px", fontSize: "11px" }}
                    >
                        {isActive ? "Tắt Cam" : "Bật Camera Thật"}
                    </button>

                    <span className="staff-camera-fps">{resolution}</span>
                </div>
            </div>

            {/* Viewport: Live Video or Fallback Message */}
            <div
                className="staff-camera-viewport"
                style={{ position: "relative", minHeight: "280px" }}
            >
                {/* Flash effect on snapshot or scan */}
                {flash && (
                    <div
                        style={{
                            position: "absolute",
                            inset: 0,
                            backgroundColor: "rgba(255, 255, 255, 0.85)",
                            zIndex: 20,
                            pointerEvents: "none",
                            transition: "opacity 0.2s ease-out",
                        }}
                    />
                )}

                {/* Real Video Element */}
                {isActive && (
                    <video
                        ref={videoRef}
                        autoPlay
                        playsInline
                        muted
                        style={{
                            width: "100%",
                            height: "100%",
                            objectFit: "cover",
                            display: stream ? "block" : "none",
                        }}
                    />
                )}

                {/* Camera disabled or Permission Error View */}
                {(!isActive || !stream) && (
                    <div
                        style={{
                            position: "absolute",
                            inset: 0,
                            display: "flex",
                            flexDirection: "column",
                            alignItems: "center",
                            justifyContent: "center",
                            padding: "20px",
                            textAlign: "center",
                            color: "#94a3b8",
                            gap: "12px",
                            background:
                                "radial-gradient(circle at center, #1e293b 0%, #090d16 100%)",
                        }}
                    >
                        {errorMsg ? (
                            <>
                                <span style={{ fontSize: "28px" }}>⚠️</span>
                                <p
                                    style={{
                                        fontSize: "13px",
                                        color: "#f87171",
                                        maxWidth: "360px",
                                    }}
                                >
                                    {errorMsg}
                                </p>
                                <button
                                    type="button"
                                    onClick={() => {
                                        setIsActive(true);
                                    }}
                                    className="staff-btn staff-btn-primary staff-btn-sm"
                                >
                                    Thử cấp lại quyền Camera
                                </button>
                            </>
                        ) : (
                            <>
                                <span style={{ fontSize: "28px" }}>📹</span>
                                <p
                                    style={{
                                        fontSize: "13px",
                                        maxWidth: "340px",
                                    }}
                                >
                                    Camera thật đang tắt. Bấm nút dưới để kết
                                    nối Camera/Webcam của máy.
                                </p>
                                <button
                                    type="button"
                                    onClick={() => setIsActive(true)}
                                    className="staff-btn staff-btn-primary staff-btn-sm"
                                >
                                    Kích hoạt Camera Thật
                                </button>
                            </>
                        )}
                    </div>
                )}

                {/* CCTV Overlays: Grid, Scanline, Target Frame */}
                <div
                    className="staff-camera-grid"
                    style={{ pointerEvents: "none" }}
                />
                {isScanning && (
                    <div
                        className="staff-camera-scanline"
                        style={{ pointerEvents: "none" }}
                    />
                )}

                {/* Target box */}
                <div
                    className="staff-camera-target"
                    style={{ pointerEvents: "none" }}
                >
                    <div className="staff-target-corner top-left" />
                    <div className="staff-target-corner top-right" />
                    <div className="staff-target-corner bottom-left" />
                    <div className="staff-target-corner bottom-right" />

                    {/* QR detected popup notification */}
                    {qrDetectedNotice && (
                        <div
                            style={{
                                position: "absolute",
                                bottom: "110%",
                                background: "#10b981",
                                color: "#ffffff",
                                padding: "6px 12px",
                                borderRadius: "6px",
                                fontSize: "12px",
                                fontWeight: "bold",
                                whiteSpace: "nowrap",
                                boxShadow: "0 4px 12px rgba(0,0,0,0.3)",
                            }}
                        >
                            ✓ Đã quét mã QR: {qrDetectedNotice}
                        </div>
                    )}

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

            {/* Bottom Camera Action Bar */}
            <div
                style={{
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "space-between",
                    padding: "10px 16px",
                    background: "#020617",
                    borderTop: "1px solid rgba(255, 255, 255, 0.1)",
                }}
            >
                <div
                    style={{
                        display: "flex",
                        alignItems: "center",
                        gap: "8px",
                    }}
                >
                    <button
                        type="button"
                        onClick={handleCapture}
                        disabled={!stream}
                        className="staff-btn staff-btn-primary staff-btn-sm"
                        style={{
                            display: "flex",
                            alignItems: "center",
                            gap: "6px",
                        }}
                    >
                        📸{" "}
                        {mode === "entry"
                            ? "Chụp ảnh biển số"
                            : "Chụp & Quét mã QR"}
                    </button>

                    {lastSnapshot && (
                        <span style={{ fontSize: "11px", color: "#10b981" }}>
                            ✓ Đã chụp ảnh frame (
                            {new Date().toLocaleTimeString("vi-VN")})
                        </span>
                    )}
                </div>

                <div style={{ fontSize: "11px", color: "#64748b" }}>
                    {mode === "entry"
                        ? "Nhận diện biển số xe vào"
                        : "Hướng camera vào mã QR để tự quét"}
                </div>
            </div>
        </div>
    );
}
