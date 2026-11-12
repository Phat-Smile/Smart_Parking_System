"use client";

import React, { useState, useEffect } from "react";
import Link from "next/link";
import { OwnerPageHeader } from "@/components/layout/OwnerPageHeader";
import { useAllLots } from "@/features/owner/lots/useOwnerMapStore";
import { backendLotId } from "@/features/owner/lots/ownerLotsSync";
import {
    configureOpeningHoursApi,
    getOpeningHoursApi,
    OpeningHoursDayItem,
} from "@/features/owner/api/ownerApi";
import { ApiError } from "@/shared/errors/api-error";

const DAY_NAMES = [
    "Chủ Nhật",
    "Thứ Hai",
    "Thứ Ba",
    "Thứ Tư",
    "Thứ Năm",
    "Thứ Sáu",
    "Thứ Bảy",
];

const DEFAULT_DAYS: OpeningHoursDayItem[] = [
    {
        dayOfWeek: 0,
        isClosed: false,
        is24Hours: false,
        opensAt: "06:00",
        closesAt: "22:00",
    },
    {
        dayOfWeek: 1,
        isClosed: false,
        is24Hours: false,
        opensAt: "06:00",
        closesAt: "23:00",
    },
    {
        dayOfWeek: 2,
        isClosed: false,
        is24Hours: false,
        opensAt: "06:00",
        closesAt: "23:00",
    },
    {
        dayOfWeek: 3,
        isClosed: false,
        is24Hours: false,
        opensAt: "06:00",
        closesAt: "23:00",
    },
    {
        dayOfWeek: 4,
        isClosed: false,
        is24Hours: false,
        opensAt: "06:00",
        closesAt: "23:00",
    },
    {
        dayOfWeek: 5,
        isClosed: false,
        is24Hours: false,
        opensAt: "06:00",
        closesAt: "23:00",
    },
    {
        dayOfWeek: 6,
        isClosed: false,
        is24Hours: false,
        opensAt: "06:00",
        closesAt: "23:00",
    },
];

// 48 distinct 30-min intervals throughout 24 hours
const TIME_OPTIONS: string[] = [];
for (let h = 0; h < 24; h++) {
    const hh = String(h).padStart(2, "0");
    TIME_OPTIONS.push(`${hh}:00`);
    TIME_OPTIONS.push(`${hh}:30`);
}
TIME_OPTIONS.push("23:59");

function formatTimeLabel(timeStr: string): string {
    if (timeStr === "23:59") return "23:59 (Hết ngày)";
    const [hStr, mStr] = timeStr.split(":");
    const h = parseInt(hStr, 10);
    if (h === 0 && mStr === "00") return "00:00 (Nửa đêm)";
    if (h === 12 && mStr === "00") return "12:00 (Trưa)";
    if (h < 12) return `${timeStr} (Sáng)`;
    if (h < 18) return `${timeStr} (Chiều)`;
    return `${timeStr} (Tối)`;
}

function calculateDuration(
    opensAt?: string | null,
    closesAt?: string | null,
): string {
    if (!opensAt || !closesAt) return "";
    const [h1, m1] = opensAt.split(":").map(Number);
    let [h2, m2] = closesAt.split(":").map(Number);
    if (closesAt === "23:59") {
        h2 = 24;
        m2 = 0;
    }
    let diffMin = h2 * 60 + m2 - (h1 * 60 + m1);
    if (diffMin < 0) diffMin += 24 * 60; // qua đêm
    const hours = Math.floor(diffMin / 60);
    const mins = diffMin % 60;
    if (mins === 0) return `${hours} tiếng/ngày`;
    return `${hours}h ${mins}p/ngày`;
}

function ensureTimeInOptions(
    time: string | null | undefined,
    list: string[],
): string[] {
    if (!time) return list;
    if (list.includes(time)) return list;
    return [...list, time].sort();
}

export default function OwnerOpeningHoursPage() {
    const lots = useAllLots();
    const [selectedLotId, setSelectedLotId] = useState<number>(
        lots[0]?.id || 1,
    );
    const [days, setDays] = useState<OpeningHoursDayItem[]>(DEFAULT_DAYS);
    const [isLoading, setIsLoading] = useState(false);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [successMessage, setSuccessMessage] = useState("");
    const [errorMessage, setErrorMessage] = useState("");

    // Bulk hours config state
    const [bulkOpensAt, setBulkOpensAt] = useState("06:00");
    const [bulkClosesAt, setBulkClosesAt] = useState("23:00");

    const currentLot = lots.find((l) => l.id === selectedLotId) || lots[0];
    // Parking keys lots by GUID; a design lot without one cannot be saved.
    const lotKey = backendLotId(currentLot);

    // Load opening hours for selected lot
    useEffect(() => {
        async function loadHours() {
            setErrorMessage("");
            setSuccessMessage("");
            if (!lotKey) {
                setDays(DEFAULT_DAYS);
                return;
            }
            setIsLoading(true);
            try {
                const res = await getOpeningHoursApi(lotKey);
                if (res?.days && res.days.length === 7) {
                    const sorted = [...res.days].sort(
                        (a, b) => a.dayOfWeek - b.dayOfWeek,
                    );
                    setDays(sorted);
                    return;
                }
                // Not configured yet: start from the default week.
                setDays(DEFAULT_DAYS);
            } catch (err: unknown) {
                setDays(DEFAULT_DAYS);
                setErrorMessage(
                    err instanceof ApiError
                        ? err.message ||
                              "Không tải được giờ hoạt động của bãi này."
                        : "Không kết nối được máy chủ để tải giờ hoạt động.",
                );
            }
        }

        void loadHours().finally(() => setIsLoading(false));
    }, [lotKey]);

    const handleUpdateDay = (
        index: number,
        updates: Partial<OpeningHoursDayItem>,
    ) => {
        setDays((prev) => {
            const next = [...prev];
            const cur = { ...next[index], ...updates };

            if (cur.isClosed) {
                cur.is24Hours = false;
                cur.opensAt = null;
                cur.closesAt = null;
            } else if (cur.is24Hours) {
                cur.isClosed = false;
                cur.opensAt = null;
                cur.closesAt = null;
            } else {
                if (!cur.opensAt) cur.opensAt = "06:00";
                if (!cur.closesAt) cur.closesAt = "23:00";
            }

            next[index] = cur;
            return next;
        });
    };

    const handleApplyPreset = (
        index: number,
        opensAt: string,
        closesAt: string,
    ) => {
        handleUpdateDay(index, {
            isClosed: false,
            is24Hours: false,
            opensAt,
            closesAt,
        });
    };

    const handleApplyBulkWeekdays = () => {
        setDays((prev) =>
            prev.map((d) => {
                if (d.dayOfWeek >= 1 && d.dayOfWeek <= 5) {
                    return {
                        ...d,
                        isClosed: false,
                        is24Hours: false,
                        opensAt: bulkOpensAt,
                        closesAt: bulkClosesAt,
                    };
                }
                return d;
            }),
        );
        setSuccessMessage(
            `Đã áp dụng khung giờ ${bulkOpensAt} - ${bulkClosesAt} cho các ngày từ Thứ 2 đến Thứ 6.`,
        );
    };

    const handleApplyBulkAllDays = () => {
        setDays((prev) =>
            prev.map((d) => ({
                ...d,
                isClosed: false,
                is24Hours: false,
                opensAt: bulkOpensAt,
                closesAt: bulkClosesAt,
            })),
        );
        setSuccessMessage(
            `Đã áp dụng khung giờ ${bulkOpensAt} - ${bulkClosesAt} cho toàn bộ 7 ngày trong tuần.`,
        );
    };

    const handleSetAll24_7 = () => {
        setDays((prev) =>
            prev.map((d) => ({
                ...d,
                isClosed: false,
                is24Hours: true,
                opensAt: null,
                closesAt: null,
            })),
        );
        setSuccessMessage("Đã chuyển toàn bộ 7 ngày sang chế độ Mở cửa 24/7.");
    };

    const handleSetWeekendClosed = () => {
        setDays((prev) =>
            prev.map((d) => {
                if (d.dayOfWeek === 0 || d.dayOfWeek === 6) {
                    return {
                        ...d,
                        isClosed: true,
                        is24Hours: false,
                        opensAt: null,
                        closesAt: null,
                    };
                }
                return d;
            }),
        );
        setSuccessMessage("Đã đặt Thứ Bảy và Chủ Nhật sang chế độ Đóng cửa.");
    };

    const handleSave = async () => {
        setErrorMessage("");
        setSuccessMessage("");
        if (!lotKey) {
            setErrorMessage(
                "Bãi này chưa có trên hệ thống nên không lưu được. Hãy chọn một bãi bạn đã đăng ký.",
            );
            return;
        }
        setIsSubmitting(true);

        try {
            await configureOpeningHoursApi(lotKey, { days });
            setSuccessMessage(
                "Đã lưu và đồng bộ lịch hoạt động 7 ngày lên máy chủ bãi xe thành công!",
            );
        } catch (err: unknown) {
            if (err instanceof ApiError) {
                if (err.status === 400) {
                    setErrorMessage(
                        err.problem?.detail ||
                            "Lịch mở cửa không đúng định dạng yêu cầu.",
                    );
                } else if (err.status === 403) {
                    setErrorMessage(
                        "Tài khoản của bạn không có quyền sửa đổi giờ hoạt động của bãi này.",
                    );
                } else {
                    setErrorMessage(
                        err.message ||
                            "Không thể cập nhật giờ hoạt động lúc này.",
                    );
                }
            } else {
                setErrorMessage(
                    "Không kết nối được máy chủ. Giờ hoạt động chưa được lưu.",
                );
            }
        } finally {
            setIsSubmitting(false);
        }
    };

    return (
        <div style={{ maxWidth: 1400, margin: "0 auto", paddingBottom: 64 }}>
            <OwnerPageHeader
                breadcrumb="/owner/opening-hours"
                title="Giờ hoạt động bãi xe"
                subtitle="Thiết lập lịch mở cửa và đóng cửa chi tiết 7 ngày trong tuần theo chuẩn US-OWNER-002"
                actions={
                    <div
                        style={{
                            display: "flex",
                            gap: 10,
                            alignItems: "center",
                        }}
                    >
                        <Link
                            href="/owner/parking-lots"
                            style={{
                                display: "inline-flex",
                                alignItems: "center",
                                padding: "9px 16px",
                                borderRadius: 10,
                                border: "1px solid #E4E4E7",
                                background: "#FFFFFF",
                                color: "#09090B",
                                fontWeight: 600,
                                fontSize: 13,
                                textDecoration: "none",
                            }}
                        >
                            ← Quay lại danh sách bãi
                        </Link>
                        <button
                            type="button"
                            onClick={handleSave}
                            disabled={isSubmitting}
                            style={{
                                display: "inline-flex",
                                alignItems: "center",
                                gap: 8,
                                padding: "9px 20px",
                                borderRadius: 10,
                                border: "none",
                                background: "#E6F85E",
                                color: "#09090B",
                                fontWeight: 700,
                                fontSize: 13,
                                cursor: isSubmitting ? "wait" : "pointer",
                                boxShadow: "0 2px 8px rgba(0,0,0,0.06)",
                            }}
                        >
                            {isSubmitting ? "Đang lưu..." : "Lưu giờ hoạt động"}
                        </button>
                    </div>
                }
            />

            {successMessage && (
                <div
                    style={{
                        marginBottom: 18,
                        padding: "12px 16px",
                        borderRadius: 12,
                        background: "#F0FDF4",
                        border: "1px solid #BBF7D0",
                        color: "#15803D",
                        fontSize: 13.5,
                        fontWeight: 600,
                        display: "flex",
                        alignItems: "center",
                        gap: 8,
                    }}
                >
                    <span>✓</span> {successMessage}
                </div>
            )}

            {errorMessage && (
                <div
                    style={{
                        marginBottom: 18,
                        padding: "12px 16px",
                        borderRadius: 12,
                        background: "#FEF2F2",
                        border: "1px solid #FECACA",
                        color: "#B91C1C",
                        fontSize: 13.5,
                        fontWeight: 600,
                        display: "flex",
                        alignItems: "center",
                        gap: 8,
                    }}
                >
                    <span>⚠️</span> {errorMessage}
                </div>
            )}

            {/* Lot Selector Card */}
            <div
                style={{
                    background: "#FFFFFF",
                    borderRadius: 16,
                    border: "1px solid #E4E4E7",
                    padding: "18px 22px",
                    marginBottom: 20,
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    flexWrap: "wrap",
                    gap: 14,
                    boxShadow: "0 1px 3px rgba(0,0,0,0.04)",
                }}
            >
                <div style={{ display: "flex", alignItems: "center", gap: 14 }}>
                    <div>
                        <div
                            style={{
                                fontSize: 12,
                                color: "#71717A",
                                fontWeight: 600,
                                textTransform: "uppercase",
                                letterSpacing: 0.5,
                            }}
                        >
                            Bãi xe đang cấu hình
                        </div>
                        <div
                            style={{
                                fontSize: 16,
                                fontWeight: 800,
                                color: "#09090B",
                                marginTop: 2,
                            }}
                        >
                            {currentLot?.name || "Chưa chọn bãi"}
                        </div>
                    </div>
                </div>

                <div
                    style={{
                        display: "flex",
                        alignItems: "center",
                        gap: 10,
                        flexWrap: "wrap",
                    }}
                >
                    <label
                        htmlFor="lot-select"
                        style={{
                            fontSize: 13,
                            fontWeight: 700,
                            color: "#09090B",
                        }}
                    >
                        Đổi bãi xe:
                    </label>
                    <select
                        id="lot-select"
                        value={selectedLotId}
                        onChange={(e) =>
                            setSelectedLotId(Number(e.target.value))
                        }
                        style={{
                            padding: "9px 14px",
                            borderRadius: 10,
                            border: "1.5px solid #E4E4E7",
                            fontSize: 13.5,
                            fontWeight: 700,
                            color: "#09090B",
                            background: "#FFFFFF",
                            outline: "none",
                            cursor: "pointer",
                        }}
                    >
                        {lots.map((l) => (
                            <option key={l.id} value={l.id}>
                                {l.name} (
                                {l.ap === "approved"
                                    ? "Đang hoạt động"
                                    : "Chờ duyệt"}
                                )
                            </option>
                        ))}
                    </select>
                </div>
            </div>

            {/* Bulk Quick Config Panel */}
            <div
                style={{
                    background: "#FAFAFA",
                    borderRadius: 16,
                    border: "1px solid #E4E4E7",
                    padding: "20px 22px",
                    marginBottom: 24,
                    boxShadow: "0 1px 3px rgba(0,0,0,0.02)",
                }}
            >
                <div
                    style={{
                        marginBottom: 14,
                    }}
                >
                    <h4
                        style={{
                            margin: 0,
                            fontSize: 14.5,
                            fontWeight: 800,
                            color: "#09090B",
                        }}
                    >
                        Cấu hình khung giờ hàng loạt (Áp dụng nhanh nhiều ngày)
                    </h4>
                </div>

                <div
                    style={{
                        display: "flex",
                        alignItems: "center",
                        gap: 14,
                        flexWrap: "wrap",
                    }}
                >
                    <div
                        style={{
                            display: "flex",
                            alignItems: "center",
                            gap: 8,
                        }}
                    >
                        <span
                            style={{
                                fontSize: 13,
                                fontWeight: 700,
                                color: "#52525B",
                            }}
                        >
                            Giờ mở cửa:
                        </span>
                        <input
                            type="time"
                            aria-label="Giờ mở cửa hàng loạt"
                            value={bulkOpensAt}
                            onChange={(e) => setBulkOpensAt(e.target.value)}
                            style={{
                                padding: "8px 12px",
                                borderRadius: 8,
                                border: "1.5px solid #D4D4D8",
                                background: "#FFFFFF",
                                color: "#09090B",
                                fontSize: 13,
                                fontWeight: 700,
                                outline: "none",
                                cursor: "pointer",
                            }}
                        />
                    </div>

                    <div
                        style={{
                            display: "flex",
                            alignItems: "center",
                            gap: 8,
                        }}
                    >
                        <span
                            style={{
                                fontSize: 13,
                                fontWeight: 700,
                                color: "#52525B",
                            }}
                        >
                            Giờ đóng cửa:
                        </span>
                        <input
                            type="time"
                            aria-label="Giờ đóng cửa hàng loạt"
                            value={bulkClosesAt}
                            onChange={(e) => setBulkClosesAt(e.target.value)}
                            style={{
                                padding: "8px 12px",
                                borderRadius: 8,
                                border: "1.5px solid #D4D4D8",
                                background: "#FFFFFF",
                                color: "#09090B",
                                fontSize: 13,
                                fontWeight: 700,
                                outline: "none",
                                cursor: "pointer",
                            }}
                        />
                    </div>

                    <div
                        style={{
                            display: "flex",
                            gap: 8,
                            flexWrap: "wrap",
                            marginLeft: "auto",
                        }}
                    >
                        <button
                            type="button"
                            onClick={handleApplyBulkWeekdays}
                            style={{
                                background: "#09090B",
                                color: "#FFFFFF",
                                border: "1px solid #09090B",
                                borderRadius: 8,
                                padding: "8px 14px",
                                fontSize: 12.5,
                                fontWeight: 700,
                                cursor: "pointer",
                                display: "inline-flex",
                                alignItems: "center",
                                gap: 6,
                            }}
                        >
                            Áp dụng Thứ 2 - Thứ 6 (T2 - T6)
                        </button>
                        <button
                            type="button"
                            onClick={handleApplyBulkAllDays}
                            style={{
                                background: "#E6F85E",
                                color: "#09090B",
                                border: "1px solid #E6F85E",
                                borderRadius: 8,
                                padding: "8px 14px",
                                fontSize: 12.5,
                                fontWeight: 800,
                                cursor: "pointer",
                                display: "inline-flex",
                                alignItems: "center",
                                gap: 6,
                            }}
                        >
                            Áp dụng Cả tuần (7 ngày)
                        </button>
                        <button
                            type="button"
                            onClick={handleSetAll24_7}
                            style={{
                                background: "#ECFDF5",
                                color: "#059669",
                                border: "1px solid #A7F3D0",
                                borderRadius: 8,
                                padding: "8px 14px",
                                fontSize: 12.5,
                                fontWeight: 700,
                                cursor: "pointer",
                            }}
                        >
                            Mở 24/7 cả tuần
                        </button>
                        <button
                            type="button"
                            onClick={handleSetWeekendClosed}
                            style={{
                                background: "#FEF2F2",
                                color: "#DC2626",
                                border: "1px solid #FECACA",
                                borderRadius: 8,
                                padding: "8px 14px",
                                fontSize: 12.5,
                                fontWeight: 700,
                                cursor: "pointer",
                            }}
                        >
                            Nghỉ Cuối tuần
                        </button>
                    </div>
                </div>
            </div>

            {/* Schedule Table Card */}
            <div
                style={{
                    background: "#FFFFFF",
                    borderRadius: 16,
                    border: "1px solid #E4E4E7",
                    padding: "24px 26px",
                    boxShadow: "0 2px 10px rgba(0,0,0,0.03)",
                }}
            >
                <div
                    style={{
                        display: "flex",
                        justifyContent: "space-between",
                        alignItems: "center",
                        marginBottom: 20,
                    }}
                >
                    <div>
                        <h3
                            style={{
                                margin: "0 0 4px",
                                fontSize: 16,
                                fontWeight: 800,
                                color: "#09090B",
                            }}
                        >
                            Lịch hoạt động chi tiết từng ngày
                        </h3>
                        <p
                            style={{
                                margin: 0,
                                fontSize: 13,
                                color: "#71717A",
                            }}
                        >
                            Chọn chế độ hoạt động và điều chỉnh thời gian mở -
                            đóng cửa cho từng ngày trong tuần.
                        </p>
                    </div>
                    <span
                        style={{
                            background: "#F4F4F5",
                            border: "1px solid #E4E4E7",
                            borderRadius: 8,
                            padding: "4px 10px",
                            fontSize: 12,
                            fontWeight: 700,
                            color: "#52525B",
                        }}
                    >
                        Định dạng 24 giờ
                    </span>
                </div>

                {isLoading ? (
                    <div
                        style={{
                            textAlign: "center",
                            padding: "48px 0",
                            color: "#71717A",
                        }}
                    >
                        Đang tải dữ liệu giờ hoạt động bãi xe...
                    </div>
                ) : (
                    <div
                        style={{
                            display: "flex",
                            flexDirection: "column",
                            gap: 12,
                        }}
                    >
                        {days.map((d, idx) => {
                            const isWeekend =
                                d.dayOfWeek === 0 || d.dayOfWeek === 6;
                            const duration = calculateDuration(
                                d.opensAt,
                                d.closesAt,
                            );
                            const opensOptions = ensureTimeInOptions(
                                d.opensAt,
                                TIME_OPTIONS,
                            );
                            const closesOptions = ensureTimeInOptions(
                                d.closesAt,
                                TIME_OPTIONS,
                            );

                            return (
                                <div
                                    key={d.dayOfWeek}
                                    style={{
                                        display: "grid",
                                        gridTemplateColumns:
                                            "minmax(130px, 1.2fr) minmax(260px, 2fr) minmax(420px, 3.8fr)",
                                        alignItems: "center",
                                        gap: 16,
                                        padding: "16px 20px",
                                        borderRadius: 12,
                                        background: isWeekend
                                            ? "#FAFAFA"
                                            : "#FFFFFF",
                                        border: "1.5px solid #E4E4E7",
                                        transition: "all 0.2s ease",
                                    }}
                                >
                                    {/* Cột 1: Tên ngày */}
                                    <div
                                        style={{
                                            display: "flex",
                                            alignItems: "center",
                                            gap: 10,
                                        }}
                                    >
                                        <span
                                            style={{
                                                width: 9,
                                                height: 9,
                                                borderRadius: "50%",
                                                background: d.isClosed
                                                    ? "#EF4444"
                                                    : d.is24Hours
                                                      ? "#10B981"
                                                      : "#09090B",
                                                flexShrink: 0,
                                            }}
                                        />
                                        <div>
                                            <div
                                                style={{
                                                    fontWeight: 800,
                                                    fontSize: 14.5,
                                                    color: "#09090B",
                                                }}
                                            >
                                                {DAY_NAMES[d.dayOfWeek]}
                                            </div>
                                            {isWeekend && (
                                                <span
                                                    style={{
                                                        fontSize: 10.5,
                                                        padding: "1px 6px",
                                                        borderRadius: 4,
                                                        background: "#FEF3C7",
                                                        color: "#92400E",
                                                        fontWeight: 700,
                                                        display: "inline-block",
                                                        marginTop: 2,
                                                    }}
                                                >
                                                    Cuối tuần
                                                </span>
                                            )}
                                        </div>
                                    </div>

                                    {/* Cột 2: Chọn Chế độ */}
                                    <div
                                        style={{
                                            display: "flex",
                                            gap: 8,
                                            flexWrap: "wrap",
                                        }}
                                    >
                                        <button
                                            type="button"
                                            onClick={() =>
                                                handleUpdateDay(idx, {
                                                    isClosed: false,
                                                    is24Hours: false,
                                                })
                                            }
                                            style={{
                                                padding: "7px 12px",
                                                borderRadius: 8,
                                                border: "1.5px solid",
                                                borderColor:
                                                    !d.isClosed && !d.is24Hours
                                                        ? "#09090B"
                                                        : "#E4E4E7",
                                                background:
                                                    !d.isClosed && !d.is24Hours
                                                        ? "#09090B"
                                                        : "#FFFFFF",
                                                color:
                                                    !d.isClosed && !d.is24Hours
                                                        ? "#E6F85E"
                                                        : "#71717A",
                                                fontSize: 12.5,
                                                fontWeight: 700,
                                                cursor: "pointer",
                                                display: "inline-flex",
                                                alignItems: "center",
                                                gap: 5,
                                            }}
                                        >
                                            Theo khung giờ
                                        </button>

                                        <button
                                            type="button"
                                            onClick={() =>
                                                handleUpdateDay(idx, {
                                                    isClosed: false,
                                                    is24Hours: true,
                                                })
                                            }
                                            style={{
                                                padding: "7px 12px",
                                                borderRadius: 8,
                                                border: "1.5px solid",
                                                borderColor: d.is24Hours
                                                    ? "#10B981"
                                                    : "#E4E4E7",
                                                background: d.is24Hours
                                                    ? "#ECFDF5"
                                                    : "#FFFFFF",
                                                color: d.is24Hours
                                                    ? "#059669"
                                                    : "#71717A",
                                                fontSize: 12.5,
                                                fontWeight: 700,
                                                cursor: "pointer",
                                            }}
                                        >
                                            Mở 24/7
                                        </button>

                                        <button
                                            type="button"
                                            onClick={() =>
                                                handleUpdateDay(idx, {
                                                    isClosed: true,
                                                    is24Hours: false,
                                                })
                                            }
                                            style={{
                                                padding: "7px 12px",
                                                borderRadius: 8,
                                                border: "1.5px solid",
                                                borderColor: d.isClosed
                                                    ? "#EF4444"
                                                    : "#E4E4E7",
                                                background: d.isClosed
                                                    ? "#FEF2F2"
                                                    : "#FFFFFF",
                                                color: d.isClosed
                                                    ? "#DC2626"
                                                    : "#71717A",
                                                fontSize: 12.5,
                                                fontWeight: 700,
                                                cursor: "pointer",
                                            }}
                                        >
                                            Đóng cửa
                                        </button>
                                    </div>

                                    {/* Cột 3: Bộ Chỉnh sửa Giờ */}
                                    <div>
                                        {d.isClosed ? (
                                            <div
                                                style={{
                                                    display: "inline-flex",
                                                    alignItems: "center",
                                                    gap: 6,
                                                    padding: "6px 12px",
                                                    background: "#FEF2F2",
                                                    border: "1px solid #FECACA",
                                                    borderRadius: 8,
                                                    fontSize: 13,
                                                    color: "#DC2626",
                                                    fontWeight: 700,
                                                }}
                                            >
                                                Bãi xe tạm ngừng đón khách ngày
                                                này
                                            </div>
                                        ) : d.is24Hours ? (
                                            <div
                                                style={{
                                                    display: "inline-flex",
                                                    alignItems: "center",
                                                    gap: 6,
                                                    padding: "6px 12px",
                                                    background: "#ECFDF5",
                                                    border: "1px solid #A7F3D0",
                                                    borderRadius: 8,
                                                    fontSize: 13,
                                                    color: "#059669",
                                                    fontWeight: 700,
                                                }}
                                            >
                                                Phục vụ liên tục cả ngày và đêm
                                                (24/24)
                                            </div>
                                        ) : (
                                            <div
                                                style={{
                                                    display: "flex",
                                                    flexDirection: "column",
                                                    gap: 8,
                                                }}
                                            >
                                                {/* Dropdown chỉnh giờ mở - đóng */}
                                                <div
                                                    style={{
                                                        display: "flex",
                                                        alignItems: "center",
                                                        gap: 10,
                                                        flexWrap: "wrap",
                                                    }}
                                                >
                                                    <div
                                                        style={{
                                                            display: "flex",
                                                            alignItems:
                                                                "center",
                                                            gap: 6,
                                                        }}
                                                    >
                                                        <span
                                                            style={{
                                                                fontSize: 13,
                                                                fontWeight: 700,
                                                                color: "#52525B",
                                                            }}
                                                        >
                                                            Từ:
                                                        </span>
                                                        <input
                                                            type="time"
                                                            aria-label={`Giờ mở cửa ${DAY_NAMES[d.dayOfWeek]}`}
                                                            value={
                                                                d.opensAt ||
                                                                "06:00"
                                                            }
                                                            onChange={(e) =>
                                                                handleUpdateDay(
                                                                    idx,
                                                                    {
                                                                        opensAt:
                                                                            e
                                                                                .target
                                                                                .value,
                                                                    },
                                                                )
                                                            }
                                                            style={{
                                                                padding:
                                                                    "7px 10px",
                                                                borderRadius: 8,
                                                                border: "1.5px solid #CBD5E1",
                                                                background:
                                                                    "#FFFFFF",
                                                                color: "#09090B",
                                                                fontSize: 13.5,
                                                                fontWeight: 700,
                                                                outline: "none",
                                                                cursor: "pointer",
                                                            }}
                                                        />
                                                    </div>

                                                    <div
                                                        style={{
                                                            display: "flex",
                                                            alignItems:
                                                                "center",
                                                            gap: 6,
                                                        }}
                                                    >
                                                        <span
                                                            style={{
                                                                fontSize: 13,
                                                                fontWeight: 700,
                                                                color: "#52525B",
                                                            }}
                                                        >
                                                            Đến:
                                                        </span>
                                                        <input
                                                            type="time"
                                                            aria-label={`Giờ đóng cửa ${DAY_NAMES[d.dayOfWeek]}`}
                                                            value={
                                                                d.closesAt ||
                                                                "23:00"
                                                            }
                                                            onChange={(e) =>
                                                                handleUpdateDay(
                                                                    idx,
                                                                    {
                                                                        closesAt:
                                                                            e
                                                                                .target
                                                                                .value,
                                                                    },
                                                                )
                                                            }
                                                            style={{
                                                                padding:
                                                                    "7px 10px",
                                                                borderRadius: 8,
                                                                border: "1.5px solid #CBD5E1",
                                                                background:
                                                                    "#FFFFFF",
                                                                color: "#09090B",
                                                                fontSize: 13.5,
                                                                fontWeight: 700,
                                                                outline: "none",
                                                                cursor: "pointer",
                                                            }}
                                                        />
                                                    </div>

                                                    {/* Thời lượng hoạt động */}
                                                    {duration && (
                                                        <span
                                                            style={{
                                                                background:
                                                                    "#F4F4F5",
                                                                border: "1px solid #E4E4E7",
                                                                color: "#18181B",
                                                                fontSize: 11.5,
                                                                fontWeight: 700,
                                                                padding:
                                                                    "4px 8px",
                                                                borderRadius: 6,
                                                            }}
                                                        >
                                                            {duration}
                                                        </span>
                                                    )}
                                                </div>

                                                {/* Presets chỉnh nhanh cho ngày này */}
                                                <div
                                                    style={{
                                                        display: "flex",
                                                        alignItems: "center",
                                                        gap: 6,
                                                        flexWrap: "wrap",
                                                    }}
                                                >
                                                    <span
                                                        style={{
                                                            fontSize: 11,
                                                            color: "#A1A1AA",
                                                            fontWeight: 600,
                                                        }}
                                                    >
                                                        Gợi ý nhanh:
                                                    </span>
                                                    <button
                                                        type="button"
                                                        onClick={() =>
                                                            handleApplyPreset(
                                                                idx,
                                                                "06:00",
                                                                "22:00",
                                                            )
                                                        }
                                                        style={{
                                                            background:
                                                                "#FFFFFF",
                                                            border: "1px dashed #CBD5E1",
                                                            borderRadius: 6,
                                                            padding: "2px 8px",
                                                            fontSize: 11,
                                                            fontWeight: 700,
                                                            color: "#52525B",
                                                            cursor: "pointer",
                                                        }}
                                                    >
                                                        06:00 - 22:00
                                                    </button>
                                                    <button
                                                        type="button"
                                                        onClick={() =>
                                                            handleApplyPreset(
                                                                idx,
                                                                "06:00",
                                                                "23:00",
                                                            )
                                                        }
                                                        style={{
                                                            background:
                                                                "#FFFFFF",
                                                            border: "1px dashed #CBD5E1",
                                                            borderRadius: 6,
                                                            padding: "2px 8px",
                                                            fontSize: 11,
                                                            fontWeight: 700,
                                                            color: "#52525B",
                                                            cursor: "pointer",
                                                        }}
                                                    >
                                                        06:00 - 23:00
                                                    </button>
                                                    <button
                                                        type="button"
                                                        onClick={() =>
                                                            handleApplyPreset(
                                                                idx,
                                                                "07:00",
                                                                "21:00",
                                                            )
                                                        }
                                                        style={{
                                                            background:
                                                                "#FFFFFF",
                                                            border: "1px dashed #CBD5E1",
                                                            borderRadius: 6,
                                                            padding: "2px 8px",
                                                            fontSize: 11,
                                                            fontWeight: 700,
                                                            color: "#52525B",
                                                            cursor: "pointer",
                                                        }}
                                                    >
                                                        07:00 - 21:00
                                                    </button>
                                                    <button
                                                        type="button"
                                                        onClick={() =>
                                                            handleApplyPreset(
                                                                idx,
                                                                "08:00",
                                                                "18:00",
                                                            )
                                                        }
                                                        style={{
                                                            background:
                                                                "#FFFFFF",
                                                            border: "1px dashed #CBD5E1",
                                                            borderRadius: 6,
                                                            padding: "2px 8px",
                                                            fontSize: 11,
                                                            fontWeight: 700,
                                                            color: "#52525B",
                                                            cursor: "pointer",
                                                        }}
                                                    >
                                                        08:00 - 18:00 (Hành
                                                        chính)
                                                    </button>
                                                </div>
                                            </div>
                                        )}
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                )}

                <div
                    style={{
                        marginTop: 28,
                        display: "flex",
                        justifyContent: "flex-end",
                        gap: 10,
                    }}
                >
                    <button
                        type="button"
                        onClick={handleSave}
                        disabled={isSubmitting}
                        style={{
                            padding: "12px 28px",
                            borderRadius: 12,
                            border: "none",
                            background: "#E6F85E",
                            color: "#09090B",
                            fontSize: 14,
                            fontWeight: 800,
                            cursor: isSubmitting ? "wait" : "pointer",
                            boxShadow: "0 4px 14px rgba(0,0,0,0.08)",
                        }}
                    >
                        {isSubmitting
                            ? "Đang lưu..."
                            : "Lưu giờ hoạt động lên hệ thống"}
                    </button>
                </div>
            </div>
        </div>
    );
}
