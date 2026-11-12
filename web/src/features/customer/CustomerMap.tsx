"use client";

import dynamic from "next/dynamic";
import Image from "next/image";
import { getPublicMapboxToken } from "@/shared/config/env";
import type { PreviewParkingPin } from "./CustomerMapScene";
import type { MapCoordinates } from "./geocodeDestination";

const MapboxScene = dynamic(() => import("./CustomerMapScene"), { ssr: false });

export type MapParkingLot = {
    id: string;
    name: string;
    latitude: number;
    longitude: number;
};

// Without a Mapbox token the lots are drawn on the illustrated map, spread by their coordinates.
function toPins(lots: MapParkingLot[]): PreviewParkingPin[] {
    if (lots.length === 0) return [];
    const lats = lots.map((lot) => lot.latitude);
    const lngs = lots.map((lot) => lot.longitude);
    const minLat = Math.min(...lats);
    const minLng = Math.min(...lngs);
    const latSpan = Math.max(...lats) - minLat || 1;
    const lngSpan = Math.max(...lngs) - minLng || 1;
    return lots.map((lot) => ({
        id: lot.id,
        name: lot.name,
        latitude: lot.latitude,
        longitude: lot.longitude,
        x: 15 + 70 * ((lot.longitude - minLng) / lngSpan),
        y: 15 + 70 * (1 - (lot.latitude - minLat) / latSpan),
    }));
}

export function CustomerMap({
    selectedId,
    onSelect,
    currentLocation,
    destinationLocation,
    lots,
}: {
    selectedId: string;
    onSelect: (id: string) => void;
    currentLocation?: { longitude: number; latitude: number } | null;
    destinationLocation?: MapCoordinates | null;
    lots: MapParkingLot[];
}) {
    const token = getPublicMapboxToken();
    const pins = toPins(lots);
    const selected = pins.find((pin) => pin.id === selectedId);

    return (
        <div className="customer-map" aria-label="Parking map">
            {token ? (
                <MapboxScene
                    token={token}
                    pins={pins}
                    selectedId={selectedId}
                    onSelect={onSelect}
                    currentLocation={currentLocation}
                    destinationLocation={destinationLocation}
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
                            aria-label={`Select parking lot ${pin.name}`}
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
                        <small>Approved parking lot</small>
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
                    ? "MAPBOX · APPROVED PARKING LOTS"
                    : "MAP PREVIEW · MAPBOX TOKEN NEEDED"}
            </span>
        </div>
    );
}
