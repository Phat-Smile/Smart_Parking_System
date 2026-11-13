import { create } from "zustand";
import { ParkingLot, ApprovalStatus } from "@/types/owner";
import { useOwnerStore } from "@/store/useOwnerStore";

// Overlay lots are loaded dynamically from Parking API
const DESIGN_LOTS: ParkingLot[] = [];

interface OwnerMapState {
    overlayLots: ParkingLot[];
    selectedLotId: number | null;
    filterStatus: "all" | "approved" | "pending" | "rejected";
    mapSearchQuery: string;

    setSelectedLotId: (id: number | null) => void;
    setFilterStatus: (s: "all" | "approved" | "pending" | "rejected") => void;
    setMapSearchQuery: (q: string) => void;
    addOverlayLot: (lot: ParkingLot) => void;
    updateLotStatus: (lotId: number, status: ApprovalStatus) => void;
}

export const useOwnerMapStore = create<OwnerMapState>((set) => ({
    overlayLots: DESIGN_LOTS,
    selectedLotId: null,
    filterStatus: "all",
    mapSearchQuery: "",

    setSelectedLotId: (id) => set({ selectedLotId: id }),
    setFilterStatus: (s) => set({ filterStatus: s }),
    setMapSearchQuery: (q) => set({ mapSearchQuery: q }),
    addOverlayLot: (lot) =>
        set((state) => ({ overlayLots: [lot, ...state.overlayLots] })),
    updateLotStatus: (lotId, status) =>
        set((state) => ({
            overlayLots: state.overlayLots.map((l) =>
                l.id === lotId ? { ...l, ap: status } : l,
            ),
        })),
}));

/**
 * Selector useAllLots():
 * Hợp nhất danh sách bãi từ useOwnerStore và useOwnerMapStore.
 * Mọi component hiển thị danh sách bãi đều dùng selector này.
 */
export function useAllLots(): ParkingLot[] {
    const storeLots = useOwnerStore((s) => s.lots);
    const overlayLots = useOwnerMapStore((s) => s.overlayLots);

    const map = new Map<number, ParkingLot>();
    for (const lot of overlayLots) {
        map.set(lot.id, lot);
    }
    for (const lot of storeLots) {
        if (!map.has(lot.id)) {
            map.set(lot.id, lot);
        }
    }
    return Array.from(map.values());
}
