"use client";

import { useEffect, useRef, useState } from "react";
import type { HubConnection } from "@microsoft/signalr";
import { createRealtimeConnection } from "@/shared/realtime/connection";

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
}

export function useParkingRealtime({
    lotId,
    onSlotStatusChanged,
    onLotOccupancyChanged,
}: UseParkingRealtimeOptions) {
    const [isConnected, setIsConnected] = useState(false);
    const [lastSlotChange, setLastSlotChange] = useState<SlotStatusChangedEvent | null>(null);
    const [lastOccupancyChange, setLastOccupancyChange] = useState<LotOccupancyChangedEvent | null>(null);

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
            connection = createRealtimeConnection({
                hubPath: "/hubs/parking",
            });
        } catch (err) {
            console.warn("Failed to create SignalR connection for parking hub", err);
            return;
        }

        connection.on("SlotStatusChanged", (event: SlotStatusChangedEvent) => {
            if (!isMounted) return;
            setLastSlotChange(event);
            slotCbRef.current?.(event);
        });

        connection.on("LotOccupancyChanged", (event: LotOccupancyChangedEvent) => {
            if (!isMounted) return;
            setLastOccupancyChange(event);
            occCbRef.current?.(event);
        });

        async function start() {
            try {
                await connection.start();
                if (!isMounted) return;
                setIsConnected(true);
                await connection.invoke("JoinLotGroup", lotId);
            } catch (err) {
                // SignalR hub might be offline in mock/dev mode; degrade gracefully
                console.warn("Could not connect to SignalR parking hub", err);
            }
        }

        start();

        return () => {
            isMounted = false;
            setIsConnected(false);
            connection.invoke("LeaveLotGroup", lotId).catch(() => {});
            connection.stop().catch(() => {});
        };
    }, [lotId]);

    return {
        isConnected,
        lastSlotChange,
        lastOccupancyChange,
    };
}
