"use client";

import { useState, useEffect } from "react";
import Link from "next/link";
import { vi } from "../../../features/customer/i18n/vi";
import {
    getMyParkingSessions,
    type MyParkingSession,
} from "../../../features/customer/mySessionsApi";
import {
    History,
    Car,
    Calendar,
    Clock,
    MapPin,
    AlertCircle,
    RefreshCw,
    Search,
    CheckCircle,
} from "lucide-react";
import { ApiError } from "@/shared/errors/api-error";

export default function ParkingHistoryPage() {
    const [sessions, setSessions] = useState<MyParkingSession[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    async function loadHistory() {
        setLoading(true);
        setError(null);
        try {
            const data = await getMyParkingSessions();
            setSessions(data);
        } catch (err: unknown) {
            if (err instanceof ApiError) {
                setError(
                    err.problem?.detail ||
                        err.message ||
                        "Không thể tải lịch sử đỗ xe.",
                );
            } else if (err instanceof Error) {
                setError(err.message);
            } else {
                setError("Có lỗi xảy ra khi tải lịch sử đỗ xe.");
            }
        } finally {
            setLoading(false);
        }
    }

    useEffect(() => {
        loadHistory();
    }, []);

    return (
        <div className="animate-in fade-in duration-500 space-y-6">
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-2xl font-bold text-white mb-1">
                        {vi.dashboard.history}
                    </h1>
                    <p className="text-[#8790a3] text-sm">
                        Danh sách các phiên đỗ xe thực tế của bạn tại các bãi
                        trong hệ thống ParkWise.
                    </p>
                </div>
                <button
                    type="button"
                    onClick={loadHistory}
                    disabled={loading}
                    className="p-3 rounded-xl bg-white/5 border border-white/10 text-[#8790a3] hover:text-white hover:border-white/20 transition-colors"
                    title="Tải lại lịch sử"
                    aria-label="Tải lại lịch sử"
                >
                    <RefreshCw
                        size={18}
                        className={loading ? "animate-spin" : ""}
                    />
                </button>
            </div>

            {/* Trạng thái Loading */}
            {loading && (
                <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
                    {[1, 2].map((i) => (
                        <div
                            key={i}
                            className="bg-[#0b0c17] border border-white/10 rounded-2xl p-6 h-48 animate-pulse flex flex-col justify-between"
                        >
                            <div className="space-y-3">
                                <div className="h-5 w-32 bg-white/10 rounded-md" />
                                <div className="h-4 w-48 bg-white/5 rounded-md" />
                            </div>
                            <div className="h-8 w-full bg-white/5 rounded-xl" />
                        </div>
                    ))}
                </div>
            )}

            {/* Trạng thái Lỗi */}
            {!loading && error && (
                <div className="p-6 bg-red-500/10 border border-red-500/30 rounded-2xl text-center space-y-3">
                    <AlertCircle size={32} className="mx-auto text-red-400" />
                    <p className="text-red-300 text-sm font-medium">{error}</p>
                    <button
                        type="button"
                        onClick={loadHistory}
                        className="px-4 py-2 bg-white/10 hover:bg-white/15 text-white rounded-xl text-xs font-semibold transition-colors"
                    >
                        Thử lại
                    </button>
                </div>
            )}

            {/* Trạng thái Trống (Empty) */}
            {!loading && !error && sessions.length === 0 && (
                <div className="bg-[#0b0c17] border border-white/10 rounded-2xl p-12 text-center space-y-4 shadow-card-light">
                    <div className="w-16 h-16 rounded-full bg-[#6246ea]/10 border border-[#6246ea]/20 flex items-center justify-center mx-auto text-[#a28aff]">
                        <History size={28} />
                    </div>
                    <div>
                        <h3 className="text-lg font-bold text-white mb-1">
                            Chưa có lịch sử gửi xe
                        </h3>
                        <p className="text-[#8790a3] text-sm max-w-md mx-auto">
                            Bạn chưa có phiên gửi xe nào trong hệ thống. Khi lái
                            xe vào các bãi thuộc mạng lưới ParkWise, lịch sử
                            phiên xe sẽ hiển thị tại đây.
                        </p>
                    </div>
                    <Link
                        href="/search"
                        className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl bg-[#6246ea] hover:bg-[#7c5cff] text-white font-bold text-sm transition-colors shadow-glow-purple"
                    >
                        <Search size={16} /> Tìm bãi đỗ xe gần đây
                    </Link>
                </div>
            )}

            {/* Danh sách Phiên xe thật */}
            {!loading && !error && sessions.length > 0 && (
                <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
                    {sessions.map((session) => {
                        const entryDate = new Date(session.entryTime);
                        const dateFormatted = entryDate.toLocaleDateString(
                            "vi-VN",
                            {
                                day: "2-digit",
                                month: "2-digit",
                                year: "numeric",
                            },
                        );
                        const timeFormatted = entryDate.toLocaleTimeString(
                            "vi-VN",
                            {
                                hour: "2-digit",
                                minute: "2-digit",
                            },
                        );

                        const isOpen = session.status === "Open";

                        return (
                            <div
                                key={session.sessionId}
                                className="bg-[#0b0c17] border border-white/10 rounded-2xl p-6 shadow-card-light hover:border-white/20 transition-colors flex flex-col justify-between"
                            >
                                <div>
                                    <div className="flex justify-between items-start mb-3">
                                        <span
                                            className={`px-2.5 py-1 rounded-md text-xs font-semibold ${
                                                isOpen
                                                    ? "bg-emerald-500/20 text-emerald-300 border border-emerald-500/30"
                                                    : "bg-white/10 text-[#d5d8e3] border border-white/15"
                                            }`}
                                        >
                                            {isOpen
                                                ? "Đang gửi xe"
                                                : "Đã hoàn tất"}
                                        </span>
                                        <span className="text-xs font-mono text-[#8790a3]">
                                            ID: #
                                            {session.sessionId
                                                .slice(0, 8)
                                                .toUpperCase()}
                                        </span>
                                    </div>

                                    <h3 className="text-lg font-bold text-white mb-1">
                                        Bãi đỗ #{session.lotId.slice(0, 8)}
                                    </h3>
                                    <p className="text-xs text-[#8790a3] flex items-center gap-1.5 mb-4">
                                        <MapPin
                                            size={13}
                                            className="text-[#6246ea]"
                                        />
                                        Mạng lưới bãi thông minh ParkWise
                                    </p>

                                    <div className="bg-[#101426] border border-white/5 rounded-xl p-3.5 space-y-2 text-xs">
                                        <div className="flex justify-between">
                                            <span className="text-[#8790a3] flex items-center gap-1.5">
                                                <Car size={13} /> Biển số
                                            </span>
                                            <span className="text-white font-mono font-bold">
                                                {session.plateNumber} (
                                                {session.vehicleType})
                                            </span>
                                        </div>
                                        <div className="flex justify-between">
                                            <span className="text-[#8790a3] flex items-center gap-1.5">
                                                <Calendar size={13} /> Giờ vào
                                            </span>
                                            <span className="text-white font-medium">
                                                {timeFormatted} ·{" "}
                                                {dateFormatted}
                                            </span>
                                        </div>
                                        {session.floorName && (
                                            <div className="flex justify-between">
                                                <span className="text-[#8790a3]">
                                                    Tầng / Khu vực
                                                </span>
                                                <span className="text-white font-medium">
                                                    {session.floorName}{" "}
                                                    {session.zoneCode
                                                        ? `(${session.zoneCode})`
                                                        : ""}
                                                </span>
                                            </div>
                                        )}
                                    </div>
                                </div>

                                {isOpen && (
                                    <div className="mt-4 pt-3 border-t border-white/10 flex justify-between items-center text-xs">
                                        <span className="text-[#8790a3]">
                                            Phiên đang hoạt động
                                        </span>
                                        <Link
                                            href="/customer/sessions"
                                            className="text-[#a28aff] hover:underline font-bold"
                                        >
                                            Xem mã QR ra cổng →
                                        </Link>
                                    </div>
                                )}
                            </div>
                        );
                    })}
                </div>
            )}
        </div>
    );
}
