"use client";

import Map from "react-map-gl/mapbox";
import "mapbox-gl/dist/mapbox-gl.css";

export default function MapScene({ token }: { token: string }) {
    return (
        <Map
            mapboxAccessToken={token}
            initialViewState={{ longitude: 0, latitude: 0, zoom: 1 }}
            mapStyle="mapbox://styles/mapbox/streets-v12"
            aria-label="Technical map canvas"
        />
    );
}
