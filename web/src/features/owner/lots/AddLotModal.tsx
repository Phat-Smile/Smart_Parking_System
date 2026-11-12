"use client";

import React, { useState } from "react";
import { useOwnerMapStore } from "./useOwnerMapStore";
import { loadOwnerLots, localLotId } from "./ownerLotsSync";
import { registerParkingLotApi } from "@/features/owner/api/ownerApi";
import { ApiError } from "@/shared/errors/api-error";

type LotType = "MultiStorey" | "Underground" | "Outdoor";

function describeRegisterError(err: unknown): string {
    if (!(err instanceof ApiError))
        return "Không kết nối được máy chủ. Bãi chưa được tạo, vui lòng thử lại.";
    if (err.status === 409)
        return "Tên bãi đỗ xe này đã được đăng ký trước đó. Vui lòng chọn tên khác.";
    if (err.status === 400)
        return (
            err.problem?.detail || "Thông tin bãi đỗ xe chưa đúng định dạng."
        );
    if (err.status === 401 || err.status === 403)
        return "Tài khoản hiện tại không có quyền đăng ký bãi. Vui lòng đăng nhập lại bằng tài khoản Chủ bãi đã được duyệt.";
    return "Máy chủ chưa tạo được bãi. Vui lòng thử lại.";
}

export function AddLotModal() {
    const { isAddModalOpen, setAddModalOpen, setSelectedLotId } =
        useOwnerMapStore();

    const [name, setName] = useState("");
    const [addr, setAddr] = useState("");
    const [lat, setLat] = useState("");
    const [lng, setLng] = useState("");
    const [lotType, setLotType] = useState<LotType>("Outdoor");
    const [supportsElectricVehicles, setSupportsElectricVehicles] =
        useState(false);
    const [supportsLargeVehicles, setSupportsLargeVehicles] = useState(false);
    const [hasEvCharging, setHasEvCharging] = useState(false);
    const [isCovered, setIsCovered] = useState(false);
    const [hasCctv, setHasCctv] = useState(false);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [errorMsg, setErrorMsg] = useState("");

    if (!isAddModalOpen) return null;

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!name.trim()) return;

        const latitude = Number(lat);
        const longitude = Number(lng);
        if (
            lat.trim() === "" ||
            lng.trim() === "" ||
            !Number.isFinite(latitude) ||
            !Number.isFinite(longitude) ||
            Math.abs(latitude) > 90 ||
            Math.abs(longitude) > 180
        ) {
            setErrorMsg(
                "Vui lòng nhập vĩ độ và kinh độ hợp lệ của bãi (VD: 10.7745 và 106.7035).",
            );
            return;
        }

        setIsSubmitting(true);
        setErrorMsg("");
        try {
            // D02.1: Parking stores what the owner typed; the lot starts as Pending.
            const res = await registerParkingLotApi({
                name: name.trim(),
                address: addr.trim(),
                latitude,
                longitude,
                type: lotType,
                features: {
                    supportsElectricVehicles,
                    supportsLargeVehicles,
                    hasEvCharging,
                    isCovered,
                    hasCctv,
                },
            });
            await loadOwnerLots();
            if (res?.id) setSelectedLotId(localLotId(res.id));
            setAddModalOpen(false);
            setName("");
            setAddr("");
            setLat("");
            setLng("");
            setSupportsElectricVehicles(false);
            setSupportsLargeVehicles(false);
            setHasEvCharging(false);
            setIsCovered(false);
            setHasCctv(false);
        } catch (err: unknown) {
            setErrorMsg(describeRegisterError(err));
        } finally {
            setIsSubmitting(false);
        }
    };

    return (
        <div
            style={{
                position: "fixed",
                inset: 0,
                background: "rgba(15,27,61,0.4)",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                zIndex: 1000,
                padding: 16,
            }}
            role="dialog"
            aria-modal="true"
            aria-labelledby="modal-title"
        >
            <div
                style={{
                    background: "#fff",
                    borderRadius: 20,
                    padding: 28,
                    width: "100%",
                    maxWidth: 480,
                    boxShadow: "0 20px 40px rgba(15,27,61,0.2)",
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
                    <h2
                        id="modal-title"
                        style={{
                            fontSize: 18,
                            fontWeight: 800,
                            margin: 0,
                            color: "#0f1b3d",
                        }}
                    >
                        Thêm bãi đỗ xe mới
                    </h2>
                    <button
                        type="button"
                        onClick={() => setAddModalOpen(false)}
                        aria-label="Đóng"
                        style={{
                            border: "none",
                            background: "none",
                            fontSize: 18,
                            cursor: "pointer",
                            color: "#6a7491",
                        }}
                    >
                        ✕
                    </button>
                </div>

                {errorMsg && (
                    <div
                        style={{
                            background: "#fef2f2",
                            border: "1px solid #fecaca",
                            color: "#991b1b",
                            padding: "10px 14px",
                            borderRadius: 10,
                            fontSize: 13,
                            marginBottom: 14,
                        }}
                    >
                        ⚠️ {errorMsg}
                    </div>
                )}

                <form
                    onSubmit={handleSubmit}
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
                                fontSize: 13,
                                fontWeight: 600,
                                color: "#0f1b3d",
                                marginBottom: 4,
                            }}
                        >
                            Tên bãi đỗ xe
                        </label>
                        <input
                            type="text"
                            required
                            placeholder="VD: Bãi xe Nguyễn Du"
                            value={name}
                            onChange={(e) => setName(e.target.value)}
                            style={{
                                width: "100%",
                                border: "1px solid #e3e8f2",
                                borderRadius: 10,
                                padding: "9px 12px",
                                fontSize: 13,
                                outline: "none",
                            }}
                        />
                    </div>

                    <div>
                        <label
                            style={{
                                display: "block",
                                fontSize: 13,
                                fontWeight: 600,
                                color: "#0f1b3d",
                                marginBottom: 4,
                            }}
                        >
                            Địa chỉ bãi
                        </label>
                        <input
                            type="text"
                            required
                            placeholder="VD: Quận 1, TP.HCM"
                            value={addr}
                            onChange={(e) => setAddr(e.target.value)}
                            style={{
                                width: "100%",
                                border: "1px solid #e3e8f2",
                                borderRadius: 10,
                                padding: "9px 12px",
                                fontSize: 13,
                                outline: "none",
                            }}
                        />
                    </div>

                    <div
                        style={{
                            display: "grid",
                            gridTemplateColumns: "1fr 1fr 1fr",
                            gap: 12,
                        }}
                    >
                        <div>
                            <label
                                htmlFor="add-lot-lat"
                                style={{
                                    display: "block",
                                    fontSize: 13,
                                    fontWeight: 600,
                                    color: "#0f1b3d",
                                    marginBottom: 4,
                                }}
                            >
                                Vĩ độ (GPS)
                            </label>
                            <input
                                id="add-lot-lat"
                                type="text"
                                inputMode="decimal"
                                required
                                placeholder="10.7745"
                                value={lat}
                                onChange={(e) => setLat(e.target.value)}
                                style={{
                                    width: "100%",
                                    border: "1px solid #e3e8f2",
                                    borderRadius: 10,
                                    padding: "9px 12px",
                                    fontSize: 13,
                                    outline: "none",
                                }}
                            />
                        </div>
                        <div>
                            <label
                                htmlFor="add-lot-lng"
                                style={{
                                    display: "block",
                                    fontSize: 13,
                                    fontWeight: 600,
                                    color: "#0f1b3d",
                                    marginBottom: 4,
                                }}
                            >
                                Kinh độ (GPS)
                            </label>
                            <input
                                id="add-lot-lng"
                                type="text"
                                inputMode="decimal"
                                required
                                placeholder="106.7035"
                                value={lng}
                                onChange={(e) => setLng(e.target.value)}
                                style={{
                                    width: "100%",
                                    border: "1px solid #e3e8f2",
                                    borderRadius: 10,
                                    padding: "9px 12px",
                                    fontSize: 13,
                                    outline: "none",
                                }}
                            />
                        </div>
                        <div>
                            <label
                                htmlFor="add-lot-type"
                                style={{
                                    display: "block",
                                    fontSize: 13,
                                    fontWeight: 600,
                                    color: "#0f1b3d",
                                    marginBottom: 4,
                                }}
                            >
                                Loại bãi
                            </label>
                            <select
                                id="add-lot-type"
                                value={lotType}
                                onChange={(e) =>
                                    setLotType(e.target.value as LotType)
                                }
                                style={{
                                    width: "100%",
                                    border: "1px solid #e3e8f2",
                                    borderRadius: 10,
                                    padding: "9px 12px",
                                    fontSize: 13,
                                    outline: "none",
                                }}
                            >
                                <option value="Outdoor">Ngoài trời</option>
                                <option value="MultiStorey">Nhiều tầng</option>
                                <option value="Underground">Tầng hầm</option>
                            </select>
                        </div>
                    </div>

                    {/* Tiện ích bãi xe */}
                    <div style={{ marginTop: 6 }}>
                        <label
                            style={{
                                display: "block",
                                fontSize: 13,
                                fontWeight: 600,
                                color: "#0f1b3d",
                                marginBottom: 6,
                            }}
                        >
                            Tiện ích bãi đỗ (Features)
                        </label>
                        <div
                            style={{
                                display: "grid",
                                gridTemplateColumns: "1fr 1fr",
                                gap: 8,
                            }}
                        >
                            <label
                                style={{
                                    display: "flex",
                                    alignItems: "center",
                                    gap: 6,
                                    fontSize: 12,
                                    color: "#334155",
                                    cursor: "pointer",
                                }}
                            >
                                <input
                                    type="checkbox"
                                    checked={supportsElectricVehicles}
                                    onChange={(e) =>
                                        setSupportsElectricVehicles(
                                            e.target.checked,
                                        )
                                    }
                                />
                                <span>Xe điện (EV)</span>
                            </label>
                            <label
                                style={{
                                    display: "flex",
                                    alignItems: "center",
                                    gap: 6,
                                    fontSize: 12,
                                    color: "#334155",
                                    cursor: "pointer",
                                }}
                            >
                                <input
                                    type="checkbox"
                                    checked={hasEvCharging}
                                    onChange={(e) =>
                                        setHasEvCharging(e.target.checked)
                                    }
                                />
                                <span>Trạm sạc EV</span>
                            </label>
                            <label
                                style={{
                                    display: "flex",
                                    alignItems: "center",
                                    gap: 6,
                                    fontSize: 12,
                                    color: "#334155",
                                    cursor: "pointer",
                                }}
                            >
                                <input
                                    type="checkbox"
                                    checked={hasCctv}
                                    onChange={(e) =>
                                        setHasCctv(e.target.checked)
                                    }
                                />
                                <span>Camera an ninh</span>
                            </label>
                            <label
                                style={{
                                    display: "flex",
                                    alignItems: "center",
                                    gap: 6,
                                    fontSize: 12,
                                    color: "#334155",
                                    cursor: "pointer",
                                }}
                            >
                                <input
                                    type="checkbox"
                                    checked={isCovered}
                                    onChange={(e) =>
                                        setIsCovered(e.target.checked)
                                    }
                                />
                                <span>Có mái che</span>
                            </label>
                            <label
                                style={{
                                    display: "flex",
                                    alignItems: "center",
                                    gap: 6,
                                    fontSize: 12,
                                    color: "#334155",
                                    cursor: "pointer",
                                }}
                            >
                                <input
                                    type="checkbox"
                                    checked={supportsLargeVehicles}
                                    onChange={(e) =>
                                        setSupportsLargeVehicles(
                                            e.target.checked,
                                        )
                                    }
                                />
                                <span>Xe cỡ lớn / SUV</span>
                            </label>
                        </div>
                    </div>

                    <div
                        style={{
                            display: "flex",
                            justifyContent: "flex-end",
                            gap: 10,
                            marginTop: 16,
                        }}
                    >
                        <button
                            type="button"
                            onClick={() => setAddModalOpen(false)}
                            style={{
                                background: "#f3f6fb",
                                border: "1px solid #e3e8f2",
                                borderRadius: 10,
                                padding: "8px 16px",
                                fontSize: 13,
                                fontWeight: 600,
                                cursor: "pointer",
                                color: "#6a7491",
                            }}
                        >
                            Hủy
                        </button>
                        <button
                            type="submit"
                            disabled={isSubmitting}
                            style={{
                                background: isSubmitting
                                    ? "#93c5fd"
                                    : "#2563eb",
                                border: "none",
                                borderRadius: 10,
                                padding: "8px 18px",
                                fontSize: 13,
                                fontWeight: 700,
                                cursor: isSubmitting
                                    ? "not-allowed"
                                    : "pointer",
                                color: "#fff",
                            }}
                        >
                            {isSubmitting
                                ? "Đang gửi duyệt..."
                                : "Gửi phê duyệt"}
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
}
