"use client";

import React, { useState } from "react";
import { useGateVoice } from "@/features/staff/audio";
import { GateVoiceControl } from "@/features/staff/audio";
import {
    buildCheckInSpeech,
    buildCheckOutSpeech,
    formatPlateForSpeech,
    formatSlotForSpeech,
} from "@/features/staff/audio";

export default function VoiceAssistantTestPage() {
    const {
        isEnabled,
        isSpeaking,
        volume,
        rate,
        playChime,
        availableVoices,
        selectedVoice,
        setVoiceURI,
        announceCheckIn,
        announceCheckOut,
        announceWarning,
        stop,
    } = useGateVoice();

    const [testPlate, setTestPlate] = useState("51F-782.91");
    const [testSlot, setTestSlot] = useState("A-01");
    const [testLot, setTestLot] = useState("ParkWise Central Lot");
    const [customWarning, setCustomWarning] = useState(
        "Xe chưa thanh toán phí đỗ. Vui lòng thanh toán tại quầy hoặc qua ứng dụng.",
    );

    const platePhonetic = formatPlateForSpeech(testPlate);
    const slotPhonetic = formatSlotForSpeech(testSlot);

    return (
        <div className="min-h-screen bg-slate-950 text-slate-100 p-6 md:p-10 font-sans">
            <div className="max-w-4xl mx-auto space-y-8">
                {/* Header */}
                <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-6">
                    <div>
                        <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-cyan-500/10 border border-cyan-500/30 text-cyan-400 text-xs font-semibold mb-2">
                            <span>🎙️ SMART GATE VOICE ASSISTANT</span>
                            <span className="w-1.5 h-1.5 rounded-full bg-cyan-400 animate-pulse" />
                        </div>
                        <h1 className="text-2xl md:text-3xl font-bold tracking-tight text-white">
                            Thử Nghiệm Trợ Lý Giọng Nói Cổng Bãi
                        </h1>
                        <p className="text-slate-400 text-sm mt-1">
                            Web Speech API native · 100% Offline · Tự nhiên
                            tiếng Việt · Zero Cloud API Cost
                        </p>
                    </div>

                    <div className="flex items-center gap-3">
                        <GateVoiceControl />
                    </div>
                </div>

                {/* Status Dashboard Banner */}
                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                    <div className="p-4 rounded-xl bg-slate-900/80 border border-slate-800">
                        <span className="text-xs text-slate-400 font-medium">
                            Trạng thái Loa
                        </span>
                        <div className="flex items-center gap-2 mt-1">
                            <span
                                className={`w-3 h-3 rounded-full ${
                                    isEnabled
                                        ? isSpeaking
                                            ? "bg-amber-400 animate-ping"
                                            : "bg-emerald-400 shadow-[0_0_8px_rgba(52,211,153,0.5)]"
                                        : "bg-rose-500"
                                }`}
                            />
                            <span className="font-semibold text-slate-200">
                                {isEnabled
                                    ? isSpeaking
                                        ? "Đang phát âm..."
                                        : "Sẵn sàng (Online)"
                                    : "Đã tắt tiếng"}
                            </span>
                        </div>
                    </div>

                    <div className="p-4 rounded-xl bg-slate-900/80 border border-slate-800">
                        <span className="text-xs text-slate-400 font-medium">
                            Giọng đọc hiện tại
                        </span>
                        <div
                            className="text-sm font-semibold text-cyan-300 truncate mt-1"
                            title={selectedVoice?.name}
                        >
                            {selectedVoice?.name || "Mặc định (vi-VN)"}
                        </div>
                    </div>

                    <div className="p-4 rounded-xl bg-slate-900/80 border border-slate-800">
                        <span className="text-xs text-slate-400 font-medium">
                            Cấu hình Âm thanh
                        </span>
                        <div className="text-xs text-slate-300 mt-1 flex gap-4 font-mono">
                            <span>Vol: {Math.round(volume * 100)}%</span>
                            <span>Speed: {rate.toFixed(1)}x</span>
                            <span>Chime: {playChime ? "ON" : "OFF"}</span>
                        </div>
                    </div>
                </div>

                {/* Live Sandbox Interactive Form */}
                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                    {/* Left Panel: Check-in Simulation */}
                    <div className="p-6 rounded-2xl bg-slate-900/60 border border-slate-800/80 space-y-4 shadow-lg">
                        <div className="flex items-center gap-2">
                            <span className="p-2 rounded-lg bg-emerald-500/10 text-emerald-400 font-bold">
                                🚗
                            </span>
                            <h2 className="text-base font-semibold text-slate-100">
                                Kịch Bản Cổng Vào (Check-in)
                            </h2>
                        </div>

                        <div className="space-y-3 text-sm">
                            <div>
                                <label className="block text-xs text-slate-400 mb-1">
                                    Biển số xe nhận diện (OCR)
                                </label>
                                <input
                                    type="text"
                                    value={testPlate}
                                    onChange={(e) =>
                                        setTestPlate(e.target.value)
                                    }
                                    className="w-full px-3 py-2 rounded-lg bg-slate-950 border border-slate-700 text-slate-100 font-mono focus:border-cyan-500 focus:outline-none"
                                />
                                <div className="text-[11px] text-cyan-400/80 mt-1">
                                    Âm tiết chuẩn hóa:{" "}
                                    <span className="font-mono text-cyan-300">
                                        {platePhonetic}
                                    </span>
                                </div>
                            </div>

                            <div>
                                <label className="block text-xs text-slate-400 mb-1">
                                    Ô đỗ phân bổ (SlotCode)
                                </label>
                                <input
                                    type="text"
                                    value={testSlot}
                                    onChange={(e) =>
                                        setTestSlot(e.target.value)
                                    }
                                    className="w-full px-3 py-2 rounded-lg bg-slate-950 border border-slate-700 text-slate-100 font-mono focus:border-cyan-500 focus:outline-none"
                                />
                                <div className="text-[11px] text-cyan-400/80 mt-1">
                                    Âm tiết chuẩn hóa:{" "}
                                    <span className="font-mono text-cyan-300">
                                        {slotPhonetic}
                                    </span>
                                </div>
                            </div>

                            <div>
                                <label className="block text-xs text-slate-400 mb-1">
                                    Tên bãi đỗ xe
                                </label>
                                <input
                                    type="text"
                                    value={testLot}
                                    onChange={(e) => setTestLot(e.target.value)}
                                    className="w-full px-3 py-2 rounded-lg bg-slate-950 border border-slate-700 text-slate-100 focus:border-cyan-500 focus:outline-none"
                                />
                            </div>

                            {/* Preview Preview Box */}
                            <div className="p-3 rounded-lg bg-slate-950/70 border border-slate-800 text-xs text-slate-300 italic">
                                &quot;
                                {buildCheckInSpeech({
                                    plateNumber: testPlate,
                                    slotCode: testSlot,
                                    lotName: testLot,
                                })}
                                &quot;
                            </div>

                            <button
                                type="button"
                                onClick={() =>
                                    announceCheckIn({
                                        plateNumber: testPlate,
                                        slotCode: testSlot,
                                        lotName: testLot,
                                    })
                                }
                                className="w-full py-2.5 rounded-lg bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-500 hover:to-teal-500 text-white font-medium text-sm transition-all shadow-[0_4px_14px_rgba(16,185,129,0.3)] flex items-center justify-center gap-2"
                            >
                                <span>🔊 Phát Giọng Chào Check-in</span>
                            </button>
                        </div>
                    </div>

                    {/* Right Panel: Check-out & Warnings */}
                    <div className="p-6 rounded-2xl bg-slate-900/60 border border-slate-800/80 space-y-4 shadow-lg">
                        <div className="flex items-center gap-2">
                            <span className="p-2 rounded-lg bg-cyan-500/10 text-cyan-400 font-bold">
                                💳
                            </span>
                            <h2 className="text-base font-semibold text-slate-100">
                                Kịch Bản Cổng Ra (Check-out) & Cảnh Báo
                            </h2>
                        </div>

                        <div className="space-y-3 text-sm">
                            <div>
                                <label className="block text-xs text-slate-400 mb-1">
                                    Xe xuất bãi thành công
                                </label>
                                <div className="p-3 rounded-lg bg-slate-950/70 border border-slate-800 text-xs text-slate-300 italic mb-2">
                                    &quot;
                                    {buildCheckOutSpeech({
                                        plateNumber: testPlate,
                                        lotName: testLot,
                                    })}
                                    &quot;
                                </div>
                                <button
                                    type="button"
                                    onClick={() =>
                                        announceCheckOut({
                                            plateNumber: testPlate,
                                            lotName: testLot,
                                        })
                                    }
                                    className="w-full py-2.5 rounded-lg bg-gradient-to-r from-cyan-600 to-blue-600 hover:from-cyan-500 hover:to-blue-500 text-white font-medium text-sm transition-all shadow-[0_4px_14px_rgba(6,182,212,0.3)] flex items-center justify-center gap-2"
                                >
                                    <span>🔊 Phát Lời Cảm Ơn Check-out</span>
                                </button>
                            </div>

                            <div className="border-t border-slate-800 pt-3">
                                <label className="block text-xs text-slate-400 mb-1">
                                    Nội dung cảnh báo vi phạm / lỗi
                                </label>
                                <textarea
                                    rows={2}
                                    value={customWarning}
                                    onChange={(e) =>
                                        setCustomWarning(e.target.value)
                                    }
                                    className="w-full px-3 py-2 rounded-lg bg-slate-950 border border-slate-700 text-slate-100 text-xs focus:border-amber-500 focus:outline-none mb-2"
                                />
                                <button
                                    type="button"
                                    onClick={() =>
                                        announceWarning(customWarning)
                                    }
                                    className="w-full py-2.5 rounded-lg bg-gradient-to-r from-amber-600 to-rose-600 hover:from-amber-500 hover:to-rose-500 text-white font-medium text-sm transition-all shadow-[0_4px_14px_rgba(245,158,11,0.3)] flex items-center justify-center gap-2"
                                >
                                    <span>⚠️ Phát Âm Thanh Cảnh Báo</span>
                                </button>
                            </div>

                            {isSpeaking && (
                                <button
                                    type="button"
                                    onClick={stop}
                                    className="w-full py-2 rounded-lg bg-rose-600/20 border border-rose-500/40 text-rose-300 text-xs font-semibold hover:bg-rose-600/30 transition-all"
                                >
                                    ■ Dừng phát âm ngay lập tức
                                </button>
                            )}
                        </div>
                    </div>
                </div>

                {/* Engine Diagnostics */}
                <div className="p-6 rounded-2xl bg-slate-900/60 border border-slate-800/80">
                    <h3 className="text-sm font-semibold uppercase tracking-wider text-slate-400 mb-3">
                        Danh Sách Giọng Đọc Phát Hiện Trên Trình Duyệt (
                        {availableVoices.length})
                    </h3>
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-2 max-h-48 overflow-y-auto">
                        {availableVoices.map((v) => (
                            <div
                                key={v.voiceURI}
                                onClick={() => setVoiceURI(v.voiceURI)}
                                className={`p-2.5 rounded-lg border text-xs cursor-pointer transition-all ${
                                    v.voiceURI === selectedVoice?.voiceURI
                                        ? "bg-cyan-500/10 border-cyan-500/50 text-cyan-300"
                                        : "bg-slate-950/40 border-slate-800 text-slate-400 hover:border-slate-700"
                                }`}
                            >
                                <div className="font-semibold flex items-center justify-between">
                                    <span>{v.name}</span>
                                    {v.voiceURI === selectedVoice?.voiceURI && (
                                        <span className="text-[10px] px-1.5 py-0.5 rounded bg-cyan-500/20 text-cyan-300">
                                            Đang dùng
                                        </span>
                                    )}
                                </div>
                                <div className="text-[11px] text-slate-500">
                                    Ngôn ngữ: {v.lang} ·{" "}
                                    {v.localService
                                        ? "Local/Offline"
                                        : "Browser Synthesizer"}
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
            </div>
        </div>
    );
}
