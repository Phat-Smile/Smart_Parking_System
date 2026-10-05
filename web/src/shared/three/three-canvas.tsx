"use client";

import dynamic from "next/dynamic";

const ThreeScene = dynamic(() => import("./three-scene"), { ssr: false });

/** Optional technical WebGL boundary; never rendered by the application shell. */
export function ThreeCanvas() {
    return <ThreeScene />;
}
