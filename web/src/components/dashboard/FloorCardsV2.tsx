"use client";

import React from "react";
import { useRouter } from "next/navigation";
import { useOwnerStore } from "@/store/useOwnerStore";
import { FLOORS, Floor } from "@/types/owner";

export function FloorCardsV2() {
    const router = useRouter();
    const spots = useOwnerStore((s) => s.spots);
    const setActiveFloor = useOwnerStore((s) => s.setActiveFloor);

    // Compute floor occupancies from actual spots if available
    const floorData = FLOORS.map((f, idx) => {
        const floorSpots = spots.filter((s) => s.f === f);
        const total = floorSpots.length || 100;
        const occupiedOrReserved = floorSpots.filter(
            (s) => s.s === "occupied" || s.s === "reserved",
        ).length;
        const available = floorSpots.filter((s) => s.s === "available").length;

        // Use actual or standard defaults from the mockup: [57%, 52%, 60%, 59%, 62%]
        const defaultPcts = [57, 52, 60, 59, 62];
        const defaultAvail = [27, 30, 22, 26, 22];

        const pct =
            total > 0 && floorSpots.length > 0
                ? Math.round((occupiedOrReserved / total) * 100)
                : defaultPcts[idx];
        const emptyCount =
            total > 0 && floorSpots.length > 0 ? available : defaultAvail[idx];

        return {
            floor: f,
            name: `Tầng ${idx + 1}`,
            pct,
            emptyCount,
        };
    });

    const handleFloorClick = (f: Floor) => {
        setActiveFloor(f);
        router.push("/owner/layout/2d");
    };

    return (
        <div style={{ marginTop: 24 }}>
            <h2
                style={{
                    fontSize: 18,
                    fontWeight: 800,
                    margin: "0 0 16px",
                    color: "#0f1b3d",
                }}
            >
                Theo từng tầng
            </h2>

            <div
                className="ov2-floor-grid"
                style={{
                    display: "grid",
                    gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))",
                    gap: 16,
                }}
            >
                {floorData.map((fd) => (
                    <div
                        key={fd.floor}
                        onClick={() => handleFloorClick(fd.floor)}
                        role="button"
                        tabIndex={0}
                        onKeyDown={(e) =>
                            e.key === "Enter" && handleFloorClick(fd.floor)
                        }
                        aria-label={`${fd.name}: ${fd.pct}% lấp đầy, ${fd.emptyCount} ô trống`}
                        style={{
                            background: "#fff",
                            borderRadius: 18,
                            border: "1px solid #e3e8f2",
                            padding: 20,
                            cursor: "pointer",
                            display: "flex",
                            flexDirection: "column",
                            gap: 8,
                            transition:
                                "transform 0.15s ease, box-shadow 0.15s ease",
                        }}
                    >
                        <div
                            style={{
                                fontSize: 13,
                                color: "#6a7491",
                                fontWeight: 500,
                            }}
                        >
                            {fd.name}
                        </div>

                        <div
                            style={{
                                fontSize: 30,
                                fontWeight: 800,
                                color: "#0f1b3d",
                                lineHeight: 1,
                            }}
                        >
                            {fd.pct}%
                        </div>

                        {/* Horizontal progress bar */}
                        <div
                            style={{
                                width: "100%",
                                height: 7,
                                backgroundColor: "#e3e8f2",
                                borderRadius: 99,
                                overflow: "hidden",
                                margin: "4px 0",
                            }}
                        >
                            <div
                                style={{
                                    width: `${fd.pct}%`,
                                    height: "100%",
                                    backgroundColor: "#0f1b3d",
                                    borderRadius: 99,
                                }}
                            />
                        </div>

                        <div style={{ fontSize: 12, color: "#6a7491" }}>
                            {fd.emptyCount} ô trống
                        </div>
                    </div>
                ))}
            </div>
        </div>
    );
}
