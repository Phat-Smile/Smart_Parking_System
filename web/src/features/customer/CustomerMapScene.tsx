"use client";

import { useEffect, useRef } from "react";
import Map, {
    FullscreenControl,
    Marker,
    NavigationControl,
    type MapRef,
} from "react-map-gl/mapbox";
import "mapbox-gl/dist/mapbox-gl.css";
import type { MapCoordinates } from "./geocodeDestination";

export type PreviewParkingPin = {
    id: string;
    name: string;
    longitude: number;
    latitude: number;
    x: number;
    y: number;
};

export default function CustomerMapScene({
    token,
    pins,
    selectedId,
    onSelect,
    currentLocation,
    destinationLocation,
}: {
    token: string;
    pins: PreviewParkingPin[];
    selectedId: string;
    onSelect: (id: string) => void;
    currentLocation?: { longitude: number; latitude: number } | null;
    destinationLocation?: MapCoordinates | null;
}) {
    const mapRef = useRef<MapRef>(null);
    const mapTarget = destinationLocation ?? currentLocation;
    const selectedPin = pins.find((pin) => pin.id === selectedId);
    const selectedLatitude = selectedPin?.latitude;
    const selectedLongitude = selectedPin?.longitude;

    useEffect(() => {
        if (mapTarget) {
            mapRef.current?.flyTo({
                center: [mapTarget.longitude, mapTarget.latitude],
                zoom: 14,
                essential: true,
            });
        }
    }, [mapTarget]);

    useEffect(() => {
        if (selectedLatitude === undefined || selectedLongitude === undefined)
            return;
        mapRef.current?.flyTo({
            center: [selectedLongitude, selectedLatitude],
            zoom: 15.5,
            essential: true,
        });
    }, [selectedLatitude, selectedLongitude]);

    return (
        <Map
            ref={mapRef}
            mapboxAccessToken={token}
            initialViewState={{
                longitude: 106.702,
                latitude: 10.776,
                zoom: 13.8,
            }}
            mapStyle="mapbox://styles/mapbox/streets-v12"
            style={{ width: "100%", height: "100%" }}
            scrollZoom
            doubleClickZoom
            touchZoomRotate
            dragRotate={false}
            aria-label="Mapbox map of central Ho Chi Minh City"
        >
            {pins.map((pin) => (
                <Marker
                    key={pin.id}
                    longitude={pin.longitude}
                    latitude={pin.latitude}
                    anchor="bottom"
                >
                    <button
                        type="button"
                        className={`customer-map-pin ${selectedId === pin.id ? "selected" : ""}`}
                        aria-label={`Select parking lot ${pin.name}`}
                        aria-pressed={selectedId === pin.id}
                        onClick={() => onSelect(pin.id)}
                    >
                        <i
                            className="fa-solid fa-car-rear"
                            aria-hidden="true"
                        />
                    </button>
                </Marker>
            ))}
            {currentLocation && (
                <Marker
                    longitude={currentLocation.longitude}
                    latitude={currentLocation.latitude}
                    anchor="center"
                >
                    <span
                        className="customer-map-you"
                        aria-label="Your current location"
                    />
                </Marker>
            )}
            {destinationLocation && (
                <Marker
                    longitude={destinationLocation.longitude}
                    latitude={destinationLocation.latitude}
                    anchor="bottom"
                >
                    <span
                        className="customer-map-destination"
                        aria-label="Searched destination"
                    />
                </Marker>
            )}
            <NavigationControl
                position="bottom-right"
                showCompass={false}
                showZoom
            />
            <FullscreenControl position="bottom-right" />
        </Map>
    );
}
