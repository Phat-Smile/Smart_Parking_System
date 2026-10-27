"use client";

import dynamic from "next/dynamic";
import Image from "next/image";
import { getPublicMapboxToken } from "@/shared/config/env";
import type { PreviewParkingPin } from "./CustomerMapScene";

const MapboxScene = dynamic(() => import("./CustomerMapScene"), { ssr: false });

export const previewParkingPins: PreviewParkingPin[] = [
    {
        id: "parkside",
        name: "Parkside Parking",
        longitude: 106.7038,
        latitude: 10.7782,
        x: 48,
        y: 38,
    },
    {
        id: "sunrise",
        name: "City parking",
        longitude: 106.708,
        latitude: 10.7727,
        x: 69,
        y: 57,
    },
    {
        id: "central",
        name: "Covered garage",
        longitude: 106.696,
        latitude: 10.7725,
        x: 31,
        y: 67,
    },
];

export function CustomerMap({
    selectedId,
    onSelect,
    currentLocation,
    visibleIds,
}: {
    selectedId: string;
    onSelect: (id: string) => void;
    currentLocation?: { longitude: number; latitude: number } | null;
    visibleIds?: string[];
}) {
    const token = getPublicMapboxToken();
    const pins = visibleIds
        ? previewParkingPins.filter((pin) => visibleIds.includes(pin.id))
        : previewParkingPins;
    const selected = pins.find((pin) => pin.id === selectedId);

    return (
        <div className="customer-map" aria-label="Parking map preview">
            {token ? (
                <MapboxScene
                    token={token}
                    pins={pins}
                    selectedId={selectedId}
                    onSelect={onSelect}
                    currentLocation={currentLocation}
                />
            ) : (
                <>
                    <Image
                        src="/images/parkwise/customer-map-preview.svg"
                        alt="Illustrated street map of central Ho Chi Minh City"
                        fill
                        sizes="(max-width: 800px) 100vw, 48vw"
                        priority
                    />
                    {pins.map((pin) => (
                        <button
                            key={pin.id}
                            type="button"
                            className={`customer-map-pin customer-map-preview-pin ${selectedId === pin.id ? "selected" : ""}`}
                            style={{ left: `${pin.x}%`, top: `${pin.y}%` }}
                            aria-label={`Select example location ${pin.name}`}
                            aria-pressed={selectedId === pin.id}
                            onClick={() => onSelect(pin.id)}
                        >
                            <span>P</span>
                        </button>
                    ))}
                </>
            )}
            {selected && (
                <div className="customer-map-selected" role="status">
                    <span className="customer-map-selected-icon">P</span>
                    <span>
                        <strong>{selected.name}</strong>
                        <small>Design preview location</small>
                    </span>
                </div>
            )}
            {currentLocation && !token && (
                <span className="customer-map-location-notice">
                    Current location selected · Mapbox map needs a token
                </span>
            )}
            <span className="customer-map-caption">
                {token
                    ? "MAPBOX · EXAMPLE LOCATIONS"
                    : "MAP PREVIEW · MAPBOX TOKEN NEEDED"}
            </span>
        </div>
    );
}
