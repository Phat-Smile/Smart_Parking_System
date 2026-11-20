"use client";

import React, { useState, useEffect } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { PageHeader } from "@/components/layout/PageHeader";
import { useOwnerStore } from "@/store/useOwnerStore";
import { vnd } from "@/types/owner";

const IconCalculator = () => (
    <svg
        width="16"
        height="16"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
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
);
const IconArrowLeft = () => (
    <svg
        width="14"
        height="14"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
    >
        <path d="m12 19-7-7 7-7" />
        <path d="M19 12H5" />
    </svg>
);
const IconCheck = ({ style }: { style?: React.CSSProperties }) => (
    <svg
        width="16"
        height="16"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
        style={style}
    >
        <polyline points="20 6 9 17 4 12" />
    </svg>
);
const IconAlertCircle = ({ style }: { style?: React.CSSProperties }) => (
    <svg
        width="18"
        height="18"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
        style={style}
    >
        <circle cx="12" cy="12" r="10" />
        <line x1="12" y1="8" x2="12" y2="12" />
        <line x1="12" y1="16" x2="12.01" y2="16" />
    </svg>
);

import {
    configurePricingApi,
    configureDepositPolicyApi,
    getDepositPolicyApi,
    getLotPricingApi,
} from "@/features/owner/api/ownerApi";
import { ApiError } from "@/shared/errors/api-error";
import { backendLotId } from "@/features/owner/lots/ownerLotsSync";

export default function LotPricingConfigPage() {
    const params = useParams();
    const rawId = Array.isArray(params?.id)
        ? params?.id[0]
        : (params?.id ?? "");
    const numId = Number(rawId);

    const lots = useOwnerStore((s) => s.lots);
    const lot = lots.find(
        (l) =>
            (!Number.isNaN(numId) && l.id === numId) ||
            l.backendId === rawId ||
            String(l.id) === rawId,
    );
    // Parking keys lots by GUID; if rawId is already a valid UUID/GUID string, use it directly.
    const isGuid =
        /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(
            rawId,
        );
    const lotKey = lot?.backendId || (isGuid ? rawId : backendLotId(lot));

    // Pricing configuration state
    const [pricingType, setPricingType] = useState<"block" | "turn">("block");
    const [carHourly, setCarHourly] = useState(lot?.pc || 30000);
    const [motoHourly, setMotoHourly] = useState(lot?.pm || 5000);
    const [blockDuration, setBlockDuration] = useState(60); // minutes
    const [overnightCar, setOvernightCar] = useState(150000);
    const [overnightMoto, setOvernightMoto] = useState(30000);
    const [deposit, setDeposit] = useState(lot?.dep || 20000);

    // Price simulator / preview state
    const [simDuration, setSimDuration] = useState(3); // hours
    const [simVehicle, setSimVehicle] = useState<"car" | "moto">("car");
    const [isOvernight, setIsOvernight] = useState(false);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [errorMessage, setErrorMessage] = useState("");
    const [successMessage, setSuccessMessage] = useState("");

    // Load deposit policy and current pricing from BE if available
    useEffect(() => {
        let isMounted = true;
        async function loadPricingAndDeposit() {
            if (!lotKey) return;
            try {
                const [depositRes, pricingRes] = await Promise.allSettled([
                    getDepositPolicyApi(lotKey),
                    getLotPricingApi(lotKey),
                ]);

                if (!isMounted) return;

                if (
                    depositRes.status === "fulfilled" &&
                    depositRes.value.isConfigured
                ) {
                    setDeposit(depositRes.value.holdDepositVnd);
                }

                if (
                    pricingRes.status === "fulfilled" &&
                    pricingRes.value?.items
                ) {
                    const carPolicy = pricingRes.value.items.find(
                        (i) => i.vehicleType.toUpperCase() === "CAR",
                    );
                    const motoPolicy = pricingRes.value.items.find(
                        (i) => i.vehicleType.toUpperCase() === "MOTORBIKE",
                    );

                    if (carPolicy) {
                        setCarHourly(carPolicy.price);
                        setOvernightCar(carPolicy.overnightFee);
                        if (carPolicy.billingMode === "PerEntry") {
                            setPricingType("turn");
                        } else {
                            setPricingType("block");
                            if (carPolicy.blockMinutes) {
                                setBlockDuration(carPolicy.blockMinutes);
                            }
                        }
                    }

                    if (motoPolicy) {
                        setMotoHourly(motoPolicy.price);
                        setOvernightMoto(motoPolicy.overnightFee);
                    }
                }
            } catch {
                if (isMounted) {
                    setErrorMessage(
                        "Không tải được mức phí hiện tại của bãi này.",
                    );
                }
            }
        }
        void loadPricingAndDeposit();
        return () => {
            isMounted = false;
        };
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
        // Block-based calculation
        const blocks = Math.ceil((simDuration * 60) / blockDuration);
        return blocks * (rate * (blockDuration / 60));
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
                "Đã lưu và áp dụng biểu phí & tiền cọc lên hệ thống máy chủ bãi xe thành công!",
            );
        } catch (err: unknown) {
            if (err instanceof ApiError) {
                if (err.status === 400) {
                    setErrorMessage(
                        err.problem?.detail ||
                            "Thông tin cấu hình giá vé chưa hợp lệ.",
                    );
                } else if (err.status === 403) {
                    setErrorMessage(
                        "Tài khoản của bạn không có quyền sửa đổi biểu phí của bãi xe này.",
                    );
                } else {
                    setErrorMessage(
                        err.message || "Không thể cập nhật biểu giá lúc này.",
                    );
                }
            } else {
                setErrorMessage(
                    "Không kết nối được máy chủ. Bảng giá chưa được lưu.",
                );
            }
        } finally {
            setIsSubmitting(false);
            // Synchronize with local store
            useOwnerStore.setState((state) => ({
                lots: state.lots.map((l) =>
                    (l.backendId && l.backendId === lotKey) ||
                    (!Number.isNaN(numId) && l.id === numId) ||
                    String(l.id) === rawId
                        ? { ...l, pc: carHourly, pm: motoHourly, dep: deposit }
                        : l,
                ),
            }));
        }
    };

    const displayId = rawId || lotKey || "1";

    return (
        <>
            {successMessage && (
                <div className="ok" style={{ marginBottom: "16px" }}>
                    <b>✓ Thành công:</b> {successMessage}
                </div>
            )}

            {errorMessage && (
                <div className="err" style={{ marginBottom: "16px" }}>
                    <b>⚠️ Lỗi:</b> {errorMessage}
                </div>
            )}

            <PageHeader
                breadcrumb={`/owner/lots/${displayId}/pricing`}
                title={`Cấu hình bảng giá · ${lot?.name || "Bãi xe"}`}
                subtitle="Thiết lập biểu phí theo lượt, theo block, qua đêm và kiểm tra xem trước giá tính toán"
                actions={
                    <div style={{ display: "flex", gap: "8px", flexWrap: "wrap" }}>
                        <Link
                            href={`/owner/parking-lots/${lot?.id ?? rawId}`}
                            className="btn"
                        >
                            <span
                                style={{
                                    display: "inline-flex",
                                    alignItems: "center",
                                    gap: "4px",
                                }}
                            >
                                <IconArrowLeft /> Về chi tiết bãi
                            </span>
                        </Link>
                        <button
                            className="btn p"
                            onClick={handleSave}
                            disabled={isSubmitting}
                        >
                            {isSubmitting ? "Đang lưu..." : "Lưu bảng giá"}
                        </button>
                    </div>
                }
            />

            <div className="grid grid-cols-1 md:grid-cols-12 gap-4">
                {/* Form cấu hình */}
                <div className="card c7 col-span-1 md:col-span-7">
                    <div className="wh">
                        <h3>Phương thức tính phí & Biểu giá</h3>
                    </div>

                    <div style={{ marginBottom: "16px" }}>
                        <span
                            className="mut"
                            style={{
                                fontSize: "12px",
                                display: "block",
                                marginBottom: "6px",
                            }}
                        >
                            Hình thức tính phí chính
                        </span>
                        <div className="segs">
                            <button
                                className={pricingType === "block" ? "on" : ""}
                                onClick={() => setPricingType("block")}
                            >
                                Theo Block thời gian
                            </button>
                            <button
                                className={pricingType === "turn" ? "on" : ""}
                                onClick={() => setPricingType("turn")}
                            >
                                Theo Lượt cố định
                            </button>
                        </div>
                    </div>

                    <div className="form">
                        <label className="f">
                            <span>
                                Giá ô tô (
                                {pricingType === "block" ? "đ/giờ" : "đ/lượt"})
                            </span>
                            <input
                                type="number"
                                value={carHourly}
                                onChange={(e) =>
                                    setCarHourly(Number(e.target.value))
                                }
                            />
                        </label>

                        <label className="f">
                            <span>
                                Giá xe máy (
                                {pricingType === "block" ? "đ/giờ" : "đ/lượt"})
                            </span>
                            <input
                                type="number"
                                value={motoHourly}
                                onChange={(e) =>
                                    setMotoHourly(Number(e.target.value))
                                }
                            />
                        </label>

                        {pricingType === "block" && (
                            <label className="f">
                                <span>Độ dài block tính phí</span>
                                <select
                                    value={blockDuration}
                                    onChange={(e) =>
                                        setBlockDuration(Number(e.target.value))
                                    }
                                >
                                    <option value={30}>Block 30 phút</option>
                                    <option value={60}>
                                        Block 60 phút (1 giờ)
                                    </option>
                                    <option value={120}>
                                        Block 120 phút (2 giờ)
                                    </option>
                                </select>
                            </label>
                        )}

                        <label className="f">
                            <span>Phí gửi qua đêm (Ô tô)</span>
                            <input
                                type="number"
                                value={overnightCar}
                                onChange={(e) =>
                                    setOvernightCar(Number(e.target.value))
                                }
                            />
                        </label>

                        <label className="f">
                            <span>Phí gửi qua đêm (Xe máy)</span>
                            <input
                                type="number"
                                value={overnightMoto}
                                onChange={(e) =>
                                    setOvernightMoto(Number(e.target.value))
                                }
                            />
                        </label>

                        <label className="f">
                            <span>Tiền cọc giữ chỗ trước (đ)</span>
                            <input
                                type="number"
                                value={deposit}
                                onChange={(e) =>
                                    setDeposit(Number(e.target.value))
                                }
                            />
                        </label>
                    </div>

                    <div
                        style={{
                            marginTop: "20px",
                            paddingTop: "16px",
                            borderTop: "1px solid var(--line)",
                        }}
                    >
                        <button
                            className="btn p"
                            onClick={handleSave}
                            disabled={isSubmitting}
                        >
                            <IconCheck
                                style={{
                                    display: "inline-block",
                                    marginRight: "6px",
                                }}
                            />
                            {isSubmitting
                                ? "Đang áp dụng..."
                                : "Áp dụng bảng giá cho bãi"}
                        </button>
                    </div>
                </div>

                {/* Xem trước giá */}
                <div className="card c5 col-span-1 md:col-span-5">
                    <div className="wh">
                        <h3>Xem trước giá tính toán</h3>
                    </div>
                    <p
                        className="mut"
                        style={{ fontSize: "13px", margin: "0 0 16px" }}
                    >
                        Thử nghiệm công thức tính toán phí gửi xe cho khách hàng
                        theo thông số thực tế.
                    </p>

                    <div className="form">
                        <label className="f">
                            <span>Loại phương tiện thử nghiệm</span>
                            <select
                                value={simVehicle}
                                onChange={(e) =>
                                    setSimVehicle(
                                        e.target.value as "car" | "moto",
                                    )
                                }
                            >
                                <option value="car">Ô tô</option>
                                <option value="moto">Xe máy</option>
                            </select>
                        </label>

                        <label className="f">
                            <span>Thời gian đỗ (giờ)</span>
                            <input
                                type="number"
                                min={0.5}
                                step={0.5}
                                disabled={isOvernight}
                                value={simDuration}
                                onChange={(e) =>
                                    setSimDuration(Number(e.target.value))
                                }
                            />
                        </label>
                    </div>

                    <div style={{ margin: "14px 0" }}>
                        <label className="f ck">
                            <input
                                type="checkbox"
                                checked={isOvernight}
                                onChange={(e) =>
                                    setIsOvernight(e.target.checked)
                                }
                            />
                            Áp dụng gửi qua đêm (sau 22:00)
                        </label>
                    </div>

                    <div
                        style={{
                            background: "var(--soft)",
                            padding: "16px",
                            borderRadius: "14px",
                            marginTop: "16px",
                            textAlign: "center",
                        }}
                    >
                        <div
                            style={{
                                display: "flex",
                                alignItems: "center",
                                justifyContent: "center",
                                gap: "8px",
                                color: "var(--mut)",
                                fontSize: "13px",
                            }}
                        >
                            <IconCalculator />
                            <span>Tổng tiền dự kiến</span>
                        </div>
                        <div
                            style={{
                                fontSize: "32px",
                                fontWeight: 800,
                                marginTop: "6px",
                                color: "var(--ink)",
                            }}
                        >
                            {vnd(calculatePreviewPrice())}
                        </div>
                        <div
                            style={{
                                fontSize: "11px",
                                color: "var(--mut)",
                                marginTop: "4px",
                            }}
                        >
                            {isOvernight
                                ? "Tính theo biểu giá qua đêm"
                                : pricingType === "turn"
                                  ? "Tính theo lượt cố định"
                                  : `Tính theo ${Math.ceil((simDuration * 60) / blockDuration)} block (${blockDuration} phút/block)`}
                        </div>
                    </div>
                </div>
            </div>
        </>
    );
}
