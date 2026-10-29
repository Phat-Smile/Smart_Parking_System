"use client";

/**
 * Plan2DGrid – Lưới sơ đồ mặt bằng 2D thẳng hàng theo dãy như mô hình 3D.
 * Không bị ngắt dòng hay xuống hàng. Hỗ trợ cuộn ngang mượt mà.
 */

import React from "react";
import { useOwnerStore } from "../../store/useOwnerStore";
import { ST, Floor, FLOORS, ParkingSpot } from "../../types/owner";

interface Plan2DGridProps {
    overrideFloor?: Floor | "all";
    onSelect?: (spot: ParkingSpot) => void;
    searchQuery?: string;
}

export function Plan2DGrid({
    overrideFloor,
    onSelect,
    searchQuery = "",
}: Plan2DGridProps) {
    const spots = useOwnerStore((s) => s.spots);
    const storeFloor = useOwnerStore((s) => s.activeFloor);
    const activeFloor = overrideFloor ?? storeFloor;
    const selectedId = useOwnerStore((s) => s.selectedId);
    const selectSpot = useOwnerStore((s) => s.selectSpot);

    const displayFloors =
        activeFloor === "all" ? FLOORS : [activeFloor as Floor];

    const handleClick = (s: ParkingSpot) => {
        selectSpot(s.id);
        if (onSelect) onSelect(s);
    };

    const renderRow = (f: Floor, r: "A" | "B" | "C" | "D") => {
        const rowSpots = spots.filter((s) => s.f === f && s.r === r);
        return (
            <div className="pg-row">
                {rowSpots.map((s) => {
                    const isSelected = selectedId === s.id;
                    const statusInfo = ST[s.s];
                    const isMatchSearch =
                        searchQuery.trim().length > 0 &&
                        (s.p
                            .toLowerCase()
                            .includes(searchQuery.toLowerCase().trim()) ||
                            s.id
                                .toLowerCase()
                                .includes(searchQuery.toLowerCase().trim()));

                    return (
                        <button
                            key={s.id}
                            type="button"
                            className={`pg-cell ${isSelected ? "pg-sel" : ""} ${isMatchSearch ? "pg-matched" : ""}`}
                            style={
                                { "--c": statusInfo[1] } as React.CSSProperties
                            }
                            onClick={() => handleClick(s)}
                            title={`${s.id} · ${statusInfo[0]}${s.p ? ` · Biển số ${s.p}` : ""}`}
                            aria-pressed={isSelected}
                            aria-label={`${s.id}: ${statusInfo[0]}`}
                        >
                            <span>
                                {s.r}
                                {String(s.n + 1).padStart(2, "0")}
                            </span>
                        </button>
                    );
                })}
            </div>
        );
    };

    return (
        <div id="plan-grid">
            {displayFloors.map((f) => (
                <div key={f} className="pg-floor">
                    <h4 className="pg-floor-title">Tầng {f[1]}</h4>
                    <div className="pg-floor-scroll">
                        {renderRow(f, "A")}
                        <div className="pg-lane">Lối xe chạy</div>
                        {renderRow(f, "B")}
                        {renderRow(f, "C")}
                        <div className="pg-lane">Lối xe chạy</div>
                        {renderRow(f, "D")}
                    </div>
                </div>
            ))}

            <style>{`
        #plan-grid { width: 100%; }

        .pg-floor { margin-bottom: 24px; }
        .pg-floor-title { margin: 0 0 10px; font-size: 14px; font-weight: 700; color: #1e293b; }

        .pg-floor-scroll {
          overflow-x: auto;
          padding-bottom: 12px;
          scrollbar-width: thin;
          scrollbar-color: #94a3b8 #f1f5f9;
        }

        .pg-floor-scroll::-webkit-scrollbar {
          height: 6px;
        }
        .pg-floor-scroll::-webkit-scrollbar-track {
          background: #f1f5f9;
          border-radius: 999px;
        }
        .pg-floor-scroll::-webkit-scrollbar-thumb {
          background: #94a3b8;
          border-radius: 999px;
        }

        .pg-row {
          display: flex;
          flex-direction: row;
          flex-wrap: nowrap;
          gap: 6px;
          min-width: max-content;
          margin-bottom: 5px;
        }

        .pg-cell {
          width: 44px;
          height: 36px;
          min-width: 44px;
          border: 1.5px solid var(--c);
          background: color-mix(in srgb, var(--c) 12%, #ffffff);
          border-radius: 8px;
          cursor: pointer;
          display: inline-flex;
          align-items: center;
          justify-content: center;
          font-size: 11.5px;
          font-weight: 700;
          line-height: 1;
          color: var(--c);
          transition: transform 0.12s ease, box-shadow 0.12s ease;
          padding: 0;
          user-select: none;
        }

        .pg-cell:hover {
          transform: translateY(-2px);
          box-shadow: 0 4px 10px rgba(0, 0, 0, 0.08);
        }

        .pg-sel {
          outline: 2.5px solid #0f1b3d;
          outline-offset: 1px;
          transform: translateY(-2px);
          box-shadow: 0 4px 12px rgba(15, 27, 61, 0.2);
        }

        .pg-matched {
          animation: pgPulse 1s infinite alternate;
          outline: 2.5px solid #2563eb;
        }

        @keyframes pgPulse {
          0% { transform: scale(1); }
          100% { transform: scale(1.12); }
        }

        .pg-lane {
          text-align: center;
          color: #94a3b8;
          font-size: 11px;
          font-weight: 500;
          padding: 6px 0;
          margin: 6px 0;
          border-top: 1px dashed #cbd5e1;
          border-bottom: 1px dashed #cbd5e1;
          min-width: max-content;
          width: 100%;
        }
      `}</style>
        </div>
    );
}

export default Plan2DGrid;
