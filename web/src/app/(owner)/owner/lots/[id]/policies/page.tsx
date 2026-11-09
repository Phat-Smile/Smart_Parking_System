"use client";

import React, { useEffect, useState, use } from "react";
import Link from "next/link";
import { OwnerPageHeader } from "@/components/layout/OwnerPageHeader";
import {
    getParkingLotDetailApi,
    getReservationRatioPolicyApi,
    configureReservationRatioPolicyApi,
    getDepositPolicyApi,
    configureDepositPolicyApi,
    type ParkingLotDetail,
} from "@/features/owner/api/ownerApi";
import { ApiError } from "@/shared/errors/api-error";

interface PageProps {
    params: Promise<{ id: string }>;
}

export default function LotPoliciesPage({ params }: PageProps) {
    const resolvedParams = use(params);
    const lotId = resolvedParams.id;

    const [lot, setLot] = useState<ParkingLotDetail | null>(null);
    const [ratio, setRatio] = useState<number>(30);
    const [deposit, setDeposit] = useState<number>(50000);
    const [loading, setLoading] = useState<boolean>(true);
    const [savingRatio, setSavingRatio] = useState<boolean>(false);
    const [savingDeposit, setSavingDeposit] = useState<boolean>(false);
    const [savingAll, setSavingAll] = useState<boolean>(false);

    const [toastMessage, setToastMessage] = useState<{
        type: "success" | "error";
        text: string;
    } | null>(null);

    function showToast(type: "success" | "error", text: string) {
        setToastMessage({ type, text });
        setTimeout(() => setToastMessage(null), 4000);
    }

    useEffect(() => {
        let active = true;
        async function loadPolicies() {
            setLoading(true);
            try {
                const [lotRes, ratioRes, depositRes] = await Promise.allSettled([
                    getParkingLotDetailApi(lotId),
                    getReservationRatioPolicyApi(lotId),
                    getDepositPolicyApi(lotId),
                ]);

                if (!active) return;

                if (lotRes.status === "fulfilled") {
                    setLot(lotRes.value);
                }
                if (ratioRes.status === "fulfilled" && ratioRes.value?.reservationRatioPercent) {
                    setRatio(ratioRes.value.reservationRatioPercent);
                }
                if (depositRes.status === "fulfilled" && depositRes.value?.holdDepositVnd) {
                    setDeposit(depositRes.value.holdDepositVnd);
                }
            } catch {
                if (active) {
                    showToast("error", "Không thể tải chính sách bãi xe. Sử dụng giá trị mặc định.");
                }
            } finally {
                if (active) setLoading(false);
            }
        }

        void loadPolicies();
        return () => {
            active = false;
        };
    }, [lotId]);

    async function handleSaveRatio() {
        setSavingRatio(true);
        try {
            await configureReservationRatioPolicyApi(lotId, ratio);
            showToast("success", `Đã lưu tỷ lệ giữ chỗ trực tuyến ${ratio}% thành công!`);
        } catch (err) {
            const msg = err instanceof ApiError ? err.message : "Lỗi khi lưu tỷ lệ giữ chỗ.";
            showToast("error", msg);
        } finally {
            setSavingRatio(false);
        }
    }

    async function handleSaveDeposit() {
        setSavingDeposit(true);
        try {
            await configureDepositPolicyApi(lotId, { holdDepositVnd: deposit });
            showToast(
                "success",
                `Đã lưu mức tiền cọc giữ chỗ ${deposit.toLocaleString("vi-VN")} ₫ thành công!`,
            );
        } catch (err) {
            const msg = err instanceof ApiError ? err.message : "Lỗi khi lưu mức tiền cọc.";
            showToast("error", msg);
        } finally {
            setSavingDeposit(false);
        }
    }

    async function handleSaveAll() {
        setSavingAll(true);
        try {
            await Promise.all([
                configureReservationRatioPolicyApi(lotId, ratio),
                configureDepositPolicyApi(lotId, { holdDepositVnd: deposit }),
            ]);
            showToast("success", "Đã cập nhật toàn bộ chính sách bãi đỗ xe thành công!");
        } catch (err) {
            const msg = err instanceof ApiError ? err.message : "Lỗi khi cập nhật chính sách.";
            showToast("error", msg);
        } finally {
            setSavingAll(false);
        }
    }

    const onlineQuotaPercent = ratio;
    const walkInQuotaPercent = 100 - ratio;

    return (
        <div style={{ maxWidth: 1000, margin: "0 auto", paddingBottom: 64 }}>
            <OwnerPageHeader
                breadcrumb={`/owner/parking-lots/${lotId}/policies`}
                title="Chính sách Giữ chỗ & Tiền cọc"
                subtitle={`Cấu hình tỷ lệ 30/70 và tiền cọc cho cơ sở: ${lot?.name ?? "Bãi đỗ xe"}`}
                actions={
                    <div style={{ display: "flex", gap: 10 }}>
                        <Link
                            href="/owner/parking-lots"
                            style={{
                                background: "#fff",
                                border: "1px solid #cbd5e1",
                                color: "#334155",
                                padding: "8px 16px",
                                borderRadius: 8,
                                fontWeight: 700,
                                fontSize: 13,
                                textDecoration: "none",
                                display: "inline-flex",
                                alignItems: "center",
                                gap: 6,
                            }}
                        >
                            ← Quay lại danh sách bãi
                        </Link>
                    </div>
                }
            />

            {/* Toast Alerts */}
            {toastMessage && (
                <div
                    role={toastMessage.type === "error" ? "alert" : "status"}
                    style={{
                        marginBottom: 20,
                        padding: "14px 20px",
                        background: toastMessage.type === "success" ? "#def7ec" : "#fde8e8",
                        border: `1.5px solid ${toastMessage.type === "success" ? "#31c48d" : "#f98080"}`,
                        color: toastMessage.type === "success" ? "#0e9f6e" : "#9b1c1c",
                        borderRadius: 14,
                        fontSize: 14,
                        fontWeight: 700,
                        display: "flex",
                        alignItems: "center",
                        gap: 10,
                        boxShadow: "0 4px 12px rgba(0,0,0,0.05)",
                    }}
                >
                    <span>{toastMessage.type === "success" ? "✓" : "⚠️"}</span>
                    <span>{toastMessage.text}</span>
                </div>
            )}

            {loading ? (
                <div
                    style={{
                        background: "#fff",
                        borderRadius: 20,
                        border: "1px solid #e2e8f0",
                        padding: 48,
                        textAlign: "center",
                        color: "#64748b",
                    }}
                >
                    <div style={{ fontSize: 24, marginBottom: 12 }}>⚡</div>
                    <p style={{ fontWeight: 600 }}>Đang nạp thông tin chính sách của bãi xe...</p>
                </div>
            ) : (
                <div style={{ display: "flex", flexDirection: "column", gap: 24 }}>
                    {/* BẢNG 1: TỶ LỆ GIỮ CHỖ (RESERVATION RATIO) */}
                    <div
                        style={{
                            background: "#fff",
                            borderRadius: 20,
                            border: "1px solid #e2e8f0",
                            padding: "28px 32px",
                            boxShadow: "0 4px 16px rgba(15,27,61,0.04)",
                        }}
                    >
                        <div
                            style={{
                                display: "flex",
                                justifyContent: "space-between",
                                alignItems: "flex-start",
                                flexWrap: "wrap",
                                gap: 12,
                                marginBottom: 16,
                            }}
                        >
                            <div>
                                <div
                                    style={{
                                        display: "inline-flex",
                                        alignItems: "center",
                                        gap: 6,
                                        background: "#e0f2fe",
                                        color: "#0369a1",
                                        padding: "4px 12px",
                                        borderRadius: 999,
                                        fontSize: 12,
                                        fontWeight: 800,
                                        marginBottom: 8,
                                    }}
                                >
                                    CHÍNH SÁCH 30/70
                                </div>
                                <h3
                                    style={{
                                        fontSize: 18,
                                        fontWeight: 800,
                                        color: "#0f172a",
                                        margin: "0 0 6px",
                                    }}
                                >
                                    Tỷ Lệ Giữ Chỗ Trực Tuyến (Reservation Ratio)
                                </h3>
                                <p style={{ fontSize: 13.5, color: "#64748b", margin: 0, lineHeight: 1.5 }}>
                                    Quy định tỷ lệ phần trăm sức chứa tối đa cho phép đặt trước online qua ứng dụng.
                                </p>
                            </div>

                            <button
                                type="button"
                                onClick={handleSaveRatio}
                                disabled={savingRatio || savingAll}
                                style={{
                                    background: "#0f172a",
                                    color: "#fff",
                                    border: "none",
                                    borderRadius: 10,
                                    padding: "9px 20px",
                                    fontSize: 13.5,
                                    fontWeight: 700,
                                    cursor: savingRatio ? "not-allowed" : "pointer",
                                    display: "inline-flex",
                                    alignItems: "center",
                                    gap: 6,
                                }}
                            >
                                {savingRatio ? "Đang lưu..." : "Lưu tỷ lệ"}
                            </button>
                        </div>

                        {/* Slider control */}
                        <div style={{ margin: "28px 0 24px" }}>
                            <div
                                style={{
                                    display: "flex",
                                    justifyContent: "space-between",
                                    alignItems: "center",
                                    marginBottom: 10,
                                }}
                            >
                                <span style={{ fontSize: 14, fontWeight: 700, color: "#1e293b" }}>
                                    Mức đặt trước cho phép:
                                </span>
                                <span
                                    style={{
                                        fontSize: 22,
                                        fontWeight: 800,
                                        color: "#2563eb",
                                        fontFamily: "monospace",
                                    }}
                                >
                                    {ratio}%
                                </span>
                            </div>

                            <input
                                type="range"
                                min={10}
                                max={50}
                                step={5}
                                value={ratio}
                                onChange={(e) => setRatio(Number(e.target.value))}
                                style={{
                                    width: "100%",
                                    height: 8,
                                    borderRadius: 4,
                                    accentColor: "#2563eb",
                                    cursor: "pointer",
                                }}
                            />

                            <div
                                style={{
                                    display: "flex",
                                    justifyContent: "space-between",
                                    fontSize: 11.5,
                                    color: "#94a3b8",
                                    marginTop: 6,
                                    fontWeight: 600,
                                }}
                            >
                                <span>Tối thiểu: 10%</span>
                                <span style={{ color: "#2563eb", fontWeight: 700 }}>
                                    Mặc định đề xuất: 30%
                                </span>
                                <span>Tối đa: 50%</span>
                            </div>
                        </div>

                        {/* Visual Quota Bar */}
                        <div
                            style={{
                                background: "#f8fafc",
                                border: "1px solid #e2e8f0",
                                borderRadius: 16,
                                padding: "18px 20px",
                            }}
                        >
                            <div style={{ fontSize: 13, fontWeight: 700, color: "#475569", marginBottom: 10 }}>
                                Mô phỏng phân bổ sức chứa bãi xe:
                            </div>

                            <div
                                style={{
                                    height: 24,
                                    borderRadius: 999,
                                    overflow: "hidden",
                                    display: "flex",
                                    background: "#e2e8f0",
                                    marginBottom: 14,
                                }}
                            >
                                <div
                                    style={{
                                        width: `${onlineQuotaPercent}%`,
                                        background: "#2563eb",
                                        display: "flex",
                                        alignItems: "center",
                                        justifyContent: "center",
                                        color: "#fff",
                                        fontSize: 11.5,
                                        fontWeight: 800,
                                        transition: "width 0.2s ease",
                                    }}
                                >
                                    Online: {onlineQuotaPercent}%
                                </div>
                                <div
                                    style={{
                                        width: `${walkInQuotaPercent}%`,
                                        background: "#0f172a",
                                        display: "flex",
                                        alignItems: "center",
                                        justifyContent: "center",
                                        color: "#eafe89",
                                        fontSize: 11.5,
                                        fontWeight: 800,
                                        transition: "width 0.2s ease",
                                    }}
                                >
                                    Khách Vãng Lai: {walkInQuotaPercent}%
                                </div>
                            </div>

                            <div style={{ fontSize: 13, color: "#475569", lineHeight: 1.6 }}>
                                💡 <strong>Giải thích nghiệp vụ:</strong> Với bãi quy mô{" "}
                                <strong>100 ô đỗ</strong> $\rightarrow$ Tối đa{" "}
                                <strong style={{ color: "#2563eb" }}>{onlineQuotaPercent} ô</strong> được
                                khách đặt trước online qua ứng dụng ParkWise.{" "}
                                <strong style={{ color: "#0f172a" }}>{walkInQuotaPercent} ô</strong> còn lại
                                luôn được bảo lưu cho khách vãng lai lái xe trực tiếp vào cổng, chống nguy cơ tắc
                                nghẽn và quá tải.
                            </div>
                        </div>
                    </div>

                    {/* BẢNG 2: TIỀN CỌC GIỮ CHỖ (DEPOSIT POLICY) */}
                    <div
                        style={{
                            background: "#fff",
                            borderRadius: 20,
                            border: "1px solid #e2e8f0",
                            padding: "28px 32px",
                            boxShadow: "0 4px 16px rgba(15,27,61,0.04)",
                        }}
                    >
                        <div
                            style={{
                                display: "flex",
                                justifyContent: "space-between",
                                alignItems: "flex-start",
                                flexWrap: "wrap",
                                gap: 12,
                                marginBottom: 16,
                            }}
                        >
                            <div>
                                <div
                                    style={{
                                        display: "inline-flex",
                                        alignItems: "center",
                                        gap: 6,
                                        background: "#fef3c7",
                                        color: "#b45309",
                                        padding: "4px 12px",
                                        borderRadius: 999,
                                        fontSize: 12,
                                        fontWeight: 800,
                                        marginBottom: 8,
                                    }}
                                >
                                    CHỐNG ẢO & GIỮ CHỖ 15 PHÚT
                                </div>
                                <h3
                                    style={{
                                        fontSize: 18,
                                        fontWeight: 800,
                                        color: "#0f172a",
                                        margin: "0 0 6px",
                                    }}
                                >
                                    Mức Tiền Cọc Đặt Chỗ (Hold Deposit Policy)
                                </h3>
                                <p style={{ fontSize: 13.5, color: "#64748b", margin: 0, lineHeight: 1.5 }}>
                                    Khoản tiền trừ từ ví khách hàng khi giữ chỗ thành công để tránh tình trạng đặt chỗ ảo.
                                </p>
                            </div>

                            <button
                                type="button"
                                onClick={handleSaveDeposit}
                                disabled={savingDeposit || savingAll}
                                style={{
                                    background: "#0f172a",
                                    color: "#fff",
                                    border: "none",
                                    borderRadius: 10,
                                    padding: "9px 20px",
                                    fontSize: 13.5,
                                    fontWeight: 700,
                                    cursor: savingDeposit ? "not-allowed" : "pointer",
                                    display: "inline-flex",
                                    alignItems: "center",
                                    gap: 6,
                                }}
                            >
                                {savingDeposit ? "Đang lưu..." : "Lưu tiền cọc"}
                            </button>
                        </div>

                        {/* Input amount & Quick Presets */}
                        <div style={{ margin: "20px 0" }}>
                            <label
                                htmlFor="deposit-input"
                                style={{
                                    display: "block",
                                    fontSize: 13,
                                    fontWeight: 700,
                                    color: "#334155",
                                    marginBottom: 6,
                                }}
                            >
                                Số tiền cọc quy định (VNĐ):
                            </label>

                            <div style={{ position: "relative", maxWidth: 360 }}>
                                <input
                                    id="deposit-input"
                                    type="number"
                                    min={10000}
                                    step={5000}
                                    value={deposit}
                                    onChange={(e) => setDeposit(Math.max(0, Number(e.target.value)))}
                                    style={{
                                        width: "100%",
                                        padding: "12px 50px 12px 16px",
                                        fontSize: 18,
                                        fontWeight: 800,
                                        borderRadius: 12,
                                        border: "1.5px solid #cbd5e1",
                                        outline: "none",
                                        color: "#0f172a",
                                    }}
                                />
                                <span
                                    style={{
                                        position: "absolute",
                                        right: 16,
                                        top: "50%",
                                        transform: "translateY(-50%)",
                                        fontSize: 15,
                                        fontWeight: 800,
                                        color: "#64748b",
                                    }}
                                >
                                    ₫
                                </span>
                            </div>

                            {/* Preset Buttons */}
                            <div style={{ display: "flex", gap: 10, marginTop: 12, flexWrap: "wrap" }}>
                                {[20000, 30000, 50000, 100000].map((preset) => (
                                    <button
                                        key={preset}
                                        type="button"
                                        onClick={() => setDeposit(preset)}
                                        style={{
                                            background: deposit === preset ? "#0f172a" : "#f1f5f9",
                                            color: deposit === preset ? "#eafe89" : "#334155",
                                            border: "none",
                                            padding: "6px 14px",
                                            borderRadius: 8,
                                            fontSize: 13,
                                            fontWeight: 700,
                                            cursor: "pointer",
                                            transition: "all 0.15s ease",
                                        }}
                                    >
                                        {preset.toLocaleString("vi-VN")} ₫
                                    </button>
                                ))}
                            </div>
                        </div>

                        <div
                            style={{
                                background: "#f8fafc",
                                border: "1px solid #e2e8f0",
                                borderRadius: 14,
                                padding: "14px 18px",
                                fontSize: 13,
                                color: "#475569",
                                lineHeight: 1.5,
                            }}
                        >
                            🛡️ <strong>Chính sách hoàn cọc:</strong> Khách hàng giữ chỗ có 15 phút để đến cổng bãi.
                            Khi xe vào bãi, khoản tiền cọc <strong>{deposit.toLocaleString("vi-VN")} ₫</strong> sẽ tự động
                            cấn trừ trực tiếp vào hóa đơn thanh toán khi kết thúc phiên đỗ. Nếu hủy trước giờ quy định, tiền
                            cọc được hoàn trả 100% về Ví ParkWise của khách.
                        </div>
                    </div>

                    {/* Action Bar Bottom */}
                    <div
                        style={{
                            display: "flex",
                            justifyContent: "flex-end",
                            gap: 12,
                            paddingTop: 8,
                        }}
                    >
                        <button
                            type="button"
                            onClick={handleSaveAll}
                            disabled={savingAll || savingRatio || savingDeposit}
                            style={{
                                background: "#eafe89",
                                color: "#09090b",
                                border: "none",
                                borderRadius: 12,
                                padding: "14px 32px",
                                fontSize: 15,
                                fontWeight: 800,
                                cursor: savingAll ? "not-allowed" : "pointer",
                                boxShadow: "0 4px 14px rgba(234,254,137,0.4)",
                                display: "inline-flex",
                                alignItems: "center",
                                gap: 8,
                            }}
                        >
                            {savingAll ? "Đang lưu cấu hình..." : "Lưu Toàn Bộ Chính Sách Bãi Xe"}
                        </button>
                    </div>
                </div>
            )}
        </div>
    );
}
