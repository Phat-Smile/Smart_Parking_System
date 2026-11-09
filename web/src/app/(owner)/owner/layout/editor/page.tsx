"use client";

import React, { useState, useEffect } from "react";
import Link from "next/link";
import { OwnerPageHeader } from "@/components/layout/OwnerPageHeader";
import { useAllLots } from "@/features/owner/lots/useOwnerMapStore";
import { backendLotId } from "@/features/owner/lots/ownerLotsSync";
import {
    getOwnerParkingLayout,
    saveOwnerParkingLayout,
    describeParkingLayoutError,
    validateLayoutInput,
    createDefaultFloor,
    createDefaultZone,
    createDefaultSlot,
    type LayoutFloorDto,
    type LayoutZoneDto,
    type LayoutSlotDto,
    type SlotOverrideState,
} from "@/features/owner/layout/ownerLayoutApi";

export default function OwnerLayoutEditorPage() {
    const lots = useAllLots();
    const [selectedLotId, setSelectedLotId] = useState<number>(
        lots[0]?.id || 1,
    );
    const [layoutVersion, setLayoutVersion] = useState<number>(0);
    const [floors, setFloors] = useState<LayoutFloorDto[]>([]);
    const [activeFloorIndex, setActiveFloorIndex] = useState<number>(0);

    const [isLoading, setIsLoading] = useState<boolean>(false);
    const [isSaving, setIsSaving] = useState<boolean>(false);
    const [saveSuccess, setSaveSuccess] = useState<string | null>(null);
    const [saveError, setSaveError] = useState<string | null>(null);
    const [validationErrors, setValidationErrors] = useState<string[]>([]);

    const currentLot = lots.find((l) => l.id === selectedLotId) || lots[0];
    const lotKey = backendLotId(currentLot);

    // Load layout for selected lot
    useEffect(() => {
        async function loadLayout() {
            setSaveError(null);
            setSaveSuccess(null);
            setValidationErrors([]);

            if (!lotKey) {
                setFloors([createDefaultFloor(1)]);
                setLayoutVersion(0);
                return;
            }

            setIsLoading(true);
            try {
                const res = await getOwnerParkingLayout(lotKey);
                setLayoutVersion(res.version);
                if (res.floors && res.floors.length > 0) {
                    setFloors(res.floors);
                } else {
                    // Initialize empty lot with a starter floor
                    setFloors([createDefaultFloor(1)]);
                }
                setActiveFloorIndex(0);
            } catch (err: unknown) {
                // Not found or error loading: provide default floor
                setFloors([createDefaultFloor(1)]);
                setLayoutVersion(0);
                setSaveError(describeParkingLayoutError(err));
            } finally {
                setIsLoading(false);
            }
        }

        void loadLayout();
    }, [lotKey]);

    const activeFloor = floors[activeFloorIndex] || floors[0];

    // Floor operations
    const handleAddFloor = () => {
        const nextIndex = floors.length + 1;
        const newFloor = createDefaultFloor(nextIndex);
        setFloors([...floors, newFloor]);
        setActiveFloorIndex(floors.length);
        setSaveSuccess(`Đã thêm Tầng ${nextIndex}`);
        setTimeout(() => setSaveSuccess(null), 2500);
    };

    const handleUpdateFloorName = (name: string) => {
        setFloors((prev) =>
            prev.map((f, idx) =>
                idx === activeFloorIndex ? { ...f, name } : f,
            ),
        );
    };

    // Zone operations
    const handleAddZone = (type: "Car" | "Motorbike") => {
        if (!activeFloor) return;
        const defaultCode =
            type === "Car"
                ? `CAR-${activeFloor.zones.filter((z) => z.vehicleGroup === "Car").length + 1}`
                : `MOTO-${activeFloor.zones.filter((z) => z.vehicleGroup === "Motorbike").length + 1}`;
        const newZone = createDefaultZone(
            type,
            defaultCode,
            type === "Car" ? 10 : 30,
        );

        setFloors((prev) =>
            prev.map((f, idx) =>
                idx === activeFloorIndex
                    ? { ...f, zones: [...f.zones, newZone] }
                    : f,
            ),
        );
    };

    const handleUpdateZone = (
        zoneId: string,
        updates: Partial<LayoutZoneDto>,
    ) => {
        setFloors((prev) =>
            prev.map((f, fIdx) =>
                fIdx === activeFloorIndex
                    ? {
                          ...f,
                          zones: f.zones.map((z) =>
                              z.id === zoneId ? { ...z, ...updates } : z,
                          ),
                      }
                    : f,
            ),
        );
    };

    const handleRemoveZone = (zoneId: string) => {
        setFloors((prev) =>
            prev.map((f, fIdx) =>
                fIdx === activeFloorIndex
                    ? { ...f, zones: f.zones.filter((z) => z.id !== zoneId) }
                    : f,
            ),
        );
    };

    // Slot operations for Car zones
    const handleAddSlot = (zoneId: string) => {
        const zone = activeFloor?.zones.find((z) => z.id === zoneId);
        if (!zone) return;

        const currentSlots = zone.slots || [];
        const nextIndex = currentSlots.length + 1;
        const nextX = currentSlots.length % 10;
        const nextY = Math.floor(currentSlots.length / 10);

        const newSlot = createDefaultSlot(zone.code, nextIndex, {
            x: nextX,
            y: nextY,
        });

        handleUpdateZone(zoneId, {
            slots: [...currentSlots, newSlot],
        });
    };

    const handleAutoGenerateSlots = (zoneId: string) => {
        const zone = activeFloor?.zones.find((z) => z.id === zoneId);
        if (!zone) return;

        const capacity = zone.capacity || 10;
        const generatedSlots: LayoutSlotDto[] = [];

        for (let i = 1; i <= capacity; i++) {
            const x = (i - 1) % 10;
            const y = Math.floor((i - 1) / 10);
            generatedSlots.push(createDefaultSlot(zone.code, i, { x, y }));
        }

        handleUpdateZone(zoneId, { slots: generatedSlots });
        setSaveSuccess(`Đã tự động tạo ${capacity} ô đỗ cho khu ${zone.code}`);
        setTimeout(() => setSaveSuccess(null), 3000);
    };

    const handleUpdateSlot = (
        zoneId: string,
        slotId: string,
        updates: Partial<LayoutSlotDto>,
    ) => {
        const zone = activeFloor?.zones.find((z) => z.id === zoneId);
        if (!zone) return;

        const updatedSlots = zone.slots.map((s) =>
            s.id === slotId ? { ...s, ...updates } : s,
        );
        handleUpdateZone(zoneId, { slots: updatedSlots });
    };

    const handleRemoveSlot = (zoneId: string, slotId: string) => {
        const zone = activeFloor?.zones.find((z) => z.id === zoneId);
        if (!zone) return;

        handleUpdateZone(zoneId, {
            slots: zone.slots.filter((s) => s.id !== slotId),
        });
    };

    // Save action
    const handleSaveLayout = async () => {
        setSaveSuccess(null);
        setSaveError(null);
        setValidationErrors([]);

        if (!lotKey) {
            setSaveError(
                "Chưa có bãi đỗ nào trên hệ thống hoặc bãi đang chọn là dữ liệu mẫu. Không thể lưu lên máy chủ.",
            );
            return;
        }

        // Client-side validation check
        const validation = validateLayoutInput(floors);
        if (!validation.valid) {
            setValidationErrors(validation.errors);
            setSaveError(
                "Dữ liệu sơ đồ chưa hợp lệ. Vui lòng kiểm tra lại các lỗi bên dưới.",
            );
            return;
        }

        setIsSaving(true);
        try {
            const result = await saveOwnerParkingLayout(lotKey, {
                expectedVersion: layoutVersion,
                floors,
            });

            setLayoutVersion(result.version);
            setFloors(result.floors);
            setSaveSuccess(
                `Lưu sơ đồ mặt bằng thành công! (Phiên bản máy chủ: v${result.version})`,
            );
            setTimeout(() => setSaveSuccess(null), 4000);
        } catch (err: unknown) {
            setSaveError(describeParkingLayoutError(err));
        } finally {
            setIsSaving(false);
        }
    };

    return (
        <>
            <OwnerPageHeader
                breadcrumb="/owner/layout/editor"
                title="Chỉnh sửa sơ đồ mặt bằng"
                subtitle={`${currentLot?.name || "Bãi đỗ xe"} · Phiên bản layout v${layoutVersion}`}
                actions={
                    <div
                        style={{
                            display: "flex",
                            gap: 10,
                            alignItems: "center",
                        }}
                    >
                        <Link
                            href="/owner/layout/2d"
                            style={{
                                background: "#f1f5f9",
                                color: "#334155",
                                border: "1px solid #cbd5e1",
                                borderRadius: 8,
                                padding: "8px 14px",
                                fontSize: 13,
                                fontWeight: 600,
                                textDecoration: "none",
                                display: "inline-flex",
                                alignItems: "center",
                                gap: 6,
                            }}
                        >
                            👁️ Xem sơ đồ 2D
                        </Link>
                        <button
                            type="button"
                            onClick={handleSaveLayout}
                            disabled={isSaving || isLoading}
                            id="save-layout-btn"
                            style={{
                                background: isSaving ? "#64748b" : "#2563eb",
                                color: "#fff",
                                border: "none",
                                borderRadius: 8,
                                padding: "8px 18px",
                                fontSize: 13,
                                fontWeight: 700,
                                cursor: isSaving ? "not-allowed" : "pointer",
                                display: "inline-flex",
                                alignItems: "center",
                                gap: 8,
                                boxShadow: "0 2px 6px rgba(37,99,235,0.25)",
                            }}
                        >
                            {isSaving ? "Đang lưu…" : "💾 Lưu sơ đồ"}
                        </button>
                    </div>
                }
            />

            <div
                style={{
                    padding: "16px 24px",
                    maxWidth: 1200,
                    margin: "0 auto",
                }}
            >
                {/* Lot selector card */}
                <div
                    style={{
                        background: "#fff",
                        border: "1px solid #e2e8f0",
                        borderRadius: 12,
                        padding: "14px 20px",
                        marginBottom: 16,
                        display: "flex",
                        justifyContent: "space-between",
                        alignItems: "center",
                        flexWrap: "wrap",
                        gap: 12,
                    }}
                >
                    <div
                        style={{
                            display: "flex",
                            alignItems: "center",
                            gap: 12,
                        }}
                    >
                        <label
                            htmlFor="lot-select"
                            style={{
                                fontSize: 14,
                                fontWeight: 600,
                                color: "#1e293b",
                            }}
                        >
                            Chọn bãi đỗ:
                        </label>
                        <select
                            id="lot-select"
                            value={selectedLotId}
                            onChange={(e) =>
                                setSelectedLotId(Number(e.target.value))
                            }
                            style={{
                                padding: "6px 12px",
                                borderRadius: 8,
                                border: "1px solid #cbd5e1",
                                fontSize: 14,
                                color: "#0f172a",
                                background: "#fff",
                                minWidth: 260,
                            }}
                        >
                            {lots.map((lot) => (
                                <option key={lot.id} value={lot.id}>
                                    {lot.name}{" "}
                                    {backendLotId(lot)
                                        ? "(Đã liên kết)"
                                        : "(Chưa có ID)"}
                                </option>
                            ))}
                        </select>
                    </div>

                    <div style={{ fontSize: 13, color: "#64748b" }}>
                        Mã bãi: <code>{lotKey || "Chưa có backend GUID"}</code>{" "}
                        · Phiên bản: <b>v{layoutVersion}</b>
                    </div>
                </div>

                {/* Alerts */}
                {saveSuccess && (
                    <div
                        id="layout-save-success"
                        role="status"
                        style={{
                            background: "#ecfdf5",
                            color: "#065f46",
                            border: "1px solid #a7f3d0",
                            borderRadius: 8,
                            padding: "12px 16px",
                            marginBottom: 16,
                            fontSize: 14,
                            fontWeight: 500,
                        }}
                    >
                        ✅ {saveSuccess}
                    </div>
                )}

                {saveError && (
                    <div
                        id="layout-save-error"
                        role="alert"
                        style={{
                            background: "#fef2f2",
                            color: "#991b1b",
                            border: "1px solid #fecaca",
                            borderRadius: 8,
                            padding: "12px 16px",
                            marginBottom: 16,
                            fontSize: 14,
                            fontWeight: 500,
                        }}
                    >
                        ❌ {saveError}
                    </div>
                )}

                {validationErrors.length > 0 && (
                    <div
                        id="layout-validation-errors"
                        role="alert"
                        style={{
                            background: "#fffbeb",
                            color: "#92400e",
                            border: "1px solid #fde68a",
                            borderRadius: 8,
                            padding: "12px 16px",
                            marginBottom: 16,
                            fontSize: 13,
                        }}
                    >
                        <b>Các lỗi cần khắc phục trước khi lưu:</b>
                        <ul style={{ margin: "6px 0 0 18px", padding: 0 }}>
                            {validationErrors.map((err, i) => (
                                <li key={i}>{err}</li>
                            ))}
                        </ul>
                    </div>
                )}

                {/* Floor tabs navigation */}
                <div
                    style={{
                        display: "flex",
                        gap: 8,
                        alignItems: "center",
                        borderBottom: "1px solid #e2e8f0",
                        paddingBottom: 8,
                        marginBottom: 20,
                        overflowX: "auto",
                    }}
                >
                    {floors.map((floor, idx) => (
                        <button
                            key={floor.id}
                            type="button"
                            onClick={() => setActiveFloorIndex(idx)}
                            style={{
                                padding: "8px 16px",
                                borderRadius: 8,
                                border: "none",
                                background:
                                    idx === activeFloorIndex
                                        ? "#2563eb"
                                        : "#f1f5f9",
                                color:
                                    idx === activeFloorIndex
                                        ? "#fff"
                                        : "#475569",
                                fontWeight:
                                    idx === activeFloorIndex ? 700 : 500,
                                fontSize: 13,
                                cursor: "pointer",
                                transition: "all 0.15s ease",
                            }}
                        >
                            {floor.name} ({floor.zones?.length || 0} khu)
                        </button>
                    ))}

                    <button
                        type="button"
                        onClick={handleAddFloor}
                        style={{
                            padding: "8px 14px",
                            borderRadius: 8,
                            border: "1px dashed #94a3b8",
                            background: "#fff",
                            color: "#2563eb",
                            fontWeight: 600,
                            fontSize: 13,
                            cursor: "pointer",
                        }}
                    >
                        + Thêm tầng
                    </button>
                </div>

                {/* Active floor editor */}
                {activeFloor && (
                    <div
                        style={{
                            display: "flex",
                            flexDirection: "column",
                            gap: 20,
                        }}
                    >
                        {/* Floor metadata */}
                        <div
                            style={{
                                background: "#fff",
                                border: "1px solid #e2e8f0",
                                borderRadius: 12,
                                padding: "16px 20px",
                                display: "flex",
                                alignItems: "center",
                                gap: 16,
                            }}
                        >
                            <label
                                style={{
                                    fontSize: 14,
                                    fontWeight: 600,
                                    color: "#334155",
                                }}
                            >
                                Tên tầng:
                            </label>
                            <input
                                type="text"
                                value={activeFloor.name}
                                maxLength={100}
                                onChange={(e) =>
                                    handleUpdateFloorName(e.target.value)
                                }
                                style={{
                                    padding: "6px 12px",
                                    borderRadius: 6,
                                    border: "1px solid #cbd5e1",
                                    fontSize: 14,
                                    width: 240,
                                }}
                            />
                            <div
                                style={{
                                    marginLeft: "auto",
                                    display: "flex",
                                    gap: 8,
                                }}
                            >
                                <button
                                    type="button"
                                    onClick={() => handleAddZone("Car")}
                                    style={{
                                        background: "#eff6ff",
                                        color: "#1d4ed8",
                                        border: "1px solid #bfdbfe",
                                        borderRadius: 6,
                                        padding: "6px 12px",
                                        fontSize: 12,
                                        fontWeight: 600,
                                        cursor: "pointer",
                                    }}
                                >
                                    + Thêm khu Ô tô (Car)
                                </button>
                                <button
                                    type="button"
                                    onClick={() => handleAddZone("Motorbike")}
                                    style={{
                                        background: "#ecfdf5",
                                        color: "#047857",
                                        border: "1px solid #a7f3d0",
                                        borderRadius: 6,
                                        padding: "6px 12px",
                                        fontSize: 12,
                                        fontWeight: 600,
                                        cursor: "pointer",
                                    }}
                                >
                                    + Thêm khu Xe máy (Motorbike)
                                </button>
                            </div>
                        </div>

                        {/* Zone list */}
                        {activeFloor.zones.length === 0 ? (
                            <div
                                style={{
                                    padding: "36px 20px",
                                    textAlign: "center",
                                    background: "#f8fafc",
                                    borderRadius: 12,
                                    border: "1px dashed #cbd5e1",
                                    color: "#64748b",
                                }}
                            >
                                Tầng này chưa có khu vực đỗ xe nào. Hãy nhấn nút
                                thêm khu vực ở trên.
                            </div>
                        ) : (
                            activeFloor.zones.map((zone) => {
                                const isCar = zone.vehicleGroup === "Car";
                                return (
                                    <div
                                        key={zone.id}
                                        style={{
                                            background: "#fff",
                                            border: "1px solid #e2e8f0",
                                            borderRadius: 12,
                                            padding: "16px 20px",
                                            boxShadow:
                                                "0 1px 3px rgba(0,0,0,0.03)",
                                        }}
                                    >
                                        <div
                                            style={{
                                                display: "flex",
                                                alignItems: "center",
                                                justifyContent: "space-between",
                                                marginBottom: 12,
                                                paddingBottom: 10,
                                                borderBottom:
                                                    "1px solid #f1f5f9",
                                            }}
                                        >
                                            <div
                                                style={{
                                                    display: "flex",
                                                    alignItems: "center",
                                                    gap: 10,
                                                }}
                                            >
                                                <span
                                                    style={{
                                                        padding: "3px 8px",
                                                        borderRadius: 6,
                                                        fontSize: 12,
                                                        fontWeight: 700,
                                                        background: isCar
                                                            ? "#dbeafe"
                                                            : "#d1fae5",
                                                        color: isCar
                                                            ? "#1e40af"
                                                            : "#065f46",
                                                    }}
                                                >
                                                    {isCar
                                                        ? "🚗 Ô TÔ"
                                                        : "🛵 XE MÁY"}
                                                </span>
                                                <strong
                                                    style={{
                                                        fontSize: 15,
                                                        color: "#0f172a",
                                                    }}
                                                >
                                                    Khu: {zone.code}
                                                </strong>
                                                <span
                                                    style={{
                                                        fontSize: 13,
                                                        color: "#64748b",
                                                    }}
                                                >
                                                    (Sức chứa: {zone.capacity}{" "}
                                                    xe)
                                                </span>
                                            </div>

                                            <button
                                                type="button"
                                                onClick={() =>
                                                    handleRemoveZone(zone.id)
                                                }
                                                style={{
                                                    background: "none",
                                                    border: "none",
                                                    color: "#ef4444",
                                                    fontSize: 12,
                                                    cursor: "pointer",
                                                    padding: "4px 8px",
                                                }}
                                            >
                                                ✕ Xóa khu
                                            </button>
                                        </div>

                                        {/* Zone settings form */}
                                        <div
                                            style={{
                                                display: "grid",
                                                gridTemplateColumns:
                                                    "repeat(auto-fit, minmax(200px, 1fr))",
                                                gap: 14,
                                                marginBottom: 16,
                                            }}
                                        >
                                            <div>
                                                <label
                                                    style={{
                                                        display: "block",
                                                        fontSize: 12,
                                                        color: "#64748b",
                                                        marginBottom: 4,
                                                    }}
                                                >
                                                    Mã khu:
                                                </label>
                                                <input
                                                    type="text"
                                                    value={zone.code}
                                                    maxLength={50}
                                                    onChange={(e) =>
                                                        handleUpdateZone(
                                                            zone.id,
                                                            {
                                                                code: e.target
                                                                    .value,
                                                            },
                                                        )
                                                    }
                                                    style={{
                                                        width: "100%",
                                                        padding: "6px 10px",
                                                        borderRadius: 6,
                                                        border: "1px solid #cbd5e1",
                                                        fontSize: 13,
                                                    }}
                                                />
                                            </div>

                                            <div>
                                                <label
                                                    style={{
                                                        display: "block",
                                                        fontSize: 12,
                                                        color: "#64748b",
                                                        marginBottom: 4,
                                                    }}
                                                >
                                                    Sức chứa (xe):
                                                </label>
                                                <input
                                                    type="number"
                                                    min={0}
                                                    value={zone.capacity}
                                                    onChange={(e) =>
                                                        handleUpdateZone(
                                                            zone.id,
                                                            {
                                                                capacity:
                                                                    parseInt(
                                                                        e.target
                                                                            .value,
                                                                        10,
                                                                    ) || 0,
                                                            },
                                                        )
                                                    }
                                                    style={{
                                                        width: "100%",
                                                        padding: "6px 10px",
                                                        borderRadius: 6,
                                                        border: "1px solid #cbd5e1",
                                                        fontSize: 13,
                                                    }}
                                                />
                                            </div>

                                            {isCar ? (
                                                <div
                                                    style={{
                                                        display: "flex",
                                                        alignItems: "center",
                                                        gap: 8,
                                                        marginTop: 20,
                                                    }}
                                                >
                                                    <input
                                                        type="checkbox"
                                                        id={`sensor-${zone.id}`}
                                                        checked={
                                                            zone.hasSlotSensor
                                                        }
                                                        onChange={(e) =>
                                                            handleUpdateZone(
                                                                zone.id,
                                                                {
                                                                    hasSlotSensor:
                                                                        e.target
                                                                            .checked,
                                                                },
                                                            )
                                                        }
                                                    />
                                                    <label
                                                        htmlFor={`sensor-${zone.id}`}
                                                        style={{
                                                            fontSize: 13,
                                                            color: "#334155",
                                                        }}
                                                    >
                                                        Có cảm biến từng ô
                                                    </label>
                                                </div>
                                            ) : (
                                                <div>
                                                    <label
                                                        style={{
                                                            display: "block",
                                                            fontSize: 12,
                                                            color: "#64748b",
                                                            marginBottom: 4,
                                                        }}
                                                    >
                                                        Hình khối vùng đỗ (Rộng
                                                        x Cao):
                                                    </label>
                                                    <div
                                                        style={{
                                                            display: "flex",
                                                            gap: 8,
                                                        }}
                                                    >
                                                        <input
                                                            type="number"
                                                            min={1}
                                                            value={
                                                                zone.bounds
                                                                    ?.width ||
                                                                10
                                                            }
                                                            onChange={(e) =>
                                                                handleUpdateZone(
                                                                    zone.id,
                                                                    {
                                                                        bounds: {
                                                                            x:
                                                                                zone
                                                                                    .bounds
                                                                                    ?.x ||
                                                                                0,
                                                                            y:
                                                                                zone
                                                                                    .bounds
                                                                                    ?.y ||
                                                                                0,
                                                                            width:
                                                                                parseInt(
                                                                                    e
                                                                                        .target
                                                                                        .value,
                                                                                    10,
                                                                                ) ||
                                                                                1,
                                                                            height:
                                                                                zone
                                                                                    .bounds
                                                                                    ?.height ||
                                                                                6,
                                                                        },
                                                                    },
                                                                )
                                                            }
                                                            placeholder="Rộng"
                                                            style={{
                                                                width: "50%",
                                                                padding:
                                                                    "6px 10px",
                                                                borderRadius: 6,
                                                                border: "1px solid #cbd5e1",
                                                                fontSize: 13,
                                                            }}
                                                        />
                                                        <input
                                                            type="number"
                                                            min={1}
                                                            value={
                                                                zone.bounds
                                                                    ?.height ||
                                                                6
                                                            }
                                                            onChange={(e) =>
                                                                handleUpdateZone(
                                                                    zone.id,
                                                                    {
                                                                        bounds: {
                                                                            x:
                                                                                zone
                                                                                    .bounds
                                                                                    ?.x ||
                                                                                0,
                                                                            y:
                                                                                zone
                                                                                    .bounds
                                                                                    ?.y ||
                                                                                0,
                                                                            width:
                                                                                zone
                                                                                    .bounds
                                                                                    ?.width ||
                                                                                10,
                                                                            height:
                                                                                parseInt(
                                                                                    e
                                                                                        .target
                                                                                        .value,
                                                                                    10,
                                                                                ) ||
                                                                                1,
                                                                        },
                                                                    },
                                                                )
                                                            }
                                                            placeholder="Cao"
                                                            style={{
                                                                width: "50%",
                                                                padding:
                                                                    "6px 10px",
                                                                borderRadius: 6,
                                                                border: "1px solid #cbd5e1",
                                                                fontSize: 13,
                                                            }}
                                                        />
                                                    </div>
                                                </div>
                                            )}
                                        </div>

                                        {/* Motorbike notice */}
                                        {!isCar && (
                                            <div
                                                style={{
                                                    fontSize: 13,
                                                    color: "#065f46",
                                                    background: "#f0fdf4",
                                                    padding: "8px 12px",
                                                    borderRadius: 6,
                                                    border: "1px solid #bbf7d0",
                                                }}
                                            >
                                                💡 Khu xe máy quản lý theo dung
                                                lượng chung ({zone.capacity}{" "}
                                                xe), không phân chia từng ô
                                                riêng biệt.
                                            </div>
                                        )}

                                        {/* Car slots management */}
                                        {isCar && (
                                            <div style={{ marginTop: 12 }}>
                                                <div
                                                    style={{
                                                        display: "flex",
                                                        alignItems: "center",
                                                        justifyContent:
                                                            "space-between",
                                                        marginBottom: 10,
                                                    }}
                                                >
                                                    <span
                                                        style={{
                                                            fontSize: 13,
                                                            fontWeight: 600,
                                                            color: "#334155",
                                                        }}
                                                    >
                                                        Danh sách ô đỗ (
                                                        {zone.slots?.length ||
                                                            0}{" "}
                                                        / {zone.capacity} ô):
                                                    </span>
                                                    <div
                                                        style={{
                                                            display: "flex",
                                                            gap: 8,
                                                        }}
                                                    >
                                                        <button
                                                            type="button"
                                                            onClick={() =>
                                                                handleAddSlot(
                                                                    zone.id,
                                                                )
                                                            }
                                                            disabled={
                                                                (zone.slots
                                                                    ?.length ||
                                                                    0) >=
                                                                zone.capacity
                                                            }
                                                            style={{
                                                                background:
                                                                    "#f1f5f9",
                                                                border: "1px solid #cbd5e1",
                                                                color: "#334155",
                                                                borderRadius: 6,
                                                                padding:
                                                                    "4px 10px",
                                                                fontSize: 12,
                                                                fontWeight: 600,
                                                                cursor:
                                                                    (zone.slots
                                                                        ?.length ||
                                                                        0) >=
                                                                    zone.capacity
                                                                        ? "not-allowed"
                                                                        : "pointer",
                                                            }}
                                                        >
                                                            + Thêm ô
                                                        </button>
                                                        <button
                                                            type="button"
                                                            onClick={() =>
                                                                handleAutoGenerateSlots(
                                                                    zone.id,
                                                                )
                                                            }
                                                            style={{
                                                                background:
                                                                    "#eff6ff",
                                                                border: "1px solid #bfdbfe",
                                                                color: "#2563eb",
                                                                borderRadius: 6,
                                                                padding:
                                                                    "4px 10px",
                                                                fontSize: 12,
                                                                fontWeight: 600,
                                                                cursor: "pointer",
                                                            }}
                                                        >
                                                            ⚡ Tự động tạo{" "}
                                                            {zone.capacity} ô
                                                        </button>
                                                    </div>
                                                </div>

                                                {/* Slots table */}
                                                {(zone.slots?.length || 0) ===
                                                0 ? (
                                                    <div
                                                        style={{
                                                            fontSize: 13,
                                                            color: "#94a3b8",
                                                            fontStyle: "italic",
                                                            padding: "10px 0",
                                                        }}
                                                    >
                                                        Chưa có ô đỗ nào. Nhấn
                                                        &quot;+ Thêm ô&quot;
                                                        hoặc &quot;Tự động tạo
                                                        ô&quot; để thêm ô đỗ.
                                                    </div>
                                                ) : (
                                                    <div
                                                        style={{
                                                            maxHeight: 260,
                                                            overflowY: "auto",
                                                            border: "1px solid #e2e8f0",
                                                            borderRadius: 8,
                                                        }}
                                                    >
                                                        <table
                                                            style={{
                                                                width: "100%",
                                                                borderCollapse:
                                                                    "collapse",
                                                                fontSize: 13,
                                                            }}
                                                        >
                                                            <thead
                                                                style={{
                                                                    background:
                                                                        "#f8fafc",
                                                                    textAlign:
                                                                        "left",
                                                                }}
                                                            >
                                                                <tr>
                                                                    <th
                                                                        style={{
                                                                            padding:
                                                                                "8px 12px",
                                                                            borderBottom:
                                                                                "1px solid #e2e8f0",
                                                                        }}
                                                                    >
                                                                        Mã ô
                                                                    </th>
                                                                    <th
                                                                        style={{
                                                                            padding:
                                                                                "8px 12px",
                                                                            borderBottom:
                                                                                "1px solid #e2e8f0",
                                                                        }}
                                                                    >
                                                                        Tọa độ
                                                                        (X, Y)
                                                                    </th>
                                                                    <th
                                                                        style={{
                                                                            padding:
                                                                                "8px 12px",
                                                                            borderBottom:
                                                                                "1px solid #e2e8f0",
                                                                        }}
                                                                    >
                                                                        Trạng
                                                                        thái
                                                                    </th>
                                                                    <th
                                                                        style={{
                                                                            padding:
                                                                                "8px 12px",
                                                                            borderBottom:
                                                                                "1px solid #e2e8f0",
                                                                            width: 60,
                                                                        }}
                                                                    ></th>
                                                                </tr>
                                                            </thead>
                                                            <tbody>
                                                                {zone.slots.map(
                                                                    (slot) => (
                                                                        <tr
                                                                            key={
                                                                                slot.id
                                                                            }
                                                                            style={{
                                                                                borderBottom:
                                                                                    "1px solid #f1f5f9",
                                                                            }}
                                                                        >
                                                                            <td
                                                                                style={{
                                                                                    padding:
                                                                                        "6px 12px",
                                                                                }}
                                                                            >
                                                                                <input
                                                                                    type="text"
                                                                                    value={
                                                                                        slot.slotCode
                                                                                    }
                                                                                    maxLength={
                                                                                        20
                                                                                    }
                                                                                    onChange={(
                                                                                        e,
                                                                                    ) =>
                                                                                        handleUpdateSlot(
                                                                                            zone.id,
                                                                                            slot.id,
                                                                                            {
                                                                                                slotCode:
                                                                                                    e
                                                                                                        .target
                                                                                                        .value,
                                                                                            },
                                                                                        )
                                                                                    }
                                                                                    style={{
                                                                                        width: 90,
                                                                                        padding:
                                                                                            "4px 8px",
                                                                                        borderRadius: 4,
                                                                                        border: "1px solid #cbd5e1",
                                                                                        fontWeight: 600,
                                                                                    }}
                                                                                />
                                                                            </td>
                                                                            <td
                                                                                style={{
                                                                                    padding:
                                                                                        "6px 12px",
                                                                                }}
                                                                            >
                                                                                <span
                                                                                    style={{
                                                                                        color: "#64748b",
                                                                                    }}
                                                                                >
                                                                                    (
                                                                                    {
                                                                                        slot
                                                                                            .position
                                                                                            .x
                                                                                    }

                                                                                    ,{" "}
                                                                                    {
                                                                                        slot
                                                                                            .position
                                                                                            .y
                                                                                    }

                                                                                    )
                                                                                </span>
                                                                            </td>
                                                                            <td
                                                                                style={{
                                                                                    padding:
                                                                                        "6px 12px",
                                                                                }}
                                                                            >
                                                                                <select
                                                                                    value={
                                                                                        slot.overrideState ||
                                                                                        ""
                                                                                    }
                                                                                    onChange={(
                                                                                        e,
                                                                                    ) =>
                                                                                        handleUpdateSlot(
                                                                                            zone.id,
                                                                                            slot.id,
                                                                                            {
                                                                                                overrideState:
                                                                                                    (e
                                                                                                        .target
                                                                                                        .value as SlotOverrideState) ||
                                                                                                    null,
                                                                                            },
                                                                                        )
                                                                                    }
                                                                                    style={{
                                                                                        padding:
                                                                                            "4px 8px",
                                                                                        borderRadius: 4,
                                                                                        border: "1px solid #cbd5e1",
                                                                                        fontSize: 12,
                                                                                    }}
                                                                                >
                                                                                    <option value="">
                                                                                        Bình
                                                                                        thường
                                                                                    </option>
                                                                                    <option value="Maintenance">
                                                                                        Bảo
                                                                                        trì
                                                                                        (Maintenance)
                                                                                    </option>
                                                                                    <option value="Unavailable">
                                                                                        Tạm
                                                                                        dừng
                                                                                        (Unavailable)
                                                                                    </option>
                                                                                </select>
                                                                            </td>
                                                                            <td
                                                                                style={{
                                                                                    padding:
                                                                                        "6px 12px",
                                                                                    textAlign:
                                                                                        "right",
                                                                                }}
                                                                            >
                                                                                <button
                                                                                    type="button"
                                                                                    onClick={() =>
                                                                                        handleRemoveSlot(
                                                                                            zone.id,
                                                                                            slot.id,
                                                                                        )
                                                                                    }
                                                                                    style={{
                                                                                        background:
                                                                                            "none",
                                                                                        border: "none",
                                                                                        color: "#94a3b8",
                                                                                        cursor: "pointer",
                                                                                        fontSize: 12,
                                                                                    }}
                                                                                >
                                                                                    ✕
                                                                                </button>
                                                                            </td>
                                                                        </tr>
                                                                    ),
                                                                )}
                                                            </tbody>
                                                        </table>
                                                    </div>
                                                )}
                                            </div>
                                        )}
                                    </div>
                                );
                            })
                        )}
                    </div>
                )}
            </div>
        </>
    );
}
