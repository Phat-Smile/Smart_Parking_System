"use client";

import React, { useState, useEffect, useRef, useCallback } from "react";
import QRCode from "react-qr-code";
import {
    VneidCitizenProfile,
    VNEID_SEED_PROFILES,
    findProfileByCccd,
} from "./vneidSeedData";
import {
    createVneidAuthSession,
    subscribeToVneidSession,
    VneidAuthSession,
} from "./vneidSync";
import {
    ShieldCheck,
    X,
    CheckCircle2,
    AlertTriangle,
    QrCode,
    Fingerprint,
    ScanFace,
    Search,
    RefreshCw,
    Clock,
    Smartphone,
    ExternalLink,
    Lock,
    Sparkles,
} from "lucide-react";

export interface VneidAuthModalProps {
    isOpen: boolean;
    onClose: () => void;
    onConfirm: (profile: VneidCitizenProfile) => void;
    defaultCccd?: string;
    title?: string;
    subtitle?: string;
}

export function VneidAuthModal(props: VneidAuthModalProps) {
    if (!props.isOpen) return null;
    return <VneidAuthModalDialog {...props} />;
}

function VneidAuthModalDialog({
    onClose,
    onConfirm,
    defaultCccd = "079201234567",
    title = "Xác thực Căn cước công dân qua VNeID",
    subtitle = "Hệ thống Smart Parking ParkWise yêu cầu chia sẻ thông tin định danh công dân mức độ 2",
}: VneidAuthModalProps) {
    const [activeTab, setActiveTab] = useState<"qr" | "preset">("qr");
    const [selectedProfile, setSelectedProfile] = useState<VneidCitizenProfile>(
        () => findProfileByCccd(defaultCccd) || VNEID_SEED_PROFILES[0],
    );
    const [searchCccd, setSearchCccd] = useState("");
    const [simulatedError, setSimulatedError] = useState<string | null>(null);

    // Dynamic QR & Session state - initialized lazily on mount
    const [session, setSession] = useState<VneidAuthSession>(() =>
        createVneidAuthSession(60),
    );
    const [countdown, setCountdown] = useState<number>(60);
    const [isExpired, setIsExpired] = useState<boolean>(false);

    // Biometric scanning simulation state
    const [isScanningBiometric, setIsScanningBiometric] =
        useState<boolean>(false);
    const [autoSyncApprovedProfile, setAutoSyncApprovedProfile] =
        useState<VneidCitizenProfile | null>(null);

    const timerRef = useRef<NodeJS.Timeout | null>(null);

    // Function to generate a new session and reset countdown
    const refreshSession = useCallback(() => {
        const newSession = createVneidAuthSession(60);
        setSession(newSession);
        setCountdown(60);
        setIsExpired(false);
        setSimulatedError(null);
    }, []);

    // Play subtle success chime using Web Audio API
    const playSuccessChime = () => {
        try {
            const AudioContextClass =
                window.AudioContext ||
                (
                    window as unknown as {
                        webkitAudioContext: typeof window.AudioContext;
                    }
                ).webkitAudioContext;
            if (!AudioContextClass) return;
            const ctx = new AudioContextClass();
            const osc = ctx.createOscillator();
            const gain = ctx.createGain();
            osc.type = "sine";
            osc.frequency.setValueAtTime(587.33, ctx.currentTime); // D5
            osc.frequency.exponentialRampToValueAtTime(
                880,
                ctx.currentTime + 0.15,
            ); // A5
            gain.gain.setValueAtTime(0.12, ctx.currentTime);
            gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.4);
            osc.connect(gain);
            gain.connect(ctx.destination);
            osc.start();
            osc.stop(ctx.currentTime + 0.45);
        } catch {
            // Audio context not allowed or unsupported
        }
    };

    // Manage countdown timer
    useEffect(() => {
        if (isExpired) return;

        timerRef.current = setInterval(() => {
            setCountdown((prev) => {
                if (prev <= 1) {
                    setIsExpired(true);
                    return 0;
                }
                return prev - 1;
            });
        }, 1000);

        return () => {
            if (timerRef.current) clearInterval(timerRef.current);
        };
    }, [isExpired]);

    // Subscribe to cross-tab broadcast approvals
    useEffect(() => {
        const unsubscribe = subscribeToVneidSession(session.sessionId, {
            onApproved: (approvedProfile) => {
                setAutoSyncApprovedProfile(approvedProfile);
                playSuccessChime();
                setTimeout(() => {
                    onConfirm(approvedProfile);
                    onClose();
                }, 1200);
            },
            onRejected: (reason) => {
                setSimulatedError(reason);
            },
        });

        return () => {
            unsubscribe();
        };
    }, [session.sessionId, onConfirm, onClose]);

    const handleSelectPreset = (profile: VneidCitizenProfile) => {
        setSelectedProfile(profile);
        setSimulatedError(null);
    };

    const handleSearch = (e: React.FormEvent) => {
        e.preventDefault();
        if (!searchCccd.trim()) return;
        const found = findProfileByCccd(searchCccd.trim());
        if (found) {
            setSelectedProfile(found);
            setSimulatedError(null);
        } else {
            setSimulatedError(
                `Số CCCD "${searchCccd}" không tồn tại trong cơ sở dữ liệu quốc gia giả lập.`,
            );
        }
    };

    const handleConfirm = () => {
        if (simulatedError) return;
        onConfirm(selectedProfile);
        onClose();
    };

    const handleTriggerBiometricScan = () => {
        if (simulatedError) return;
        setIsScanningBiometric(true);
        playSuccessChime();
        setTimeout(() => {
            setIsScanningBiometric(false);
            onConfirm(selectedProfile);
            onClose();
        }, 1300);
    };

    const mobileSimulatorUrl = session
        ? `/simulator/vneid?sessionId=${session.sessionId}`
        : "/simulator/vneid";

    return (
        <div
            data-testid="vneid-auth-modal"
            className="fixed inset-0 z-50 flex items-center justify-center p-3 sm:p-4 bg-black/60 backdrop-blur-xs animate-in fade-in duration-200"
        >
            <div className="relative w-full max-w-xl bg-white rounded-2xl shadow-2xl overflow-hidden border border-slate-200 flex flex-col max-h-[92vh]">
                {/* VNeID Official Red Header */}
                <div className="bg-gradient-to-r from-red-800 via-red-700 to-amber-700 px-5 py-3.5 text-white flex items-center justify-between shrink-0 shadow-md">
                    <div className="flex items-center gap-3">
                        <div className="w-9 h-9 rounded-full bg-red-900 border-2 border-amber-300 flex items-center justify-center shadow-inner shrink-0">
                            <svg
                                viewBox="0 0 100 100"
                                className="w-6 h-6"
                                fill="none"
                            >
                                <circle cx="50" cy="50" r="46" fill="#DC2626" />
                                <circle
                                    cx="50"
                                    cy="50"
                                    r="44"
                                    stroke="#FDE047"
                                    strokeWidth="3"
                                />
                                <polygon
                                    points="50,15 58,38 82,38 62,53 70,76 50,61 30,76 38,53 18,38 42,38"
                                    fill="#FDE047"
                                />
                            </svg>
                        </div>
                        <div>
                            <div className="flex items-center gap-1.5">
                                <span className="text-[11px] font-bold tracking-wider text-amber-300 uppercase">
                                    BỘ CÔNG AN
                                </span>
                                <span className="text-amber-200 text-xs">
                                    •
                                </span>
                                <span className="text-[11px] font-semibold text-amber-100">
                                    CỤC CẢNH SÁT QLHC VỀ TTXH
                                </span>
                            </div>
                            <h2 className="text-sm sm:text-base font-extrabold tracking-tight text-white flex items-center gap-1.5">
                                Định danh điện tử Quốc gia (VNeID)
                                <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-bold bg-amber-400 text-red-950">
                                    MỨC 2
                                </span>
                            </h2>
                        </div>
                    </div>
                    <button
                        type="button"
                        onClick={onClose}
                        className="p-1 rounded-lg text-white/80 hover:text-white hover:bg-white/10 transition"
                        aria-label="Đóng"
                    >
                        <X className="w-5 h-5" />
                    </button>
                </div>

                {/* Navigation Tabs: Realistic QR SSO vs Examiner Quick Preset */}
                <div className="bg-slate-100 px-5 pt-2 border-b border-slate-200 flex items-center gap-2 text-xs font-bold text-slate-600 shrink-0">
                    <button
                        type="button"
                        onClick={() => {
                            setActiveTab("qr");
                            setSimulatedError(null);
                        }}
                        className={`pb-2.5 px-3 border-b-2 flex items-center gap-1.5 transition ${
                            activeTab === "qr"
                                ? "border-red-600 text-red-700 font-extrabold"
                                : "border-transparent text-slate-500 hover:text-slate-800"
                        }`}
                    >
                        <QrCode className="w-4 h-4" />
                        Quét mã QR bằng VNeID (Khuyên dùng)
                    </button>
                    <button
                        type="button"
                        onClick={() => {
                            setActiveTab("preset");
                            setSimulatedError(null);
                        }}
                        className={`pb-2.5 px-3 border-b-2 flex items-center gap-1.5 transition ${
                            activeTab === "preset"
                                ? "border-red-600 text-red-700 font-extrabold"
                                : "border-transparent text-slate-500 hover:text-slate-800"
                        }`}
                    >
                        <ScanFace className="w-4 h-4" />
                        Chế độ Giám khảo / Test nhanh
                    </button>
                </div>

                {/* Modal Scrollable Body */}
                <div className="p-4 sm:p-5 overflow-y-auto space-y-4">
                    {/* Service request banner */}
                    <div className="bg-amber-50 border border-amber-200/80 rounded-xl p-3 flex items-start gap-2.5">
                        <ShieldCheck className="w-5 h-5 text-amber-600 shrink-0 mt-0.5" />
                        <div className="text-xs text-amber-900 leading-relaxed">
                            <p className="font-bold text-amber-950">{title}</p>
                            <p className="text-amber-800 mt-0.5">{subtitle}</p>
                        </div>
                    </div>

                    {/* Auto Sync Success Banner (Cross-tab) */}
                    {autoSyncApprovedProfile && (
                        <div className="bg-emerald-50 border-2 border-emerald-500 rounded-xl p-4 flex items-center gap-3 text-emerald-900 animate-in zoom-in-95 duration-200">
                            <div className="w-9 h-9 rounded-full bg-emerald-100 flex items-center justify-center shrink-0 text-emerald-600">
                                <CheckCircle2 className="w-6 h-6" />
                            </div>
                            <div className="text-xs">
                                <p className="font-extrabold text-sm text-emerald-950">
                                    Đã xác thực thành công từ Ứng dụng VNeID!
                                </p>
                                <p className="text-emerald-800 mt-0.5">
                                    Công dân:{" "}
                                    <strong>
                                        {autoSyncApprovedProfile.fullName}
                                    </strong>{" "}
                                    — CCCD:{" "}
                                    <span className="font-mono font-bold">
                                        {autoSyncApprovedProfile.cccd}
                                    </span>
                                </p>
                            </div>
                        </div>
                    )}

                    {/* Biometric Scanning Radar Animation */}
                    {isScanningBiometric && (
                        <div className="bg-blue-50 border border-blue-200 rounded-2xl p-6 flex flex-col items-center justify-center text-center space-y-3 animate-in fade-in">
                            <div className="relative w-16 h-16 flex items-center justify-center">
                                <div className="absolute inset-0 rounded-full border-4 border-blue-400 border-t-transparent animate-spin" />
                                <Fingerprint className="w-8 h-8 text-blue-600 animate-pulse" />
                            </div>
                            <div>
                                <h4 className="text-sm font-bold text-blue-950">
                                    Đang xác thực Sinh trắc học & Đối soát CSDL
                                    Quốc gia...
                                </h4>
                                <p className="text-xs text-blue-700 mt-0.5">
                                    Vui lòng đợi giây lát trong khi VNeID chứng
                                    thực dữ liệu.
                                </p>
                            </div>
                        </div>
                    )}

                    {/* Simulated Error Alert */}
                    {simulatedError && (
                        <div className="bg-red-50 border border-red-200 rounded-xl p-3 flex items-center gap-2.5 text-xs text-red-700 animate-in fade-in">
                            <AlertTriangle className="w-4 h-4 text-red-600 shrink-0" />
                            <span>{simulatedError}</span>
                        </div>
                    )}

                    {/* TAB 1: REALISTIC QR CODE SSO FLOW */}
                    {activeTab === "qr" &&
                        !autoSyncApprovedProfile &&
                        !isScanningBiometric && (
                            <div className="flex flex-col items-center space-y-4 pt-1 pb-2">
                                {/* QR Code Container */}
                                <div className="relative p-4 bg-white rounded-2xl border-2 border-slate-200 shadow-md flex flex-col items-center">
                                    {isExpired ? (
                                        <div className="w-52 h-52 flex flex-col items-center justify-center bg-slate-50 rounded-xl border border-dashed border-slate-300 text-center p-4">
                                            <Clock className="w-10 h-10 text-slate-400 mb-2" />
                                            <p className="text-xs font-bold text-slate-700">
                                                Mã QR đã hết hiệu lực
                                            </p>
                                            <p className="text-[11px] text-slate-500 mt-0.5">
                                                Vui lòng làm mới để lấy mã phiên
                                                mới
                                            </p>
                                            <button
                                                type="button"
                                                onClick={refreshSession}
                                                className="mt-3 px-3 py-1.5 bg-red-700 text-white text-xs font-bold rounded-lg hover:bg-red-800 transition flex items-center gap-1.5"
                                            >
                                                <RefreshCw className="w-3.5 h-3.5" />
                                                Làm mới mã
                                            </button>
                                        </div>
                                    ) : (
                                        <>
                                            <div className="p-2.5 bg-white rounded-xl shadow-inner">
                                                {session && (
                                                    <QRCode
                                                        value={
                                                            session.qrPayload
                                                        }
                                                        size={190}
                                                        level="M"
                                                    />
                                                )}
                                            </div>

                                            {/* Countdown progress indicator */}
                                            <div className="mt-3 flex items-center gap-1.5 text-xs text-slate-600 font-medium">
                                                <Clock className="w-3.5 h-3.5 text-amber-600 animate-pulse" />
                                                <span>
                                                    Mã tự động làm mới sau
                                                </span>
                                                <span className="font-mono font-bold text-red-700 bg-red-50 px-1.5 py-0.5 rounded border border-red-200">
                                                    {countdown}s
                                                </span>
                                                <button
                                                    type="button"
                                                    onClick={refreshSession}
                                                    className="ml-2 text-slate-400 hover:text-slate-700 p-1"
                                                    title="Làm mới ngay"
                                                >
                                                    <RefreshCw className="w-3.5 h-3.5" />
                                                </button>
                                            </div>
                                        </>
                                    )}
                                </div>

                                {/* 3-Step Scan Instructions */}
                                <div className="w-full bg-slate-50 rounded-xl p-3 border border-slate-200/80 text-xs text-slate-700 space-y-2">
                                    <p className="font-bold text-slate-900 flex items-center gap-1">
                                        <Smartphone className="w-4 h-4 text-red-600" />
                                        Hướng dẫn quét mã bằng ứng dụng VNeID:
                                    </p>
                                    <ol className="list-decimal list-inside space-y-1 text-slate-600 leading-relaxed text-[11.5px]">
                                        <li>
                                            Mở ứng dụng <strong>VNeID</strong>{" "}
                                            trên điện thoại thông minh.
                                        </li>
                                        <li>
                                            Bấm vào biểu tượng{" "}
                                            <strong>Quét mã QR</strong> ở góc
                                            trên bên phải màn hình chính.
                                        </li>
                                        <li>
                                            Hướng camera quét mã QR ở trên và
                                            xác nhận chia sẻ định danh Mức 2.
                                        </li>
                                    </ol>
                                </div>

                                {/* Direct Simulator Link Button (Cross-tab Bridge) */}
                                <div className="w-full bg-gradient-to-r from-red-50 to-amber-50 rounded-xl p-3 border border-red-200/60 flex items-center justify-between gap-3">
                                    <div className="text-xs">
                                        <p className="font-bold text-red-950 flex items-center gap-1">
                                            <Sparkles className="w-3.5 h-3.5 text-amber-600" />
                                            Mô phỏng điện thoại công dân:
                                        </p>
                                        <p className="text-[11px] text-red-800 mt-0.5">
                                            Mở App VNeID trên tab mới để duyệt
                                            phiên xác thực này ngay lập tức.
                                        </p>
                                    </div>
                                    <a
                                        href={mobileSimulatorUrl}
                                        target="_blank"
                                        rel="noreferrer"
                                        className="px-3 py-2 bg-red-700 hover:bg-red-800 text-white text-xs font-bold rounded-lg shadow-sm transition flex items-center gap-1.5 shrink-0"
                                    >
                                        <ExternalLink className="w-3.5 h-3.5" />
                                        Mở App VNeID
                                    </a>
                                </div>
                            </div>
                        )}

                    {/* TAB 2: EXAMINER / FAST PRESET MODE */}
                    {activeTab === "preset" &&
                        !autoSyncApprovedProfile &&
                        !isScanningBiometric && (
                            <div className="space-y-3.5">
                                <div className="flex items-center justify-between">
                                    <span className="text-xs font-bold text-slate-700 uppercase tracking-wider flex items-center gap-1">
                                        <ScanFace className="w-3.5 h-3.5 text-blue-600" />
                                        Chọn hồ sơ công dân mẫu (Master Sheet
                                        Demo):
                                    </span>
                                </div>

                                {/* Seed Profiles Grid */}
                                <div className="grid grid-cols-2 sm:grid-cols-4 gap-1.5">
                                    {VNEID_SEED_PROFILES.slice(0, 4).map(
                                        (p) => {
                                            const isSelected =
                                                selectedProfile.cccd === p.cccd;
                                            return (
                                                <button
                                                    key={p.cccd}
                                                    type="button"
                                                    onClick={() =>
                                                        handleSelectPreset(p)
                                                    }
                                                    className={`text-left p-2.5 rounded-xl border text-xs transition relative ${
                                                        isSelected
                                                            ? "border-blue-600 bg-blue-50/70 text-blue-900 font-semibold ring-1 ring-blue-500"
                                                            : "border-slate-200 bg-white text-slate-700 hover:border-slate-300 hover:bg-slate-50"
                                                    }`}
                                                >
                                                    <p className="font-bold text-[11.5px] truncate text-slate-900">
                                                        {p.fullName}
                                                    </p>
                                                    <p className="font-mono text-[10.5px] text-slate-500">
                                                        {p.cccd}
                                                    </p>
                                                    <p className="text-[10px] text-blue-700 truncate mt-1 font-medium bg-blue-100/60 px-1.5 py-0.5 rounded w-fit">
                                                        {p.label
                                                            .split("(")[0]
                                                            .trim()}
                                                    </p>
                                                </button>
                                            );
                                        },
                                    )}
                                </div>

                                {/* Search by CCCD */}
                                <form
                                    onSubmit={handleSearch}
                                    className="flex gap-2"
                                >
                                    <div className="relative flex-1">
                                        <input
                                            type="text"
                                            value={searchCccd}
                                            onChange={(e) =>
                                                setSearchCccd(e.target.value)
                                            }
                                            placeholder="Hoặc nhập 12 số CCCD để tra cứu (vd: 000000000002)..."
                                            className="w-full text-xs pl-8 pr-3 py-2 rounded-lg border border-slate-300 focus:outline-hidden focus:ring-2 focus:ring-red-500 font-mono"
                                        />
                                        <Search className="w-3.5 h-3.5 text-slate-400 absolute left-2.5 top-2.5" />
                                    </div>
                                    <button
                                        type="submit"
                                        className="px-3 py-2 bg-slate-800 text-white text-xs font-semibold rounded-lg hover:bg-slate-900 transition shrink-0"
                                    >
                                        Tra cứu
                                    </button>
                                </form>

                                {/* Selected Profile Verified Scope Summary */}
                                <div className="bg-slate-50 rounded-xl p-3 border border-slate-200/80 text-xs text-slate-700 space-y-2">
                                    <div className="flex items-center justify-between border-b pb-1.5">
                                        <span className="font-bold text-slate-900">
                                            Dữ liệu Căn cước Mức độ 2 sẽ cấp
                                            quyền:
                                        </span>
                                        <span className="text-[10px] font-bold text-emerald-700 bg-emerald-50 px-2 py-0.5 rounded border border-emerald-200 flex items-center gap-1">
                                            <Lock className="w-3 h-3" /> Đã kiểm
                                            định
                                        </span>
                                    </div>
                                    <div className="grid grid-cols-2 gap-x-4 gap-y-1 text-[11.5px]">
                                        <div>
                                            <span className="text-slate-500">
                                                Số CCCD:
                                            </span>{" "}
                                            <strong className="font-mono text-slate-900">
                                                {selectedProfile.cccd}
                                            </strong>
                                        </div>
                                        <div>
                                            <span className="text-slate-500">
                                                Họ và tên:
                                            </span>{" "}
                                            <strong className="text-slate-900 uppercase">
                                                {selectedProfile.fullName}
                                            </strong>
                                        </div>
                                        <div>
                                            <span className="text-slate-500">
                                                Ngày sinh:
                                            </span>{" "}
                                            <strong>
                                                {selectedProfile.dobDisplay}
                                            </strong>
                                        </div>
                                        <div>
                                            <span className="text-slate-500">
                                                Giới tính:
                                            </span>{" "}
                                            <strong>
                                                {selectedProfile.genderDisplay}
                                            </strong>
                                        </div>
                                        <div className="col-span-2">
                                            <span className="text-slate-500">
                                                Nơi thường trú:
                                            </span>{" "}
                                            <strong>
                                                {selectedProfile.residence}
                                            </strong>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        )}

                    {/* Data sharing disclosure */}
                    <div className="bg-slate-50 rounded-xl p-2.5 border border-slate-200/80 text-[10.5px] text-slate-500 leading-tight">
                        <p>
                            Quy trình định danh và chia sẻ thông tin điện tử
                            tuân thủ quy định tại Nghị định số 59/2022/NĐ-CP của
                            Chính phủ.
                        </p>
                    </div>
                </div>

                {/* Modal Footer Controls */}
                <div className="bg-slate-100 px-5 py-3 border-t border-slate-200 flex flex-wrap items-center justify-between gap-2 shrink-0">
                    <div className="flex items-center gap-2">
                        <button
                            type="button"
                            onClick={() =>
                                setSimulatedError(
                                    "Mô phỏng lỗi: Cổng VNeID phản hồi CCCD không trùng khớp cơ sở dữ liệu quốc gia.",
                                )
                            }
                            className="text-[11px] text-amber-700 hover:text-amber-800 font-medium underline"
                        >
                            Giả lập lỗi từ chối
                        </button>
                    </div>

                    <div className="flex items-center gap-2 ml-auto">
                        <button
                            type="button"
                            onClick={onClose}
                            className="px-4 py-2 rounded-lg text-xs font-semibold text-slate-700 hover:bg-slate-200 transition"
                        >
                            Hủy bỏ
                        </button>

                        {/* If in Examiner mode, allow Biometric Scan or instant confirm */}
                        {activeTab === "preset" ? (
                            <button
                                type="button"
                                onClick={handleTriggerBiometricScan}
                                className="px-5 py-2 rounded-lg text-xs font-bold text-white bg-gradient-to-r from-blue-700 to-indigo-700 hover:from-blue-800 hover:to-indigo-800 shadow-sm transition flex items-center gap-1.5"
                            >
                                <Fingerprint className="w-4 h-4" />
                                Quét Sinh trắc học & Xác thực
                            </button>
                        ) : null}

                        {/* Always keep the confirm button visible for compatibility */}
                        <button
                            type="button"
                            onClick={handleConfirm}
                            className="px-5 py-2 rounded-lg text-xs font-bold text-white bg-gradient-to-r from-red-700 to-amber-700 hover:from-red-800 hover:to-amber-800 shadow-sm transition flex items-center gap-1.5"
                        >
                            <CheckCircle2 className="w-4 h-4" />
                            Xác nhận chia sẻ thông tin
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
}
