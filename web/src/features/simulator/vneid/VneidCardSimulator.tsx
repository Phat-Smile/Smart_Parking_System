"use client";

import React from "react";
import QRCode from "react-qr-code";
import { VneidCitizenProfile, generateVneidQrString } from "./vneidSeedData";
import { ShieldCheck, User } from "lucide-react";

interface VneidCardSimulatorProps {
    profile: VneidCitizenProfile;
    className?: string;
    showBackSide?: boolean;
}

export function VneidCardSimulator({
    profile,
    className = "",
}: VneidCardSimulatorProps) {
    const qrString = generateVneidQrString(profile);

    return (
        <div
            data-testid="vneid-card-simulator"
            className={`relative w-full max-w-[480px] aspect-[85.6/53.98] rounded-2xl p-4 sm:p-5 text-slate-800 shadow-xl overflow-hidden select-none border border-amber-200/60 transition-all ${className}`}
            style={{
                background:
                    "linear-gradient(135deg, #fffdf2 0%, #fef3c7 40%, #e0f2fe 80%, #fef9c3 100%)",
                boxShadow:
                    "0 10px 25px -5px rgba(180, 83, 9, 0.15), 0 8px 10px -6px rgba(180, 83, 9, 0.1)",
            }}
        >
            {/* Guilloche Security Pattern Overlay */}
            <div
                className="absolute inset-0 opacity-15 pointer-events-none mix-blend-multiply"
                style={{
                    backgroundImage: `radial-gradient(circle at 50% 50%, #f59e0b 1px, transparent 1px), radial-gradient(circle at 25% 25%, #0284c7 1px, transparent 1px)`,
                    backgroundSize: "16px 16px",
                }}
            />

            {/* Card Header: Quốc hiệu & Tiêu ngữ */}
            <div className="relative z-10 flex items-start justify-between border-b border-amber-400/40 pb-2 mb-2">
                {/* National Emblem SVG */}
                <div className="flex items-center gap-2">
                    <div className="w-8 h-8 sm:w-10 sm:h-10 rounded-full bg-red-600 flex items-center justify-center p-1 shadow-inner border border-amber-300">
                        <svg
                            viewBox="0 0 100 100"
                            className="w-full h-full"
                            fill="none"
                        >
                            <circle cx="50" cy="50" r="46" fill="#DC2626" />
                            <circle
                                cx="50"
                                cy="50"
                                r="44"
                                stroke="#FDE047"
                                strokeWidth="2"
                            />
                            {/* Gold Star */}
                            <polygon
                                points="50,15 58,38 82,38 62,53 70,76 50,61 30,76 38,53 18,38 42,38"
                                fill="#FDE047"
                            />
                            <path
                                d="M20,80 Q50,95 80,80"
                                stroke="#FDE047"
                                strokeWidth="6"
                                strokeLinecap="round"
                            />
                        </svg>
                    </div>
                    <div className="text-left">
                        <p className="text-[9px] sm:text-[11px] font-bold text-red-700 tracking-tight uppercase leading-tight">
                            CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM
                        </p>
                        <p className="text-[7.5px] sm:text-[9px] font-semibold text-slate-700 leading-tight">
                            Độc lập - Tự do - Hạnh phúc
                        </p>
                        <p className="text-[9px] sm:text-[11px] font-extrabold text-blue-900 tracking-wider mt-0.5 uppercase">
                            CĂN CƯỚC CÔNG DÂN
                        </p>
                    </div>
                </div>

                {/* Official QR Code */}
                <div className="bg-white p-1 rounded-md border border-slate-300 shadow-sm flex flex-col items-center">
                    <QRCode
                        value={qrString}
                        size={48}
                        level="M"
                        className="w-10 h-10 sm:w-12 sm:h-12"
                    />
                </div>
            </div>

            {/* Card Body */}
            <div className="relative z-10 flex gap-3 sm:gap-4 items-stretch">
                {/* Left Column: Smart Chip & Avatar */}
                <div className="flex flex-col items-center justify-between w-20 sm:w-24 shrink-0">
                    {/* Authentic Electronic Chip */}
                    <div
                        className="w-9 h-7 sm:w-11 sm:h-8 rounded bg-gradient-to-br from-amber-300 via-amber-400 to-yellow-500 border border-amber-600/70 shadow-sm relative overflow-hidden flex items-center justify-center"
                        title="Chip điện tử bảo mật VNeID"
                    >
                        <div className="absolute inset-0 flex items-center justify-center">
                            <div className="w-full h-[1px] bg-amber-700/60" />
                        </div>
                        <div className="absolute inset-0 flex items-center justify-center">
                            <div className="h-full w-[1px] bg-amber-700/60" />
                        </div>
                        <div className="w-4 h-3 rounded border border-amber-700/80 bg-amber-300/60 z-10" />
                    </div>

                    {/* Photo Frame 3x4 */}
                    <div className="w-18 h-24 sm:w-22 sm:h-28 rounded border-2 border-slate-300 bg-slate-100/90 shadow-sm flex flex-col items-center justify-center overflow-hidden relative mt-1">
                        <div className="w-10 h-10 rounded-full bg-slate-300 flex items-center justify-center text-slate-500 mb-1">
                            <User className="w-6 h-6 text-slate-500" />
                        </div>
                        <span className="text-[8px] text-slate-500 font-medium">
                            ẢNH 3x4
                        </span>
                        <div className="absolute bottom-0 inset-x-0 bg-slate-200/90 py-0.5 text-center">
                            <span className="text-[7.5px] font-bold text-slate-700">
                                {profile.genderDisplay}
                            </span>
                        </div>
                    </div>
                </div>

                {/* Right Column: Citizen Details */}
                <div className="flex-1 flex flex-col justify-between text-left leading-tight min-w-0">
                    <div>
                        {/* CCCD Number */}
                        <div className="flex items-baseline gap-1.5 mb-1">
                            <span className="text-[8.5px] sm:text-[10px] text-slate-600 font-semibold uppercase shrink-0">
                                Số / No.:
                            </span>
                            <span
                                data-testid="vneid-card-cccd"
                                className="text-sm sm:text-base font-extrabold text-red-600 tracking-wider font-mono"
                            >
                                {profile.cccd.replace(
                                    /(\d{3})(\d{3})(\d{3})(\d{3})/,
                                    "$1 $2 $3 $4",
                                )}
                            </span>
                        </div>

                        {/* Full Name */}
                        <div className="mb-1">
                            <span className="text-[7.5px] sm:text-[9px] text-slate-500 font-semibold uppercase block">
                                Họ và tên / Full name:
                            </span>
                            <p
                                data-testid="vneid-card-fullname"
                                className="text-xs sm:text-sm font-black text-slate-900 tracking-wide truncate uppercase"
                            >
                                {profile.fullName}
                            </p>
                        </div>

                        {/* DOB & Gender */}
                        <div className="grid grid-cols-2 gap-1 mb-1">
                            <div>
                                <span className="text-[7.5px] sm:text-[9px] text-slate-500 font-semibold uppercase block">
                                    Ngày sinh / Date of birth:
                                </span>
                                <p className="text-[10px] sm:text-xs font-bold text-slate-800">
                                    {profile.dobDisplay}
                                </p>
                            </div>
                            <div>
                                <span className="text-[7.5px] sm:text-[9px] text-slate-500 font-semibold uppercase block">
                                    Giới tính / Sex:
                                </span>
                                <p className="text-[10px] sm:text-xs font-bold text-slate-800">
                                    {profile.genderDisplay}
                                </p>
                            </div>
                        </div>

                        {/* Hometown */}
                        <div className="mb-1">
                            <span className="text-[7.5px] sm:text-[9px] text-slate-500 font-semibold uppercase block">
                                Quê quán / Place of origin:
                            </span>
                            <p className="text-[9.5px] sm:text-[11px] font-semibold text-slate-800 truncate">
                                {profile.hometown}
                            </p>
                        </div>

                        {/* Residence */}
                        <div className="mb-1">
                            <span className="text-[7.5px] sm:text-[9px] text-slate-500 font-semibold uppercase block">
                                Nơi thường trú / Place of residence:
                            </span>
                            <p className="text-[9px] sm:text-[10.5px] font-semibold text-slate-800 line-clamp-2">
                                {profile.residence}
                            </p>
                        </div>
                    </div>

                    {/* Expiry Date */}
                    <div className="pt-1 border-t border-amber-300/40 flex items-center justify-between text-[8px] sm:text-[9.5px] text-slate-600">
                        <span>
                            Có giá trị đến / Date of expiry:{" "}
                            <strong>{profile.expiryDate}</strong>
                        </span>
                        <span className="text-emerald-700 font-bold flex items-center gap-0.5">
                            <ShieldCheck className="w-3 h-3" />
                            ĐÃ XÁC THỰC MỨC 2
                        </span>
                    </div>
                </div>
            </div>
        </div>
    );
}
