"use client";

import { useEffect, useRef, useState } from "react";
import type { HubConnection } from "@microsoft/signalr";
import { createRealtimeConnection } from "@/shared/realtime/connection";
import { getAccessToken } from "@/shared/api/access-token";

export interface SlotStatusChangedEvent {
    lotId: string;
    slotId: string;
    slotCode: string;
    effectiveStatus: string;
    previousStatus: string;
    timestamp: string;
}

export interface LotOccupancyChangedEvent {
    lotId: string;
    availableCarSlots: number;
    availableMotorbikeSlots: number;
    timestamp: string;
}

export interface UseParkingRealtimeOptions {
    lotId?: string | null;
    onSlotStatusChanged?: (event: SlotStatusChangedEvent) => void;
    onLotOccupancyChanged?: (event: LotOccupancyChangedEvent) => void;
    /**
     * Staff, owner and admin screens: send the JWT and join the lot's operations
     * group, which carries per-slot status. Public pages only get lot totals.
     */
    operations?: boolean;
}

export function useParkingRealtime({
    lotId,
    onSlotStatusChanged,
    onLotOccupancyChanged,
    operations = false,
}: UseParkingRealtimeOptions) {
    const [isConnected, setIsConnected] = useState(false);
    const [lastSlotChange, setLastSlotChange] =
        useState<SlotStatusChangedEvent | null>(null);
    const [lastOccupancyChange, setLastOccupancyChange] =
        useState<LotOccupancyChangedEvent | null>(null);

    const slotCbRef = useRef(onSlotStatusChanged);
    const occCbRef = useRef(onLotOccupancyChanged);

    useEffect(() => {
        slotCbRef.current = onSlotStatusChanged;
        occCbRef.current = onLotOccupancyChanged;
    }, [onSlotStatusChanged, onLotOccupancyChanged]);

    useEffect(() => {
        if (!lotId) return;

        let isMounted = true;
        let connection: HubConnection;

        try {
            connection = createRealtimeConnection(
                operations
                    ? {
                          hubPath: "/hubs/parking",
                          accessTokenFactory: () => getAccessToken() ?? "",
                      }
                    : { hubPath: "/hubs/parking" },
            );
        } catch (err) {
            console.warn(
                "Failed to create SignalR connection for parking hub",
                err,
            );
            return;
        }

        connection.on("SlotStatusChanged", (event: SlotStatusChangedEvent) => {
            if (
                !isMounted ||
                typeof event?.lotId !== "string" ||
                event.lotId.toLowerCase() !== lotId.toLowerCase()
            )
                return;
            setLastSlotChange(event);
            slotCbRef.current?.(event);
        });

        connection.on(
            "LotOccupancyChanged",
            (event: LotOccupancyChangedEvent) => {
                if (
                    !isMounted ||
                    typeof event?.lotId !== "string" ||
                    event.lotId.toLowerCase() !== lotId.toLowerCase()
                )
                    return;
                setLastOccupancyChange(event);
                occCbRef.current?.(event);
            },
        );

        async function start() {
            try {
                await connection.start();
                if (!isMounted) return;
                setIsConnected(true);
                await connection.invoke("JoinLotGroup", lotId);
                if (operations) {
                    await connection.invoke("JoinLotOperationsGroup", lotId);
                }
            } catch (err) {
                // SignalR hub might be offline in mock/dev mode; degrade gracefully
                console.warn("Could not connect to SignalR parking hub", err);
            }
        }

        start();

        return () => {
            isMounted = false;
            setIsConnected(false);
            setLastSlotChange(null);
            setLastOccupancyChange(null);
            connection.invoke("LeaveLotGroup", lotId).catch(() => {});
            if (operations) {
                connection
                    .invoke("LeaveLotOperationsGroup", lotId)
                    .catch(() => {});
            }
            connection.stop().catch(() => {});
        };
    }, [lotId, operations]);

    return {
        isConnected,
        lastSlotChange,
        lastOccupancyChange,
    };
}
