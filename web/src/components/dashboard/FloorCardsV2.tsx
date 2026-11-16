"use client";

import React from "react";
import { useRouter } from "next/navigation";
import { useOwnerOccupancy } from "@/features/owner/dashboard/ownerOccupancy";

export function FloorCardsV2() {
    const router = useRouter();
    const { data } = useOwnerOccupancy();

    // One card per real floor of the owner's approved lots.
    const floorData = (data?.floors ?? []).map((floor, idx) => ({
        key: `${idx}-${floor.name}`,
        name: floor.name || `Tầng ${idx + 1}`,
        pct:
            floor.total > 0
                ? Math.round(
                      ((floor.occupied + floor.reserved) / floor.total) * 100,
                  )
                : 0,
        emptyCount: floor.available,
    }));

    const handleFloorClick = () => {
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
                        key={fd.key}
                        onClick={handleFloorClick}
                        role="button"
                        tabIndex={0}
                        onKeyDown={(e) =>
                            e.key === "Enter" && handleFloorClick()
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
