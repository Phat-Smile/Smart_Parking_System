"use client";

import dynamic from "next/dynamic";
import Image from "next/image";
import { getPublicMapboxToken } from "@/shared/config/env";
import { LotPin } from "./components/LotPin";
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
function toPins(
    lots: MapParkingLot[],
    percentByLot: Record<string, number> | null | undefined,
): PreviewParkingPin[] {
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
        percent: percentByLot?.[lot.id] ?? null,
    }));
}

export function CustomerMap({
    selectedId,
    onSelect,
    currentLocation,
    destinationLocation,
    lots,
    percentByLot,
}: {
    selectedId: string;
    onSelect: (id: string) => void;
    currentLocation?: { longitude: number; latitude: number } | null;
    destinationLocation?: MapCoordinates | null;
    lots: MapParkingLot[];
    /** Free-space probability per lot id at the arrival hour chosen on the scrubber. */
    percentByLot?: Record<string, number> | null;
}) {
    const token = getPublicMapboxToken();
    const pins = toPins(lots, percentByLot);
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
                        <LotPin
                            key={pin.id}
                            name={pin.name}
                            percent={pin.percent}
                            selected={selectedId === pin.id}
                            onSelect={() => onSelect(pin.id)}
                            style={{ left: `${pin.x}%`, top: `${pin.y}%` }}
                            preview
                        />
                    ))}
                </>
            )}
            {selected && (
                <div className="customer-map-selected" role="status">
                    <span className="customer-map-selected-icon">
                        <i
                            className="fa-solid fa-car-rear"
                            aria-hidden="true"
                        />
                    </span>
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
