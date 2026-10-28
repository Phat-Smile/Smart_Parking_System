"use client";

import React from "react";
import { useOwnerStore } from "../../store/useOwnerStore";
import { ST, KIND, SlotStatusKey } from "../../types/owner";
import { K_VISIBLE } from "../../lib/mapStatus";

export function SlotInspector() {
    const selectedId = useOwnerStore((s) => s.selectedId);
    const spots = useOwnerStore((s) => s.spots);
    const updateSpotStatus = useOwnerStore((s) => s.updateSpotStatus);

    const spot = spots.find((s) => s.id === selectedId);

    if (!spot) {
        return (
            <div id="info" className="infobar">
                <p className="mut">
                    Bấm vào một ô trên mô hình 3D hoặc sơ đồ 2D để xem ô đó đang
                    trống, có xe hay đã đặt trước.
                </p>
            </div>
        );
    }

    const statusInfo = ST[spot.s];

    return (
        <div id="info" className="infobar">
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
                    {spot.s === "occupied" && ` · ${KIND[spot.i % 9]}`}
                </p>
            )}
            <div className="chips">
                {K_VISIBLE.map((k) => (
                    <button
                        key={k}
                        className="chip"
                        onClick={() =>
                            updateSpotStatus(spot.id, k as SlotStatusKey)
                        }
                    >
                        {ST[k][0]}
                    </button>
                ))}
            </div>
        </div>
    );
}

export default SlotInspector;
