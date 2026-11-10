"use client";

import React, { useState } from "react";
import Link from "next/link";
import {
    VneidCitizenProfile,
    VNEID_SEED_PROFILES,
} from "@/features/simulator/vneid/vneidSeedData";
import { VneidCardSimulator } from "@/features/simulator/vneid/VneidCardSimulator";
import {
    ShieldCheck,
    Smartphone,
    Copy,
    Check,
    ExternalLink,
    QrCode,
    FileText,
    Lock,
    Home,
    Wallet,
    Bell,
    Settings,
    Wifi,
    BatteryMedium,
    Search,
    Scan,
    Fingerprint,
    CheckCircle2,
} from "lucide-react";
import {
    getActiveVneidSession,
    approveVneidSession,
    rejectVneidSession,
    VneidAuthSession,
} from "@/features/simulator/vneid/vneidSync";

export function VneidMobileSimulator() {
    const [selectedProfile, setSelectedProfile] = useState<VneidCitizenProfile>(
        VNEID_SEED_PROFILES[0],
    );
    const [copiedField, setCopiedField] = useState<string | null>(null);
    const [searchTerm, setSearchTerm] = useState("");
    const [activeTab, setActiveTab] = useState<"card" | "qr" | "history">(
        "card",
    );

    // Cross-tab Synchronization State
    const [activeSession, setActiveSession] = useState<VneidAuthSession | null>(
        null,
    );
    const [approvalToast, setApprovalToast] = useState<string | null>(null);
    const [isProcessingApproval, setIsProcessingApproval] =
        useState<boolean>(false);
    const [historyItems, setHistoryItems] = useState<
        Array<{
            id: string;
            service: string;
            purpose: string;
            time: string;
            status: string;
        }>
    >([
        {
            id: "hist_init",
            service: "Hệ thống ParkWise",
            purpose: "Đăng ký hồ sơ Chủ bãi / Khách hàng",
            time: "Hôm nay lúc 09:30",
            status: "Đã chia sẻ",
        },
    ]);

    // Check for active session in query string or localStorage
    React.useEffect(() => {
        if (typeof window === "undefined") return;

        const checkSession = () => {
            const urlParams = new URLSearchParams(window.location.search);
            const querySessionId = urlParams.get("sessionId");

            const current = getActiveVneidSession();
            if (querySessionId) {
                if (current && current.sessionId === querySessionId) {
                    setActiveSession(current);
                } else {
                    setActiveSession({
                        sessionId: querySessionId,
                        createdAt: Date.now(),
                        expiresAt: Date.now() + 60000,
                        qrPayload: `vneid://oauth2/authorize?session_id=${querySessionId}`,
                    });
                }
            } else if (current) {
                setActiveSession(current);
            } else {
                setActiveSession(null);
            }
        };

        checkSession();
        const interval = setInterval(checkSession, 2000);
        return () => clearInterval(interval);
    }, []);

    const handleApproveActiveSession = () => {
        if (!activeSession) return;
        setIsProcessingApproval(true);

        setTimeout(() => {
            approveVneidSession(activeSession.sessionId, selectedProfile);
            setIsProcessingApproval(false);
            setActiveSession(null);
            setApprovalToast(
                `✓ Đã phê duyệt chia sẻ thông tin định danh cho ParkWise!`,
            );

            // Prepend to history
            const now = new Date();
            const timeStr = `Hôm nay lúc ${now.getHours().toString().padStart(2, "0")}:${now.getMinutes().toString().padStart(2, "0")}`;
            setHistoryItems((prev) => [
                {
                    id: `hist_${Date.now()}`,
                    service: "Hệ thống ParkWise",
                    purpose: "Xác thực danh tính Căn cước công dân Mức 2",
                    time: timeStr,
                    status: "Đã chia sẻ",
                },
                ...prev,
            ]);

            setTimeout(() => setApprovalToast(null), 4000);
        }, 800);
    };

    const handleRejectActiveSession = () => {
        if (!activeSession) return;
        rejectVneidSession(
            activeSession.sessionId,
            "Người dùng đã từ chối chia sẻ thông tin.",
        );
        setActiveSession(null);
        setApprovalToast("Đã từ chối yêu cầu kết nối từ ParkWise.");
        setTimeout(() => setApprovalToast(null), 3000);
    };

    const handleCopy = (text: string, fieldName: string) => {
        navigator.clipboard.writeText(text);
        setCopiedField(fieldName);
        setTimeout(() => setCopiedField(null), 2000);
    };

    const filteredProfiles = VNEID_SEED_PROFILES.filter(
        (p) =>
            p.fullName.toLowerCase().includes(searchTerm.toLowerCase()) ||
            p.cccd.includes(searchTerm) ||
            p.label.toLowerCase().includes(searchTerm.toLowerCase()),
    );

    return (
        <div className="min-h-screen bg-slate-900 text-slate-100 flex flex-col font-sans">
            {/* Top Demo Navigation Bar */}
            <header className="bg-slate-950 border-b border-slate-800 px-4 py-3 flex flex-wrap items-center justify-between gap-3 shrink-0">
                <div className="flex items-center gap-3">
                    <div className="w-8 h-8 rounded-lg bg-red-700 flex items-center justify-center font-black text-amber-300 text-sm border border-amber-400">
                        VN
                    </div>
                    <div>
                        <h1 className="text-sm font-bold text-white flex items-center gap-2">
                            VNeID Mobile Simulator
                            <span className="text-[10px] bg-red-950 text-red-300 border border-red-800 px-1.5 py-0.5 rounded font-mono">
                                BỘ CÔNG AN DEMO
                            </span>
                        </h1>
                        <p className="text-xs text-slate-400">
                            Giả lập ứng dụng Định danh điện tử Quốc gia phục vụ
                            kiểm thử hệ thống ParkWise
                        </p>
                    </div>
                </div>

                <div className="flex items-center gap-2">
                    <Link
                        href="/login"
                        className="px-3 py-1.5 text-xs font-medium text-slate-300 bg-slate-800 hover:bg-slate-700 rounded-lg transition flex items-center gap-1.5"
                    >
                        Đến Cổng ParkWise{" "}
                        <ExternalLink className="w-3.5 h-3.5" />
                    </Link>
                </div>
            </header>

            {/* Main Workspace Layout: Side Controls + Mobile Phone Simulator */}
            <main className="flex-1 flex flex-col lg:flex-row overflow-hidden">
                {/* Left Side: Master Sheet Seed Selector & Info */}
                <section className="w-full lg:w-96 bg-slate-950/80 border-r border-slate-800 p-4 sm:p-5 flex flex-col gap-4 overflow-y-auto shrink-0">
                    <div>
                        <div className="flex items-center justify-between mb-1.5">
                            <h2 className="text-xs font-bold text-amber-400 uppercase tracking-wider flex items-center gap-1.5">
                                <Smartphone className="w-4 h-4 text-amber-400" />
                                Hồ sơ định danh mẫu (Master Sheet)
                            </h2>
                            <span className="text-[11px] text-slate-400 font-mono">
                                {VNEID_SEED_PROFILES.length} bản ghi
                            </span>
                        </div>
                        <p className="text-xs text-slate-400 leading-relaxed">
                            Chọn hồ sơ bên dưới để nạp tức thì dữ liệu căn cước
                            công dân vào điện thoại giả lập.
                        </p>
                    </div>

                    {/* Search Bar */}
                    <div className="relative">
                        <input
                            type="text"
                            value={searchTerm}
                            onChange={(e) => setSearchTerm(e.target.value)}
                            placeholder="Tìm theo tên, CCCD, vai trò..."
                            className="w-full bg-slate-900 border border-slate-800 rounded-lg text-xs px-3 py-2 pl-8 text-white placeholder-slate-500 focus:outline-hidden focus:border-red-600 font-sans"
                        />
                        <Search className="w-3.5 h-3.5 text-slate-500 absolute left-2.5 top-2.5" />
                    </div>

                    {/* Preset List */}
                    <div className="space-y-2 flex-1">
                        {filteredProfiles.map((p) => {
                            const isSelected = selectedProfile.cccd === p.cccd;
                            return (
                                <button
                                    key={p.cccd}
                                    type="button"
                                    onClick={() => setSelectedProfile(p)}
                                    className={`w-full text-left p-3 rounded-xl border transition-all ${
                                        isSelected
                                            ? "bg-red-950/40 border-red-600 text-white shadow-md ring-1 ring-red-500/50"
                                            : "bg-slate-900/60 border-slate-800/80 text-slate-300 hover:bg-slate-850 hover:border-slate-700"
                                    }`}
                                >
                                    <div className="flex items-center justify-between">
                                        <span className="font-bold text-xs text-amber-300 truncate">
                                            {p.label}
                                        </span>
                                        <span className="text-[10px] font-mono px-1.5 py-0.5 rounded bg-slate-800 text-slate-300">
                                            {p.genderDisplay}
                                        </span>
                                    </div>

                                    <div className="mt-1 flex items-baseline justify-between font-mono text-xs">
                                        <span className="text-slate-100 font-bold">
                                            {p.cccd}
                                        </span>
                                        <span className="text-slate-400 text-[11px]">
                                            {p.dobDisplay}
                                        </span>
                                    </div>

                                    <p className="text-[11px] text-slate-400 mt-1 line-clamp-1">
                                        {p.description}
                                    </p>
                                </button>
                            );
                        })}
                    </div>

                    {/* Quick Copy Data Helper */}
                    <div className="bg-slate-900 border border-slate-800 rounded-xl p-3 text-xs space-y-2">
                        <p className="font-semibold text-slate-300 flex items-center justify-between">
                            <span>Sao chép nhanh cho Test & Demo:</span>
                        </p>
                        <div className="space-y-1.5 font-mono text-[11px]">
                            <div className="flex items-center justify-between bg-slate-950 p-1.5 rounded border border-slate-800/80">
                                <span className="text-slate-400">CCCD:</span>
                                <span className="font-bold text-amber-300">
                                    {selectedProfile.cccd}
                                </span>
                                <button
                                    type="button"
                                    onClick={() =>
                                        handleCopy(selectedProfile.cccd, "cccd")
                                    }
                                    className="text-slate-400 hover:text-white p-1"
                                    title="Sao chép số CCCD"
                                >
                                    {copiedField === "cccd" ? (
                                        <Check className="w-3.5 h-3.5 text-emerald-400" />
                                    ) : (
                                        <Copy className="w-3.5 h-3.5" />
                                    )}
                                </button>
                            </div>

                            <div className="flex items-center justify-between bg-slate-950 p-1.5 rounded border border-slate-800/80">
                                <span className="text-slate-400">Họ tên:</span>
                                <span className="font-bold text-white truncate max-w-[160px]">
                                    {selectedProfile.fullName}
                                </span>
                                <button
                                    type="button"
                                    onClick={() =>
                                        handleCopy(
                                            selectedProfile.fullName,
                                            "name",
                                        )
                                    }
                                    className="text-slate-400 hover:text-white p-1"
                                    title="Sao chép Họ và tên"
                                >
                                    {copiedField === "name" ? (
                                        <Check className="w-3.5 h-3.5 text-emerald-400" />
                                    ) : (
                                        <Copy className="w-3.5 h-3.5" />
                                    )}
                                </button>
                            </div>

                            {selectedProfile.demoAccount && (
                                <div className="flex items-center justify-between bg-slate-950 p-1.5 rounded border border-slate-800/80">
                                    <span className="text-slate-400">
                                        Email:
                                    </span>
                                    <span className="text-blue-300 truncate max-w-[160px]">
                                        {selectedProfile.demoAccount}
                                    </span>
                                    <button
                                        type="button"
                                        onClick={() =>
                                            handleCopy(
                                                selectedProfile.demoAccount ||
                                                    "",
                                                "email",
                                            )
                                        }
                                        className="text-slate-400 hover:text-white p-1"
                                        title="Sao chép Email demo"
                                    >
                                        {copiedField === "email" ? (
                                            <Check className="w-3.5 h-3.5 text-emerald-400" />
                                        ) : (
                                            <Copy className="w-3.5 h-3.5" />
                                        )}
                                    </button>
                                </div>
                            )}
                        </div>
                    </div>
                </section>

                {/* Right Side: Realistic VNeID Smartphone Mockup */}
                <section className="flex-1 p-4 sm:p-6 lg:p-8 flex items-center justify-center overflow-y-auto">
                    {/* Smartphone Frame (390 x 844 aspect, standard modern mobile) */}
                    <div className="w-full max-w-[420px] bg-black rounded-[44px] p-3 shadow-2xl border-4 border-slate-700/80 ring-1 ring-slate-800 flex flex-col min-h-[780px] relative overflow-hidden select-none">
                        {/* Phone Screen Container */}
                        <div className="flex-1 bg-slate-100 rounded-[36px] overflow-hidden flex flex-col text-slate-900 relative">
                            {/* Phone Status Bar */}
                            <div className="bg-[#8B1B1B] text-white px-6 pt-3 pb-1 flex items-center justify-between text-[11px] font-semibold tracking-tight">
                                <span>09:41</span>
                                <div className="w-20 h-4 bg-black rounded-full mx-auto" />
                                <div className="flex items-center gap-1.5">
                                    <Wifi className="w-3.5 h-3.5" />
                                    <span className="text-[10px]">5G</span>
                                    <BatteryMedium className="w-4 h-4" />
                                </div>
                            </div>

                            {/* VNeID Mobile App Header (Authentic Deep Red Tone) */}
                            <div className="bg-gradient-to-b from-[#8B1B1B] to-[#A31D1D] text-white px-5 pt-3 pb-5 shadow-md">
                                <div className="flex items-center justify-between">
                                    <div className="flex items-center gap-2.5">
                                        {/* Emblem */}
                                        <div className="w-9 h-9 rounded-full bg-red-900 border-2 border-amber-300 flex items-center justify-center shadow-inner">
                                            <svg
                                                viewBox="0 0 100 100"
                                                className="w-6 h-6"
                                                fill="none"
                                            >
                                                <circle
                                                    cx="50"
                                                    cy="50"
                                                    r="46"
                                                    fill="#DC2626"
                                                />
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
                                            <p className="text-[9.5px] font-bold text-amber-300 tracking-wider uppercase">
                                                BỘ CÔNG AN
                                            </p>
                                            <h3 className="text-xs font-black tracking-tight text-white uppercase">
                                                ĐỊNH DANH ĐIỆN TỬ (VNeID)
                                            </h3>
                                        </div>
                                    </div>

                                    <div className="flex items-center gap-2">
                                        <button
                                            type="button"
                                            onClick={() => {
                                                const current =
                                                    getActiveVneidSession();
                                                if (current) {
                                                    setActiveSession(current);
                                                } else {
                                                    setApprovalToast(
                                                        "Không có yêu cầu kết nối nào đang chờ.",
                                                    );
                                                    setTimeout(
                                                        () =>
                                                            setApprovalToast(
                                                                null,
                                                            ),
                                                        3000,
                                                    );
                                                }
                                            }}
                                            className="p-1.5 rounded-full bg-red-950/70 hover:bg-red-950 text-amber-300 relative transition flex items-center justify-center border border-amber-300/40"
                                            title="Quét mã QR đăng nhập Web"
                                        >
                                            <Scan className="w-4 h-4" />
                                            {activeSession && (
                                                <span className="absolute -top-0.5 -right-0.5 w-2.5 h-2.5 rounded-full bg-amber-400 animate-ping" />
                                            )}
                                        </button>

                                        <div className="bg-amber-400 text-red-950 font-black text-[9px] px-2 py-0.5 rounded-full flex items-center gap-1 shadow-xs">
                                            <ShieldCheck className="w-3 h-3" />
                                            MỨC 2
                                        </div>
                                    </div>
                                </div>

                                {/* Citizen Greeting Card */}
                                <div className="mt-3 bg-red-900/60 rounded-xl p-2.5 border border-red-700/60 flex items-center justify-between">
                                    <div>
                                        <p className="text-[10px] text-amber-200">
                                            Xin chào công dân,
                                        </p>
                                        <p className="text-xs font-black text-white tracking-wide uppercase">
                                            {selectedProfile.fullName}
                                        </p>
                                    </div>
                                    <span className="text-[9.5px] font-mono text-amber-200 bg-red-950/70 px-2 py-0.5 rounded">
                                        {selectedProfile.cccd
                                            .slice(-4)
                                            .padStart(12, "•")}
                                    </span>
                                </div>
                            </div>

                            {/* Toast Notification */}
                            {approvalToast && (
                                <div className="mx-4 mt-2 bg-emerald-50 border border-emerald-300 text-emerald-900 rounded-xl p-2.5 text-xs flex items-center gap-2 animate-in fade-in duration-200 shadow-sm">
                                    <CheckCircle2 className="w-4 h-4 text-emerald-600 shrink-0" />
                                    <span className="font-semibold text-[11px]">
                                        {approvalToast}
                                    </span>
                                </div>
                            )}

                            {/* Active In-App Consent Action Sheet for Web Login */}
                            {activeSession && (
                                <div className="mx-4 mt-2 bg-white rounded-2xl shadow-xl border-2 border-red-600 p-3.5 z-30 animate-in zoom-in-95 duration-200">
                                    <div className="flex items-center justify-between pb-1.5 border-b border-slate-100">
                                        <span className="text-[11px] font-black text-red-700 flex items-center gap-1.5 uppercase">
                                            <ShieldCheck className="w-4 h-4 text-red-600" />
                                            Cấp quyền chia sẻ định danh
                                        </span>
                                        <span className="text-[9.5px] font-mono bg-red-50 text-red-700 px-1.5 py-0.5 rounded font-bold border border-red-200">
                                            PARKWISE
                                        </span>
                                    </div>
                                    <p className="text-[11px] text-slate-700 mt-2 leading-relaxed">
                                        Hệ thống{" "}
                                        <strong>Smart Parking ParkWise</strong>{" "}
                                        yêu cầu chia sẻ thông tin Căn cước công
                                        dân Mức độ 2:
                                    </p>
                                    <div className="bg-slate-50 p-2 rounded-xl border border-slate-200/80 my-2 text-[10.5px] space-y-1 text-slate-700">
                                        <p className="flex justify-between">
                                            <span className="text-slate-500">
                                                Họ và tên:
                                            </span>
                                            <strong className="text-slate-900">
                                                {selectedProfile.fullName}
                                            </strong>
                                        </p>
                                        <p className="flex justify-between">
                                            <span className="text-slate-500">
                                                Số CCCD:
                                            </span>
                                            <strong className="font-mono text-slate-900">
                                                {selectedProfile.cccd}
                                            </strong>
                                        </p>
                                        <p className="flex justify-between">
                                            <span className="text-slate-500">
                                                Ngày sinh:
                                            </span>
                                            <strong>
                                                {selectedProfile.dobDisplay}
                                            </strong>
                                        </p>
                                    </div>
                                    <div className="flex gap-2 mt-2.5">
                                        <button
                                            type="button"
                                            onClick={handleRejectActiveSession}
                                            className="flex-1 py-1.5 bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-bold rounded-lg transition"
                                        >
                                            Từ chối
                                        </button>
                                        <button
                                            type="button"
                                            onClick={handleApproveActiveSession}
                                            disabled={isProcessingApproval}
                                            className="flex-1 py-1.5 bg-gradient-to-r from-red-700 to-amber-700 hover:from-red-800 hover:to-amber-800 text-white text-xs font-bold rounded-lg shadow-sm transition flex items-center justify-center gap-1.5"
                                        >
                                            <Fingerprint className="w-3.5 h-3.5" />
                                            {isProcessingApproval
                                                ? "Đang gửi..."
                                                : "Xác nhận (FaceID)"}
                                        </button>
                                    </div>
                                </div>
                            )}

                            {/* Navigation Segment Authentic to VNeID */}
                            <div className="bg-white px-4 py-2 border-b border-slate-200 flex items-center justify-around text-xs font-bold text-slate-600">
                                <button
                                    type="button"
                                    onClick={() => setActiveTab("card")}
                                    className={`pb-1.5 border-b-2 transition ${
                                        activeTab === "card"
                                            ? "border-red-600 text-red-700 font-extrabold"
                                            : "border-transparent text-slate-500 hover:text-slate-800"
                                    }`}
                                >
                                    Thẻ Căn cước
                                </button>
                                <button
                                    type="button"
                                    onClick={() => setActiveTab("qr")}
                                    className={`pb-1.5 border-b-2 transition ${
                                        activeTab === "qr"
                                            ? "border-red-600 text-red-700 font-extrabold"
                                            : "border-transparent text-slate-500 hover:text-slate-800"
                                    }`}
                                >
                                    Mã QR Định danh
                                </button>
                                <button
                                    type="button"
                                    onClick={() => setActiveTab("history")}
                                    className={`pb-1.5 border-b-2 transition ${
                                        activeTab === "history"
                                            ? "border-red-600 text-red-700 font-extrabold"
                                            : "border-transparent text-slate-500 hover:text-slate-800"
                                    }`}
                                >
                                    Lịch sử chia sẻ
                                </button>
                            </div>

                            {/* Scrollable Content inside Mobile App */}
                            <div className="flex-1 p-3 overflow-y-auto space-y-3 bg-slate-50">
                                {activeTab === "card" && (
                                    <div className="space-y-3">
                                        {/* The Authentic CCCD Card */}
                                        <div className="flex justify-center">
                                            <VneidCardSimulator
                                                profile={selectedProfile}
                                            />
                                        </div>

                                        {/* VNeID Mobile Native Actions */}
                                        <div className="grid grid-cols-3 gap-2 text-center">
                                            <button
                                                type="button"
                                                onClick={() =>
                                                    setActiveTab("qr")
                                                }
                                                className="bg-white p-2.5 rounded-xl border border-slate-200 shadow-xs flex flex-col items-center gap-1 hover:bg-slate-50 transition"
                                            >
                                                <div className="w-8 h-8 rounded-full bg-red-50 text-red-700 flex items-center justify-center">
                                                    <QrCode className="w-4 h-4" />
                                                </div>
                                                <span className="text-[10px] font-bold text-slate-700">
                                                    Xuất trình QR
                                                </span>
                                            </button>

                                            <button
                                                type="button"
                                                onClick={() =>
                                                    setActiveTab("history")
                                                }
                                                className="bg-white p-2.5 rounded-xl border border-slate-200 shadow-xs flex flex-col items-center gap-1 hover:bg-slate-50 transition"
                                            >
                                                <div className="w-8 h-8 rounded-full bg-amber-50 text-amber-700 flex items-center justify-center">
                                                    <FileText className="w-4 h-4" />
                                                </div>
                                                <span className="text-[10px] font-bold text-slate-700">
                                                    Thông tin chi tiết
                                                </span>
                                            </button>

                                            <button
                                                type="button"
                                                className="bg-white p-2.5 rounded-xl border border-slate-200 shadow-xs flex flex-col items-center gap-1 hover:bg-slate-50 transition"
                                            >
                                                <div className="w-8 h-8 rounded-full bg-blue-50 text-blue-700 flex items-center justify-center">
                                                    <Lock className="w-4 h-4" />
                                                </div>
                                                <span className="text-[10px] font-bold text-slate-700">
                                                    Khóa thẻ CCCD
                                                </span>
                                            </button>
                                        </div>

                                        {/* Security notice */}
                                        <div className="bg-amber-50 border border-amber-200/70 rounded-xl p-2.5 text-[10.5px] text-amber-900 leading-tight">
                                            <p className="font-bold flex items-center gap-1 text-amber-950">
                                                <ShieldCheck className="w-3.5 h-3.5 text-amber-700 shrink-0" />
                                                Định danh điện tử có giá trị
                                                tương đương CCCD gắn chip vật lý
                                            </p>
                                            <p className="mt-0.5 text-amber-800">
                                                Theo Nghị định số 59/2022/NĐ-CP
                                                của Chính phủ về định danh và
                                                xác thực điện tử.
                                            </p>
                                        </div>
                                    </div>
                                )}

                                {activeTab === "qr" && (
                                    <div className="bg-white p-4 rounded-2xl border border-slate-200 shadow-sm flex flex-col items-center text-center space-y-3">
                                        <p className="text-xs font-bold text-slate-800 uppercase">
                                            MÃ QR CÔNG DÂN (CCCD GẮN CHIP)
                                        </p>
                                        <div className="p-3 bg-white rounded-xl border-2 border-slate-300 shadow-inner">
                                            <div className="w-44 h-44 flex items-center justify-center">
                                                <VneidCardSimulator
                                                    profile={selectedProfile}
                                                    className="scale-90"
                                                />
                                            </div>
                                        </div>
                                        <p className="font-mono text-sm font-black text-red-700 tracking-wider">
                                            {selectedProfile.cccd}
                                        </p>
                                        <p className="text-[11px] text-slate-500 max-w-[260px]">
                                            Quét mã này tại quầy hoặc trên cổng
                                            ParkWise để xác thực tự động thông
                                            tin.
                                        </p>
                                    </div>
                                )}

                                {activeTab === "history" && (
                                    <div className="space-y-2">
                                        {historyItems.map((item) => (
                                            <div
                                                key={item.id}
                                                className="bg-white p-3 rounded-xl border border-slate-200 shadow-xs space-y-2"
                                            >
                                                <div className="flex items-center justify-between text-xs font-bold text-slate-800 border-b pb-1.5">
                                                    <span>{item.service}</span>
                                                    <span className="text-[10px] text-emerald-600 bg-emerald-50 px-1.5 py-0.5 rounded font-bold">
                                                        {item.status}
                                                    </span>
                                                </div>
                                                <p className="text-[11px] text-slate-600">
                                                    Đã cung cấp thông tin CCCD
                                                    gắn chip phục vụ:{" "}
                                                    <strong>
                                                        {item.purpose}
                                                    </strong>
                                                </p>
                                                <p className="text-[10px] text-slate-400 font-mono">
                                                    Thời gian: {item.time}
                                                </p>
                                            </div>
                                        ))}
                                    </div>
                                )}
                            </div>

                            {/* Bottom Mobile App Navigation Bar (Authentic VNeID) */}
                            <div className="bg-white border-t border-slate-200 px-4 py-2 flex items-center justify-around text-slate-400 shrink-0">
                                <button
                                    type="button"
                                    className="flex flex-col items-center gap-0.5 text-slate-500 hover:text-slate-800"
                                >
                                    <Home className="w-5 h-5" />
                                    <span className="text-[9px] font-semibold">
                                        Trang chủ
                                    </span>
                                </button>
                                <button
                                    type="button"
                                    className="flex flex-col items-center gap-0.5 text-red-700 font-bold"
                                >
                                    <Wallet className="w-5 h-5 text-red-700" />
                                    <span className="text-[9px]">
                                        Ví giấy tờ
                                    </span>
                                </button>
                                <button
                                    type="button"
                                    className="flex flex-col items-center gap-0.5 text-slate-500 hover:text-slate-800"
                                >
                                    <Bell className="w-5 h-5" />
                                    <span className="text-[9px] font-semibold">
                                        Thông báo
                                    </span>
                                </button>
                                <button
                                    type="button"
                                    className="flex flex-col items-center gap-0.5 text-slate-500 hover:text-slate-800"
                                >
                                    <Settings className="w-5 h-5" />
                                    <span className="text-[9px] font-semibold">
                                        Cài đặt
                                    </span>
                                </button>
                            </div>

                            {/* Phone Home Indicator Bar */}
                            <div className="bg-white pb-1 pt-0.5 flex justify-center">
                                <div className="w-32 h-1 bg-slate-400 rounded-full" />
                            </div>
                        </div>
                    </div>
                </section>
            </main>
        </div>
    );
}
