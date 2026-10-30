import { create } from "zustand";
import {
    ParkingSpot,
    SlotStatusKey,
    Floor,
    ParkingLot,
    Reservation,
    ActivityItem,
    createInitialSpots,
    INITIAL_LOTS,
    INITIAL_RESERVATIONS,
    INITIAL_ACTIVITIES,
    KIND,
    FLOORS,
} from "../types/owner";
import { mapStatus } from "../lib/mapStatus";

interface OwnerStoreState {
    spots: ParkingSpot[];
    selectedId: string | null;
    activeFloor: Floor | "all";
    viewMode: "both" | "3d" | "2d";
    lots: ParkingLot[];
    currentLotId: number;
    reservations: Reservation[];
    activities: ActivityItem[];
    reportFilter: { mode: "day" | "month" | "year"; date: string };
    revenuePeriod: "day" | "week" | "month";
    reservationSearch: { q: string; s: string };
    editingLot: boolean;
    detailTab: "info" | "zones" | "types" | "price" | "policy" | "appr";

    // Actions
    selectSpot: (id: string | null) => void;
    updateSpotStatus: (id: string, s: SlotStatusKey) => void;
    setActiveFloor: (f: Floor | "all") => void;
    setViewMode: (m: "both" | "3d" | "2d") => void;
    setCurrentLotId: (id: number) => void;
    setRevenuePeriod: (p: "day" | "week" | "month") => void;
    setReportFilter: (
        filter: Partial<{ mode: "day" | "month" | "year"; date: string }>,
    ) => void;
    setReservationSearch: (search: Partial<{ q: string; s: string }>) => void;
    setDetailTab: (
        t: "info" | "zones" | "types" | "price" | "policy" | "appr",
    ) => void;
    setEditingLot: (editing: boolean) => void;
    updateLot: (id: number, data: Partial<ParkingLot>) => void;
    addLot: (lot: ParkingLot) => void;
    deleteZone: (lotId: number, zoneIdx: number) => void;
    addZone: (
        lotId: number,
        zone: { n: string; t: "car" | "moto"; c: number; s: "open" | "closed" },
    ) => void;
    updateZoneStatus: (
        lotId: number,
        zoneIdx: number,
        s: "open" | "closed",
    ) => void;
    simulateTick: () => void;
}

export const useOwnerStore = create<OwnerStoreState>((set, get) => ({
    // Apply mapStatus at seed point so engine 3D and all UI receive normalized s.s
    spots: createInitialSpots().map((sp) => ({ ...sp, s: mapStatus(sp.s) })),
    selectedId: null,
    activeFloor: "all",
    viewMode: "both",
    lots: INITIAL_LOTS,
    currentLotId: 0,
    reservations: INITIAL_RESERVATIONS,
    activities: INITIAL_ACTIVITIES,
    reportFilter: { mode: "day", date: "2025-10-20" },
    revenuePeriod: "week",
    reservationSearch: { q: "", s: "" },
    editingLot: false,
    detailTab: "info",

    selectSpot: (id) => set({ selectedId: id }),

    updateSpotStatus: (id, s) =>
        set((state) => ({
            spots: state.spots.map((spot) =>
                spot.id === id ? { ...spot, s } : spot,
            ),
        })),

    setActiveFloor: (f) => set({ activeFloor: f }),

    setViewMode: (m) => set({ viewMode: m }),

    setCurrentLotId: (id) => set({ currentLotId: id }),

    setRevenuePeriod: (p) => set({ revenuePeriod: p }),

    setReportFilter: (filter) =>
        set((state) => ({
            reportFilter: { ...state.reportFilter, ...filter },
        })),

    setReservationSearch: (search) =>
        set((state) => ({
            reservationSearch: { ...state.reservationSearch, ...search },
        })),

    setDetailTab: (t) => set({ detailTab: t }),

    setEditingLot: (editing) => set({ editingLot: editing }),

    updateLot: (id, data) =>
        set((state) => ({
            lots: state.lots.map((l) => (l.id === id ? { ...l, ...data } : l)),
        })),

    addLot: (lot) =>
        set((state) => ({
            lots: [...state.lots, lot],
        })),

    deleteZone: (lotId, zoneIdx) =>
        set((state) => ({
            lots: state.lots.map((l) => {
                if (l.id !== lotId) return l;
                const newZones = [...l.zones];
                newZones.splice(zoneIdx, 1);
                return { ...l, zones: newZones };
            }),
        })),

    addZone: (lotId, zone) =>
        set((state) => ({
            lots: state.lots.map((l) => {
                if (l.id !== lotId) return l;
                return { ...l, zones: [...l.zones, zone] };
            }),
        })),

    updateZoneStatus: (lotId, zoneIdx, s) =>
        set((state) => ({
            lots: state.lots.map((l) => {
                if (l.id !== lotId) return l;
                const newZones = [...l.zones];
                newZones[zoneIdx] = { ...newZones[zoneIdx], s };
                return { ...l, zones: newZones };
            }),
        })),

    simulateTick: () => {
        const { spots } = get();
        const newSpots = [...spots];
        for (let k = 0; k < 4; k++) {
            const idx = Math.floor(Math.random() * newSpots.length);
            const sp = newSpots[idx];
            if (["available", "reserved", "occupied"].includes(sp.s)) {
                const raw = (["available", "reserved", "occupied"] as const)[
                    Math.floor(Math.random() * 3)
                ];
                // Apply mapStatus to keep normalization consistent during simulation
                newSpots[idx] = { ...sp, s: mapStatus(raw) };
            }
        }
        set({ spots: newSpots });
    },
}));

// Helper selector functions
export function getSpotCounts(spots: ParkingSpot[]) {
    const c: Record<SlotStatusKey, number> = {
        available: 0,
        reserved: 0,
        occupied: 0,
        maintenance: 0,
        verify: 0,
    };
    for (const s of spots) {
        if (c[s.s] !== undefined) c[s.s]++;
    }
    return c;
}

export function getVehicleCounts(spots: ParkingSpot[]) {
    const k = { Sedan: 0, SUV: 0, EV: 0 };
    for (const s of spots) {
        if (s.s === "occupied") {
            const kind = KIND[s.i % 9] as "Sedan" | "SUV" | "EV";
            if (k[kind] !== undefined) k[kind]++;
        }
    }
    return k;
}

export function getFloorCounts(spots: ParkingSpot[]) {
    return FLOORS.map((f) => {
        const count = spots.filter(
            (s) => s.f === f && (s.s === "occupied" || s.s === "reserved"),
        ).length;
        return { floor: f, count, free: 100 - count };
    });
}
