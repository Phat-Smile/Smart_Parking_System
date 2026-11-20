"use client";

import React, { useState, useEffect } from "react";
import Link from "next/link";
import { AlertTriangle, Ticket, Car, Bike, Flame } from "lucide-react";
import { useAllLots } from "@/features/owner/lots/useOwnerMapStore";
import { backendLotId } from "@/features/owner/lots/ownerLotsSync";
import {
    configurePricingApi,
    configureDepositPolicyApi,
    getDepositPolicyApi,
} from "@/features/owner/api/ownerApi";
import { ApiError } from "@/shared/errors/api-error";
import { useOwnerViewMode } from "@/features/owner/mode/useOwnerViewMode";

// ── Components Helper cho Giao diện Bảng giá chuẩn Mockup ──

function ToggleSwitch({
    checked,
    onChange,
    disabled = false,
}: {
    checked: boolean;
    onChange: (checked: boolean) => void;
    disabled?: boolean;
}) {
    return (
        <button
            type="button"
            role="switch"
            aria-checked={checked}
            disabled={disabled}
            onClick={() => onChange(!checked)}
            style={{
                width: 44,
                height: 24,
                borderRadius: 999,
                background: checked ? "#19301D" : "#E1E7DE",
                border: "none",
                padding: 2,
                cursor: disabled ? "not-allowed" : "pointer",
                display: "inline-flex",
                alignItems: "center",
                transition: "background 0.2s ease",
                flexShrink: 0,
                opacity: disabled ? 0.6 : 1,
            }}
        >
            <span
                style={{
                    width: 20,
                    height: 20,
                    borderRadius: "50%",
                    background: checked ? "#B8F34A" : "#FFFFFF",
                    boxShadow: "0 1px 3px rgba(0,0,0,0.2)",
                    transform: checked ? "translateX(20px)" : "translateX(0)",
                    transition: "transform 0.2s ease, background 0.2s ease",
                    display: "block",
                }}
            />
        </button>
    );
}

function InputWithSuffix({
    value,
    onChange,
    suffix,
    min = 0,
    step = 1000,
}: {
    value: number;
    onChange: (val: number) => void;
    suffix: string;
    min?: number;
    step?: number;
}) {
    return (
        <div
            style={{
                display: "flex",
                alignItems: "center",
                borderRadius: 8,
                border: "1px solid #E1E7DE",
                background: "#ffffff",
                overflow: "hidden",
            }}
        >
            <input
                type="number"
                min={min}
                step={step}
                value={value}
                onChange={(e) => onChange(Number(e.target.value))}
                style={{
                    flex: 1,
                    border: "none",
                    outline: "none",
                    padding: "9px 12px",
                    fontSize: 13.5,
                    fontWeight: 600,
                    color: "#172018",
                    background: "transparent",
                    width: "100%",
                }}
            />
            <span
                style={{
                    padding: "9px 12px",
                    fontSize: 12,
                    fontWeight: 600,
                    color: "#718071",
                    background: "#F6F8F4",
                    userSelect: "none",
                    whiteSpace: "nowrap",
                }}
            >
                {suffix}
            </span>
        </div>
    );
}

export default function OwnerPricingManagementPage() {
    const lots = useAllLots();
    const [selectedLotId, setSelectedLotId] = useState<number>(
        lots[0]?.id || 1,
    );

    const [viewMode, setViewMode] = useOwnerViewMode();
    const isSampleMode = viewMode === "sample";

    useEffect(() => {
        const params = new URLSearchParams(window.location.search);
        if (params.get("mode") === "sample" && viewMode !== "sample") {
            setViewMode("sample");
        }
    }, [viewMode, setViewMode]);

    // Initial snapshot values để hỗ trợ "Hoàn tác"
    const initialConfig = {
        pricingType: "block" as "block" | "turn",
        carHourly: 30000,
        motoHourly: 5000,
        blockDuration: 60,
        overnightCar: 150000,
        overnightMoto: 30000,
        deposit: 20000,
        enablePeakSurge: true,
        enableCongestionSurge: true,
    };

    // Pricing configuration state
    const [pricingType, setPricingType] = useState<"block" | "turn">(
        initialConfig.pricingType,
    );
    const [carHourly, setCarHourly] = useState<number>(initialConfig.carHourly);
    const [motoHourly, setMotoHourly] = useState<number>(
        initialConfig.motoHourly,
    );
    const [blockDuration, setBlockDuration] = useState<number>(
        initialConfig.blockDuration,
    ); // minutes
    const [overnightCar, setOvernightCar] = useState<number>(
        initialConfig.overnightCar,
    );
    const [overnightMoto, setOvernightMoto] = useState<number>(
        initialConfig.overnightMoto,
    );
    const [deposit, setDeposit] = useState<number>(initialConfig.deposit);
    const [enablePeakSurge, setEnablePeakSurge] = useState<boolean>(
        initialConfig.enablePeakSurge,
    );
    const [enableCongestionSurge, setEnableCongestionSurge] = useState<boolean>(
        initialConfig.enableCongestionSurge,
    );

    // Price simulator / preview state
    const [simVehicle, setSimVehicle] = useState<"car" | "moto">("car");
    const [simDuration, setSimDuration] = useState<number>(3); // hours
    const [isOvernight, setIsOvernight] = useState<boolean>(false);
    const [simSurgePeak, setSimSurgePeak] = useState<boolean>(false);
    const [simSurgeCongestion, setSimSurgeCongestion] =
        useState<boolean>(false);

    const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
    const [errorMessage, setErrorMessage] = useState<string>("");
    const [successMessage, setSuccessMessage] = useState<string>("");

    const currentLot = lots.find((l) => l.id === selectedLotId) || lots[0];
    const lotKey = backendLotId(currentLot);

    // Load deposit policy whenever lotId changes
    useEffect(() => {
        async function loadDeposit() {
            if (!lotKey) return;
            try {
                const res = await getDepositPolicyApi(lotKey);
                if (res.isConfigured) setDeposit(res.holdDepositVnd);
            } catch {
                // Silently fallback to current deposit
            }
        }
        void loadDeposit();
    }, [lotKey]);

    // Handle Reset ("Hoàn tác")
    const handleReset = () => {
        setPricingType(initialConfig.pricingType);
        setCarHourly(initialConfig.carHourly);
        setMotoHourly(initialConfig.motoHourly);
        setBlockDuration(initialConfig.blockDuration);
        setOvernightCar(initialConfig.overnightCar);
        setOvernightMoto(initialConfig.overnightMoto);
        setDeposit(initialConfig.deposit);
        setEnablePeakSurge(initialConfig.enablePeakSurge);
        setEnableCongestionSurge(initialConfig.enableCongestionSurge);
        setSimVehicle("car");
        setSimDuration(3);
        setIsOvernight(false);
        setSimSurgePeak(false);
        setSimSurgeCongestion(false);
        setErrorMessage("");
        setSuccessMessage("Đã hoàn tác về giá trị mặc định ban đầu.");
        setTimeout(() => setSuccessMessage(""), 3000);
    };

    // Calculate simulated breakdown & total
    const rate = simVehicle === "car" ? carHourly : motoHourly;
    const overnightRate = simVehicle === "car" ? overnightCar : overnightMoto;

    // Giá gốc
    let basePrice = 0;
    if (pricingType === "turn") {
        basePrice = rate;
    } else {
        const blocks = Math.ceil((simDuration * 60) / (blockDuration || 60));
        basePrice = blocks * (rate * ((blockDuration || 60) / 60));
    }

    // Phụ thu qua đêm
    const overnightFee = isOvernight ? overnightRate : 0;

    // Phụ thu giờ cao điểm (+20%)
    const peakFee =
        enablePeakSurge && simSurgePeak ? Math.round(basePrice * 0.2) : 0;

    // Tổng thanh toán
    const totalPayment = (isOvernight ? overnightFee : basePrice) + peakFee;

    const handleSave = async () => {
        setErrorMessage("");
        setSuccessMessage("");

        // Nếu đang ở Chế độ Xem mẫu Sandbox, lưu an toàn vào phiên thử nghiệm
        if (isSampleMode) {
            setIsSubmitting(true);
            setTimeout(() => {
                try {
                    if (typeof window !== "undefined") {
                        window.sessionStorage.setItem(
                            "parkwise_owner_sandbox_pricing",
                            JSON.stringify({
                                pricingType,
                                carHourly,
                                motoHourly,
                                blockDuration,
                                overnightCar,
                                overnightMoto,
                                deposit,
                                enablePeakSurge,
                                enableCongestionSurge,
                                savedAt: new Date().toISOString(),
                            }),
                        );
                    }
                } catch {}
                setIsSubmitting(false);
                setSuccessMessage(
                    "✓ Đã lưu bảng giá vào phiên thử nghiệm Sandbox (Không ảnh hưởng dữ liệu thật)!",
                );
                setTimeout(() => setSuccessMessage(""), 5000);
            }, 300);
            return;
        }

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
            await Promise.all([
                configurePricingApi(lotKey, "CAR", {
                    billingMode,
                    price: carHourly,
                    blockMinutes: blockMins,
                    overnightFee: overnightCar,
                    reason: "Chủ bãi cập nhật biểu giá ô tô",
                }),
                configurePricingApi(lotKey, "MOTORBIKE", {
                    billingMode,
                    price: motoHourly,
                    blockMinutes: blockMins,
                    overnightFee: overnightMoto,
                    reason: "Chủ bãi cập nhật biểu giá xe máy",
                }),
                configureDepositPolicyApi(lotKey, {
                    holdDepositVnd: deposit,
                }),
            ]);

            setSuccessMessage("Đã lưu bảng giá và chính sách cọc thành công!");
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
        <div className="pricing-page-container">
            {/* Banner khi đang ở Chế độ Xem mẫu Sandbox */}
            {isSampleMode && (
                <div
                    style={{
                        background: "#FEFCE8",
                        border: "1.5px solid #FDE047",
                        borderRadius: 14,
                        padding: "14px 18px",
                        marginBottom: 20,
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "space-between",
                        flexWrap: "wrap",
                        gap: 12,
                    }}
                >
                    <div
                        style={{
                            display: "flex",
                            alignItems: "center",
                            gap: 10,
                        }}
                    >
                        <AlertTriangle className="w-5 h-5 text-amber-600 shrink-0" />
                        <div>
                            <div
                                style={{
                                    fontWeight: 800,
                                    color: "#854D0E",
                                    fontSize: 14,
                                }}
                            >
                                Đang ở Chế độ Xem mẫu (Sandbox Preview)
                            </div>
                            <p
                                style={{
                                    margin: "2px 0 0",
                                    color: "#A16207",
                                    fontSize: 13,
                                }}
                            >
                                Bạn đang xem và thử nghiệm Bảng giá & Chính sách
                                cọc của Bản mẫu. Mọi thao tác cấu hình chỉ áp
                                dụng cho phiên thử nghiệm và không ảnh hưởng cơ
                                sở dữ liệu thực tế.
                            </p>
                        </div>
                    </div>
                    <div
                        style={{
                            display: "flex",
                            gap: 8,
                            alignItems: "center",
                        }}
                    >
                        <Link
                            href="/owner/simulation"
                            style={{
                                background: "#19301D",
                                color: "#FFFFFF",
                                padding: "7px 14px",
                                borderRadius: 8,
                                fontWeight: 700,
                                fontSize: 12.5,
                                textDecoration: "none",
                            }}
                        >
                            ← Quay lại Tổng quan mẫu
                        </Link>
                        <button
                            type="button"
                            onClick={() => {
                                setViewMode("live");
                                window.location.href = "/owner/dashboard";
                            }}
                            style={{
                                background: "#FFFFFF",
                                border: "1px solid #CA8A04",
                                color: "#854D0E",
                                padding: "6px 14px",
                                borderRadius: 8,
                                fontWeight: 700,
                                fontSize: 12.5,
                                cursor: "pointer",
                            }}
                        >
                            Về Dashboard chính →
                        </button>
                    </div>
                </div>
            )}

            {/* Header: Tiêu đề + Nút Hoàn tác + Nút Lưu cấu hình */}
            <div
                style={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    flexWrap: "wrap",
                    gap: 16,
                    marginBottom: 20,
                }}
            >
                <div>
                    <h1
                        style={{
                            fontSize: 24,
                            fontWeight: 850,
                            color: "#172018",
                            margin: "0 0 6px",
                            letterSpacing: "-0.5px",
                        }}
                    >
                        Bảng giá & Chính sách cọc
                    </h1>
                    <p style={{ fontSize: 13.5, color: "#718071", margin: 0 }}>
                        Thiết lập cách tính phí, tiền cọc và phụ thu cho từng
                        bãi đỗ xe.
                    </p>
                </div>

                <div style={{ display: "flex", gap: 10, alignItems: "center" }}>
                    <button
                        type="button"
                        onClick={handleReset}
                        style={{
                            padding: "8px 18px",
                            background: "#ffffff",
                            border: "1px solid #E1E7DE",
                            borderRadius: 10,
                            fontSize: 13.5,
                            fontWeight: 700,
                            color: "#172018",
                            cursor: "pointer",
                            transition: "all 0.15s ease",
                        }}
                    >
                        Hoàn tác
                    </button>
                    <button
                        type="button"
                        onClick={handleSave}
                        disabled={isSubmitting}
                        style={{
                            padding: "8px 20px",
                            background: "#19301D",
                            color: "#ffffff",
                            border: "none",
                            borderRadius: 10,
                            fontSize: 13.5,
                            fontWeight: 800,
                            display: "flex",
                            alignItems: "center",
                            gap: 8,
                            cursor: isSubmitting ? "not-allowed" : "pointer",
                            opacity: isSubmitting ? 0.7 : 1,
                            transition: "all 0.15s ease",
                            boxShadow: "0 2px 6px rgba(25, 48, 29, 0.2)",
                        }}
                    >
                        <svg
                            width="15"
                            height="15"
                            viewBox="0 0 24 24"
                            fill="none"
                            stroke="currentColor"
                            strokeWidth="2.2"
                            strokeLinecap="round"
                            strokeLinejoin="round"
                        >
                            <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" />
                            <polyline points="17 21 17 13 7 13 7 21" />
                            <polyline points="7 3 7 8 15 8" />
                        </svg>
                        {isSubmitting ? "Đang lưu..." : "Lưu cấu hình"}
                    </button>
                </div>
            </div>

            {/* Thanh chọn Bãi đỗ xe */}
            <div className="pricing-top-bar">
                <div style={{ display: "flex", alignItems: "center", gap: 14 }}>
                    <span
                        style={{
                            fontSize: 13.5,
                            fontWeight: 750,
                            color: "#172018",
                        }}
                    >
                        Bãi đỗ xe
                    </span>
                    <select
                        value={selectedLotId}
                        onChange={(e) =>
                            setSelectedLotId(Number(e.target.value))
                        }
                        style={{
                            padding: "8px 14px",
                            borderRadius: 10,
                            border: "1px solid #E1E7DE",
                            background: "#ffffff",
                            fontSize: 13.5,
                            fontWeight: 600,
                            color: "#172018",
                            outline: "none",
                            cursor: "pointer",
                            minWidth: 240,
                        }}
                    >
                        {lots.map((l) => (
                            <option key={l.id} value={l.id}>
                                {l.name}
                            </option>
                        ))}
                    </select>
                </div>

                <div
                    style={{
                        display: "flex",
                        alignItems: "center",
                        gap: 6,
                        fontSize: 12.5,
                        color: "#269B62",
                        fontWeight: 700,
                    }}
                >
                    <span
                        style={{
                            width: 8,
                            height: 8,
                            borderRadius: "50%",
                            background: "#269B62",
                            display: "inline-block",
                        }}
                    />
                    Đang cấu hình bãi này
                </div>
            </div>

            {/* Thông báo Alert */}
            {successMessage && (
                <div
                    role="status"
                    style={{
                        padding: "12px 18px",
                        borderRadius: 12,
                        background: "#EAF7EE",
                        border: "1px solid #C6E8AF",
                        color: "#269B62",
                        fontSize: 13.5,
                        fontWeight: 700,
                        marginBottom: 20,
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
                        background: "#FDEEEE",
                        border: "1px solid #FCD4D4",
                        color: "#DC4545",
                        fontSize: 13.5,
                        fontWeight: 700,
                        marginBottom: 20,
                    }}
                >
                    ⚠️ {errorMessage}
                </div>
            )}

            {/* ── Hàng 1: Hình thức tính giá (50%) & Biểu giá phương tiện (50%) ── */}
            <div className="pricing-grid-row-50">
                {/* ── Card 1: Hình thức tính giá ── */}
                <div className="pricing-card">
                    <div
                        style={{
                            display: "flex",
                            alignItems: "center",
                            gap: 10,
                            marginBottom: 16,
                        }}
                    >
                        <div
                            style={{
                                width: 26,
                                height: 26,
                                borderRadius: "50%",
                                background: "#EAF7EE",
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "center",
                                color: "#19301D",
                            }}
                        >
                            <svg
                                width="14"
                                height="14"
                                viewBox="0 0 24 24"
                                fill="none"
                                stroke="currentColor"
                                strokeWidth="2.5"
                                strokeLinecap="round"
                                strokeLinejoin="round"
                            >
                                <circle cx="12" cy="12" r="10" />
                                <polyline points="12 6 12 12 16 14" />
                            </svg>
                        </div>
                        <h3
                            style={{
                                fontSize: 14.5,
                                fontWeight: 800,
                                color: "#172018",
                                margin: 0,
                            }}
                        >
                            Hình thức tính giá
                        </h3>
                    </div>

                    <div className="pricing-options-2col">
                        {/* Theo block thời gian */}
                        <div
                            onClick={() => setPricingType("block")}
                            style={{
                                padding: "12px 14px",
                                borderRadius: 10,
                                border:
                                    pricingType === "block"
                                        ? "1.5px solid #19301D"
                                        : "1px solid #E1E7DE",
                                background:
                                    pricingType === "block"
                                        ? "#F6F8F4"
                                        : "#ffffff",
                                cursor: "pointer",
                                transition: "all 0.15s ease",
                            }}
                        >
                            <div
                                style={{
                                    fontSize: 13.5,
                                    fontWeight: 750,
                                    color: "#172018",
                                    marginBottom: 3,
                                }}
                            >
                                Theo block thời gian
                            </div>
                            <div
                                style={{
                                    fontSize: 11.5,
                                    color: "#718071",
                                    lineHeight: 1.35,
                                }}
                            >
                                Tính theo từng khoảng 30, 60 phút...
                            </div>
                        </div>

                        {/* Theo lượt vào / ra */}
                        <div
                            onClick={() => setPricingType("turn")}
                            style={{
                                padding: "12px 14px",
                                borderRadius: 10,
                                border:
                                    pricingType === "turn"
                                        ? "1.5px solid #19301D"
                                        : "1px solid #E1E7DE",
                                background:
                                    pricingType === "turn"
                                        ? "#F6F8F4"
                                        : "#ffffff",
                                cursor: "pointer",
                                transition: "all 0.15s ease",
                            }}
                        >
                            <div
                                style={{
                                    fontSize: 13.5,
                                    fontWeight: 750,
                                    color: "#172018",
                                    marginBottom: 3,
                                    display: "flex",
                                    alignItems: "center",
                                    gap: 6,
                                }}
                            >
                                <Ticket className="w-4 h-4 text-emerald-600 shrink-0" />
                                <span>Theo lượt vào / ra</span>
                            </div>
                            <div
                                style={{
                                    fontSize: 11.5,
                                    color: "#718071",
                                    lineHeight: 1.35,
                                }}
                            >
                                Giá cố định mỗi lượt, không tính giờ.
                            </div>
                        </div>
                    </div>

                    <div>
                        <label
                            style={{
                                display: "block",
                                fontSize: 12,
                                fontWeight: 700,
                                color: "#718071",
                                marginBottom: 6,
                            }}
                        >
                            Độ dài mỗi block
                        </label>
                        <select
                            value={blockDuration}
                            disabled={pricingType === "turn"}
                            onChange={(e) =>
                                setBlockDuration(Number(e.target.value))
                            }
                            style={{
                                width: "100%",
                                padding: "9px 12px",
                                borderRadius: 10,
                                border: "1px solid #E1E7DE",
                                background:
                                    pricingType === "turn"
                                        ? "#F6F8F4"
                                        : "#ffffff",
                                fontSize: 13,
                                color: "#172018",
                                outline: "none",
                                cursor:
                                    pricingType === "turn"
                                        ? "not-allowed"
                                        : "pointer",
                            }}
                        >
                            <option value={30}>30 phút / block</option>
                            <option value={60}>60 phút (1 giờ) / block</option>
                            <option value={120}>
                                120 phút (2 giờ) / block
                            </option>
                        </select>
                    </div>
                </div>

                {/* ── Card 2: Biểu giá phương tiện ── */}
                <div className="pricing-card">
                    <div
                        style={{
                            display: "flex",
                            alignItems: "center",
                            gap: 10,
                            marginBottom: 16,
                        }}
                    >
                        <div
                            style={{
                                width: 26,
                                height: 26,
                                borderRadius: "50%",
                                background: "#EAF7EE",
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "center",
                                color: "#19301D",
                            }}
                        >
                            <svg
                                width="14"
                                height="14"
                                viewBox="0 0 24 24"
                                fill="none"
                                stroke="currentColor"
                                strokeWidth="2.5"
                                strokeLinecap="round"
                                strokeLinejoin="round"
                            >
                                <rect
                                    x="2"
                                    y="5"
                                    width="20"
                                    height="14"
                                    rx="2"
                                />
                                <line x1="2" y1="10" x2="22" y2="10" />
                            </svg>
                        </div>
                        <h3
                            style={{
                                fontSize: 14.5,
                                fontWeight: 800,
                                color: "#172018",
                                margin: 0,
                            }}
                        >
                            Biểu giá phương tiện
                        </h3>
                    </div>

                    <div className="pricing-options-2col">
                        {/* Cột Ô tô */}
                        <div
                            style={{
                                padding: "14px 12px",
                                borderRadius: 12,
                                border: "1px solid #E1E7DE",
                                background: "#F6F8F4",
                            }}
                        >
                            <div
                                style={{
                                    fontSize: 14,
                                    fontWeight: 750,
                                    color: "#172018",
                                    marginBottom: 10,
                                    display: "flex",
                                    alignItems: "center",
                                    gap: 6,
                                }}
                            >
                                <Car className="w-4 h-4 text-blue-600 shrink-0" />
                                <span>Ô tô</span>
                            </div>

                            <label
                                style={{
                                    display: "block",
                                    fontSize: 11.5,
                                    fontWeight: 600,
                                    color: "#718071",
                                    marginBottom: 4,
                                }}
                            >
                                {pricingType === "block"
                                    ? "Giá theo block"
                                    : "Giá mỗi lượt"}
                            </label>
                            <div style={{ marginBottom: 10 }}>
                                <InputWithSuffix
                                    value={carHourly}
                                    onChange={setCarHourly}
                                    suffix="VND"
                                />
                            </div>

                            <label
                                style={{
                                    display: "block",
                                    fontSize: 11.5,
                                    fontWeight: 600,
                                    color: "#718071",
                                    marginBottom: 4,
                                }}
                            >
                                Phụ thu qua đêm
                            </label>
                            <InputWithSuffix
                                value={overnightCar}
                                onChange={setOvernightCar}
                                suffix="VND"
                                step={5000}
                            />
                        </div>

                        {/* Cột Xe máy */}
                        <div
                            style={{
                                padding: "14px 12px",
                                borderRadius: 12,
                                border: "1px solid #E1E7DE",
                                background: "#F6F8F4",
                            }}
                        >
                            <div
                                style={{
                                    fontSize: 14,
                                    fontWeight: 750,
                                    color: "#172018",
                                    marginBottom: 10,
                                    display: "flex",
                                    alignItems: "center",
                                    gap: 6,
                                }}
                            >
                                <Bike className="w-4 h-4 text-emerald-600 shrink-0" />
                                <span>Xe máy</span>
                            </div>

                            <label
                                style={{
                                    display: "block",
                                    fontSize: 11.5,
                                    fontWeight: 600,
                                    color: "#718071",
                                    marginBottom: 4,
                                }}
                            >
                                {pricingType === "block"
                                    ? "Giá theo block"
                                    : "Giá mỗi lượt"}
                            </label>
                            <div style={{ marginBottom: 10 }}>
                                <InputWithSuffix
                                    value={motoHourly}
                                    onChange={setMotoHourly}
                                    suffix="VND"
                                />
                            </div>

                            <label
                                style={{
                                    display: "block",
                                    fontSize: 11.5,
                                    fontWeight: 600,
                                    color: "#718071",
                                    marginBottom: 4,
                                }}
                            >
                                Phụ thu qua đêm
                            </label>
                            <InputWithSuffix
                                value={overnightMoto}
                                onChange={setOvernightMoto}
                                suffix="VND"
                                step={5000}
                            />
                        </div>
                    </div>
                </div>
            </div>

            {/* ── Hàng 2: Tiền cọc giữ chỗ (50%) & Giá động & giờ cao điểm (50%) ── */}
            <div className="pricing-grid-row-50">
                {/* ── Card 3: Tiền cọc giữ chỗ ── */}
                <div className="pricing-card">
                    <div
                        style={{
                            display: "flex",
                            alignItems: "center",
                            gap: 10,
                            marginBottom: 16,
                        }}
                    >
                        <div
                            style={{
                                width: 26,
                                height: 26,
                                borderRadius: "50%",
                                background: "#EAF7EE",
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "center",
                                color: "#19301D",
                            }}
                        >
                            <svg
                                width="14"
                                height="14"
                                viewBox="0 0 24 24"
                                fill="none"
                                stroke="currentColor"
                                strokeWidth="2.5"
                                strokeLinecap="round"
                                strokeLinejoin="round"
                            >
                                <rect
                                    width="20"
                                    height="14"
                                    x="2"
                                    y="5"
                                    rx="2"
                                />
                                <line x1="2" x2="22" y1="10" y2="10" />
                            </svg>
                        </div>
                        <h3
                            style={{
                                fontSize: 14.5,
                                fontWeight: 800,
                                color: "#172018",
                                margin: 0,
                            }}
                        >
                            Tiền cọc giữ chỗ
                        </h3>
                    </div>

                    <label
                        style={{
                            display: "block",
                            fontSize: 12,
                            fontWeight: 700,
                            color: "#718071",
                            marginBottom: 6,
                        }}
                    >
                        Tiền cọc khi khách đặt trước
                    </label>
                    <InputWithSuffix
                        value={deposit}
                        onChange={setDeposit}
                        suffix="VND / lượt"
                        step={5000}
                    />

                    {/* Info Callout */}
                    <div
                        style={{
                            display: "flex",
                            alignItems: "flex-start",
                            gap: 10,
                            marginTop: 16,
                            padding: "12px 14px",
                            borderRadius: 10,
                            background: "#EAF7EE",
                            border: "1px solid #C6E8AF",
                        }}
                    >
                        <svg
                            width="16"
                            height="16"
                            viewBox="0 0 24 24"
                            fill="none"
                            stroke="#269B62"
                            strokeWidth="2.4"
                            strokeLinecap="round"
                            strokeLinejoin="round"
                            style={{ marginTop: 2, flexShrink: 0 }}
                        >
                            <circle cx="12" cy="12" r="10" />
                            <line x1="12" y1="16" x2="12" y2="12" />
                            <line x1="12" y1="8" x2="12.01" y2="8" />
                        </svg>
                        <span
                            style={{
                                fontSize: 12,
                                color: "#19301D",
                                lineHeight: 1.45,
                                fontWeight: 550,
                            }}
                        >
                            Khi khách vào bãi thành công, tiền cọc được trừ vào
                            tổng phí đỗ. Nếu khách không đến sau thời gian giữ
                            chỗ, cọc xử lý theo điều khoản hủy.
                        </span>
                    </div>
                </div>

                {/* ── Card 4: Giá động & giờ cao điểm ── */}
                <div className="pricing-card">
                    <div
                        style={{
                            display: "flex",
                            alignItems: "center",
                            gap: 10,
                            marginBottom: 16,
                        }}
                    >
                        <div
                            style={{
                                width: 26,
                                height: 26,
                                borderRadius: "50%",
                                background: "#EAF7EE",
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "center",
                                color: "#19301D",
                            }}
                        >
                            <svg
                                width="14"
                                height="14"
                                viewBox="0 0 24 24"
                                fill="none"
                                stroke="currentColor"
                                strokeWidth="2.5"
                                strokeLinecap="round"
                                strokeLinejoin="round"
                            >
                                <polyline points="23 6 13.5 15.5 8.5 10.5 1 18" />
                                <polyline points="17 6 23 6 23 12" />
                            </svg>
                        </div>
                        <h3
                            style={{
                                fontSize: 14.5,
                                fontWeight: 800,
                                color: "#172018",
                                margin: 0,
                            }}
                        >
                            Giá động & giờ cao điểm
                        </h3>
                    </div>

                    {/* Mục 1: Giờ cao điểm */}
                    <div style={{ marginBottom: 18 }}>
                        <div
                            style={{
                                display: "flex",
                                justifyContent: "space-between",
                                alignItems: "center",
                                marginBottom: 4,
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
                                        fontSize: 13.5,
                                        fontWeight: 750,
                                        color: "#172018",
                                        display: "inline-flex",
                                        alignItems: "center",
                                        gap: 4,
                                    }}
                                >
                                    <Flame className="w-3.5 h-3.5 text-orange-500 shrink-0" />
                                    Giờ cao điểm
                                </span>
                                <span
                                    style={{
                                        fontSize: 11,
                                        fontWeight: 700,
                                        color: "#269B62",
                                        background: "#EAF7EE",
                                        borderRadius: 6,
                                        padding: "1px 6px",
                                    }}
                                >
                                    +20%
                                </span>
                            </div>
                            <ToggleSwitch
                                checked={enablePeakSurge}
                                onChange={setEnablePeakSurge}
                            />
                        </div>
                        <div style={{ fontSize: 12, color: "#718071" }}>
                            07:30–09:00 và 17:00–19:00, Thứ Hai đến Thứ Sáu.
                        </div>
                    </div>
                </div>
            </div>

            {/* ── Hàng 3: Card 5: Mô phỏng tính tiền (Full Width) ── */}
            <div className="pricing-sim-card">
                <div
                    style={{
                        display: "flex",
                        alignItems: "center",
                        gap: 10,
                        marginBottom: 18,
                    }}
                >
                    <div
                        style={{
                            width: 26,
                            height: 26,
                            borderRadius: "50%",
                            background: "#EAF7EE",
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "center",
                            color: "#19301D",
                        }}
                    >
                        <svg
                            width="14"
                            height="14"
                            viewBox="0 0 24 24"
                            fill="none"
                            stroke="currentColor"
                            strokeWidth="2.5"
                            strokeLinecap="round"
                            strokeLinejoin="round"
                        >
                            <rect width="16" height="20" x="4" y="2" rx="2" />
                            <line x1="8" x2="16" y1="6" y2="6" />
                            <line x1="16" x2="16" y1="14" y2="14" />
                            <path d="M16 10h.01" />
                            <path d="M12 10h.01" />
                            <path d="M8 10h.01" />
                            <path d="M12 14h.01" />
                            <path d="M8 14h.01" />
                            <path d="M12 18h.01" />
                            <path d="M8 18h.01" />
                        </svg>
                    </div>
                    <h3
                        style={{
                            fontSize: 15.5,
                            fontWeight: 800,
                            color: "#172018",
                            margin: 0,
                        }}
                    >
                        Mô phỏng tính tiền
                    </h3>
                </div>

                <div className="pricing-sim-grid">
                    <div className="pricing-sim-inputs">
                        {/* Loại phương tiện: segmented tabs */}
                        <div style={{ marginBottom: 14 }}>
                            <label
                                style={{
                                    display: "block",
                                    fontSize: 12,
                                    fontWeight: 600,
                                    color: "#718071",
                                    marginBottom: 6,
                                }}
                            >
                                Loại phương tiện
                            </label>
                            <div
                                style={{
                                    display: "flex",
                                    borderRadius: 10,
                                    border: "1px solid #E1E7DE",
                                    background: "#F6F8F4",
                                    padding: 3,
                                    gap: 3,
                                }}
                            >
                                <button
                                    type="button"
                                    onClick={() => setSimVehicle("car")}
                                    style={{
                                        flex: 1,
                                        padding: "8px 16px",
                                        border: "none",
                                        borderRadius: 7,
                                        background:
                                            simVehicle === "car"
                                                ? "#19301D"
                                                : "transparent",
                                        color:
                                            simVehicle === "car"
                                                ? "#FFFFFF"
                                                : "#718071",
                                        fontSize: 13,
                                        fontWeight:
                                            simVehicle === "car" ? 750 : 600,
                                        cursor: "pointer",
                                        transition: "all 0.15s ease",
                                        boxShadow:
                                            simVehicle === "car"
                                                ? "0 1px 3px rgba(25, 48, 29, 0.15)"
                                                : "none",
                                    }}
                                >
                                    Ô tô
                                </button>
                                <button
                                    type="button"
                                    onClick={() => setSimVehicle("moto")}
                                    style={{
                                        flex: 1,
                                        padding: "8px 16px",
                                        border: "none",
                                        borderRadius: 7,
                                        background:
                                            simVehicle === "moto"
                                                ? "#19301D"
                                                : "transparent",
                                        color:
                                            simVehicle === "moto"
                                                ? "#FFFFFF"
                                                : "#718071",
                                        fontSize: 13,
                                        fontWeight:
                                            simVehicle === "moto" ? 750 : 600,
                                        cursor: "pointer",
                                        transition: "all 0.15s ease",
                                        boxShadow:
                                            simVehicle === "moto"
                                                ? "0 1px 3px rgba(25, 48, 29, 0.15)"
                                                : "none",
                                    }}
                                >
                                    Xe máy
                                </button>
                            </div>
                        </div>

                        {/* Thời gian đỗ */}
                        <div style={{ marginBottom: 14 }}>
                            <label
                                style={{
                                    display: "block",
                                    fontSize: 12,
                                    fontWeight: 600,
                                    color: "#718071",
                                    marginBottom: 6,
                                }}
                            >
                                Thời gian đỗ
                            </label>
                            <select
                                value={simDuration}
                                onChange={(e) =>
                                    setSimDuration(Number(e.target.value))
                                }
                                style={{
                                    width: "100%",
                                    padding: "9px 12px",
                                    borderRadius: 8,
                                    border: "1px solid #E1E7DE",
                                    background: "#ffffff",
                                    fontSize: 13,
                                    color: "#172018",
                                    outline: "none",
                                    cursor: "pointer",
                                }}
                            >
                                <option value={1}>1 giờ</option>
                                <option value={2}>2 giờ</option>
                                <option value={3}>3 giờ</option>
                                <option value={4}>4 giờ</option>
                                <option value={5}>5 giờ</option>
                                <option value={8}>8 giờ</option>
                                <option value={12}>12 giờ</option>
                                <option value={24}>24 giờ (1 ngày)</option>
                            </select>
                        </div>

                        {/* Checkbox áp dụng qua đêm */}
                        <div style={{ marginBottom: 16 }}>
                            <label
                                style={{
                                    display: "inline-flex",
                                    alignItems: "center",
                                    gap: 8,
                                    cursor: "pointer",
                                    fontSize: 13,
                                    color: "#172018",
                                    fontWeight: 500,
                                }}
                            >
                                <input
                                    type="checkbox"
                                    checked={isOvernight}
                                    onChange={(e) =>
                                        setIsOvernight(e.target.checked)
                                    }
                                    style={{
                                        width: 16,
                                        height: 16,
                                        cursor: "pointer",
                                        accentColor: "#19301D",
                                    }}
                                />
                                Áp dụng phí gửi qua đêm
                            </label>
                        </div>

                        {/* Khung Giả lập tình huống */}
                        <div
                            style={{
                                padding: "14px 16px",
                                background: "#F6F8F4",
                                borderRadius: 12,
                                border: "1px solid #E1E7DE",
                                marginBottom: 20,
                            }}
                        >
                            <div
                                style={{
                                    fontSize: 12,
                                    fontWeight: 700,
                                    color: "#718071",
                                    marginBottom: 10,
                                    textTransform: "uppercase",
                                    letterSpacing: "0.4px",
                                }}
                            >
                                Giả lập tình huống
                            </div>
                            <div
                                style={{
                                    display: "flex",
                                    flexDirection: "column",
                                    gap: 10,
                                }}
                            >
                                <label
                                    style={{
                                        display: "inline-flex",
                                        alignItems: "center",
                                        gap: 8,
                                        cursor: enablePeakSurge
                                            ? "pointer"
                                            : "not-allowed",
                                        fontSize: 13,
                                        color: enablePeakSurge
                                            ? "#172018"
                                            : "#A3AFA3",
                                        fontWeight: 500,
                                    }}
                                >
                                    <input
                                        type="checkbox"
                                        checked={simSurgePeak}
                                        disabled={!enablePeakSurge}
                                        onChange={(e) =>
                                            setSimSurgePeak(e.target.checked)
                                        }
                                        style={{
                                            width: 15,
                                            height: 15,
                                            accentColor: "#19301D",
                                        }}
                                    />
                                    Giờ cao điểm (+20%)
                                </label>
                            </div>
                        </div>
                    </div>

                    <div className="pricing-sim-results">
                        {/* Banner Tổng thanh toán nổi bật: Forest Green #19301D + Lime #B8F34A label + White text */}
                        <div
                            style={{
                                background: "#19301D",
                                borderRadius: 12,
                                padding: "16px 20px",
                                marginBottom: 18,
                            }}
                        >
                            <div
                                style={{
                                    fontSize: 11.5,
                                    fontWeight: 750,
                                    color: "#B8F34A",
                                    marginBottom: 4,
                                    textTransform: "uppercase",
                                    letterSpacing: "0.5px",
                                }}
                            >
                                Tổng thanh toán
                            </div>
                            <div
                                style={{
                                    fontSize: 28,
                                    fontWeight: 850,
                                    color: "#ffffff",
                                    letterSpacing: "-0.5px",
                                }}
                            >
                                {totalPayment.toLocaleString("vi-VN")} đ
                            </div>
                        </div>

                        {/* Bảng kê chi tiết Breakdown lines */}
                        <div
                            style={{
                                display: "flex",
                                flexDirection: "column",
                                gap: 12,
                                fontSize: 13,
                            }}
                        >
                            <div
                                style={{
                                    display: "flex",
                                    justifyContent: "space-between",
                                    color: "#718071",
                                }}
                            >
                                <span>Giá gốc</span>
                                <span
                                    style={{
                                        fontWeight: 650,
                                        color: "#172018",
                                    }}
                                >
                                    {basePrice.toLocaleString("vi-VN")} đ
                                </span>
                            </div>

                            <div
                                style={{
                                    display: "flex",
                                    justifyContent: "space-between",
                                    color: "#718071",
                                }}
                            >
                                <span>Phụ thu qua đêm</span>
                                <span
                                    style={{
                                        color:
                                            overnightFee > 0
                                                ? "#172018"
                                                : "#718071",
                                        fontWeight:
                                            overnightFee > 0 ? 600 : 400,
                                    }}
                                >
                                    {overnightFee.toLocaleString("vi-VN")} đ
                                </span>
                            </div>

                            <div
                                style={{
                                    display: "flex",
                                    justifyContent: "space-between",
                                    color: "#718071",
                                }}
                            >
                                <span>Phụ thu giờ cao điểm</span>
                                <span
                                    style={{
                                        color:
                                            peakFee > 0 ? "#172018" : "#718071",
                                        fontWeight: peakFee > 0 ? 600 : 400,
                                    }}
                                >
                                    {peakFee.toLocaleString("vi-VN")} đ
                                </span>
                            </div>

                            <div
                                style={{
                                    display: "flex",
                                    justifyContent: "space-between",
                                    paddingTop: 12,
                                    borderTop: "1px solid #E1E7DE",
                                    color: "#172018",
                                    fontWeight: 650,
                                }}
                            >
                                <span>Tiền cọc (trừ khi ra bãi)</span>
                                <span
                                    style={{
                                        fontWeight: 800,
                                        color: "#172018",
                                    }}
                                >
                                    {deposit.toLocaleString("vi-VN")} đ
                                </span>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}
