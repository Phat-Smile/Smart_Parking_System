"use client";

import React, { useState, useRef, useEffect } from "react";
import { useGateVoice } from "./useGateVoice";
import { isVietnameseVoice } from "./voiceSelection";

export function GateVoiceControl() {
    const {
        isEnabled,
        isSpeaking,
        volume,
        rate,
        playChime,
        availableVoices,
        selectedVoice,
        logs,
        toggleEnabled,
        setVolume,
        setRate,
        setPlayChime,
        setVoiceURI,
        testVoice,
        stop,
    } = useGateVoice();

    const [isOpen, setIsOpen] = useState(false);
    const panelRef = useRef<HTMLDivElement>(null);

    // Đóng khi click ra ngoài
    useEffect(() => {
        function handleClickOutside(event: MouseEvent) {
            if (
                panelRef.current &&
                !panelRef.current.contains(event.target as Node)
            ) {
                setIsOpen(false);
            }
        }
        if (isOpen) {
            document.addEventListener("mousedown", handleClickOutside);
        }
        return () => {
            document.removeEventListener("mousedown", handleClickOutside);
        };
    }, [isOpen]);

    return (
        <div className="relative inline-block" ref={panelRef}>
            {/* Header Button Trigger */}
            <button
                type="button"
                onClick={() => setIsOpen((prev) => !prev)}
                className={`relative flex items-center gap-2 px-3 py-1.5 rounded-lg border text-xs font-medium transition-all ${
                    isEnabled
                        ? "bg-slate-800/80 border-cyan-500/40 text-cyan-300 hover:border-cyan-400 shadow-[0_0_12px_rgba(6,182,212,0.15)]"
                        : "bg-slate-800/40 border-slate-700 text-slate-400 hover:text-slate-300"
                }`}
                title={
                    isEnabled
                        ? "Trợ lý giọng nói Cổng bãi (Đang bật)"
                        : "Trợ lý giọng nói Cổng bãi (Đã tắt tiếng)"
                }
            >
                {/* Speaker Icon & Audio Wave */}
                <span className="text-sm">
                    {isEnabled ? (isSpeaking ? "🔊" : "🔉") : "🔇"}
                </span>

                <span className="hidden sm:inline">
                    {isEnabled ? "Loa Cổng" : "Tắt loa"}
                </span>

                {/* Animated Wave Indicator khi đang phát thanh */}
                {isEnabled && isSpeaking && (
                    <span className="flex items-end gap-0.5 h-3 ml-0.5">
                        <span className="w-0.5 h-full bg-cyan-400 animate-pulse" />
                        <span className="w-0.5 h-2 bg-cyan-300 animate-pulse delay-75" />
                        <span className="w-0.5 h-3 bg-cyan-400 animate-pulse delay-150" />
                    </span>
                )}

                {/* Status Dot */}
                <span
                    className={`w-2 h-2 rounded-full ${
                        isEnabled
                            ? isSpeaking
                                ? "bg-amber-400 animate-ping"
                                : "bg-emerald-400"
                            : "bg-rose-500"
                    }`}
                />
            </button>

            {/* Dropdown Control Panel */}
            {isOpen && (
                <div
                    className="absolute right-0 mt-2 w-80 sm:w-96 rounded-xl border border-slate-700/80 bg-slate-900/95 backdrop-blur-md p-4 text-slate-200 shadow-2xl z-50 animate-in fade-in zoom-in-95 duration-150"
                    style={{ minWidth: "320px" }}
                >
                    {/* Header */}
                    <div className="flex items-center justify-between pb-3 mb-3 border-b border-slate-800">
                        <div className="flex items-center gap-2">
                            <span className="p-1.5 rounded-lg bg-cyan-500/10 text-cyan-400 text-base">
                                🎙️
                            </span>
                            <div>
                                <h4 className="text-sm font-semibold text-slate-100 leading-tight">
                                    Trợ Lý Giọng Nói Cổng Bãi
                                </h4>
                                <p className="text-[11px] text-slate-400">
                                    Web Speech API · 100% Offline
                                </p>
                            </div>
                        </div>

                        <button
                            type="button"
                            onClick={() => setIsOpen(false)}
                            className="p-1 text-slate-400 hover:text-slate-200 rounded-md transition-colors"
                        >
                            ✕
                        </button>
                    </div>

                    {/* Quick Toggles */}
                    <div className="grid grid-cols-2 gap-2 mb-3">
                        <button
                            type="button"
                            onClick={toggleEnabled}
                            className={`flex items-center justify-center gap-1.5 px-3 py-2 rounded-lg border text-xs font-medium transition-all ${
                                isEnabled
                                    ? "bg-emerald-500/10 border-emerald-500/40 text-emerald-400"
                                    : "bg-rose-500/10 border-rose-500/40 text-rose-400"
                            }`}
                        >
                            <span>
                                {isEnabled ? "✓ Bật âm thanh" : "✕ Đã tắt âm"}
                            </span>
                        </button>

                        <button
                            type="button"
                            onClick={() => setPlayChime(!playChime)}
                            className={`flex items-center justify-center gap-1.5 px-3 py-2 rounded-lg border text-xs font-medium transition-all ${
                                playChime
                                    ? "bg-cyan-500/10 border-cyan-500/40 text-cyan-300"
                                    : "bg-slate-800 border-slate-700 text-slate-400"
                            }`}
                        >
                            <span>
                                🔔 Chuông Chime {playChime ? "Bật" : "Tắt"}
                            </span>
                        </button>
                    </div>

                    {/* Sliders: Volume & Speed */}
                    <div className="space-y-3 mb-3 p-2.5 rounded-lg bg-slate-950/40 border border-slate-800/80">
                        {/* Volume */}
                        <div>
                            <div className="flex justify-between text-xs mb-1">
                                <span className="text-slate-400">Âm lượng</span>
                                <span className="font-mono text-cyan-300">
                                    {Math.round(volume * 100)}%
                                </span>
                            </div>
                            <input
                                type="range"
                                min="0"
                                max="1"
                                step="0.05"
                                value={volume}
                                onChange={(e) =>
                                    setVolume(parseFloat(e.target.value))
                                }
                                className="w-full h-1.5 bg-slate-700 rounded-lg appearance-none cursor-pointer accent-cyan-400"
                            />
                        </div>

                        {/* Speed */}
                        <div>
                            <div className="flex justify-between text-xs mb-1">
                                <span className="text-slate-400">
                                    Tốc độ đọc
                                </span>
                                <span className="font-mono text-cyan-300">
                                    {rate.toFixed(1)}x
                                </span>
                            </div>
                            <input
                                type="range"
                                min="0.8"
                                max="1.4"
                                step="0.1"
                                value={rate}
                                onChange={(e) =>
                                    setRate(parseFloat(e.target.value))
                                }
                                className="w-full h-1.5 bg-slate-700 rounded-lg appearance-none cursor-pointer accent-cyan-400"
                            />
                        </div>

                        {/* Voice Selector */}
                        {availableVoices.length > 0 && (
                            <div>
                                <label className="block text-xs text-slate-400 mb-1">
                                    Giọng đọc tiếng Việt (Engine)
                                </label>
                                <select
                                    value={selectedVoice?.voiceURI || ""}
                                    onChange={(e) =>
                                        setVoiceURI(e.target.value)
                                    }
                                    className="w-full bg-slate-900 border border-slate-700 rounded-md px-2 py-1.5 text-xs text-slate-200 focus:outline-none focus:border-cyan-500"
                                >
                                    {availableVoices.map((v) => (
                                        <option
                                            key={v.voiceURI}
                                            value={v.voiceURI}
                                        >
                                            {v.name} ({v.lang})
                                        </option>
                                    ))}
                                </select>
                                {!availableVoices.some(isVietnameseVoice) && (
                                    <p className="mt-1 text-[11px] text-amber-400">
                                        Máy chưa có giọng tiếng Việt nên có thể
                                        đọc bằng giọng tiếng Anh. Cài giọng
                                        Vietnamese trong cài đặt Giọng nói của
                                        hệ điều hành rồi tải lại trang.
                                    </p>
                                )}
                            </div>
                        )}
                    </div>

                    {/* Test Speech Buttons */}
                    <div className="mb-3">
                        <div className="text-[11px] font-semibold uppercase tracking-wider text-slate-400 mb-1.5">
                            Phát Thử Giọng Nói (Test)
                        </div>
                        <div className="grid grid-cols-3 gap-1.5">
                            <button
                                type="button"
                                onClick={() => testVoice("checkin")}
                                className="px-2 py-1.5 rounded bg-emerald-500/10 border border-emerald-500/30 hover:bg-emerald-500/20 text-emerald-300 text-[11px] font-medium transition-all"
                            >
                                🚗 Check-in
                            </button>
                            <button
                                type="button"
                                onClick={() => testVoice("checkout")}
                                className="px-2 py-1.5 rounded bg-cyan-500/10 border border-cyan-500/30 hover:bg-cyan-500/20 text-cyan-300 text-[11px] font-medium transition-all"
                            >
                                💳 Check-out
                            </button>
                            <button
                                type="button"
                                onClick={() => testVoice("warning")}
                                className="px-2 py-1.5 rounded bg-amber-500/10 border border-amber-500/30 hover:bg-amber-500/20 text-amber-300 text-[11px] font-medium transition-all"
                            >
                                ⚠️ Cảnh báo
                            </button>
                        </div>
                    </div>

                    {/* Stop Button khi đang phát âm */}
                    {isSpeaking && (
                        <button
                            type="button"
                            onClick={stop}
                            className="w-full mb-3 py-1.5 rounded-lg bg-rose-500/20 border border-rose-500/40 text-rose-300 text-xs font-semibold hover:bg-rose-500/30 transition-all"
                        >
                            ■ Dừng phát âm ngay
                        </button>
                    )}

                    {/* Recent Speech Logs */}
                    {logs.length > 0 && (
                        <div className="border-t border-slate-800 pt-2">
                            <div className="text-[10px] uppercase font-semibold text-slate-500 mb-1">
                                Phát thanh gần nhất
                            </div>
                            <div className="max-h-24 overflow-y-auto space-y-1 text-[11px]">
                                {logs.slice(0, 3).map((log) => (
                                    <div
                                        key={log.id}
                                        className="p-1.5 rounded bg-slate-950/30 border border-slate-800/60 text-slate-300 truncate"
                                        title={log.text}
                                    >
                                        <span className="text-cyan-400 mr-1">
                                            [
                                            {log.timestamp.toLocaleTimeString(
                                                "vi-VN",
                                                {
                                                    hour: "2-digit",
                                                    minute: "2-digit",
                                                    second: "2-digit",
                                                },
                                            )}
                                            ]
                                        </span>
                                        {log.text}
                                    </div>
                                ))}
                            </div>
                        </div>
                    )}
                </div>
            )}
        </div>
    );
}
