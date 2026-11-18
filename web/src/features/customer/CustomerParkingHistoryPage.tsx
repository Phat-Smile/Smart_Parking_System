"use client";

import { useState, useEffect, useCallback } from "react";
import Link from "next/link";
import { ParkWiseAction } from "@/features/parkwise/ParkWiseAction";
import {
    getMyParkingSessionHistory,
    type MyParkingSessionHistoryItem,
} from "@/features/customer/mySessionsApi";
import {
    History,
    Car,
    Calendar,
    AlertCircle,
    RefreshCw,
    Search,
    ChevronLeft,
    ChevronRight,
    Clock,
} from "lucide-react";
import { ApiError } from "@/shared/errors/api-error";
import { useCustomerLanguage } from "./i18n/customerLanguage";

export default function ParkingHistoryPage() {
    const { copy, language } = useCustomerLanguage();
    const [sessions, setSessions] = useState<MyParkingSessionHistoryItem[]>([]);
    const [page, setPage] = useState(1);
    const [totalCount, setTotalCount] = useState(0);
    const [pageSize] = useState(10);
    const [statusFilter, setStatusFilter] = useState<string>("");
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const loadHistory = useCallback(async () => {
        setLoading(true);
        setError(null);
        try {
            const data = await getMyParkingSessionHistory(
                statusFilter || undefined,
                page,
                pageSize,
            );
            setSessions(data.items);
            setTotalCount(data.totalCount);
        } catch (err: unknown) {
            if (err instanceof ApiError) {
                setError(
                    err.status === 401
                        ? copy(
                              "Sign in to view your parking history.",
                              "Vui lòng đăng nhập để xem lịch sử đỗ xe.",
                          )
                        : copy(
                              "Could not load parking history. Please try again.",
                              "Không thể tải lịch sử đỗ xe. Vui lòng thử lại.",
                          ),
                );
            } else if (err instanceof TypeError) {
                setError(
                    copy(
                        "Could not connect to load parking history. Please try again later.",
                        "Không thể kết nối để tải lịch sử đỗ xe. Vui lòng thử lại sau.",
                    ),
                );
            } else {
                setError(
                    copy(
                        "An error occurred while loading parking history.",
                        "Đã xảy ra lỗi khi tải lịch sử đỗ xe.",
                    ),
                );
            }
        } finally {
            setLoading(false);
        }
    }, [copy, page, pageSize, statusFilter]);

    useEffect(() => {
        let active = true;
        queueMicrotask(() => {
            if (active) void loadHistory();
        });
        return () => {
            active = false;
        };
    }, [loadHistory]);

    const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

    return (
        <div className="pw-app-activity-content animate-in fade-in duration-500 space-y-6">
            <div className="pw-app-activity-summary flex items-center justify-between">
                <div>
                    <h1 className="text-2xl font-bold text-white mb-1">
                        {copy("Parking History", "Lịch sử đỗ xe")}
                    </h1>
                    <p className="text-[#8790a3] text-sm">
                        {copy(
                            "Your completed and active parking sessions across the ParkWise network.",
                            "Các phiên đỗ đang hoạt động và đã hoàn thành của bạn trong hệ thống ParkWise.",
                        )}
                    </p>
                </div>
                <div className="flex items-center gap-3">
                    <select
                        value={statusFilter}
                        onChange={(e) => {
                            setStatusFilter(e.target.value);
                            setPage(1);
                        }}
                        className="customer-data-filter"
                        aria-label={copy(
                            "Filter by status",
                            "Lọc theo trạng thái",
                        )}
                    >
                        <option value="">
                            {copy("All statuses", "Tất cả trạng thái")}
                        </option>
                        <option value="Active">
                            {copy("Active (in lot)", "Đang đỗ xe")}
                        </option>
                        <option value="Completed">
                            {copy("Completed", "Đã hoàn thành")}
                        </option>
                        <option value="Cancelled">
                            {copy("Cancelled", "Đã hủy")}
                        </option>
                    </select>
                    <ParkWiseAction
                        variant="secondary"
                        iconOnly
                        type="button"
                        onClick={loadHistory}
                        disabled={loading}

                        title={copy("Refresh history", "Làm mới lịch sử")}
                        aria-label={copy("Refresh history", "Làm mới lịch sử")}
                        aria-busy={loading}
                    >
                        <RefreshCw
                            size={18}
                            className={loading ? "animate-spin" : ""}
                        />
                    </ParkWiseAction>
                </div>
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
                    <ParkWiseAction
                        type="button"
                        onClick={loadHistory}
                        variant="secondary"
                    >
                        {copy("Try again", "Thử lại")}
                    </ParkWiseAction>
                </div>
            )}

            {/* Trạng thái Trống (Empty) */}
            {!loading && !error && sessions.length === 0 && (
                <div className="pw-app-empty customer-data-empty bg-[#0b0c17] border border-white/10 rounded-2xl p-12 text-center space-y-4 shadow-card-light">
                    <div className="w-16 h-16 rounded-full bg-[#6246ea]/10 border border-[#6246ea]/20 flex items-center justify-center mx-auto text-[#a28aff]">
                        <History
                            size={28}
                            className="pw-app-empty-desktop-icon"
                        />
                        <Car
                            size={64}
                            className="pw-app-empty-car"
                            aria-hidden="true"
                        />
                    </div>
                    <div>
                        <h3 className="text-lg font-bold text-white mb-1">
                            {copy(
                                "No parking history found",
                                "Không tìm thấy lịch sử đỗ xe",
                            )}
                        </h3>
                    </div>
                    <ParkWiseAction href="/search">
                        <Search size={16} />{" "}
                        {copy("Find nearby parking", "Tìm bãi đỗ gần đây")}
                    </ParkWiseAction>
                </div>
            )}

            {/* Danh sách Phiên xe thật */}
            {!loading && !error && sessions.length > 0 && (
                <>
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
                        {sessions.map((session) => {
                            const entryDate = new Date(session.entryTime);
                            const dateFormatted = entryDate.toLocaleDateString(
                                language === "vi" ? "vi-VN" : "en-GB",
                                {
                                    day: "2-digit",
                                    month: "2-digit",
                                    year: "numeric",
                                },
                            );
                            const entryTimeFormatted =
                                entryDate.toLocaleTimeString(
                                    language === "vi" ? "vi-VN" : "en-GB",
                                    {
                                        hour: "2-digit",
                                        minute: "2-digit",
                                    },
                                );
                            const exitTimeFormatted = session.exitTime
                                ? new Date(session.exitTime).toLocaleTimeString(
                                      language === "vi" ? "vi-VN" : "en-GB",
                                      {
                                          hour: "2-digit",
                                          minute: "2-digit",
                                      },
                                  )
                                : null;

                            const isActive = session.status === "Active";
                            const isCompleted = session.status === "Completed";

                            return (
                                <div
                                    key={session.sessionId}
                                    className="bg-[#0b0c17] border border-white/10 rounded-2xl p-6 shadow-card-light hover:border-white/20 transition-colors flex flex-col justify-between"
                                >
                                    <div>
                                        <div className="flex justify-between items-start mb-3">
                                            <span
                                                className={`px-2.5 py-1 rounded-md text-xs font-semibold ${
                                                    isActive
                                                        ? "bg-emerald-500/20 text-emerald-300 border border-emerald-500/30"
                                                        : isCompleted
                                                          ? "bg-blue-500/20 text-blue-300 border border-blue-500/30"
                                                          : "bg-white/10 text-[#d5d8e3] border border-white/15"
                                                }`}
                                            >
                                                {isActive
                                                    ? copy(
                                                          "Parking now",
                                                          "Đang đỗ xe",
                                                      )
                                                    : isCompleted
                                                      ? copy(
                                                            "Completed",
                                                            "Đã hoàn thành",
                                                        )
                                                      : copy(
                                                            session.status,
                                                            session.status,
                                                        )}
                                            </span>
                                            <span className="text-xs text-[#8790a3] flex items-center gap-1.5">
                                                <Calendar size={13} />{" "}
                                                {dateFormatted}
                                            </span>
                                        </div>

                                        <div className="flex items-center gap-3 mb-4">
                                            <div className="w-10 h-10 rounded-xl bg-white/5 border border-white/10 flex items-center justify-center text-white">
                                                <Car size={20} />
                                            </div>
                                            <div>
                                                <h2 className="text-base font-bold text-white tracking-wide">
                                                    {session.plateNumber}
                                                </h2>
                                                <p className="text-xs text-[#8790a3]">
                                                    {session.vehicleType ===
                                                    "Car"
                                                        ? copy("Car", "Ô tô")
                                                        : copy(
                                                              "Motorbike",
                                                              "Xe máy",
                                                          )}
                                                </p>
                                            </div>
                                        </div>

                                        <div className="grid grid-cols-2 gap-3 text-xs bg-white/[0.02] border border-white/5 rounded-xl p-3 mb-4">
                                            <div>
                                                <span className="text-[#8790a3] block mb-0.5">
                                                    {copy(
                                                        "Entry time",
                                                        "Giờ vào",
                                                    )}
                                                </span>
                                                <span className="text-white font-medium flex items-center gap-1">
                                                    <Clock
                                                        size={12}
                                                        className="text-[#6246ea]"
                                                    />
                                                    {entryTimeFormatted}
                                                </span>
                                            </div>
                                            <div>
                                                <span className="text-[#8790a3] block mb-0.5">
                                                    {copy(
                                                        "Exit time",
                                                        "Giờ ra",
                                                    )}
                                                </span>
                                                <span className="text-white font-medium">
                                                    {exitTimeFormatted ? (
                                                        <span className="flex items-center gap-1">
                                                            <Clock
                                                                size={12}
                                                                className="text-emerald-400"
                                                            />
                                                            {exitTimeFormatted}
                                                        </span>
                                                    ) : (
                                                        copy(
                                                            "In progress...",
                                                            "Đang diễn ra...",
                                                        )
                                                    )}
                                                </span>
                                            </div>
                                        </div>
                                    </div>

                                    <div className="pt-3 border-t border-white/5 flex justify-between items-center text-xs text-[#8790a3]">
                                        <span>
                                            ID: {session.sessionId.slice(0, 8)}
                                            ...
                                        </span>
                                        {isActive && (
                                            <Link
                                                href="/customer/sessions"
                                                className="text-[#a28aff] hover:underline font-semibold"
                                            >
                                                {copy(
                                                    "View exit QR",
                                                    "Xem mã QR ra bãi",
                                                )}{" "}
                                                &rarr;
                                            </Link>
                                        )}
                                        {isCompleted && (
                                            <Link
                                                href={`/customer/receipts/${session.sessionId}`}
                                                className="text-emerald-400 hover:underline font-semibold"
                                            >
                                                {copy(
                                                    "View receipt",
                                                    "Xem hóa đơn",
                                                )}{" "}
                                                &rarr;
                                            </Link>
                                        )}
                                    </div>
                                </div>
                            );
                        })}
                    </div>

                    {/* Phân trang */}
                    {totalPages > 1 && (
                        <div className="flex items-center justify-between pt-4 border-t border-white/10 text-xs text-[#8790a3]">
                            <span>
                                {copy(
                                    `Page ${page} of ${totalPages} (${totalCount} sessions)`,
                                    `Trang ${page} / ${totalPages} (Tổng ${totalCount} phiên)`,
                                )}
                            </span>
                            <div className="flex items-center gap-2">
                                <button
                                    type="button"
                                    onClick={() =>
                                        setPage((p) => Math.max(1, p - 1))
                                    }
                                    disabled={page <= 1}
                                    className="p-2 rounded-lg bg-white/5 border border-white/10 disabled:opacity-40 hover:bg-white/10 text-white transition-colors"
                                    aria-label="Previous page"
                                >
                                    <ChevronLeft size={16} />
                                </button>
                                <button
                                    type="button"
                                    onClick={() =>
                                        setPage((p) =>
                                            Math.min(totalPages, p + 1),
                                        )
                                    }
                                    disabled={page >= totalPages}
                                    className="p-2 rounded-lg bg-white/5 border border-white/10 disabled:opacity-40 hover:bg-white/10 text-white transition-colors"
                                    aria-label="Next page"
                                >
                                    <ChevronRight size={16} />
                                </button>
                            </div>
                        </div>
                    )}
                </>
            )}
        </div>
    );
}
