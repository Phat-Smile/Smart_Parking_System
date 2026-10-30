import {
    getOwnerParkingLotsApi,
    type OwnerParkingLotItem,
} from "@/features/owner/api/ownerApi";
import { useOwnerStore } from "@/store/useOwnerStore";
import type { ApprovalStatus, ParkingLot } from "@/types/owner";
import { useOwnerMapStore } from "./useOwnerMapStore";

/*
 * The owner portal screens key lots by a number; Parking keys them by GUID. Each real lot keeps its
 * GUID in `backendId`, and every call to Parking uses it. Design lots have no GUID, so nothing is
 * saved for them.
 */

/** Stable screen number for a Parking lot GUID. */
export function localLotId(backendId: string): number {
    let hash = 0;
    for (const char of backendId)
        hash = (hash << 5) - hash + char.charCodeAt(0);
    return (Math.abs(hash) % 1_000_000) + 1000;
}

export function backendLotId(
    lot: Pick<ParkingLot, "backendId"> | { id: number } | undefined,
): string | null {
    return lot && "backendId" in lot && lot.backendId ? lot.backendId : null;
}

const STATUS: Record<OwnerParkingLotItem["approvalStatus"], ApprovalStatus> = {
    Pending: "pending",
    Approved: "approved",
    Rejected: "rejected",
};

// Parking's owner list has no prices, capacity or hours: those fields stay empty (0) until the owner
// opens the screen that reads them, instead of showing design values.
export function toOwnerLot(item: OwnerParkingLotItem): ParkingLot {
    return {
        id: localLotId(item.id),
        backendId: item.id,
        name: item.name,
        addr: item.address,
        gps: `${item.latitude}, ${item.longitude}`,
        op: "",
        cl2: "",
        vc: false,
        vm: false,
        cc: 0,
        cm: 0,
        fls: 0,
        pc: 0,
        pm: 0,
        dep: 0,
        res: "off",
        hold: 0,
        maxr: 0,
        fc: 0,
        ap: STATUS[item.approvalStatus] ?? "pending",
        why: item.rejectionReason ?? undefined,
        zones: [],
    };
}

/** Loads the signed-in owner's lots (up to 100) and drops the design lots. */
export async function loadOwnerLots(): Promise<ParkingLot[]> {
    try {
        const page = await getOwnerParkingLotsApi({ page: 1, pageSize: 100 });
        const lots = page.items.map(toOwnerLot);
        useOwnerStore.setState({ lots });
        useOwnerMapStore.setState({
            overlayLots: [],
            selectedLotId: lots[0]?.id ?? null,
        });
        return lots;
    } catch (error) {
        useOwnerStore.setState({ lots: [] });
        useOwnerMapStore.setState({ overlayLots: [], selectedLotId: null });
        throw error;
    }
}
