"use client";

import { useSyncExternalStore } from "react";
import type {
    AdminParkingLot,
    ApprovalStatus,
    ReviewedParkingLot,
} from "./types";

/*
 * SAMPLE DATA — Parking.WebAPI does not expose admin list/detail/summary
 * endpoints yet (US-ADMIN-002 only ships approve/reject). Replace
 * `listLots` / `findLot` with real gateway calls when they exist; the pages
 * only depend on these two functions plus `recordDecision`.
 */
const grid = (name: string, rows: number, cols: number, blocked: number[]) => ({
    name,
    rows,
    cols,
    blocked,
});

const sampleLots: AdminParkingLot[] = [
    {
        id: "11111111-1111-1111-1111-111111111111",
        name: "Vincom Center Basement",
        ownerName: "Nguyễn Minh Khoa",
        ownerEmail: "owner@parking.test",
        submittedAtUtc: "2026-10-15T02:30:00Z",
        status: "Pending",
        rejectionReason: null,
        reviewedAtUtc: null,
        address: "72 Lê Thánh Tôn, Bến Nghé, Quận 1",
        city: "TP. Hồ Chí Minh",
        latitude: 10.77808,
        longitude: 106.70201,
        is24h: false,
        openTime: "06:00",
        closeTime: "23:00",
        vehicleTypes: ["Car", "Motorbike"],
        totalCapacity: 240,
        zones: [
            {
                id: "B1-A",
                name: "B1 · Zone A",
                vehicleType: "Car",
                slots: 80,
                pricePerHourVnd: 30000,
            },
            {
                id: "B1-B",
                name: "B1 · Zone B",
                vehicleType: "Car",
                slots: 60,
                pricePerHourVnd: 30000,
            },
            {
                id: "B2-M",
                name: "B2 · Motorbike",
                vehicleType: "Motorbike",
                slots: 100,
                pricePerHourVnd: 6000,
            },
        ],
        pricing: {
            firstHourVnd: 30000,
            extraHourVnd: 20000,
            dailyCapVnd: 250000,
            overnightVnd: 150000,
        },
        holdDepositVnd: 20000,
        reservation: {
            holdMinutes: 30,
            freeCancelMinutes: 15,
            noShowRule: "Deposit is kept after the hold window expires.",
        },
        layout: [
            grid("B1", 6, 12, [24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35]),
            grid("B2", 5, 12, [0, 11, 48, 59]),
        ],
        source: "sample",
    },
    {
        id: "22222222-2222-2222-2222-222222222222",
        name: "Thảo Điền Riverside Parking",
        ownerName: "Trần Thu Hà",
        ownerEmail: "ha.tran@parkwise.vn",
        submittedAtUtc: "2026-10-16T08:10:00Z",
        status: "Pending",
        rejectionReason: null,
        reviewedAtUtc: null,
        address: "12 Quốc Hương, Thảo Điền, TP. Thủ Đức",
        city: "TP. Hồ Chí Minh",
        latitude: 10.80388,
        longitude: 106.73319,
        is24h: true,
        openTime: "00:00",
        closeTime: "23:59",
        vehicleTypes: ["Car", "EV"],
        totalCapacity: 64,
        zones: [
            {
                id: "G-A",
                name: "Ground · Zone A",
                vehicleType: "Car",
                slots: 52,
                pricePerHourVnd: 25000,
            },
            {
                id: "G-EV",
                name: "Ground · EV charging",
                vehicleType: "EV",
                slots: 12,
                pricePerHourVnd: 35000,
            },
        ],
        pricing: {
            firstHourVnd: 25000,
            extraHourVnd: 15000,
            dailyCapVnd: 200000,
            overnightVnd: 120000,
        },
        holdDepositVnd: 15000,
        reservation: {
            holdMinutes: 20,
            freeCancelMinutes: 10,
            noShowRule: "Deposit is kept after the hold window expires.",
        },
        layout: [
            grid(
                "Ground",
                4,
                16,
                [
                    16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30,
                    31,
                ],
            ),
        ],
        source: "sample",
    },
    {
        id: "33333333-3333-3333-3333-333333333333",
        name: "Bitexco Tower Parking",
        ownerName: "Lê Quốc Bảo",
        ownerEmail: "bao.le@parkwise.vn",
        submittedAtUtc: "2026-10-09T03:00:00Z",
        status: "Approved",
        rejectionReason: null,
        reviewedAtUtc: "2026-10-10T04:12:00Z",
        address: "2 Hải Triều, Bến Nghé, Quận 1",
        city: "TP. Hồ Chí Minh",
        latitude: 10.77163,
        longitude: 106.70436,
        is24h: true,
        openTime: "00:00",
        closeTime: "23:59",
        vehicleTypes: ["Car"],
        totalCapacity: 180,
        zones: [
            {
                id: "B3",
                name: "B3 · Cars",
                vehicleType: "Car",
                slots: 180,
                pricePerHourVnd: 40000,
            },
        ],
        pricing: {
            firstHourVnd: 40000,
            extraHourVnd: 30000,
            dailyCapVnd: 320000,
            overnightVnd: 180000,
        },
        holdDepositVnd: 25000,
        reservation: {
            holdMinutes: 30,
            freeCancelMinutes: 15,
            noShowRule: "Deposit is kept after the hold window expires.",
        },
        layout: [
            grid(
                "B3",
                6,
                15,
                [30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44],
            ),
        ],
        source: "sample",
    },
    {
        id: "44444444-4444-4444-4444-444444444444",
        name: "Bến Thành Market Lot",
        ownerName: "Phạm Gia Huy",
        ownerEmail: "huy.pham@parkwise.vn",
        submittedAtUtc: "2026-10-11T06:45:00Z",
        status: "Rejected",
        rejectionReason: "Missing required fire safety documentation",
        reviewedAtUtc: "2026-10-12T01:20:00Z",
        address: "Lê Lợi, Bến Thành, Quận 1",
        city: "TP. Hồ Chí Minh",
        latitude: 10.77252,
        longitude: 106.69804,
        is24h: false,
        openTime: "07:00",
        closeTime: "22:00",
        vehicleTypes: ["Motorbike"],
        totalCapacity: 150,
        zones: [
            {
                id: "M",
                name: "Open yard",
                vehicleType: "Motorbike",
                slots: 150,
                pricePerHourVnd: 5000,
            },
        ],
        pricing: {
            firstHourVnd: 5000,
            extraHourVnd: 3000,
            dailyCapVnd: 30000,
            overnightVnd: 20000,
        },
        holdDepositVnd: 5000,
        reservation: {
            holdMinutes: 15,
            freeCancelMinutes: 5,
            noShowRule: "Deposit is kept after the hold window expires.",
        },
        layout: [grid("Yard", 5, 15, [])],
        source: "sample",
    },
];

// Decisions confirmed by the backend in this browser tab overlay the sample data.
const STORAGE_KEY = "parkwise-admin-decisions";
type Decision = {
    status: ApprovalStatus;
    rejectionReason: string | null;
    reviewedAtUtc: string;
};
let decisions: Record<string, Decision> = {};
let snapshot: AdminParkingLot[] = sampleLots;
const listeners = new Set<() => void>();

function rebuild() {
    snapshot = sampleLots.map((lot) =>
        decisions[lot.id] ? { ...lot, ...decisions[lot.id] } : lot,
    );
    for (const listener of listeners) listener();
}

let hydrated = false;
function hydrate() {
    if (hydrated || typeof window === "undefined") return;
    hydrated = true;
    try {
        decisions = JSON.parse(
            window.sessionStorage.getItem(STORAGE_KEY) ?? "{}",
        );
    } catch {
        decisions = {};
    }
    rebuild();
}

export function recordDecision(result: ReviewedParkingLot) {
    decisions = {
        ...decisions,
        [result.parkingLotId]: {
            status: result.approvalStatus,
            rejectionReason: result.rejectionReason,
            reviewedAtUtc: new Date().toISOString(),
        },
    };
    try {
        window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify(decisions));
    } catch {
        // Losing the local overlay only affects the demo list, not the backend.
    }
    rebuild();
}

function subscribe(listener: () => void) {
    hydrate();
    listeners.add(listener);
    return () => listeners.delete(listener);
}

export function useAdminLots() {
    return useSyncExternalStore(
        subscribe,
        () => snapshot,
        () => sampleLots,
    );
}

export function findLot(lots: AdminParkingLot[], id: string) {
    return lots.find((lot) => lot.id.toLowerCase() === id.toLowerCase());
}

export function countByStatus(lots: AdminParkingLot[]) {
    return {
        Pending: lots.filter((l) => l.status === "Pending").length,
        Approved: lots.filter((l) => l.status === "Approved").length,
        Rejected: lots.filter((l) => l.status === "Rejected").length,
    };
}
