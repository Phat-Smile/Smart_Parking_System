/* eslint-disable @typescript-eslint/no-explicit-any */
"use client";

import React, { useEffect, useRef } from "react";
import { useParkingStore } from "../../store/useParkingStore";
import { mk3D, ParkingSlot } from "./parkingScene3D";
import { SlotStatus } from "../../domain/types";

// Convert domain status to prototype status
const mapStatus = (s: SlotStatus): ParkingSlot["s"] => {
    if (s === "available") return "available";
    if (s === "assigned") return "reserved";
    if (s === "occupied") return "occupied";
    if (s === "maintenance") return "maintenance";
    if (s === "needs_verification") return "verify";
    return "available"; // fallback for unavailable
};

export function ParkingScene() {
    const containerRef = useRef<HTMLDivElement>(null);
    const apiRef = useRef<any>(null);
    const { slots, activeFloor, selectedSlotId, selectSlot } =
        useParkingStore();

    const slotsRef = useRef(slots);
    useEffect(() => {
        slotsRef.current = slots;
    }, [slots]);

    useEffect(() => {
        const container = containerRef.current;
        if (!container) return;

        const mapSlot = (slot: any): ParkingSlot => ({
            id: slot.id,
            f: ("T" + slot.floorId.replace("f", "")) as ParkingSlot["f"],
            r: slot.row === 0 ? "A" : "B",
            n: slot.index,
            i:
                slot.index +
                (slot.row === 1 ? 10 : 0) +
                (parseInt(slot.floorId.replace("f", "")) - 1) * 20,
            s: mapStatus(slot.status),
            p: slot.vehicle?.plate || "",
        });

        const spots = Object.values(slotsRef.current).map(mapSlot);

        // Initialize mk3D ONLY ONCE
        apiRef.current = mk3D(container, spots, (id) => {
            selectSlot(id);
        });

        return () => {
            if (apiRef.current) {
                apiRef.current.dispose();
                apiRef.current = null;
            }
            if (container) {
                container.innerHTML = "";
            }
        };
    }, [selectSlot]); // Do NOT depend on slots to avoid recreating WebGL context!

    // Update spots smoothly without recreating WebGL
    useEffect(() => {
        if (apiRef.current) {
            const mapSlot = (slot: any): ParkingSlot => ({
                id: slot.id,
                f: ("T" + slot.floorId.replace("f", "")) as ParkingSlot["f"],
                r: slot.row === 0 ? "A" : "B",
                n: slot.index,
                i:
                    slot.index +
                    (slot.row === 1 ? 10 : 0) +
                    (parseInt(slot.floorId.replace("f", "")) - 1) * 20,
                s: mapStatus(slot.status),
                p: slot.vehicle?.plate || "",
            });
            apiRef.current.setSpots(Object.values(slots).map(mapSlot));
        }
    }, [slots]);

    // Sync floor
    useEffect(() => {
        if (apiRef.current) {
            apiRef.current.setFloor(activeFloor);
        }
    }, [activeFloor]);

    // Sync selection
    useEffect(() => {
        if (apiRef.current) {
            apiRef.current.setSelection(selectedSlotId);
        }
    }, [selectedSlotId]);

    return <div ref={containerRef} className="w-full h-full relative" />;
}
