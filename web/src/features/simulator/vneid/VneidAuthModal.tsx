"use client";

import React, { useState } from "react";
import {
    VneidCitizenProfile,
    VNEID_SEED_PROFILES,
    findProfileByCccd,
} from "./vneidSeedData";
import { VneidCardSimulator } from "./VneidCardSimulator";
import {
    ShieldCheck,
    X,
    CheckCircle2,
    AlertTriangle,
    UserCheck,
    Search,
    ExternalLink,
} from "lucide-react";

export interface VneidAuthModalProps {
    isOpen: boolean;
    onClose: () => void;
    onConfirm: (profile: VneidCitizenProfile) => void;
    defaultCccd?: string;
    title?: string;
    subtitle?: string;
}

export function VneidAuthModal({
    isOpen,
    onClose,
    onConfirm,
    defaultCccd = "079201234567",
    title = "Xác thực Căn cước công dân qua VNeID",
    subtitle = "Hệ thống Smart Parking ParkWise yêu cầu chia sẻ thông tin định danh công dân mức độ 2",
}: VneidAuthModalProps) {
    const [selectedProfile, setSelectedProfile] = useState<VneidCitizenProfile>(
        () => findProfileByCccd(defaultCccd) || VNEID_SEED_PROFILES[0],
    );
    const [searchCccd, setSearchCccd] = useState("");
    const [simulatedError, setSimulatedError] = useState<string | null>(null);

    if (!isOpen) return null;

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
                `Số CCCD "${searchCccd}" không tồn tại trong hệ thống Cơ sở dữ liệu Quốc gia về Dân cư.`,
            );
        }
    };

    const handleConfirm = () => {
        if (simulatedError) return;
        onConfirm(selectedProfile);
        onClose();
    };

    return (
        <div
            data-testid="vneid-auth-modal"
            className="fixed inset-0 z-50 flex items-center justify-center p-3 sm:p-4 bg-black/60 backdrop-blur-xs animate-in fade-in duration-200"
        >
            <div className="relative w-full max-w-2xl bg-white rounded-2xl shadow-2xl overflow-hidden border border-slate-200 flex flex-col max-h-[92vh]">
                {/* VNeID Official Red Header */}
                <div className="bg-gradient-to-r from-red-800 via-red-700 to-amber-700 px-5 py-3.5 text-white flex items-center justify-between shrink-0 shadow-md">
                    <div className="flex items-center gap-3">
                        <div className="w-9 h-9 rounded-full bg-red-900 border-2 border-amber-300 flex items-center justify-center shadow-inner">
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

                    {/* Quick Seed Selector for Demo */}
                    <div className="space-y-1.5">
                        <div className="flex items-center justify-between">
                            <span className="text-xs font-bold text-slate-700 uppercase tracking-wider flex items-center gap-1">
                                <UserCheck className="w-3.5 h-3.5 text-blue-600" />
                                Chọn hồ sơ mẫu (Master Sheet Demo):
                            </span>
                            <a
                                href="/simulator/vneid"
                                target="_blank"
                                rel="noreferrer"
                                className="text-[11px] text-blue-600 hover:underline flex items-center gap-0.5 font-medium"
                            >
                                Mở trang Simulator độc lập{" "}
                                <ExternalLink className="w-3 h-3" />
                            </a>
                        </div>

                        <div className="grid grid-cols-2 sm:grid-cols-4 gap-1.5">
                            {VNEID_SEED_PROFILES.slice(0, 4).map((p) => {
                                const isSelected =
                                    selectedProfile.cccd === p.cccd;
                                return (
                                    <button
                                        key={p.cccd}
                                        type="button"
                                        onClick={() => handleSelectPreset(p)}
                                        className={`text-left p-2 rounded-lg border text-xs transition relative ${
                                            isSelected
                                                ? "border-blue-600 bg-blue-50/70 text-blue-900 font-semibold ring-1 ring-blue-500"
                                                : "border-slate-200 bg-white text-slate-700 hover:border-slate-300 hover:bg-slate-50"
                                        }`}
                                    >
                                        <p className="font-bold text-[11px] truncate text-slate-900">
                                            {p.fullName}
                                        </p>
                                        <p className="font-mono text-[10px] text-slate-500">
                                            {p.cccd}
                                        </p>
                                        <p className="text-[10px] text-blue-700 truncate mt-0.5 font-medium">
                                            {p.label.split("(")[0].trim()}
                                        </p>
                                    </button>
                                );
                            })}
                        </div>
                    </div>

                    {/* Search by CCCD */}
                    <form onSubmit={handleSearch} className="flex gap-2">
                        <div className="relative flex-1">
                            <input
                                type="text"
                                value={searchCccd}
                                onChange={(e) => setSearchCccd(e.target.value)}
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

                    {/* Simulated Error Alert */}
                    {simulatedError && (
                        <div className="bg-red-50 border border-red-200 rounded-xl p-3 flex items-center gap-2.5 text-xs text-red-700 animate-in fade-in">
                            <AlertTriangle className="w-4 h-4 text-red-600 shrink-0" />
                            <span>{simulatedError}</span>
                        </div>
                    )}

                    {/* Authentic VNeID CCCD Chip Card Simulator */}
                    <div className="flex flex-col items-center justify-center pt-1 pb-2">
                        <VneidCardSimulator profile={selectedProfile} />
                    </div>

                    {/* Data sharing disclosure */}
                    <div className="bg-slate-50 rounded-xl p-3 border border-slate-200/80 text-[11px] text-slate-600 space-y-1">
                        <p className="font-bold text-slate-800">
                            Thông tin sẽ được chia sẻ tự động với PARKWISE:
                        </p>
                        <ul className="list-disc list-inside space-y-0.5 text-slate-600">
                            <li>
                                Số CCCD:{" "}
                                <strong className="font-mono text-slate-800">
                                    {selectedProfile.cccd}
                                </strong>
                            </li>
                            <li>
                                Họ và tên:{" "}
                                <strong className="text-slate-800">
                                    {selectedProfile.fullName}
                                </strong>
                            </li>
                            <li>
                                Ngày sinh:{" "}
                                <strong>{selectedProfile.dobDisplay}</strong> |
                                Giới tính:{" "}
                                <strong>{selectedProfile.genderDisplay}</strong>
                            </li>
                            <li>
                                Nơi thường trú:{" "}
                                <strong>{selectedProfile.residence}</strong>
                            </li>
                        </ul>
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
