"use client";

import { useSyncExternalStore } from "react";

export type OwnerViewMode = "live" | "sample";

const STORAGE_KEY = "parkwise_owner_view_mode";

let listeners: Array<() => void> = [];

function notify() {
    for (const listener of listeners) {
        try {
            listener();
        } catch {}
    }
}

export function getOwnerViewMode(): OwnerViewMode {
    if (typeof window === "undefined") return "live";
    try {
        // Query param takes explicit priority
        const params = new URLSearchParams(window.location.search);
        if (params.get("mode") === "sample") {
            return "sample";
        }
        if (params.get("mode") === "live") {
            return "live";
        }

        // Pathname inspection
        if (window.location.pathname.startsWith("/owner/simulation")) {
            return "sample";
        }

        // Check persistent sessionStorage
        const stored = window.sessionStorage.getItem(STORAGE_KEY);
        if (stored === "sample" || stored === "live") {
            return stored;
        }
    } catch {}
    return "live";
}

export function setOwnerViewMode(mode: OwnerViewMode) {
    if (typeof window === "undefined") return;
    try {
        window.sessionStorage.setItem(STORAGE_KEY, mode);
        notify();
    } catch {}
}

export function subscribeOwnerViewMode(listener: () => void) {
    listeners.push(listener);
    return () => {
        listeners = listeners.filter((l) => l !== listener);
    };
}

/**
 * Hook useOwnerViewMode:
 * Cung cấp ngữ cảnh phân biệt tuyệt đối giữa "live" (Dashboard chính) và "sample" (Bản mẫu/Sandbox).
 */
export function useOwnerViewMode(): [OwnerViewMode, (mode: OwnerViewMode) => void] {
    const mode = useSyncExternalStore(
        subscribeOwnerViewMode,
        getOwnerViewMode,
        () => "live" as OwnerViewMode,
    );

    return [mode, setOwnerViewMode];
}
