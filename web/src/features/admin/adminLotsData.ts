"use client";

import { useSyncExternalStore } from "react";
import { getAdminParkingLots } from "./reviewParkingLot";
import type { AdminParkingLot } from "./types";

/*
 * The lots the admin shell, dashboard and notifications count, as Parking lists them
 * (GET /api/parking/admin/lots). Loaded on first use and refreshed after a review decision.
 * A failed load keeps the last list: the counts never show invented lots.
 */
const EMPTY: AdminParkingLot[] = [];
let snapshot: AdminParkingLot[] = EMPTY;
let loaded = false;
let loading: Promise<void> | null = null;
const listeners = new Set<() => void>();

export function refreshAdminLots(): Promise<void> {
    loading ??= getAdminParkingLots("All")
        .then((lots) => {
            snapshot = lots;
            loaded = true;
            for (const listener of listeners) listener();
        })
        .catch(() => {
            // The lot pages show their own error; the badges keep the last known list.
        })
        .finally(() => {
            loading = null;
        });
    return loading;
}

function subscribe(listener: () => void) {
    listeners.add(listener);
    if (!loaded) void refreshAdminLots();
    return () => listeners.delete(listener);
}

export function useAdminLots() {
    return useSyncExternalStore(
        subscribe,
        () => snapshot,
        () => EMPTY,
    );
}

export function countByStatus(lots: AdminParkingLot[]) {
    return {
        Pending: lots.filter((l) => l.status === "Pending").length,
        Approved: lots.filter((l) => l.status === "Approved").length,
        Rejected: lots.filter((l) => l.status === "Rejected").length,
    };
}
