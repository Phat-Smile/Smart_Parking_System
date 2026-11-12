"use client";

import React, { useState } from "react";
import Link from "next/link";
import { useAllLots, useOwnerMapStore } from "./useOwnerMapStore";
import {
    useLotInfo,
    useLotInfoStore,
    formatVnd,
    formatDateVi,
    formatDurationSince,
} from "./lotInfo";
import { getLotCoordinates } from "./lotGeo";
import { AP } from "@/types/owner";

export function LotStatusPanel() {
    const lots = useAllLots();
    const { selectedLotId, updateLotStatus } = useOwnerMapStore();
    const selectedLot = lots.find((l) => l.id === selectedLotId);
    const lotInfo = useLotInfo(selectedLotId);
    const resubmitLot = useLotInfoStore((s) => s.resubmitLot);

    const [toastMessage, setToastMessage] = useState<string | null>(null);

    const showToast = (msg: string) => {
        setToastMessage(msg);
        setTimeout(() => setToastMessage(null), 3000);
    };

    const handleResubmit = () => {
        if (!selectedLot) return;
        resubmitLot(selectedLot.id);
        updateLotStatus(selectedLot.id, "pending");
        showToast("Đã gửi lại hồ sơ bãi đỗ xe thành công!");
    };

    const coords = selectedLot ? getLotCoordinates(selectedLot.id) : null;
    const googleMapsUrl = coords
        ? `https://www.google.com/maps/search/?api=1&query=${coords[1]},${coords[0]}`
        : "#";

    // Format date and duration
    let dateTitle = "Hoạt động từ:";
    let dateValue = "Chưa cập nhật";
    if (selectedLot?.ap === "approved") {
        dateTitle = "Hoạt động từ:";
        const dStr = formatDateVi(lotInfo?.openedAt);
        const duration = formatDurationSince(lotInfo?.openedAt);
        dateValue = duration ? `${dStr} (${duration})` : dStr;
    } else if (selectedLot?.ap === "pending") {
        dateTitle = "Gửi hồ sơ ngày:";
        dateValue = formatDateVi(lotInfo?.submittedAt);
    } else if (selectedLot?.ap === "rejected") {
        dateTitle = "Bị từ chối ngày:";
        dateValue = formatDateVi(lotInfo?.rejectedAt);
    }

    const isApproved = selectedLot?.ap === "approved";
    const isPending = selectedLot?.ap === "pending";
    const isRejected = selectedLot?.ap === "rejected";

    const priceSectionTitle = isApproved
        ? "Bảng giá giữ xe"
        : "Bảng giá đăng ký";

    return (
        <div
            className="ov2-card"
            style={{
                background: "#fff",
                borderRadius: 18,
                border: "1px solid #e3e8f2",
                padding: 24,
                display: "flex",
                flexDirection: "column",
                maxHeight: "calc(100vh - 140px)",
                overflowY: "auto",
            }}
        >
            <div
                style={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    marginBottom: 16,
                }}
            >
                <h2
                    style={{
                        fontSize: 18,
                        fontWeight: 800,
                        margin: 0,
                        color: "#0f1b3d",
                    }}
                >
                    Trạng thái bãi
                </h2>
                {selectedLot && (
                    <span
                        style={{
                            background: isApproved
                                ? "#def7ec"
                                : isPending
                                  ? "#fef3c7"
                                  : "#fde8e8",
                            color: isApproved
                                ? "#0e9f6e"
                                : isPending
                                  ? "#b45309"
                                  : "#e5484d",
                            borderRadius: 999,
                            padding: "3px 12px",
                            fontSize: 11.5,
                            fontWeight: 700,
                        }}
                    >
                        {AP[selectedLot.ap]
                            ? AP[selectedLot.ap][0]
                            : selectedLot.ap}
                    </span>
                )}
            </div>

            {!selectedLot ? (
                <div
                    style={{
                        flex: 1,
                        display: "flex",
                        flexDirection: "column",
                        justifyContent: "center",
                        color: "#6a7491",
                        fontSize: 13.5,
                        lineHeight: 1.6,
                        minHeight: 280,
                    }}
                >
                    Bấm vào một ghim trên bản đồ hoặc một thẻ bãi bên dưới để
                    xem bãi đó đang hoạt động, chờ duyệt hay bị từ chối.
                </div>
            ) : (
                <div
                    style={{
                        display: "flex",
                        flexDirection: "column",
                        gap: 18,
                    }}
                >
                    {/* Header Lot info */}
                    <div>
                        <div
                            style={{
                                fontSize: 18,
                                fontWeight: 800,
                                color: "#0f1b3d",
                            }}
                        >
                            {selectedLot.name}
                        </div>
                        <div
                            style={{
                                fontSize: 13,
                                color: "#6a7491",
                                marginTop: 2,
                            }}
                        >
                            {selectedLot.addr}
                        </div>
                    </div>

                    {/* Rejection notice */}
                    {isRejected &&
                        (selectedLot.why || lotInfo?.rejectReason) && (
                            <div
                                style={{
                                    background: "#fff5f5",
                                    border: "1px solid #fed7d7",
                                    borderRadius: 12,
                                    padding: "12px 14px",
                                    fontSize: 12.5,
                                    color: "#c53030",
                                    lineHeight: 1.5,
                                }}
                            >
                                <strong>Lý do từ chối:</strong>{" "}
                                {selectedLot.why || lotInfo?.rejectReason}
                            </div>
                        )}

                    {/* Mục 1: Thông tin */}
                    <div>
                        <div
                            style={{
                                fontSize: 12,
                                fontWeight: 700,
                                textTransform: "uppercase",
                                color: "#6a7491",
                                letterSpacing: 0.5,
                                marginBottom: 8,
                            }}
                        >
                            Thông tin vận hành
                        </div>
                        <div
                            style={{
                                background: "#f8faff",
                                borderRadius: 12,
                                border: "1px solid #eef2fa",
                                padding: "12px 14px",
                                display: "flex",
                                flexDirection: "column",
                                gap: 8,
                                fontSize: 13,
                            }}
                        >
                            <div
                                style={{
                                    display: "flex",
                                    justifyContent: "space-between",
                                }}
                            >
                                <span style={{ color: "#6a7491" }}>
                                    {dateTitle}
                                </span>
                                <span
                                    style={{
                                        fontWeight: 600,
                                        color: "#0f1b3d",
                                        textAlign: "right",
                                    }}
                                >
                                    {dateValue}
                                </span>
                            </div>

                            <div
                                style={{
                                    display: "flex",
                                    justifyContent: "space-between",
                                }}
                            >
                                <span style={{ color: "#6a7491" }}>
                                    Quy mô & Lấp đầy:
                                </span>
                                <span
                                    style={{
                                        fontWeight: 600,
                                        color: "#0f1b3d",
                                    }}
                                >
                                    {selectedLot.cc} ô đỗ{" "}
                                    {isApproved ? "· Lấp đầy 58%" : ""}
                                </span>
                            </div>

                            <div
                                style={{
                                    display: "flex",
                                    justifyContent: "space-between",
                                }}
                            >
                                <span style={{ color: "#6a7491" }}>
                                    Giờ mở cửa:
                                </span>
                                <span
                                    style={{
                                        fontWeight: 600,
                                        color: "#0f1b3d",
                                    }}
                                >
                                    {lotInfo?.openingHours ||
                                        `${selectedLot.op} – ${selectedLot.cl2}`}
                                </span>
                            </div>

                            <div
                                style={{
                                    display: "flex",
                                    justifyContent: "space-between",
                                }}
                            >
                                <span style={{ color: "#6a7491" }}>
                                    Hotline liên hệ:
                                </span>
                                <span
                                    style={{
                                        fontWeight: 600,
                                        color: "#2563eb",
                                    }}
                                >
                                    {lotInfo?.phone || "Chưa cập nhật"}
                                </span>
                            </div>
                        </div>
                    </div>

                    {/* Mục 2: Bảng giá */}
                    <div>
                        <div
                            style={{
                                fontSize: 12,
                                fontWeight: 700,
                                textTransform: "uppercase",
                                color: "#6a7491",
                                letterSpacing: 0.5,
                                marginBottom: 8,
                            }}
                        >
                            {priceSectionTitle}
                        </div>
                        <div
                            style={{
                                background: "#f8faff",
                                borderRadius: 12,
                                border: "1px solid #eef2fa",
                                padding: "12px 14px",
                                display: "flex",
                                flexDirection: "column",
                                gap: 7,
                                fontSize: 12.5,
                            }}
                        >
                            <div
                                style={{
                                    display: "flex",
                                    justifyContent: "space-between",
                                }}
                            >
                                <span style={{ color: "#6a7491" }}>
                                    Ô tô con giờ đầu:
                                </span>
                                <span
                                    style={{
                                        fontWeight: 700,
                                        color: "#0f1b3d",
                                    }}
                                >
                                    {formatVnd(lotInfo?.prices?.carFirstHour)}
                                </span>
                            </div>

                            <div
                                style={{
                                    display: "flex",
                                    justifyContent: "space-between",
                                }}
                            >
                                <span style={{ color: "#6a7491" }}>
                                    Ô tô mỗi giờ tiếp theo:
                                </span>
                                <span
                                    style={{
                                        fontWeight: 700,
                                        color: "#0f1b3d",
                                    }}
                                >
                                    {formatVnd(lotInfo?.prices?.carNextHour)}
                                </span>
                            </div>

                            <div
                                style={{
                                    display: "flex",
                                    justifyContent: "space-between",
                                }}
                            >
                                <span style={{ color: "#6a7491" }}>
                                    Qua đêm (22h – 06h):
                                </span>
                                <span
                                    style={{
                                        fontWeight: 700,
                                        color: "#0f1b3d",
                                    }}
                                >
                                    {formatVnd(lotInfo?.prices?.overnight)}
                                </span>
                            </div>

                            <div
                                style={{
                                    display: "flex",
                                    justifyContent: "space-between",
                                }}
                            >
                                <span style={{ color: "#6a7491" }}>
                                    Tối đa mỗi ngày:
                                </span>
                                <span
                                    style={{
                                        fontWeight: 700,
                                        color: "#0f1b3d",
                                    }}
                                >
                                    {formatVnd(lotInfo?.prices?.dailyMax)}
                                </span>
                            </div>

                            <div
                                style={{
                                    display: "flex",
                                    justifyContent: "space-between",
                                }}
                            >
                                <span style={{ color: "#6a7491" }}>
                                    Xe máy mỗi lượt:
                                </span>
                                <span
                                    style={{
                                        fontWeight: 700,
                                        color: "#0f1b3d",
                                    }}
                                >
                                    {formatVnd(
                                        lotInfo?.prices?.motorbikePerVisit,
                                    )}
                                </span>
                            </div>

                            <div
                                style={{
                                    display: "flex",
                                    justifyContent: "space-between",
                                }}
                            >
                                <span style={{ color: "#6a7491" }}>
                                    Vé tháng ô tô:
                                </span>
                                <span
                                    style={{
                                        fontWeight: 700,
                                        color: "#0f1b3d",
                                    }}
                                >
                                    {formatVnd(lotInfo?.prices?.carMonthlyPass)}
                                </span>
                            </div>

                            <div
                                style={{
                                    display: "flex",
                                    justifyContent: "space-between",
                                }}
                            >
                                <span style={{ color: "#6a7491" }}>
                                    Tiền cọc giữ chỗ:
                                </span>
                                <span
                                    style={{
                                        fontWeight: 700,
                                        color: "#0f1b3d",
                                    }}
                                >
                                    {formatVnd(
                                        lotInfo?.prices?.reservationDeposit,
                                    )}
                                </span>
                            </div>
                        </div>
                    </div>

                    {/* Action buttons */}
                    <div
                        style={{
                            display: "flex",
                            flexDirection: "column",
                            gap: 8,
                            marginTop: 4,
                        }}
                    >
                        {isApproved && (
                            <div style={{ display: "flex", gap: 8 }}>
                                <Link
                                    href="/owner/layout/2d"
                                    style={{
                                        flex: 1,
                                        textAlign: "center",
                                        background: "#0f1b3d",
                                        color: "#fff",
                                        borderRadius: 10,
                                        padding: "9px 12px",
                                        fontSize: 13,
                                        fontWeight: 600,
                                        textDecoration: "none",
                                    }}
                                >
                                    Xem sơ đồ 2D / 3D
                                </Link>

                                <a
                                    href={googleMapsUrl}
                                    target="_blank"
                                    rel="noopener noreferrer"
                                    style={{
                                        flex: 1,
                                        textAlign: "center",
                                        background: "#f3f6fb",
                                        border: "1px solid #e3e8f2",
                                        color: "#0f1b3d",
                                        borderRadius: 10,
                                        padding: "9px 12px",
                                        fontSize: 13,
                                        fontWeight: 600,
                                        textDecoration: "none",
                                    }}
                                >
                                    Chỉ đường ↗
                                </a>
                            </div>
                        )}

                        {isPending && (
                            <button
                                type="button"
                                onClick={() =>
                                    showToast(
                                        "Đã gửi nhắc duyệt hồ sơ đến ban quản trị hệ thống!",
                                    )
                                }
                                style={{
                                    background: "#fef3c7",
                                    border: "1px solid #fde68a",
                                    color: "#b45309",
                                    borderRadius: 10,
                                    padding: "9px 12px",
                                    fontSize: 13,
                                    fontWeight: 700,
                                    cursor: "pointer",
                                }}
                            >
                                Nhắc duyệt
                            </button>
                        )}

                        {isRejected && (
                            <button
                                type="button"
                                onClick={handleResubmit}
                                style={{
                                    background: "#2563eb",
                                    border: "none",
                                    color: "#fff",
                                    borderRadius: 10,
                                    padding: "9px 12px",
                                    fontSize: 13,
                                    fontWeight: 700,
                                    cursor: "pointer",
                                }}
                            >
                                Chỉnh sửa và gửi lại
                            </button>
                        )}

                        <Link
                            href={`/owner/parking-lots/${selectedLot.id}`}
                            style={{
                                textAlign: "center",
                                color: "#6a7491",
                                fontSize: 12.5,
                                fontWeight: 600,
                                padding: "4px",
                                textDecoration: "none",
                            }}
                        >
                            Cấu hình chi tiết bãi đỗ xe →
                        </Link>
                    </div>
                </div>
            )}

            {/* Toast Feedback */}
            {toastMessage && (
                <div
                    role="status"
                    style={{
                        position: "fixed",
                        bottom: 24,
                        right: 24,
                        background: "#0f1b3d",
                        color: "#fff",
                        padding: "12px 20px",
                        borderRadius: 10,
                        fontSize: 13,
                        boxShadow: "0 8px 24px rgba(0,0,0,0.2)",
                        zIndex: 1100,
                        animation: "fadeIn 0.2s ease",
                    }}
                >
                    {toastMessage}
                </div>
            )}
        </div>
    );
}
