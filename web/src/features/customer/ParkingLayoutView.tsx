"use client";

import dynamic from "next/dynamic";
import { useState } from "react";
import {
    Accessibility,
    Box,
    CarFront,
    Map as MapIcon,
    Minus,
    Plus,
    Zap,
} from "lucide-react";
import { parkingSpots, type ParkingSpot } from "./parkingLayoutData";

const ParkingLayout3D = dynamic(() => import("./ParkingLayout3D"), {
    ssr: false,
    loading: () => (
        <div className="lot-detail-map-loading" aria-label="Loading 3D map" />
    ),
});

type LayoutView = "2d" | "3d" | "overview";

function Spot2D({
    spot,
    selected,
    onSelect,
}: {
    spot: ParkingSpot;
    selected: boolean;
    onSelect: (id: string) => void;
}) {
    return (
        <button
            type="button"
            className={`lot-detail-map-spot is-${selected ? "selected" : spot.status}`}
            aria-label={`Space ${spot.id}, ${selected ? "selected" : spot.status}${spot.feature ? `, ${spot.feature === "ev" ? "EV charging" : "accessible"}` : ""}`}
            title={`Space ${spot.id} · ${selected ? "Selected" : spot.status}`}
            disabled={spot.status !== "available"}
            onClick={() => onSelect(spot.id)}
        >
            <span className="lot-detail-map-spot-number">{spot.id}</span>
            {spot.feature === "ev" ? (
                <Zap size={16} />
            ) : spot.feature === "accessible" ? (
                <Accessibility size={16} />
            ) : (
                <CarFront size={18} />
            )}
        </button>
    );
}

export function ParkingLayoutView({
    selectedSpot,
    onSelectSpot,
}: {
    selectedSpot: string | null;
    onSelectSpot: (id: string) => void;
}) {
    const [view, setView] = useState<LayoutView>("2d");
    const [zoom, setZoom] = useState(1);
    const available = parkingSpots.filter(
        (spot) => spot.status === "available",
    ).length;

    return (
        <section
            className="lot-detail-layout-section"
            aria-labelledby="lot-detail-layout-title"
        >
            <div className="lot-detail-layout-heading">
                <div>
                    <h2 id="lot-detail-layout-title">Parking Layout</h2>
                    <p>Explore the parking area and choose a space.</p>
                </div>
                <div
                    className="lot-detail-layout-switch"
                    role="tablist"
                    aria-label="Parking layout views"
                >
                    <button
                        type="button"
                        role="tab"
                        aria-selected={view === "2d"}
                        className={view === "2d" ? "active" : ""}
                        onClick={() => setView("2d")}
                    >
                        <MapIcon size={18} /> 2D Map
                    </button>
                    <button
                        type="button"
                        role="tab"
                        aria-selected={view === "3d"}
                        className={view === "3d" ? "active" : ""}
                        onClick={() => setView("3d")}
                    >
                        <Box size={18} /> 3D Map
                    </button>
                    <button
                        type="button"
                        role="tab"
                        aria-selected={view === "overview"}
                        className={view === "overview" ? "active" : ""}
                        onClick={() => setView("overview")}
                    >
                        <CarFront size={18} /> Overview
                    </button>
                </div>
            </div>
            <div className="lot-detail-map-panel" role="tabpanel">
                {view === "3d" ? (
                    <div className="lot-detail-map-3d">
                        <ParkingLayout3D
                            selectedSpot={selectedSpot}
                            onSelectSpot={onSelectSpot}
                            zoom={zoom}
                        />
                        <span className="lot-detail-map-hint">
                            Drag to rotate · scroll to zoom
                        </span>
                    </div>
                ) : (
                    <div className="lot-detail-map-scroll">
                        <div
                            className="lot-detail-map-2d"
                            style={{ transform: `scale(${zoom})` }}
                        >
                            <div className="lot-detail-map-tree tree-a" />
                            <div className="lot-detail-map-tree tree-b" />
                            <div className="lot-detail-map-tree tree-c" />
                            <div className="lot-detail-map-tree tree-d" />
                            <div className="lot-detail-map-spots">
                                <div className="lot-detail-map-row">
                                    {parkingSpots
                                        .filter((spot) => spot.side === "north")
                                        .map((spot) => (
                                            <Spot2D
                                                key={spot.id}
                                                spot={spot}
                                                selected={
                                                    spot.id === selectedSpot
                                                }
                                                onSelect={onSelectSpot}
                                            />
                                        ))}
                                </div>
                                <div className="lot-detail-map-road">
                                    <strong>ENTRANCE →</strong>
                                    <span>
                                        ← &nbsp;&nbsp;&nbsp; ←
                                        &nbsp;&nbsp;&nbsp; ← &nbsp;&nbsp;&nbsp;
                                        ←
                                    </span>
                                    <strong>EXIT →</strong>
                                </div>
                                <div className="lot-detail-map-row">
                                    {parkingSpots
                                        .filter((spot) => spot.side === "south")
                                        .map((spot) => (
                                            <Spot2D
                                                key={spot.id}
                                                spot={spot}
                                                selected={
                                                    spot.id === selectedSpot
                                                }
                                                onSelect={onSelectSpot}
                                            />
                                        ))}
                                </div>
                            </div>
                        </div>
                    </div>
                )}
                <div className="lot-detail-map-controls" aria-label="Map zoom">
                    <button
                        type="button"
                        aria-label="Zoom in"
                        onClick={() =>
                            setZoom((current) =>
                                Math.min(
                                    1.6,
                                    Math.round((current + 0.2) * 10) / 10,
                                ),
                            )
                        }
                    >
                        <Plus size={18} />
                    </button>
                    <button
                        type="button"
                        aria-label="Zoom out"
                        onClick={() =>
                            setZoom((current) =>
                                Math.max(
                                    0.8,
                                    Math.round((current - 0.2) * 10) / 10,
                                ),
                            )
                        }
                    >
                        <Minus size={18} />
                    </button>
                    <button
                        type="button"
                        aria-label="Reset zoom"
                        onClick={() => setZoom(1)}
                    >
                        1:1
                    </button>
                </div>
                {view === "overview" && (
                    <div className="lot-detail-map-overview">
                        <strong>Ground floor</strong>
                        <span>
                            Two rows · one central lane · entrance on the left
                        </span>
                    </div>
                )}
            </div>
            <div className="lot-detail-map-legend">
                <span>
                    <i className="available" /> Available
                </span>
                <span>
                    <i className="occupied" /> Occupied
                </span>
                <span>
                    <i className="selected" /> Selected
                </span>
                <span>
                    <i className="reserved" /> Reserved
                </span>
                <span>
                    <Zap size={14} /> EV charging
                </span>
                <span>
                    <Accessibility size={14} /> Accessible
                </span>
                <strong>{available} spaces shown as available</strong>
            </div>
        </section>
    );
}
