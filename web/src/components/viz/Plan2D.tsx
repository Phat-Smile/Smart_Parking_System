"use client";

import React from "react";
import { useOwnerStore } from "../../store/useOwnerStore";
import { ST, Floor, FLOORS } from "../../types/owner";

interface Plan2DProps {
    overrideFloor?: Floor | "all";
}

const EMOJI_MAP: Record<string, string> = {
    occupied: "🚗",
    reserved: "🔖",
    maintenance: "🔧",
    verify: "❓",
};

export function Plan2D({ overrideFloor }: Plan2DProps) {
    const spots = useOwnerStore((s) => s.spots);
    const storeFloor = useOwnerStore((s) => s.activeFloor);
    const activeFloor = overrideFloor ?? storeFloor;
    const selectedId = useOwnerStore((s) => s.selectedId);
    const selectSpot = useOwnerStore((s) => s.selectSpot);

    const displayFloors =
        activeFloor === "all" ? FLOORS : [activeFloor as Floor];

    const renderRow = (f: Floor, r: "A" | "B" | "C" | "D") => {
        const rowSpots = spots.filter((s) => s.f === f && s.r === r);
        return (
            <div className="pr">
                {rowSpots.map((s) => {
                    const isSelected = selectedId === s.id;
                    const statusInfo = ST[s.s];
                    return (
                        <button
                            key={s.id}
                            className={`cell ${isSelected ? "sel" : ""}`}
                            style={
                                { "--c": statusInfo[1] } as React.CSSProperties
                            }
                            onClick={() => selectSpot(s.id)}
                            title={`${s.id} · ${statusInfo[0]}`}
                        >
                            <small>
                                {s.r}
                                {s.n + 1}
                            </small>
                            {EMOJI_MAP[s.s] || ""}
                        </button>
                    );
                })}
            </div>
        );
    };

    return (
        <div id="plan">
            {displayFloors.map((f) => (
                <div key={f} className="pf">
                    <h4>Tầng {f[1]}</h4>
                    {renderRow(f, "A")}
                    <div className="ln">Lối xe chạy</div>
                    {renderRow(f, "B")}
                    {renderRow(f, "C")}
                    <div className="ln">Lối xe chạy</div>
                    {renderRow(f, "D")}
                </div>
            ))}
        </div>
    );
}

export default Plan2D;
