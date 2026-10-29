"use client";

import React from "react";
import dynamic from "next/dynamic";
import { getPublicMapboxToken } from "@/shared/config/env";
import { useAllLots, useOwnerMapStore } from "./useOwnerMapStore";
import { GoogleStyleMap } from "./GoogleStyleMap";
import "mapbox-gl/dist/mapbox-gl.css";

const OwnerMapScene = dynamic(() => import("./OwnerMapScene"), { ssr: false });

interface OwnerMapboxProps {
    token?: string;
}

export function OwnerMapbox({ token: propToken }: OwnerMapboxProps) {
    // Use prop token from server component or fallback to client public token
    const token = propToken || getPublicMapboxToken();
    const lots = useAllLots();
    const {
        selectedLotId,
        setSelectedLotId,
        mapSearchQuery,
        setMapSearchQuery,
        setAddModalOpen,
    } = useOwnerMapStore();

    const filteredLots = lots.filter(
        (l) =>
            !mapSearchQuery ||
            l.name.toLowerCase().includes(mapSearchQuery.toLowerCase()) ||
            l.addr.toLowerCase().includes(mapSearchQuery.toLowerCase()),
    );

    return (
        <div
            className="ov2-card"
            style={{
                background: "#fff",
                borderRadius: 18,
                border: "1px solid #e3e8f2",
                padding: 24,
                display: "flex",
                flexDirection: "column",
            }}
        >
            {/* Header */}
            <div
                style={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    flexWrap: "wrap",
                    gap: 12,
                    marginBottom: 16,
                }}
            >
                <h2
                    style={{
                        fontSize: 18,
                        fontWeight: 800,
                        margin: 0,
                        color: "#0f1b3d",
                    }}
                >
                    Vị trí các bãi
                </h2>

                <button
                    type="button"
                    onClick={() => setAddModalOpen(true)}
                    style={{
                        background: "#d9f99d",
                        color: "#143806",
                        border: "none",
                        borderRadius: 10,
                        padding: "8px 16px",
                        fontSize: 13,
                        fontWeight: 700,
                        cursor: "pointer",
                        display: "flex",
                        alignItems: "center",
                        gap: 6,
                    }}
                >
                    + Thêm bãi mới
                </button>
            </div>

            {/* Map Container */}
            <div
                style={{
                    position: "relative",
                    width: "100%",
                    height: 440,
                    borderRadius: 14,
                    overflow: "hidden",
                    background: "#eef3fb",
                    border: "1px solid #e3e8f2",
                }}
            >
                {/* Compact Google Maps Search Bar inside map */}
                <div
                    style={{
                        position: "absolute",
                        top: 14,
                        left: 14,
                        zIndex: 10,
                        width: "calc(100% - 28px)",
                        maxWidth: 210,
                    }}
                >
                    <div
                        style={{
                            display: "flex",
                            alignItems: "center",
                            background: "#fff",
                            borderRadius: 8,
                            padding: "7px 11px",
                            boxShadow: "0 2px 6px rgba(0,0,0,0.22)",
                            border: "1px solid #dadce0",
                            gap: 8,
                        }}
                    >
                        <svg
                            width="14"
                            height="14"
                            viewBox="0 0 24 24"
                            fill="none"
                            stroke="#5f6368"
                            strokeWidth="2.5"
                            strokeLinecap="round"
                            strokeLinejoin="round"
                        >
                            <circle cx="11" cy="11" r="8" />
                            <line x1="21" y1="21" x2="16.65" y2="16.65" />
                        </svg>
                        <input
                            type="text"
                            placeholder="Tìm bãi trên bản đồ"
                            value={mapSearchQuery}
                            onChange={(e) => setMapSearchQuery(e.target.value)}
                            aria-label="Tìm bãi trên bản đồ"
                            style={{
                                border: "none",
                                background: "transparent",
                                outline: "none",
                                width: "100%",
                                fontSize: 12,
                                color: "#202124",
                            }}
                        />
                    </div>
                </div>

                {/* Mapbox Live Scene or Google-Style Interactive Tile Map */}
                {token ? (
                    <OwnerMapScene
                        token={token}
                        selectedLotId={selectedLotId}
                        onSelectLot={(id: number | null) =>
                            setSelectedLotId(id)
                        }
                        lots={filteredLots}
                    />
                ) : (
                    <GoogleStyleMap filteredLots={filteredLots} />
                )}
            </div>

            {/* Map Legend */}
            <div
                style={{
                    display: "flex",
                    gap: 20,
                    marginTop: 14,
                    fontSize: 12.5,
                    color: "#6a7491",
                    flexWrap: "wrap",
                }}
            >
                <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
                    <span
                        style={{
                            width: 8,
                            height: 8,
                            borderRadius: "50%",
                            backgroundColor: "#16a36a",
                        }}
                    />
                    <span>Đang hoạt động</span>
                </div>
                <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
                    <span
                        style={{
                            width: 8,
                            height: 8,
                            borderRadius: "50%",
                            backgroundColor: "#f59e0b",
                        }}
                    />
                    <span>Chờ duyệt</span>
                </div>
                <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
                    <span
                        style={{
                            width: 8,
                            height: 8,
                            borderRadius: "50%",
                            backgroundColor: "#e5484d",
                        }}
                    />
                    <span>Bị từ chối</span>
                </div>
            </div>
        </div>
    );
}
