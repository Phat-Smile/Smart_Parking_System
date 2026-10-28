"use client";

import React from "react";
import { useOwnerStore } from "@/store/useOwnerStore";
import { ST, FLOORS, Floor, SlotStatusKey } from "@/types/owner";

interface SlotStatusListPanelProps {
    status: SlotStatusKey;
    onClose: () => void;
    onSelectSpot?: (spotId: string) => void;
}

export function SlotStatusListPanel({
    status,
    onClose,
    onSelectSpot,
}: SlotStatusListPanelProps) {
    const spots = useOwnerStore((s) => s.spots);
    const activeFloor = useOwnerStore((s) => s.activeFloor);
    const setActiveFloor = useOwnerStore((s) => s.setActiveFloor);
    const selectSpot = useOwnerStore((s) => s.selectSpot);

    const [statusName, statusColor] = ST[status];

    // Các ô liên quan dựa trên activeFloor
    const relevantSpots =
        activeFloor === "all"
            ? spots
            : spots.filter((s) => s.f === activeFloor);
    const totalCount = relevantSpots.length || 1;
    const matchingSpots = relevantSpots.filter((s) => s.s === status);
    const matchingCount = matchingSpots.length;
    const pct = Math.round((matchingCount / totalCount) * 100);

    const scopeLabel =
        activeFloor === "all" ? "tất cả tầng" : `Tầng ${activeFloor[1]}`;

    // Nhóm theo tầng
    const targetFloors = activeFloor === "all" ? FLOORS : [activeFloor];
    const floorGroups = targetFloors.map((f) => {
        const list = spots.filter((s) => s.f === f && s.s === status);
        return {
            floor: f,
            name: `Tầng ${f[1]}`,
            count: list.length,
            spots: list,
        };
    });

    const handleSpotClick = (spotId: string, floor: Floor) => {
        setActiveFloor(floor);
        selectSpot(spotId);
        if (onSelectSpot) {
            onSelectSpot(spotId);
        }
    };

    return (
        <div
            className="ov2-slot-filter-panel"
            style={{
                background: "#ffffff",
                borderRadius: 16,
                border: `1.5px solid ${statusColor}44`,
                boxShadow: "0 6px 20px rgba(15, 27, 61, 0.08)",
                padding: 18,
                margin: "12px 0 16px",
                animation: "ov2SlideIn 0.15s ease-out",
            }}
        >
            {/* Header panel */}
            <div
                style={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    marginBottom: 12,
                    borderBottom: "1px solid #f1f3f7",
                    paddingBottom: 10,
                }}
            >
                <div>
                    <div
                        style={{
                            fontSize: 15,
                            fontWeight: 800,
                            color: "#0f1b3d",
                        }}
                    >
                        <span style={{ color: statusColor }}>
                            {matchingCount} ô {statusName.toLowerCase()}
                        </span>
                        <span
                            style={{
                                color: "#64748b",
                                fontWeight: 600,
                                fontSize: 13,
                            }}
                        >
                            {" "}
                            · {scopeLabel} · {pct}% số ô
                        </span>
                    </div>
                </div>

                <button
                    type="button"
                    onClick={onClose}
                    aria-label="Đóng danh sách lọc ô"
                    style={{
                        background: "#f8faff",
                        border: "1px solid #e2e8f0",
                        borderRadius: 8,
                        padding: "4px 8px",
                        fontSize: 12,
                        fontWeight: 600,
                        color: "#64748b",
                        cursor: "pointer",
                    }}
                >
                    ✕ Đóng
                </button>
            </div>

            {/* Danh sách từng tầng */}
            <div
                style={{
                    display: "flex",
                    flexDirection: "column",
                    gap: 10,
                    maxHeight: 280,
                    overflowY: "auto",
                }}
            >
                {floorGroups.map((group) => {
                    if (group.count === 0 && activeFloor === "all") return null;

                    return (
                        <div
                            key={group.floor}
                            style={{
                                display: "flex",
                                flexDirection: "column",
                                gap: 6,
                                padding: "8px 12px",
                                borderRadius: 10,
                                background: "#f8faff",
                                border: "1px solid #edf2f7",
                            }}
                        >
                            <div
                                style={{
                                    display: "flex",
                                    justifyContent: "space-between",
                                    alignItems: "center",
                                }}
                            >
                                <button
                                    type="button"
                                    onClick={() => setActiveFloor(group.floor)}
                                    style={{
                                        border: "none",
                                        background: "none",
                                        fontSize: 13,
                                        fontWeight: 700,
                                        color: "#2563EB",
                                        cursor: "pointer",
                                        padding: 0,
                                        textAlign: "left",
                                    }}
                                >
                                    {group.name} ({group.count} ô) →
                                </button>
                            </div>

                            {/* Danh sách mã ô dạng pill bấm được */}
                            <div
                                style={{
                                    display: "flex",
                                    flexWrap: "wrap",
                                    gap: 6,
                                }}
                            >
                                {group.spots.length > 0 ? (
                                    group.spots.map((sp) => (
                                        <button
                                            key={sp.id}
                                            type="button"
                                            onClick={() =>
                                                handleSpotClick(
                                                    sp.id,
                                                    group.floor,
                                                )
                                            }
                                            aria-label={`Chọn ô ${sp.id}`}
                                            style={{
                                                background: "#ffffff",
                                                border: `1px solid ${statusColor}66`,
                                                borderRadius: 6,
                                                padding: "3px 8px",
                                                fontSize: 11.5,
                                                fontWeight: 700,
                                                color: "#0f1b3d",
                                                cursor: "pointer",
                                                transition: "all 0.12s ease",
                                            }}
                                            onMouseEnter={(e) => {
                                                e.currentTarget.style.backgroundColor = `${statusColor}18`;
                                                e.currentTarget.style.borderColor =
                                                    statusColor;
                                            }}
                                            onMouseLeave={(e) => {
                                                e.currentTarget.style.backgroundColor =
                                                    "#ffffff";
                                                e.currentTarget.style.borderColor = `${statusColor}66`;
                                            }}
                                        >
                                            {sp.id}
                                        </button>
                                    ))
                                ) : (
                                    <span
                                        style={{
                                            fontSize: 12,
                                            color: "#94a3b8",
                                        }}
                                    >
                                        Không có ô nào
                                    </span>
                                )}
                            </div>
                        </div>
                    );
                })}
            </div>
        </div>
    );
}
