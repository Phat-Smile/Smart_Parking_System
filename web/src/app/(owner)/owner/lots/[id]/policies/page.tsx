"use client";

import React, { useState, useEffect } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { PageHeader } from "@/components/layout/PageHeader";
import { useOwnerStore } from "@/store/useOwnerStore";
import { vnd } from "@/types/owner";
import {
    getDepositPolicyApi,
    configureDepositPolicyApi,
    getReservationRatioPolicyApi,
    configureReservationRatioPolicyApi,
    getReservationAvailabilityApi,
    configureReservationAvailabilityApi,
} from "@/features/owner/api/ownerApi";
import { backendLotId } from "@/features/owner/lots/ownerLotsSync";
import { ApiError } from "@/shared/errors/api-error";

export default function LotPoliciesPage() {
    const params = useParams();
    const rawId = params?.id;
    const lotId = Number(rawId) || 1;

    const lots = useOwnerStore((s) => s.lots);
    const lot =
        lots.find((l) => l.id === lotId) ||
        lots.find((l) => l.backendId === rawId) ||
        lots[0];

    // Resolve GUID for backend API call
    const lotKey =
        backendLotId(lot) ||
        (typeof rawId === "string" && rawId.includes("-") ? rawId : null);

    // States for 3 policies
    const [reservationEnabled, setReservationEnabled] = useState(true);
    const [ratioPercent, setRatioPercent] = useState(30);
    const [depositVnd, setDepositVnd] = useState(50000);

    // Loading & Feedback states
    const [isLoading, setIsLoading] = useState(true);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [successMessage, setSuccessMessage] = useState("");
    const [errorMessage, setErrorMessage] = useState("");

    // Total estimated capacity from lot
    const totalCapacity = lot && lot.cc + lot.cm > 0 ? lot.cc + lot.cm : 100;
    const reservedSlots = Math.round((totalCapacity * ratioPercent) / 100);
    const walkInSlots = Math.max(0, totalCapacity - reservedSlots);

    // Load initial policies from Backend
    useEffect(() => {
        let isMounted = true;

        async function loadPolicies() {
            if (!lotKey) {
                setIsLoading(false);
                return;
            }
            setIsLoading(true);
            try {
                const [availRes, ratioRes, depositRes] =
                    await Promise.allSettled([
                        getReservationAvailabilityApi(lotKey),
                        getReservationRatioPolicyApi(lotKey),
                        getDepositPolicyApi(lotKey),
                    ]);

                if (!isMounted) return;

                if (
                    availRes.status === "fulfilled" &&
                    availRes.value.isConfigured
                ) {
                    setReservationEnabled(availRes.value.reservationEnabled);
                }
                if (
                    ratioRes.status === "fulfilled" &&
                    ratioRes.value.isConfigured
                ) {
                    setRatioPercent(ratioRes.value.reservationRatioPercent);
                }
                if (
                    depositRes.status === "fulfilled" &&
                    depositRes.value.isConfigured
                ) {
                    setDepositVnd(depositRes.value.holdDepositVnd);
                }
            } catch {
                if (isMounted) {
                    setErrorMessage(
                        "Không thể tải một số chính sách từ máy chủ.",
                    );
                }
            } finally {
                if (isMounted) {
                    setIsLoading(false);
                }
            }
        }

        loadPolicies();
        return () => {
            isMounted = false;
        };
    }, [lotKey]);

    const handleSave = async (e: React.FormEvent) => {
        e.preventDefault();
        setErrorMessage("");
        setSuccessMessage("");

        if (!lotKey) {
            setErrorMessage(
                "Bãi đỗ này chưa được định danh trên máy chủ. Vui lòng chọn một bãi xe hợp lệ.",
            );
            return;
        }

        if (ratioPercent < 10 || ratioPercent > 50) {
            setErrorMessage("Tỷ lệ giữ chỗ phải từ 10% đến 50%.");
            return;
        }

        if (depositVnd < 0) {
            setErrorMessage("Tiền cọc giữ chỗ không thể là số âm.");
            return;
        }

        setIsSubmitting(true);

        try {
            await Promise.all([
                configureReservationAvailabilityApi(lotKey, {
                    reservationEnabled,
                }),
                configureReservationRatioPolicyApi(lotKey, {
                    reservationRatioPercent: ratioPercent,
                }),
                configureDepositPolicyApi(lotKey, {
                    holdDepositVnd: depositVnd,
                }),
            ]);

            setSuccessMessage(
                "Đã lưu và đồng bộ thành công cả 3 chính sách đặt chỗ & cọc lên máy chủ!",
            );

            // Update local owner store
            useOwnerStore.setState((state) => ({
                lots: state.lots.map((l) =>
                    l.id === lotId || l.backendId === lotKey
                        ? {
                              ...l,
                              res: reservationEnabled ? "on" : "off",
                              dep: depositVnd,
                              maxr: reservedSlots,
                          }
                        : l,
                ),
            }));
        } catch (err: unknown) {
            if (err instanceof ApiError) {
                setErrorMessage(
                    err.problem?.detail ||
                        err.message ||
                        "Không thể lưu chính sách lúc này.",
                );
            } else {
                setErrorMessage(
                    "Lỗi kết nối máy chủ khi lưu chính sách đặt chỗ.",
                );
            }
        } finally {
            setIsSubmitting(false);
        }
    };

    return (
        <div style={{ paddingBottom: "40px" }}>
            {successMessage && (
                <div
                    data-testid="policies-success-banner"
                    className="ok"
                    style={{ marginBottom: "16px" }}
                >
                    <b>✓ Thành công:</b> {successMessage}
                </div>
            )}

            {errorMessage && (
                <div
                    data-testid="policies-error-banner"
                    className="err"
                    style={{ marginBottom: "16px" }}
                >
                    <b>⚠️ Lỗi:</b> {errorMessage}
                </div>
            )}

            <PageHeader
                breadcrumb={`/owner/lots/${lotId}/policies`}
                title={`Chính sách đặt chỗ & cọc · ${lot?.name || "Bãi xe"}`}
                subtitle="Cấu hình nhận giữ chỗ trực tuyến, phân bổ tỷ lệ chỗ đặt trước và thiết lập mức cọc chống no-show"
                actions={
                    <div style={{ display: "flex", gap: "8px" }}>
                        <Link
                            href={`/owner/parking-lots/${lotId}`}
                            className="btn"
                        >
                            Quay lại bãi xe
                        </Link>
                        <Link
                            href={`/owner/lots/${lotId}/pricing`}
                            className="btn"
                        >
                            Biểu giá bãi
                        </Link>
                    </div>
                }
            />

            <form onSubmit={handleSave}>
                <div
                    style={{
                        display: "grid",
                        gridTemplateColumns:
                            "repeat(auto-fit, minmax(320px, 1fr))",
                        gap: "20px",
                        marginTop: "16px",
                    }}
                >
                    {/* Policy 1: Reservation Availability */}
                    <div className="card" style={{ padding: "20px" }}>
                        <div
                            style={{
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "space-between",
                                marginBottom: "12px",
                            }}
                        >
                            <h3
                                style={{
                                    margin: 0,
                                    fontSize: "16px",
                                    fontWeight: 700,
                                    color: "#0f172a",
                                }}
                            >
                                1. Trạng thái Đặt chỗ Trực tuyến
                            </h3>
                            <span
                                style={{
                                    display: "inline-flex",
                                    alignItems: "center",
                                    gap: "6px",
                                    padding: "3px 8px",
                                    borderRadius: "12px",
                                    fontSize: "12px",
                                    fontWeight: 600,
                                    backgroundColor: reservationEnabled
                                        ? "#dcfce7"
                                        : "#f1f5f9",
                                    color: reservationEnabled
                                        ? "#166534"
                                        : "#64748b",
                                }}
                            >
                                {reservationEnabled
                                    ? "● Đang nhận đặt trước"
                                    : "○ Tạm ngưng đặt trước"}
                            </span>
                        </div>

                        <p
                            style={{
                                fontSize: "13px",
                                color: "#64748b",
                                lineHeight: "1.5",
                                marginBottom: "16px",
                            }}
                        >
                            Cho phép khách hàng tìm kiếm bãi xe trên bản đồ ứng
                            dụng ParkWise và bấm giữ chỗ trước 15 phút trước khi
                            đến.
                        </p>

                        <label
                            style={{
                                display: "flex",
                                alignItems: "center",
                                gap: "10px",
                                cursor: "pointer",
                                padding: "12px",
                                borderRadius: "8px",
                                border: "1px solid #e2e8f0",
                                backgroundColor: reservationEnabled
                                    ? "#f0fdf4"
                                    : "#ffffff",
                            }}
                        >
                            <input
                                data-testid="reservation-enabled-checkbox"
                                type="checkbox"
                                checked={reservationEnabled}
                                onChange={(e) =>
                                    setReservationEnabled(e.target.checked)
                                }
                                style={{ width: "18px", height: "18px" }}
                            />
                            <div>
                                <div
                                    style={{
                                        fontWeight: 600,
                                        fontSize: "14px",
                                        color: "#0f172a",
                                    }}
                                >
                                    Bật tính năng đặt chỗ trước
                                </div>
                                <div
                                    style={{
                                        fontSize: "12px",
                                        color: "#64748b",
                                    }}
                                >
                                    Khi tắt, khách chỉ có thể gửi xe vãng lai
                                    trực tiếp tại cổng.
                                </div>
                            </div>
                        </label>
                    </div>

                    {/* Policy 2: Reservation Ratio */}
                    <div className="card" style={{ padding: "20px" }}>
                        <div
                            style={{
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "space-between",
                                marginBottom: "12px",
                            }}
                        >
                            <h3
                                style={{
                                    margin: 0,
                                    fontSize: "16px",
                                    fontWeight: 700,
                                    color: "#0f172a",
                                }}
                            >
                                2. Tỷ lệ Suất Giữ chỗ ({ratioPercent}%)
                            </h3>
                            <span
                                style={{
                                    fontSize: "12px",
                                    fontWeight: 600,
                                    color: "#2563eb",
                                    backgroundColor: "#eff6ff",
                                    padding: "2px 8px",
                                    borderRadius: "12px",
                                }}
                            >
                                Quy định: 10% - 50%
                            </span>
                        </div>

                        <p
                            style={{
                                fontSize: "13px",
                                color: "#64748b",
                                lineHeight: "1.5",
                                marginBottom: "16px",
                            }}
                        >
                            Tỷ lệ tối đa số ô đỗ dành riêng cho khách đặt trước
                            qua app (phần còn lại dành cho khách vãng lai tại
                            cổng).
                        </p>

                        <div style={{ marginBottom: "16px" }}>
                            <div
                                style={{
                                    display: "flex",
                                    justifyContent: "space-between",
                                    fontSize: "13px",
                                    marginBottom: "6px",
                                }}
                            >
                                <span>10%</span>
                                <b
                                    style={{
                                        color: "#2563eb",
                                        fontSize: "15px",
                                    }}
                                >
                                    {ratioPercent}%
                                </b>
                                <span>50%</span>
                            </div>
                            <input
                                data-testid="ratio-percent-slider"
                                type="range"
                                min={10}
                                max={50}
                                step={5}
                                value={ratioPercent}
                                onChange={(e) =>
                                    setRatioPercent(Number(e.target.value))
                                }
                                style={{
                                    width: "100%",
                                    accentColor: "#2563eb",
                                }}
                            />
                        </div>

                        {/* Visual Slot Distribution Bar */}
                        <div
                            style={{
                                padding: "12px",
                                borderRadius: "8px",
                                backgroundColor: "#f8fafc",
                                border: "1px solid #e2e8f0",
                            }}
                        >
                            <div
                                style={{
                                    fontSize: "12px",
                                    fontWeight: 600,
                                    color: "#475569",
                                    marginBottom: "8px",
                                }}
                            >
                                Dự tính phân bổ ô đỗ (Tổng sức chứa:{" "}
                                {totalCapacity} ô):
                            </div>
                            <div
                                style={{
                                    height: "14px",
                                    borderRadius: "7px",
                                    overflow: "hidden",
                                    display: "flex",
                                    backgroundColor: "#cbd5e1",
                                    marginBottom: "8px",
                                }}
                            >
                                <div
                                    style={{
                                        width: `${ratioPercent}%`,
                                        backgroundColor: "#3b82f6",
                                        transition: "width 0.2s",
                                    }}
                                />
                                <div
                                    style={{
                                        width: `${100 - ratioPercent}%`,
                                        backgroundColor: "#94a3b8",
                                    }}
                                />
                            </div>
                            <div
                                style={{
                                    display: "flex",
                                    justifyContent: "space-between",
                                    fontSize: "12px",
                                }}
                            >
                                <span
                                    style={{
                                        color: "#1d4ed8",
                                        fontWeight: 600,
                                    }}
                                >
                                    ● Đặt trước: {reservedSlots} ô
                                </span>
                                <span
                                    style={{
                                        color: "#475569",
                                        fontWeight: 500,
                                    }}
                                >
                                    ● Vãng lai: {walkInSlots} ô
                                </span>
                            </div>
                        </div>
                    </div>

                    {/* Policy 3: Hold Deposit */}
                    <div
                        className="card"
                        style={{ padding: "20px", gridColumn: "1 / -1" }}
                    >
                        <div
                            style={{
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "space-between",
                                marginBottom: "12px",
                            }}
                        >
                            <h3
                                style={{
                                    margin: 0,
                                    fontSize: "16px",
                                    fontWeight: 700,
                                    color: "#0f172a",
                                }}
                            >
                                3. Mức Tiền Cọc Giữ Chỗ 15 Phút
                            </h3>
                            <span
                                style={{
                                    fontSize: "14px",
                                    fontWeight: 700,
                                    color: "#059669",
                                }}
                            >
                                {vnd(depositVnd)}
                            </span>
                        </div>

                        <p
                            style={{
                                fontSize: "13px",
                                color: "#64748b",
                                lineHeight: "1.5",
                                marginBottom: "16px",
                            }}
                        >
                            Khoản tiền tạm trừ từ Ví ParkWise của khách khi xác
                            nhận giữ chỗ. Khoản này sẽ được khấu trừ vào phí đỗ
                            xe khi check-in, hoặc hoàn lại nếu khách tự hủy
                            trước 5 phút.
                        </p>

                        <div
                            style={{
                                display: "flex",
                                flexWrap: "wrap",
                                gap: "12px",
                                alignItems: "center",
                                marginBottom: "16px",
                            }}
                        >
                            <div style={{ flex: "1 1 240px" }}>
                                <label
                                    style={{
                                        display: "block",
                                        fontSize: "12px",
                                        fontWeight: 600,
                                        color: "#475569",
                                        marginBottom: "4px",
                                    }}
                                >
                                    Số tiền cọc tùy chỉnh (VNĐ):
                                </label>
                                <input
                                    data-testid="deposit-vnd-input"
                                    type="number"
                                    step={5000}
                                    min={0}
                                    value={depositVnd}
                                    onChange={(e) =>
                                        setDepositVnd(Number(e.target.value))
                                    }
                                    style={{
                                        width: "100%",
                                        padding: "8px 12px",
                                        borderRadius: "6px",
                                        border: "1px solid #cbd5e1",
                                        fontSize: "14px",
                                        fontWeight: 600,
                                    }}
                                />
                            </div>

                            <div
                                style={{
                                    display: "flex",
                                    gap: "8px",
                                    flexWrap: "wrap",
                                }}
                            >
                                {[0, 20000, 50000, 100000].map((preset) => (
                                    <button
                                        key={preset}
                                        type="button"
                                        onClick={() => setDepositVnd(preset)}
                                        style={{
                                            padding: "6px 12px",
                                            borderRadius: "6px",
                                            fontSize: "13px",
                                            fontWeight: 600,
                                            border:
                                                depositVnd === preset
                                                    ? "1px solid #059669"
                                                    : "1px solid #cbd5e1",
                                            backgroundColor:
                                                depositVnd === preset
                                                    ? "#ecfdf5"
                                                    : "#f8fafc",
                                            color:
                                                depositVnd === preset
                                                    ? "#065f46"
                                                    : "#334155",
                                            cursor: "pointer",
                                        }}
                                    >
                                        {preset === 0
                                            ? "Miễn cọc"
                                            : vnd(preset)}
                                    </button>
                                ))}
                            </div>
                        </div>

                        {/* Rules reminder */}
                        <div
                            style={{
                                padding: "12px 14px",
                                borderRadius: "8px",
                                backgroundColor: "#fefce8",
                                border: "1px solid #fef08a",
                                fontSize: "12px",
                                color: "#854d0e",
                                lineHeight: "1.6",
                            }}
                        >
                            <b>📌 Quy định vận hành Smart Parking:</b>
                            <ul style={{ margin: "4px 0 0 16px", padding: 0 }}>
                                <li>
                                    Nếu khách check-in đúng giờ: Tiền cọc được
                                    trừ trực tiếp vào hóa đơn cuối cùng.
                                </li>
                                <li>
                                    Nếu khách hủy sớm (trước 5 phút): Hệ thống
                                    hoàn 100% tiền cọc về ví của khách.
                                </li>
                                <li>
                                    Nếu quá 15 phút không đến (No-show): Ô đỗ tự
                                    động giải phóng cho tài xế khác, tiền cọc
                                    được ghi nhận bồi hoàn doanh thu cho chủ
                                    bãi.
                                </li>
                            </ul>
                        </div>
                    </div>
                </div>

                <div
                    style={{
                        marginTop: "24px",
                        display: "flex",
                        justifyContent: "flex-end",
                        gap: "12px",
                    }}
                >
                    <Link href={`/owner/parking-lots/${lotId}`} className="btn">
                        Hủy thay đổi
                    </Link>
                    <button
                        data-testid="save-policies-button"
                        type="submit"
                        className="btn pri"
                        disabled={isSubmitting || isLoading}
                        style={{
                            display: "inline-flex",
                            alignItems: "center",
                            gap: "6px",
                            padding: "8px 20px",
                            fontWeight: 600,
                        }}
                    >
                        {isSubmitting
                            ? "Đang lưu chính sách..."
                            : "Lưu tất cả chính sách"}
                    </button>
                </div>
            </form>
        </div>
    );
}
