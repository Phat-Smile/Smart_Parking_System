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

export interface AdminLotsMetaState {
    isLoading: boolean;
    isRefreshing: boolean;
    hasLoaded: boolean;
    error: unknown;
    lastUpdatedAt: string | null;
}

const INITIAL_META: AdminLotsMetaState = {
    isLoading: false,
    isRefreshing: false,
    hasLoaded: false,
    error: null,
    lastUpdatedAt: null,
};

let metaSnapshot: AdminLotsMetaState = INITIAL_META;

function notifyListeners() {
    for (const listener of listeners) listener();
}

export function refreshAdminLots(): Promise<void> {
    if (!loading) {
        metaSnapshot = {
            ...metaSnapshot,
            isLoading: !loaded,
            isRefreshing: true,
            error: null,
        };
        notifyListeners();

        loading = getAdminParkingLots("All")
            .then((lots) => {
                snapshot = lots;
                loaded = true;
                metaSnapshot = {
                    isLoading: false,
                    isRefreshing: false,
                    hasLoaded: true,
                    error: null,
                    lastUpdatedAt: new Date().toISOString(),
                };
                notifyListeners();
            })
            .catch((err: unknown) => {
                // The lot pages show their own error; the badges keep the last known list.
                metaSnapshot = {
                    ...metaSnapshot,
                    isLoading: false,
                    isRefreshing: false,
                    error: err,
                };
                notifyListeners();
            })
            .finally(() => {
                loading = null;
            });
    }
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

export function useAdminLotsMeta(): AdminLotsMetaState {
    return useSyncExternalStore(
        subscribe,
        () => metaSnapshot,
        () => INITIAL_META,
    );
}

export function countByStatus(lots: AdminParkingLot[]) {
    return {
        Pending: lots.filter((l) => l.status === "Pending").length,
        Approved: lots.filter((l) => l.status === "Approved").length,
        Rejected: lots.filter((l) => l.status === "Rejected").length,
    };
}
