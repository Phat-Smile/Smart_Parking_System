"use client";

import React, { useState, useEffect } from "react";
import Link from "next/link";
import { OwnerPageHeader } from "@/components/layout/OwnerPageHeader";
import { useAllLots } from "@/features/owner/lots/useOwnerMapStore";
import { backendLotId } from "@/features/owner/lots/ownerLotsSync";
import {
    configurePricingApi,
    configureDepositPolicyApi,
    getDepositPolicyApi,
} from "@/features/owner/api/ownerApi";
import { ApiError } from "@/shared/errors/api-error";

export default function OwnerPricingManagementPage() {
    const lots = useAllLots();
    const [selectedLotId, setSelectedLotId] = useState<number>(
        lots[0]?.id || 1,
    );

    // Pricing configuration state
    const [pricingType, setPricingType] = useState<"block" | "turn">("block");
    const [carHourly, setCarHourly] = useState<number>(30000);
    const [motoHourly, setMotoHourly] = useState<number>(5000);
    const [blockDuration, setBlockDuration] = useState<number>(60); // minutes
    const [overnightCar, setOvernightCar] = useState<number>(150000);
    const [overnightMoto, setOvernightMoto] = useState<number>(30000);
    const [deposit, setDeposit] = useState<number>(20000);

    // Price simulator / preview state
    const [simDuration, setSimDuration] = useState<number>(3); // hours
    const [simVehicle, setSimVehicle] = useState<"car" | "moto">("car");
    const [isOvernight, setIsOvernight] = useState<boolean>(false);
    const [enablePeakSurge, setEnablePeakSurge] = useState<boolean>(true);
    const [enableCongestionSurge, setEnableCongestionSurge] =
        useState<boolean>(true);
    const [simSurgePeak, setSimSurgePeak] = useState<boolean>(false);
    const [simSurgeCongestion, setSimSurgeCongestion] =
        useState<boolean>(false);

    const [isLoadingDeposit, setIsLoadingDeposit] = useState<boolean>(false);
    const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
    const [errorMessage, setErrorMessage] = useState<string>("");
    const [successMessage, setSuccessMessage] = useState<string>("");

    const currentLot = lots.find((l) => l.id === selectedLotId) || lots[0];
    // Parking keys lots by GUID; a design lot without one cannot be saved.
    const lotKey = backendLotId(currentLot);

    // Load deposit policy whenever lotId changes
    useEffect(() => {
        async function loadDeposit() {
            if (!lotKey) return;
            setIsLoadingDeposit(true);
            try {
                const res = await getDepositPolicyApi(lotKey);
                if (res.isConfigured) setDeposit(res.holdDepositVnd);
            } catch {
                setErrorMessage("Không tải được mức cọc hiện tại của bãi này.");
            } finally {
                setIsLoadingDeposit(false);
            }
        }
        loadDeposit();
    }, [lotKey]);

    // Calculate estimated price
    const calculatePreviewPrice = () => {
        const rate = simVehicle === "car" ? carHourly : motoHourly;
        const overnightRate =
            simVehicle === "car" ? overnightCar : overnightMoto;

        if (isOvernight) {
            return overnightRate;
        }
        if (pricingType === "turn") {
            return rate;
        }
        const blocks = Math.ceil((simDuration * 60) / (blockDuration || 60));
        return blocks * (rate * ((blockDuration || 60) / 60));
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

        const billingMode = pricingType === "block" ? "Block" : "PerEntry";
        const blockMins = pricingType === "block" ? blockDuration : null;

        try {
            // 1. Configure pricing for Car (PUT /api/parking/lots/{lotId}/pricing/CAR)
            await configurePricingApi(lotKey, "CAR", {
                billingMode,
                price: carHourly,
                blockMinutes: blockMins,
                overnightFee: overnightCar,
                reason: "Chủ bãi cập nhật biểu giá ô tô",
            });

            // 2. Configure pricing for Motorbike (PUT /api/parking/lots/{lotId}/pricing/MOTORBIKE)
            await configurePricingApi(lotKey, "MOTORBIKE", {
                billingMode,
                price: motoHourly,
                blockMinutes: blockMins,
                overnightFee: overnightMoto,
                reason: "Chủ bãi cập nhật biểu giá xe máy",
            });

            // 3. Configure deposit policy (PUT /api/parking/lots/{lotId}/deposit-policy)
            await configureDepositPolicyApi(lotKey, {
                holdDepositVnd: deposit,
            });

            setSuccessMessage(
                "Đã lưu biểu giá và chính sách cọc thành công lên máy chủ!",
            );
            setTimeout(() => setSuccessMessage(""), 4000);
        } catch (err: unknown) {
            if (err instanceof ApiError) {
                setErrorMessage(`Lỗi từ máy chủ: ${err.message}`);
            } else {
                setErrorMessage(
                    "Không kết nối được máy chủ. Biểu giá chưa được lưu.",
                );
            }
        } finally {
            setIsSubmitting(false);
        }
    };

    return (
        <div style={{ maxWidth: 1200, margin: "0 auto", paddingBottom: 60 }}>
            <OwnerPageHeader
                breadcrumb="/owner/pricing"
                title="Bảng giá & Chính sách cọc"
                subtitle="Cấu hình biểu giá giữ xe và tiền cọc giữ chỗ qua hệ thống máy chủ"
            />

            {/* Lot selector bar */}
            <div
                style={{
                    background: "#fff",
                    borderRadius: 16,
                    padding: "16px 20px",
                    border: "1px solid #e3e8f2",
                    marginBottom: 24,
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "space-between",
                    flexWrap: "wrap",
                    gap: 16,
                }}
            >
                <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
                    <label
                        htmlFor="lot-select"
                        style={{
                            fontSize: 13.5,
                            fontWeight: 700,
                            color: "#0f1b3d",
                        }}
                    >
                        Chọn bãi đỗ xe:
                    </label>
                    <select
                        id="lot-select"
                        value={selectedLotId}
                        onChange={(e) =>
                            setSelectedLotId(Number(e.target.value))
                        }
                        style={{
                            padding: "8px 14px",
                            borderRadius: 10,
                            border: "1.5px solid #2563eb",
                            background: "#f8faff",
                            color: "#0f1b3d",
                            fontSize: 13.5,
                            fontWeight: 700,
                            outline: "none",
                            cursor: "pointer",
                        }}
                    >
                        {lots.map((l) => (
                            <option key={l.id} value={l.id}>
                                {l.name} ({l.addr || "TP.HCM"})
                            </option>
                        ))}
                    </select>
                </div>

                <div style={{ fontSize: 13, color: "#6a7491" }}>
                    Đang cấu hình:{" "}
                    <b style={{ color: "#0f1b3d" }}>{currentLot?.name}</b>
                </div>
            </div>

            {/* Alerts */}
            {successMessage && (
                <div
                    role="status"
                    style={{
                        padding: "12px 18px",
                        borderRadius: 12,
                        background: "#def7ec",
                        border: "1px solid #bcf0da",
                        color: "#0e9f6e",
                        fontSize: 13.5,
                        fontWeight: 700,
                        marginBottom: 20,
                        display: "flex",
                        alignItems: "center",
                        gap: 10,
                    }}
                >
                    ✓ {successMessage}
                </div>
            )}

            {errorMessage && (
                <div
                    role="alert"
                    style={{
                        padding: "12px 18px",
                        borderRadius: 12,
                        background: "#fde8e8",
                        border: "1px solid #fbd5d5",
                        color: "#9b1c1c",
                        fontSize: 13.5,
                        fontWeight: 700,
                        marginBottom: 20,
                        display: "flex",
                        alignItems: "center",
                        gap: 10,
                    }}
                >
                    ⚠️ {errorMessage}
                </div>
            )}

            <div
                style={{
                    display: "grid",
                    gridTemplateColumns:
                        "minmax(0, 1.8fr) minmax(320px, 1.2fr)",
                    gap: 24,
                }}
            >
                {/* Left column: Form configuration */}
                <div
                    style={{
                        display: "flex",
                        flexDirection: "column",
                        gap: 20,
                    }}
                >
                    {/* Card 1: Cách tính giá */}
                    <div
                        style={{
                            background: "#fff",
                            borderRadius: 20,
                            border: "1px solid #e3e8f2",
                            padding: 24,
                        }}
                    >
                        <h3
                            style={{
                                fontSize: 16,
                                fontWeight: 800,
                                color: "#0f1b3d",
                                margin: "0 0 16px",
                            }}
                        >
                            1. Hình thức tính giá
                        </h3>

                        <div
                            style={{
                                display: "grid",
                                gridTemplateColumns: "1fr 1fr",
                                gap: 14,
                            }}
                        >
                            <button
                                type="button"
                                onClick={() => setPricingType("block")}
                                style={{
                                    padding: 16,
                                    borderRadius: 14,
                                    border:
                                        pricingType === "block"
                                            ? "2px solid #2563eb"
                                            : "1px solid #e3e8f2",
                                    background:
                                        pricingType === "block"
                                            ? "#eff6ff"
                                            : "#fff",
                                    textAlign: "left",
                                    cursor: "pointer",
                                    transition: "all 0.15s ease",
                                }}
                            >
                                <div
                                    style={{
                                        fontWeight: 800,
                                        fontSize: 14,
                                        color: "#0f1b3d",
                                        marginBottom: 4,
                                    }}
                                >
                                    ⏱ Theo block thời gian
                                </div>
                                <div style={{ fontSize: 12, color: "#6a7491" }}>
                                    Tính cước theo từng khoảng thời gian (30p,
                                    60p, ...)
                                </div>
                            </button>

                            <button
                                type="button"
                                onClick={() => setPricingType("turn")}
                                style={{
                                    padding: 16,
                                    borderRadius: 14,
                                    border:
                                        pricingType === "turn"
                                            ? "2px solid #2563eb"
                                            : "1px solid #e3e8f2",
                                    background:
                                        pricingType === "turn"
                                            ? "#eff6ff"
                                            : "#fff",
                                    textAlign: "left",
                                    cursor: "pointer",
                                    transition: "all 0.15s ease",
                                }}
                            >
                                <div
                                    style={{
                                        fontWeight: 800,
                                        fontSize: 14,
                                        color: "#0f1b3d",
                                        marginBottom: 4,
                                    }}
                                >
                                    🎫 Theo lượt vào / ra
                                </div>
                                <div style={{ fontSize: 12, color: "#6a7491" }}>
                                    Giá cố định cho mỗi lượt đỗ xe bất kể thời
                                    gian
                                </div>
                            </button>
                        </div>

                        {pricingType === "block" && (
                            <div style={{ marginTop: 18 }}>
                                <label
                                    style={{
                                        display: "block",
                                        fontSize: 13,
                                        fontWeight: 700,
                                        color: "#0f1b3d",
                                        marginBottom: 6,
                                    }}
                                >
                                    Độ dài block tính tiền:
                                </label>
                                <select
                                    value={blockDuration}
                                    onChange={(e) =>
                                        setBlockDuration(Number(e.target.value))
                                    }
                                    style={{
                                        width: "100%",
                                        padding: "10px 14px",
                                        borderRadius: 10,
                                        border: "1px solid #e3e8f2",
                                        background: "#f8faff",
                                        fontSize: 13.5,
                                        color: "#0f1b3d",
                                        outline: "none",
                                    }}
                                >
                                    <option value={30}>30 phút / block</option>
                                    <option value={60}>
                                        60 phút (1 giờ) / block
                                    </option>
                                    <option value={120}>
                                        120 phút (2 giờ) / block
                                    </option>
                                </select>
                            </div>
                        )}
                    </div>

                    {/* Card 2: Mức giá cho từng loại phương tiện */}
                    <div
                        style={{
                            background: "#fff",
                            borderRadius: 20,
                            border: "1px solid #e3e8f2",
                            padding: 24,
                        }}
                    >
                        <h3
                            style={{
                                fontSize: 16,
                                fontWeight: 800,
                                color: "#0f1b3d",
                                margin: "0 0 16px",
                            }}
                        >
                            2. Biểu giá phương tiện
                        </h3>

                        <div
                            style={{
                                display: "grid",
                                gridTemplateColumns:
                                    "repeat(auto-fit, minmax(220px, 1fr))",
                                gap: 16,
                            }}
                        >
                            {/* Ô tô */}
                            <div
                                style={{
                                    padding: 16,
                                    borderRadius: 14,
                                    border: "1px solid #e3e8f2",
                                    background: "#fafbfc",
                                }}
                            >
                                <div
                                    style={{
                                        fontWeight: 800,
                                        fontSize: 14,
                                        color: "#0f1b3d",
                                        marginBottom: 12,
                                    }}
                                >
                                    🚗 Ô tô (Car)
                                </div>
                                <div style={{ marginBottom: 12 }}>
                                    <label
                                        style={{
                                            display: "block",
                                            fontSize: 12,
                                            fontWeight: 700,
                                            color: "#6a7491",
                                            marginBottom: 4,
                                        }}
                                    >
                                        {pricingType === "block"
                                            ? "Giá theo block (đ):"
                                            : "Giá mỗi lượt (đ):"}
                                    </label>
                                    <input
                                        type="number"
                                        min="0"
                                        step="1000"
                                        value={carHourly}
                                        onChange={(e) =>
                                            setCarHourly(Number(e.target.value))
                                        }
                                        style={{
                                            width: "100%",
                                            padding: "8px 12px",
                                            borderRadius: 8,
                                            border: "1px solid #e3e8f2",
                                            background: "#fff",
                                            fontSize: 14,
                                            fontWeight: 700,
                                            color: "#0f1b3d",
                                        }}
                                    />
                                </div>
                                <div>
                                    <label
                                        style={{
                                            display: "block",
                                            fontSize: 12,
                                            fontWeight: 700,
                                            color: "#6a7491",
                                            marginBottom: 4,
                                        }}
                                    >
                                        Phụ thu qua đêm (đ):
                                    </label>
                                    <input
                                        type="number"
                                        min="0"
                                        step="5000"
                                        value={overnightCar}
                                        onChange={(e) =>
                                            setOvernightCar(
                                                Number(e.target.value),
                                            )
                                        }
                                        style={{
                                            width: "100%",
                                            padding: "8px 12px",
                                            borderRadius: 8,
                                            border: "1px solid #e3e8f2",
                                            background: "#fff",
                                            fontSize: 14,
                                            fontWeight: 700,
                                            color: "#0f1b3d",
                                        }}
                                    />
                                </div>
                            </div>

                            {/* Xe máy */}
                            <div
                                style={{
                                    padding: 16,
                                    borderRadius: 14,
                                    border: "1px solid #e3e8f2",
                                    background: "#fafbfc",
                                }}
                            >
                                <div
                                    style={{
                                        fontWeight: 800,
                                        fontSize: 14,
                                        color: "#0f1b3d",
                                        marginBottom: 12,
                                    }}
                                >
                                    🛵 Xe máy (Motorbike)
                                </div>
                                <div style={{ marginBottom: 12 }}>
                                    <label
                                        style={{
                                            display: "block",
                                            fontSize: 12,
                                            fontWeight: 700,
                                            color: "#6a7491",
                                            marginBottom: 4,
                                        }}
                                    >
                                        {pricingType === "block"
                                            ? "Giá theo block (đ):"
                                            : "Giá mỗi lượt (đ):"}
                                    </label>
                                    <input
                                        type="number"
                                        min="0"
                                        step="1000"
                                        value={motoHourly}
                                        onChange={(e) =>
                                            setMotoHourly(
                                                Number(e.target.value),
                                            )
                                        }
                                        style={{
                                            width: "100%",
                                            padding: "8px 12px",
                                            borderRadius: 8,
                                            border: "1px solid #e3e8f2",
                                            background: "#fff",
                                            fontSize: 14,
                                            fontWeight: 700,
                                            color: "#0f1b3d",
                                        }}
                                    />
                                </div>
                                <div>
                                    <label
                                        style={{
                                            display: "block",
                                            fontSize: 12,
                                            fontWeight: 700,
                                            color: "#6a7491",
                                            marginBottom: 4,
                                        }}
                                    >
                                        Phụ thu qua đêm (đ):
                                    </label>
                                    <input
                                        type="number"
                                        min="0"
                                        step="5000"
                                        value={overnightMoto}
                                        onChange={(e) =>
                                            setOvernightMoto(
                                                Number(e.target.value),
                                            )
                                        }
                                        style={{
                                            width: "100%",
                                            padding: "8px 12px",
                                            borderRadius: 8,
                                            border: "1px solid #e3e8f2",
                                            background: "#fff",
                                            fontSize: 14,
                                            fontWeight: 700,
                                            color: "#0f1b3d",
                                        }}
                                    />
                                </div>
                            </div>
                        </div>
                    </div>

                    {/* Card 3: Chính sách đặt cọc giữ chỗ */}
                    <div
                        style={{
                            background: "#fff",
                            borderRadius: 20,
                            border: "1px solid #e3e8f2",
                            padding: 24,
                        }}
                    >
                        <h3
                            style={{
                                fontSize: 16,
                                fontWeight: 800,
                                color: "#0f1b3d",
                                margin: "0 0 16px",
                            }}
                        >
                            3. Chính sách tiền cọc giữ chỗ (Deposit Policy)
                        </h3>

                        <div
                            style={{
                                display: "flex",
                                flexDirection: "column",
                                gap: 12,
                            }}
                        >
                            <div>
                                <label
                                    style={{
                                        display: "block",
                                        fontSize: 13,
                                        fontWeight: 700,
                                        color: "#0f1b3d",
                                        marginBottom: 6,
                                    }}
                                >
                                    Tiền cọc yêu cầu khi khách đặt trước (VND):
                                </label>
                                <div
                                    style={{
                                        display: "flex",
                                        alignItems: "center",
                                        gap: 12,
                                    }}
                                >
                                    <input
                                        type="number"
                                        min="0"
                                        step="5000"
                                        value={deposit}
                                        onChange={(e) =>
                                            setDeposit(Number(e.target.value))
                                        }
                                        style={{
                                            flex: 1,
                                            padding: "10px 14px",
                                            borderRadius: 10,
                                            border: "1.5px solid #2563eb",
                                            background: "#f8faff",
                                            fontSize: 15,
                                            fontWeight: 800,
                                            color: "#2563eb",
                                            outline: "none",
                                        }}
                                    />
                                    <span
                                        style={{
                                            fontSize: 13,
                                            color: "#6a7491",
                                            fontWeight: 600,
                                        }}
                                    >
                                        VNĐ / lượt đặt
                                    </span>
                                </div>
                            </div>

                            <div
                                style={{
                                    fontSize: 12.5,
                                    color: "#6a7491",
                                    lineHeight: 1.5,
                                }}
                            >
                                ℹ️ Tiền cọc sẽ được trừ trực tiếp vào tổng tiền
                                phiên đỗ khi khách check-in bãi đỗ thành công.
                                Nếu khách không đến nhận chỗ sau thời gian giữ
                                chỗ quy định, tiền cọc sẽ được xử lý theo điều
                                khoản hủy.
                            </div>
                        </div>
                    </div>

                    {/* Card 4: Chính sách Giá Động & Giờ Cao Điểm */}
                    <div
                        style={{
                            background: "#fff",
                            borderRadius: 20,
                            border: "1px solid #e3e8f2",
                            padding: 24,
                        }}
                    >
                        <div
                            style={{
                                display: "flex",
                                alignItems: "center",
                                gap: 10,
                                marginBottom: 12,
                            }}
                        >
                            <span style={{ fontSize: 20 }}>🔥</span>
                            <h3
                                style={{
                                    fontSize: 16,
                                    fontWeight: 800,
                                    color: "#0f1b3d",
                                    margin: 0,
                                }}
                            >
                                4. Chính sách Giá Động & Giờ Cao Điểm (Smart
                                Dynamic Surge)
                            </h3>
                        </div>
                        <p
                            style={{
                                fontSize: 13,
                                color: "#6a7491",
                                margin: "0 0 16px",
                            }}
                        >
                            Tự động tăng giá linh hoạt dựa trên khung giờ cao
                            điểm đô thị và tỷ lệ lấp đầy bãi để tối ưu hóa doanh
                            thu.
                        </p>

                        <div
                            style={{
                                display: "flex",
                                flexDirection: "column",
                                gap: 14,
                            }}
                        >
                            <label
                                style={{
                                    display: "flex",
                                    alignItems: "flex-start",
                                    gap: 12,
                                    padding: "12px 14px",
                                    borderRadius: 12,
                                    background: enablePeakSurge
                                        ? "#fffbeb"
                                        : "#f8faff",
                                    border: enablePeakSurge
                                        ? "1.5px solid #f59e0b"
                                        : "1px solid #e3e8f2",
                                    cursor: "pointer",
                                }}
                            >
                                <input
                                    type="checkbox"
                                    checked={enablePeakSurge}
                                    onChange={(e) =>
                                        setEnablePeakSurge(e.target.checked)
                                    }
                                    style={{ marginTop: 3 }}
                                />
                                <div>
                                    <strong
                                        style={{
                                            display: "block",
                                            fontSize: 13.5,
                                            color: "#0f1b3d",
                                        }}
                                    >
                                        Bật phụ thu Giờ cao điểm (+20% đơn giá)
                                    </strong>
                                    <span
                                        style={{
                                            fontSize: 12,
                                            color: "#6a7491",
                                            lineHeight: 1.4,
                                        }}
                                    >
                                        Áp dụng tự động vào khung giờ Sáng
                                        (07:30 - 09:00) và Chiều (17:00 - 19:00)
                                        từ Thứ Hai đến Thứ Sáu.
                                    </span>
                                </div>
                            </label>

                            <label
                                style={{
                                    display: "flex",
                                    alignItems: "flex-start",
                                    gap: 12,
                                    padding: "12px 14px",
                                    borderRadius: 12,
                                    background: enableCongestionSurge
                                        ? "#fef2f2"
                                        : "#f8faff",
                                    border: enableCongestionSurge
                                        ? "1.5px solid #ef4444"
                                        : "1px solid #e3e8f2",
                                    cursor: "pointer",
                                }}
                            >
                                <input
                                    type="checkbox"
                                    checked={enableCongestionSurge}
                                    onChange={(e) =>
                                        setEnableCongestionSurge(
                                            e.target.checked,
                                        )
                                    }
                                    style={{ marginTop: 3 }}
                                />
                                <div>
                                    <strong
                                        style={{
                                            display: "block",
                                            fontSize: 13.5,
                                            color: "#0f1b3d",
                                        }}
                                    >
                                        Bật phụ thu Bão ô đỗ / Tắc nghẽn (+30%
                                        đơn giá)
                                    </strong>
                                    <span
                                        style={{
                                            fontSize: 12,
                                            color: "#6a7491",
                                            lineHeight: 1.4,
                                        }}
                                    >
                                        Tự động kích hoạt khi tỷ lệ chiếm dụng
                                        bãi vượt quá 85% sức chứa để điều tiết
                                        lưu lượng xe.
                                    </span>
                                </div>
                            </label>
                        </div>
                    </div>

                    <button
                        type="button"
                        onClick={handleSave}
                        disabled={isSubmitting}
                        style={{
                            background: "#0f1b3d",
                            color: "#fff",
                            border: "none",
                            borderRadius: 12,
                            padding: "14px 28px",
                            fontSize: 14,
                            fontWeight: 800,
                            cursor: isSubmitting ? "not-allowed" : "pointer",
                            opacity: isSubmitting ? 0.7 : 1,
                            boxShadow: "0 4px 14px rgba(15,27,61,0.2)",
                            alignSelf: "flex-start",
                        }}
                    >
                        {isSubmitting
                            ? "Đang lưu lên máy chủ..."
                            : "💾 Lưu toàn bộ biểu giá & Chính sách cọc"}
                    </button>
                </div>

                {/* Right column: Simulator / Preview */}
                <div
                    style={{
                        display: "flex",
                        flexDirection: "column",
                        gap: 20,
                    }}
                >
                    <div
                        style={{
                            background: "#fff",
                            borderRadius: 20,
                            border: "1px solid #e3e8f2",
                            padding: 24,
                            position: "sticky",
                            top: 24,
                        }}
                    >
                        <h3
                            style={{
                                fontSize: 16,
                                fontWeight: 800,
                                color: "#0f1b3d",
                                margin: "0 0 16px",
                            }}
                        >
                            🔍 Mô phỏng tính tiền thực tế
                        </h3>

                        <div
                            style={{
                                display: "flex",
                                flexDirection: "column",
                                gap: 14,
                            }}
                        >
                            <div>
                                <label
                                    style={{
                                        display: "block",
                                        fontSize: 12,
                                        fontWeight: 700,
                                        color: "#6a7491",
                                        marginBottom: 6,
                                    }}
                                >
                                    Loại phương tiện kiểm tra:
                                </label>
                                <div
                                    style={{
                                        display: "grid",
                                        gridTemplateColumns: "1fr 1fr",
                                        gap: 8,
                                    }}
                                >
                                    <button
                                        type="button"
                                        onClick={() => setSimVehicle("car")}
                                        style={{
                                            padding: "8px 12px",
                                            borderRadius: 8,
                                            border:
                                                simVehicle === "car"
                                                    ? "2px solid #2563eb"
                                                    : "1px solid #e3e8f2",
                                            background:
                                                simVehicle === "car"
                                                    ? "#eff6ff"
                                                    : "#fff",
                                            fontSize: 12.5,
                                            fontWeight: 700,
                                            cursor: "pointer",
                                        }}
                                    >
                                        🚗 Ô tô
                                    </button>
                                    <button
                                        type="button"
                                        onClick={() => setSimVehicle("moto")}
                                        style={{
                                            padding: "8px 12px",
                                            borderRadius: 8,
                                            border:
                                                simVehicle === "moto"
                                                    ? "2px solid #2563eb"
                                                    : "1px solid #e3e8f2",
                                            background:
                                                simVehicle === "moto"
                                                    ? "#eff6ff"
                                                    : "#fff",
                                            fontSize: 12.5,
                                            fontWeight: 700,
                                            cursor: "pointer",
                                        }}
                                    >
                                        🛵 Xe máy
                                    </button>
                                </div>
                            </div>

                            <div>
                                <label
                                    style={{
                                        display: "block",
                                        fontSize: 12,
                                        fontWeight: 700,
                                        color: "#6a7491",
                                        marginBottom: 6,
                                    }}
                                >
                                    Thời gian đỗ xe:
                                </label>
                                <select
                                    value={simDuration}
                                    onChange={(e) =>
                                        setSimDuration(Number(e.target.value))
                                    }
                                    style={{
                                        width: "100%",
                                        padding: "8px 12px",
                                        borderRadius: 8,
                                        border: "1px solid #e3e8f2",
                                        fontSize: 13,
                                        color: "#0f1b3d",
                                    }}
                                >
                                    <option value={1}>1 giờ</option>
                                    <option value={2}>2 giờ</option>
                                    <option value={3}>3 giờ</option>
                                    <option value={5}>5 giờ</option>
                                    <option value={8}>8 giờ</option>
                                    <option value={12}>12 giờ</option>
                                </select>
                            </div>

                            <label
                                style={{
                                    display: "flex",
                                    alignItems: "center",
                                    gap: 8,
                                    cursor: "pointer",
                                    fontSize: 13,
                                }}
                            >
                                <input
                                    type="checkbox"
                                    checked={isOvernight}
                                    onChange={(e) =>
                                        setIsOvernight(e.target.checked)
                                    }
                                />
                                <span
                                    style={{
                                        fontWeight: 600,
                                        color: "#0f1b3d",
                                    }}
                                >
                                    Áp dụng phí gửi qua đêm
                                </span>
                            </label>

                            {/* Surge Simulator Toggles */}
                            <div
                                style={{
                                    padding: "10px 12px",
                                    background: "#fff",
                                    borderRadius: 10,
                                    border: "1px solid #e3e8f2",
                                    display: "flex",
                                    flexDirection: "column",
                                    gap: 8,
                                }}
                            >
                                <span
                                    style={{
                                        fontSize: 11.5,
                                        fontWeight: 700,
                                        color: "#6a7491",
                                        textTransform: "uppercase",
                                    }}
                                >
                                    Thử nghiệm Giá Động:
                                </span>
                                <label
                                    style={{
                                        display: "flex",
                                        alignItems: "center",
                                        gap: 8,
                                        cursor: "pointer",
                                        fontSize: 12.5,
                                        color: "#0f1b3d",
                                    }}
                                >
                                    <input
                                        type="checkbox"
                                        checked={simSurgePeak}
                                        onChange={(e) =>
                                            setSimSurgePeak(e.target.checked)
                                        }
                                        disabled={!enablePeakSurge}
                                    />
                                    <span>🔥 Giả lập Giờ cao điểm (+20%)</span>
                                </label>
                                <label
                                    style={{
                                        display: "flex",
                                        alignItems: "center",
                                        gap: 8,
                                        cursor: "pointer",
                                        fontSize: 12.5,
                                        color: "#0f1b3d",
                                    }}
                                >
                                    <input
                                        type="checkbox"
                                        checked={simSurgeCongestion}
                                        onChange={(e) =>
                                            setSimSurgeCongestion(
                                                e.target.checked,
                                            )
                                        }
                                        disabled={!enableCongestionSurge}
                                    />
                                    <span>
                                        🚨 Giả lập Bão ô đỗ &gt;85% (+30%)
                                    </span>
                                </label>
                            </div>

                            {/* Estimate Box */}
                            <div
                                style={{
                                    marginTop: 10,
                                    padding: 16,
                                    borderRadius: 14,
                                    background: "#f8faff",
                                    border: "1.5px dashed #bfdbfe",
                                }}
                            >
                                <div
                                    style={{
                                        fontSize: 12,
                                        color: "#6a7491",
                                        fontWeight: 600,
                                    }}
                                >
                                    Cước phí khách hàng thanh toán:
                                </div>
                                <div
                                    style={{
                                        fontSize: 24,
                                        fontWeight: 900,
                                        color: "#2563eb",
                                        margin: "6px 0",
                                    }}
                                >
                                    {calculatePreviewPrice().toLocaleString(
                                        "vi-VN",
                                    )}{" "}
                                    đ
                                </div>
                                <div
                                    style={{ fontSize: 11.5, color: "#6a7491" }}
                                >
                                    Tiền cọc yêu cầu:{" "}
                                    <b>{deposit.toLocaleString("vi-VN")} đ</b>{" "}
                                    (khách trả khi đặt, bù trừ khi check-out)
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}
