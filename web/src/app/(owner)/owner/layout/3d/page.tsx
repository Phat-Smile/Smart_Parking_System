"use client";

import React, { useEffect, useState } from "react";
import dynamic from "next/dynamic";
import { OwnerPageHeader as PageHeader } from "../../../../../components/layout/OwnerPageHeader";
import {
    useOwnerStore,
    getSpotCounts,
} from "../../../../../store/useOwnerStore";
import { ST, FLOORS, Floor, SlotStatusKey } from "../../../../../types/owner";
import { K_VISIBLE } from "../../../../../lib/mapStatus";
import { SlotStatusChips } from "@/features/owner/layout/SlotStatusChips";
import { SlotStatusListPanel } from "@/features/owner/layout/SlotStatusListPanel";

// Dynamically import 3D viewer — must remain ssr:false, do NOT change
const ParkingScene3DViewer = dynamic(
    () => import("../../../../../components/viz/ParkingScene3DViewer"),
    { ssr: false },
);

// ──────────────────────────────────────────────────────────
// FloorStatsPanel – hiển thị thống kê tầng dạng rút gọn (Accordion).
// Bấm một tầng → Mở chi tiết thanh trạng thái & setActiveFloor.
// ──────────────────────────────────────────────────────────
function FloorStatsPanel() {
    const spots = useOwnerStore((s) => s.spots);
    const activeFloor = useOwnerStore((s) => s.activeFloor);
    const setActiveFloor = useOwnerStore((s) => s.setActiveFloor);

    // Quản lý tầng đang mở rộng (Accordion). Mặc định mở tầng active hoặc 'all'.
    const [expandedFloor, setExpandedFloor] = useState<string | null>(activeFloor || "all");

    useEffect(() => {
        if (activeFloor) {
            setExpandedFloor(activeFloor);
        }
    }, [activeFloor]);

    const rows = ["all", ...FLOORS] as const;

    const handleToggle = (f: Floor | "all") => {
        setActiveFloor(f);
        setExpandedFloor((prev) => (prev === f ? null : f));
    };

    return (
        <div id="floor-stats">
            {rows.map((f) => {
                const floorSpots =
                    f === "all" ? spots : spots.filter((s) => s.f === f);
                const counts = getSpotCounts(floorSpots);
                const total = floorSpots.length || 1;
                const isActive = activeFloor === f;
                const isExpanded = expandedFloor === f;

                return (
                    <div
                        key={f}
                        className={`fs-row ${isActive ? "fs-active" : ""} ${isExpanded ? "fs-expanded" : "fs-collapsed"}`}
                        onClick={() => handleToggle(f as Floor | "all")}
                        role="button"
                        tabIndex={0}
                        onKeyDown={(e) =>
                            e.key === "Enter" &&
                            handleToggle(f as Floor | "all")
                        }
                        aria-pressed={isActive}
                        aria-expanded={isExpanded}
                    >
                        <div className="fs-label">
                            <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                                <svg
                                    width="12"
                                    height="12"
                                    viewBox="0 0 24 24"
                                    fill="none"
                                    stroke="currentColor"
                                    strokeWidth="2.5"
                                    strokeLinecap="round"
                                    strokeLinejoin="round"
                                    style={{
                                        transform: isExpanded ? "rotate(90deg)" : "rotate(0deg)",
                                        transition: "transform 0.18s ease",
                                        color: isActive ? "var(--ink, #0f1b3d)" : "#8a94a6",
                                        flexShrink: 0,
                                    }}
                                    aria-hidden="true"
                                >
                                    <polyline points="9 18 15 12 9 6" />
                                </svg>
                                <span className="fs-name">
                                    {f === "all" ? "Tất cả tầng" : `Tầng ${f[1]}`}
                                </span>
                            </div>
                            <span className="fs-total mut">
                                {floorSpots.length} ô
                            </span>
                        </div>

                        {/* Chi tiết trạng thái (Trống, Đã đặt, Có xe, Bảo trì) - chỉ mở khi được chọn */}
                        {isExpanded && (
                            <div className="fs-content">
                                {K_VISIBLE.map((k) => {
                                    const pct = Math.round((counts[k] / total) * 100);
                                    return (
                                        <div key={k} className="fs-stat">
                                            <div className="fs-bar-row">
                                                <span
                                                    className="fs-stat-label"
                                                    style={{ color: ST[k][1] }}
                                                >
                                                    {ST[k][0]}
                                                </span>
                                                <span className="fs-stat-count">
                                                    {counts[k]}
                                                </span>
                                            </div>
                                            <div className="fs-bar">
                                                <div
                                                    className="fs-bar-fill"
                                                    style={{
                                                        width: `${pct}%`,
                                                        background: ST[k][1],
                                                    }}
                                                />
                                            </div>
                                        </div>
                                    );
                                })}
                            </div>
                        )}
                    </div>
                );
            })}
        </div>
    );
}

// ──────────────────────────────────────────────────────────
// SlotDetailPanel – chi tiết ô khi đã chọn.
// ──────────────────────────────────────────────────────────
function SlotDetailPanel({ onBack }: { onBack: () => void }) {
    const selectedId = useOwnerStore((s) => s.selectedId);
    const spots = useOwnerStore((s) => s.spots);
    const updateSpotStatus = useOwnerStore((s) => s.updateSpotStatus);

    const spot = spots.find((s) => s.id === selectedId);
    if (!spot) return null;

    const statusInfo = ST[spot.s];

    return (
        <div id="slot-detail" className="infobar">
            <button
                className="btn sm"
                style={{ marginBottom: 12 }}
                onClick={onBack}
            >
                ← Danh sách tầng
            </button>
            <h3>Ô {spot.id}</h3>
            <p className="mut">
                Tầng {spot.f[1]} · Dãy {spot.r} · Vị trí {spot.n + 1}
            </p>
            <p>
                <span
                    className="tag"
                    style={{ "--c": statusInfo[1] } as React.CSSProperties}
                >
                    {statusInfo[0]}
                </span>
            </p>
            {(spot.s === "occupied" || spot.s === "reserved") && (
                <p>
                    Biển số <b>{spot.p}</b>
                </p>
            )}
            <p className="mut" style={{ fontSize: 12, marginTop: 8 }}>
                Đổi trạng thái:
            </p>
            <div className="chips">
                {K_VISIBLE.map((k) => (
                    <button
                        key={k}
                        className="chip"
                        onClick={() => updateSpotStatus(spot.id, k)}
                    >
                        {ST[k][0]}
                    </button>
                ))}
            </div>
        </div>
    );
}

// ──────────────────────────────────────────────────────────
// Layout3DPage – trang chính
// ──────────────────────────────────────────────────────────
export default function Layout3DPage() {
    const activeFloor = useOwnerStore((s) => s.activeFloor);
    const setActiveFloor = useOwnerStore((s) => s.setActiveFloor);
    const selectedId = useOwnerStore((s) => s.selectedId);
    const selectSpot = useOwnerStore((s) => s.selectSpot);
    const lots = useOwnerStore((s) => s.lots);
    const simulateTick = useOwnerStore((s) => s.simulateTick);

    // showDetail được derive trực tiếp từ selectedId — không dùng state riêng
    const showDetail = !!selectedId;

    const [selectedStatus, setSelectedStatus] = useState<SlotStatusKey | null>(
        null,
    );

    // Auto-live simulation
    useEffect(() => {
        const timer = setInterval(() => simulateTick(), 5000);
        return () => clearInterval(timer);
    }, [simulateTick]);

    // Khi bấm ô trên 3D → chọn ô (showDetail tự cập nhật qua selectedId)
    const handleSlotPick = (id: string) => {
        selectSpot(id);
    };

    const mainLot = lots[0] || { name: "Bãi đỗ xe Vincom Đồng Khởi" };

    return (
        <>
            <PageHeader
                breadcrumb="/owner/layout/3d"
                title="Sơ đồ 3D"
                subtitle={`${mainLot.name} · theo dõi trực tiếp`}
            />

            {/* Chip thống kê tổng bấm được */}
            <SlotStatusChips
                selectedStatus={selectedStatus}
                onToggleStatus={(k) =>
                    setSelectedStatus((prev) => (prev === k ? null : k))
                }
                onClearStatus={() => setSelectedStatus(null)}
            />

            <div className="grid">
                {/* Khung 3D - 8 cột */}
                <div className="card c8">
                    <div className="wh">
                        {/* Tab chọn tầng */}
                        <div className="segs" id="ftabs">
                            {(["all", ...FLOORS] as const).map((f) => (
                                <button
                                    key={f}
                                    className={activeFloor === f ? "on" : ""}
                                    onClick={() =>
                                        setActiveFloor(f as Floor | "all")
                                    }
                                >
                                    {f === "all"
                                        ? "Tất cả tầng"
                                        : `Tầng ${f[1]}`}
                                </button>
                            ))}
                        </div>
                    </div>

                    {/* Engine 3D — KHÔNG sửa props, dùng đúng dynamic import */}
                    <ParkingScene3DViewer onSlotPick={handleSlotPick} />

                    {/* Legend */}
                    <div className="lgd">
                        {K_VISIBLE.map((k) => (
                            <span key={k}>
                                <i style={{ background: ST[k][1] }} />
                                {ST[k][0]}
                            </span>
                        ))}
                    </div>
                </div>

                {/* Khung Chi tiết / Thống kê tầng / Lọc ô theo trạng thái - 4 cột */}
                <div className="card c4">
                    <div className="wh">
                        <h3>
                            {selectedStatus
                                ? `Lọc theo trạng thái`
                                : showDetail && selectedId
                                  ? `Chi tiết ô`
                                  : "Thống kê tầng"}
                        </h3>
                    </div>

                    {selectedStatus ? (
                        <SlotStatusListPanel
                            status={selectedStatus}
                            onClose={() => setSelectedStatus(null)}
                            onSelectSpot={(id) => handleSlotPick(id)}
                        />
                    ) : showDetail ? (
                        <SlotDetailPanel onBack={() => selectSpot(null)} />
                    ) : (
                        <FloorStatsPanel />
                    )}

                    <p className="mut" style={{ marginTop: 12, fontSize: 12 }}>
                        <span className="live" />
                        Trạng thái cập nhật trực tiếp, 2D và 3D luôn khớp nhau.
                    </p>
                </div>
            </div>

            {/* CSS scoped cho FloorStatsPanel (Accordion) */}
            <style>{`
        #floor-stats { display: flex; flex-direction: column; gap: 8px; }

        @keyframes fsFadeIn {
          from { opacity: 0; transform: translateY(-4px); }
          to { opacity: 1; transform: translateY(0); }
        }

        .fs-row {
          padding: 10px 14px;
          border-radius: 12px;
          border: 1px solid var(--line);
          cursor: pointer;
          transition: background 0.15s ease, border-color 0.15s ease, box-shadow 0.15s ease;
          user-select: none;
        }
        .fs-row:hover { background: var(--soft); }
        .fs-active {
          border-color: var(--pw-lime, #AAEC38);
          background: color-mix(in srgb, var(--pw-lime, #AAEC38) 8%, #fff);
        }
        .fs-expanded {
          box-shadow: 0 4px 14px rgba(15, 27, 61, 0.05);
        }

        .fs-label {
          display: flex;
          justify-content: space-between;
          align-items: center;
        }
        .fs-name { font-weight: 700; font-size: 13.5px; }
        .fs-total { font-size: 11.5px; }

        .fs-content {
          margin-top: 10px;
          padding-top: 8px;
          border-top: 1px dashed var(--line);
          animation: fsFadeIn 0.18s ease-out;
        }

        .fs-stat { margin-bottom: 5px; }
        .fs-stat:last-child { margin-bottom: 0; }
        .fs-bar-row {
          display: flex;
          justify-content: space-between;
          font-size: 11px;
          margin-bottom: 2px;
        }
        .fs-stat-label { font-weight: 600; }
        .fs-stat-count { color: var(--mut); }

        .fs-bar {
          height: 4px;
          background: var(--line);
          border-radius: 4px;
          overflow: hidden;
        }
        .fs-bar-fill {
          height: 100%;
          border-radius: 4px;
          transition: width 0.3s ease;
        }
      `}</style>
        </>
    );
}
