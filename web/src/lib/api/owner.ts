import { useQuery } from "@tanstack/react-query";
import { ParkingLot, Slot } from "../../domain/types";

// Base API fetcher
async function fetchApi<T>(
    endpoint: string,
    options?: RequestInit,
): Promise<T> {
    const res = await fetch(`/api/owner${endpoint}`, {
        ...options,
        headers: {
            "Content-Type": "application/json",
            ...options?.headers,
        },
    });
    if (!res.ok) {
        throw new Error(`API error: ${res.statusText}`);
    }
    return res.json();
}

// Hooks
export const ownerKeys = {
    all: ["owner"] as const,
    lots: () => [...ownerKeys.all, "lots"] as const,
    lot: (id: string) => [...ownerKeys.lots(), id] as const,
    slots: (lotId: string) => [...ownerKeys.lot(lotId), "slots"] as const,
};

export function useParkingLots() {
    return useQuery({
        queryKey: ownerKeys.lots(),
        queryFn: () => fetchApi<ParkingLot[]>("/lots"),
    });
}

export function useParkingLot(id: string) {
    return useQuery({
        queryKey: ownerKeys.lot(id),
        queryFn: () => fetchApi<ParkingLot>(`/lots/${id}`),
        enabled: !!id,
    });
}

export function useParkingSlots(lotId: string) {
    return useQuery({
        queryKey: ownerKeys.slots(lotId),
        queryFn: () => fetchApi<Slot[]>(`/lots/${lotId}/slots`),
        enabled: !!lotId,
    });
}
