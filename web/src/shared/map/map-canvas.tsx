"use client";

import dynamic from "next/dynamic";
import { getPublicMapboxToken } from "@/shared/config/env";

const MapScene = dynamic(() => import("./map-scene"), { ssr: false });

/** Optional technical map boundary; no business markers or requests. */
export function MapCanvas() {
    const token = getPublicMapboxToken();
    if (!token) return <p>Map preview requires a public Mapbox token.</p>;
    return <MapScene token={token} />;
}
