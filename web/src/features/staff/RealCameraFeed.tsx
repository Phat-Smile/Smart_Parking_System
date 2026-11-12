"use client";

import React, { useEffect, useRef, useState, useCallback } from "react";
import {
    Camera,
    RefreshCw,
    Power,
    VideoOff,
    AlertTriangle,
    CheckCircle2,
    ScanLine,
    Sparkles,
    ShieldAlert,
} from "lucide-react";
import { PlateBadge } from "./StaffUi";
import { recognizePlateFromCanvas } from "./anpr/plateOcr";

export interface RealCameraFeedProps {
    title?: string;
    mode?: "entry" | "exit";
    detectedPlate?: string;
    confidence?: number;
    isScanning?: boolean;
    onPlateDetected?: (
        plate: string,
        vehicleType?: "Car" | "Motorbike",
        confidence?: number,
        snapshotUrl?: string,
    ) => void;
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
    const [scanFeedback, setScanFeedback] = useState<string>("");
    const [lastSnapshot, setLastSnapshot] = useState<string | null>(null);

    // ANPR State
    const [isOcrProcessing, setIsOcrProcessing] = useState<boolean>(false);
    const [internalPlate, setInternalPlate] = useState<string>("");
    const [internalConf, setInternalConf] = useState<number | undefined>(undefined);
    const [autoScanEnabled, setAutoScanEnabled] = useState<boolean>(true);
    const [isLocked, setIsLocked] = useState<boolean>(false);
    const lastDetectedPlateRef = useRef<string>("");
    const isScanningRef = useRef<boolean>(false);
    const isLockedRef = useRef<boolean>(false);

    // Đồng bộ reset khi detectedPlate từ bên ngoài bị xóa (Xóa / Tiếp nhận xe tiếp theo) hoặc thay đổi
    useEffect(() => {
        if (!detectedPlate) {
            setInternalPlate("");
            setInternalConf(undefined);
            lastDetectedPlateRef.current = "";
            setIsLocked(false);
            isLockedRef.current = false;
        } else {
            setIsLocked(true);
            isLockedRef.current = true;
        }
    }, [detectedPlate]);

    const effectivePlate = (detectedPlate && detectedPlate.trim()) || internalPlate;

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
            .catch(() => { });
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
                    videoRef.current.play().catch(() => { });
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

    // Auto-scan ANPR loop: Tự động quét và nhận diện biển số xe khi đưa vào khung hình camera
    useEffect(() => {
        if (!isActive || !stream || !autoScanEnabled || !onPlateDetected) return;

        let active = true;
        let timerId: NodeJS.Timeout | null = null;

        const runScanLoop = async () => {
            if (!active || isLockedRef.current) return;
            if (
                !videoRef.current ||
                videoRef.current.readyState < 2 ||
                isScanningRef.current
            ) {
                if (active && !isLockedRef.current) timerId = setTimeout(runScanLoop, 300);
                return;
            }

            const video = videoRef.current;
            const canvas = canvasRef.current || document.createElement("canvas");
            canvas.width = video.videoWidth || 1280;
            canvas.height = video.videoHeight || 720;
            const ctx = canvas.getContext("2d");
            if (!ctx) {
                if (active && !isLockedRef.current) timerId = setTimeout(runScanLoop, 300);
                return;
            }
            ctx.drawImage(video, 0, 0, canvas.width, canvas.height);

            isScanningRef.current = true;

            try {
                const res = await recognizePlateFromCanvas(canvas);
                if (active && res && res.plateNumber) {
                    // Đã quét thành công biển số -> Khóa quét 1 lần duy nhất ("chỉ quét 1 lần")
                    lastDetectedPlateRef.current = res.plateNumber;
                    setInternalPlate(res.plateNumber);
                    setInternalConf(res.confidence);
                    setIsLocked(true);
                    isLockedRef.current = true;

                    const snap = canvas.toDataURL("image/jpeg", 0.9);
                    setLastSnapshot(snap);
                    setFlash(true);
                    setTimeout(() => setFlash(false), 200);

                    setScanFeedback(`Đã nhận diện: ${res.plateNumber} (Đã khóa)`);
                    setTimeout(() => setScanFeedback(""), 4500);

                    onPlateDetected(
                        res.plateNumber,
                        res.vehicleType,
                        res.confidence,
                        snap,
                    );
                    return;
                }
            } catch {
                // Bỏ qua lỗi frame đơn lẻ
            } finally {
                isScanningRef.current = false;
                if (active && !isLockedRef.current) {
                    // Quét siêu tốc mỗi 300ms khi đưa điện thoại vào để bắt tức thì ("quét liền")!
                    timerId = setTimeout(runScanLoop, 300);
                }
            }
        };

        timerId = setTimeout(runScanLoop, 250);

        return () => {
            active = false;
            if (timerId) clearTimeout(timerId);
        };
    }, [isActive, stream, autoScanEnabled, onPlateDetected]);

    // Capture snapshot from video canvas and run OCR
    const handleCapture = useCallback(async () => {
        if (!videoRef.current) return;
        const video = videoRef.current;
        const canvas = canvasRef.current || document.createElement("canvas");
        canvas.width = video.videoWidth || 1280;
        canvas.height = video.videoHeight || 720;

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

        // Chạy nhận diện OCR quang học trực tiếp từ frame vừa chụp
        if (onPlateDetected) {
            setIsOcrProcessing(true);
            try {
                const ocrRes = await recognizePlateFromCanvas(canvas);
                if (ocrRes && ocrRes.plateNumber) {
                    lastDetectedPlateRef.current = ocrRes.plateNumber;
                    setInternalPlate(ocrRes.plateNumber);
                    setInternalConf(ocrRes.confidence);
                    setIsLocked(true);
                    isLockedRef.current = true;
                    setScanFeedback(`Đã nhận diện: ${ocrRes.plateNumber} (Đã khóa)`);
                    setTimeout(() => setScanFeedback(""), 4500);

                    onPlateDetected(
                        ocrRes.plateNumber,
                        ocrRes.vehicleType,
                        ocrRes.confidence,
                        dataUrl,
                    );
                } else {
                    setScanFeedback("Chưa đọc được biển số rõ — Hãy giơ điện thoại lại gần hơn và bấm chụp lại");
                    setTimeout(() => setScanFeedback(""), 4500);
                }
            } catch (err) {
                console.warn("[Camera Capture OCR error]", err);
            } finally {
                setIsOcrProcessing(false);
            }
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
                    .catch(() => { });
            } catch {
                // Ignore
            }
        }
    }, [mode, onCaptureSnapshot, onPlateDetected, onQrCodeScanned]);

    // Switch between available cameras
    const handleSwitchCamera = useCallback(() => {
        if (devices.length <= 1) return;
        const currentIndex = devices.findIndex(
            (d) => d.deviceId === selectedDeviceId,
        );
        const nextIndex = (currentIndex + 1) % devices.length;
        setSelectedDeviceId(devices[nextIndex].deviceId);
    }, [devices, selectedDeviceId]);

    const isCameraConnected = isActive && Boolean(stream);
    const hasPlate = Boolean(effectivePlate && effectivePlate.trim());
    const confValue =
        typeof confidence === "number"
            ? confidence
            : typeof internalConf === "number"
                ? internalConf
                : 0;
    const isHighConf = confValue >= 0.8;
    const isScanningBusy = isScanning || isOcrProcessing;

    return (
        <div className="staff-camera-feed">
            <canvas ref={canvasRef} style={{ display: "none" }} />

            {/* Header with Camera status & Controls */}
            <div className="staff-camera-header">
                <div className="staff-camera-header-left">
                    <span
                        className={`staff-rec-indicator ${isCameraConnected ? "is-live" : "is-offline"
                            }`}
                        title={
                            isCameraConnected
                                ? "Luồng video từ Camera đang hoạt động trực tiếp"
                                : errorMsg
                                    ? "Camera mất kết nối hoặc chưa cấp quyền"
                                    : "Camera đang tạm tắt"
                        }
                    >
                        <span
                            className={`staff-rec-dot ${!isCameraConnected ? "dot-offline" : ""
                                }`}
                        />
                        {isCameraConnected
                            ? "REC LIVE"
                            : errorMsg
                                ? "MẤT KẾT NỐI"
                                : "OFFLINE"}
                    </span>
                    <span className="staff-camera-title">{title}</span>
                </div>

                <div className="staff-camera-header-right">
                    {/* Auto Scan Toggle Pill */}
                    {onPlateDetected && (
                        <button
                            type="button"
                            onClick={() => setAutoScanEnabled(!autoScanEnabled)}
                            className={`staff-cam-ctrl-btn ${autoScanEnabled ? "btn-active" : "btn-warning"
                                }`}
                            title="Bật/Tắt chế độ tự động nhận diện biển số ANPR khi đưa biển số vào camera"
                        >
                            <Sparkles size={11} strokeWidth={2.2} />
                            <span>
                                Auto OCR: {autoScanEnabled ? "BẬT" : "TẮT (Bấm để Bật)"}
                            </span>
                        </button>
                    )}

                    {/* Recognition Status Pill */}
                    {isScanningBusy ? (
                        <span
                            className="staff-cam-state-pill state-scanning"
                            title="Hệ thống ANPR đang quét và phân tích biển số xe"
                        >
                            <ScanLine
                                size={12}
                                strokeWidth={2.3}
                                className="staff-spin"
                            />
                            <span>Đang nhận diện...</span>
                        </span>
                    ) : hasPlate && isHighConf ? (
                        <span
                            className="staff-cam-state-pill state-success"
                            title="Biển số đã được nhận diện tự động với độ tin cậy cao"
                        >
                            <CheckCircle2 size={12} strokeWidth={2.3} />
                            <span>Nhận diện thành công</span>
                        </span>
                    ) : hasPlate && !isHighConf ? (
                        <span
                            className="staff-cam-state-pill state-warning"
                            title="Độ tin cậy OCR thấp — Cần nhân viên kiểm tra và xác nhận thủ công"
                        >
                            <AlertTriangle size={12} strokeWidth={2.3} />
                            <span>Cần xác nhận thủ công</span>
                        </span>
                    ) : (
                        <span
                            className="staff-cam-state-pill state-error"
                            title="Chưa phát hiện hoặc nhận diện thất bại biển số trong khung hình"
                        >
                            <ShieldAlert size={12} strokeWidth={2.3} />
                            <span>Chưa nhận diện</span>
                        </span>
                    )}

                    {devices.length > 1 && (
                        <button
                            type="button"
                            onClick={handleSwitchCamera}
                            className="staff-cam-ctrl-btn"
                            title={`Chuyển đổi thiết bị Camera (${devices.length} thiết bị khả dụng)`}
                        >
                            <RefreshCw size={12} strokeWidth={2.2} />
                            <span>Đổi cam ({devices.length})</span>
                        </button>
                    )}

                    <button
                        type="button"
                        onClick={() => setIsActive(!isActive)}
                        className={`staff-cam-ctrl-btn ${isActive ? "btn-active" : "btn-turn-on"
                            }`}
                        title={
                            isActive
                                ? "Tạm dừng kết nối luồng video từ Camera"
                                : "Bật kết nối luồng video từ Camera vật lý"
                        }
                    >
                        <Power size={12} strokeWidth={2.2} />
                        <span>{isActive ? "Tắt Cam" : "Bật Camera"}</span>
                    </button>

                    <span
                        className="staff-camera-fps"
                        title="Độ phân giải luồng hình ảnh Camera ANPR hiện tại"
                    >
                        {resolution}
                    </span>
                </div>
            </div>

            {/* Camera Body: Split into Video Viewport (77%) and Dedicated ANPR Side-Panel (23%) */}
            <div className="staff-camera-body">
                {/* 1. Main Video Viewport (100% clean video, no overlapping cards) */}
                <div className="staff-camera-viewport">
                    {/* Flash effect on snapshot or scan */}
                    {flash && <div className="staff-camera-flash" />}

                    {/* Real Video Element (object-fit: contain prevents cropping important plate details) */}
                    {isActive && (
                        <video
                            ref={videoRef}
                            autoPlay
                            playsInline
                            muted
                            className="staff-camera-video"
                            style={{
                                display: stream ? "block" : "none",
                            }}
                        />
                    )}

                    {/* Camera disabled or Permission Error View */}
                    {(!isActive || !stream) && (
                        <div className="staff-camera-fallback">
                            {errorMsg ? (
                                <>
                                    <div className="staff-cam-fallback-icon is-error">
                                        <AlertTriangle
                                            size={22}
                                            strokeWidth={2}
                                        />
                                    </div>
                                    <strong className="staff-cam-fallback-title">
                                        Camera mất kết nối hoặc chưa cấp quyền
                                    </strong>
                                    <p className="staff-cam-fallback-desc is-error">
                                        {errorMsg}
                                    </p>
                                    <button
                                        type="button"
                                        onClick={() => {
                                            setErrorMsg("");
                                            setIsActive(true);
                                        }}
                                        className="staff-btn staff-btn-primary staff-btn-sm"
                                        title="Thử kết nối lại và yêu cầu quyền truy cập Camera trên trình duyệt"
                                    >
                                        <RefreshCw
                                            size={13}
                                            strokeWidth={2.2}
                                        />
                                        <span>Thử kết nối lại Camera</span>
                                    </button>
                                </>
                            ) : (
                                <>
                                    <div className="staff-cam-fallback-icon">
                                        <VideoOff size={22} strokeWidth={2} />
                                    </div>
                                    <strong className="staff-cam-fallback-title">
                                        Camera trực tiếp đang tạm tắt
                                    </strong>
                                    <p className="staff-cam-fallback-desc">
                                        Hệ thống vẫn hiển thị khung nhận diện
                                        ANPR. Bấm nút bên dưới để bật luồng
                                        video từ Camera/Webcam.
                                    </p>
                                    <button
                                        type="button"
                                        onClick={() => setIsActive(true)}
                                        className="staff-btn staff-btn-primary staff-btn-sm"
                                        title="Kích hoạt luồng video từ Camera thiết bị"
                                    >
                                        <Camera size={13} strokeWidth={2.2} />
                                        <span>Kích hoạt Camera Thật</span>
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
                        className={`staff-camera-target ${hasPlate
                                ? isHighConf
                                    ? "target-locked"
                                    : "target-warning"
                                : ""
                            }`}
                        style={{ pointerEvents: "none" }}
                    >
                        <div className="staff-target-corner top-left" />
                        <div className="staff-target-corner top-right" />
                        <div className="staff-target-corner bottom-left" />
                        <div className="staff-target-corner bottom-right" />

                        {/* QR or Plate detected popup notification */}
                        {scanFeedback ? (
                            <div className="staff-camera-qr-toast">
                                <Sparkles size={13} strokeWidth={2.3} />
                                <span>{scanFeedback}</span>
                            </div>
                        ) : qrDetectedNotice ? (
                            <div className="staff-camera-qr-toast">
                                <CheckCircle2 size={13} strokeWidth={2.3} />
                                <span>Đã quét mã QR: {qrDetectedNotice}</span>
                            </div>
                        ) : !autoScanEnabled ? (
                            <div
                                className="staff-camera-qr-toast"
                                style={{ background: "rgba(217, 119, 6, 0.95)", color: "#fff" }}
                            >
                                <AlertTriangle size={13} strokeWidth={2.3} />
                                <span>Chế độ tự động quét đang TẮT — Bấm nút &quot;Auto OCR: TẮT&quot; ở trên để Bật lại</span>
                            </div>
                        ) : null}
                    </div>
                </div>

                {/* 2. Dedicated ANPR Side-Panel (Never blocks video stream) */}
                <div className="staff-camera-anpr-panel">
                    <div className="staff-anpr-panel-head">
                        <span className="staff-anpr-panel-title">
                            NHẬN DIỆN BIỂN SỐ
                        </span>
                        <span className="staff-anpr-panel-tag">ANPR HD</span>
                    </div>

                    <div className="staff-anpr-panel-plate">
                        {hasPlate && effectivePlate ? (
                            <PlateBadge plate={effectivePlate} />
                        ) : (
                            <div className="staff-anpr-plate-empty">
                                <span>Chờ xe vào khung ngắm...</span>
                            </div>
                        )}
                    </div>

                    <div className="staff-anpr-stats-list">
                        <div className="staff-anpr-stat-row">
                            <span className="staff-anpr-stat-label">
                                Độ tin cậy OCR
                            </span>
                            {confValue > 0 ? (
                                <span
                                    className={`staff-anpr-stat-val ${isHighConf ? "val-high" : "val-low"
                                        }`}
                                >
                                    {isHighConf ? (
                                        <Sparkles size={11} strokeWidth={2.3} />
                                    ) : (
                                        <AlertTriangle
                                            size={11}
                                            strokeWidth={2.3}
                                        />
                                    )}
                                    <span>
                                        {(confValue * 100).toFixed(0)}%
                                    </span>
                                </span>
                            ) : (
                                <span className="staff-anpr-stat-val val-muted">
                                    —
                                </span>
                            )}
                        </div>

                        <div className="staff-anpr-stat-row">
                            <span className="staff-anpr-stat-label">
                                Trạng thái
                            </span>
                            <span
                                className={`staff-anpr-badge ${isScanning
                                        ? "badge-scanning"
                                        : hasPlate
                                            ? isHighConf
                                                ? "badge-success"
                                                : "badge-warning"
                                            : "badge-idle"
                                    }`}
                            >
                                {isScanning
                                    ? "Đang quét..."
                                    : hasPlate
                                        ? isHighConf
                                            ? "Thành công"
                                            : "Cần kiểm tra"
                                        : "Chờ quét"}
                            </span>
                        </div>

                        <div className="staff-anpr-stat-row">
                            <span className="staff-anpr-stat-label">
                                Tự động khớp
                            </span>
                            <span
                                className={`staff-anpr-badge ${hasPlate && isHighConf
                                        ? "badge-success"
                                        : "badge-neutral"
                                    }`}
                            >
                                {hasPlate && isHighConf
                                    ? "Đã khớp"
                                    : "Chờ xác nhận"}
                            </span>
                        </div>
                    </div>
                </div>
            </div>

            {/* Bottom Camera Action Bar */}
            <div className="staff-camera-footer">
                <div className="staff-camera-footer-left">
                    <button
                        type="button"
                        onClick={handleCapture}
                        disabled={!stream}
                        className="staff-cam-capture-btn"
                        title={
                            stream
                                ? "Chụp và lưu ảnh frame biển số hiện tại từ Camera"
                                : "Cần bật Camera thật để chụp frame hình ảnh"
                        }
                    >
                        <Camera size={14} strokeWidth={2.2} />
                        <span>
                            {mode === "entry"
                                ? isLocked
                                    ? "Chụp lại / Quét lại"
                                    : "Chụp ảnh biển số"
                                : "Chụp & Quét mã QR"}
                        </span>
                    </button>

                    {lastSnapshot ? (
                        <span
                            className="staff-cam-snap-status"
                            title="Đã lưu hình ảnh đối chiếu từ Camera"
                        >
                            <CheckCircle2 size={13} strokeWidth={2.3} />
                            <span>
                                {isLocked ? "Đã khóa 1 lần · " : ""}Đã lưu frame (
                                {new Date().toLocaleTimeString("vi-VN")})
                            </span>
                        </span>
                    ) : (
                        <span className="staff-cam-mode-hint">
                            {isCameraConnected
                                ? "Luồng Camera sẵn sàng chụp đối chiếu"
                                : "Chế độ ANPR tiêu chuẩn"}
                        </span>
                    )}
                </div>

                <div className="staff-camera-footer-right">
                    <span
                        className="staff-cam-ocr-summary"
                        title="Trạng thái bộ đọc biển số quang học ANPR/OCR"
                    >
                        <ScanLine size={13} strokeWidth={2.2} />
                        <span>
                            {mode === "entry"
                                ? typeof confidence === "number"
                                    ? `OCR ${(confidence * 100).toFixed(0)}% · ${confidence >= 0.8
                                        ? "Tự động khớp"
                                        : "Cần kiểm tra"
                                    }`
                                    : "Nhận diện biển số xe vào"
                                : "Hướng camera vào mã QR để tự quét"}
                        </span>
                    </span>
                </div>
            </div>
        </div>
    );
}
