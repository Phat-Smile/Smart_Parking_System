"use client";

import React, { useEffect } from "react";
import dynamic from "next/dynamic";
import { useParkingStore } from "../../store/useParkingStore";
import { useOwnerStore } from "../../store/useOwnerStore";
import { useParkingSlots } from "../../lib/api/owner";
import { useParkingRealtime } from "@/features/parking/useParkingRealtime";
import type { SlotStatus } from "../../domain/types";
import type { SlotStatusKey } from "../../types/owner";
import { Plan2D } from "./Plan2D";

const ParkingScene = dynamic(
    () => import("./ParkingScene").then((m) => m.ParkingScene),
    {
        ssr: false,
        loading: () => (
            <div className="absolute inset-0 flex items-center justify-center bg-owner-bg text-owner-text-secondary">
                Đang tải mô hình 3D...
            </div>
        ),
    },
);

export function ParkingViz() {
    const { viewMode, setViewMode, setSlots } = useParkingStore();
    const updateSlotStatus = useParkingStore((s) => s.updateSlotStatus);
    const updateOwnerSpotStatus = useOwnerStore((s) => s.updateSpotStatus);

    // Hardcoded lotId for now based on seed data
    const { data: slots, isLoading } = useParkingSlots("lot-1");

    useParkingRealtime({
        lotId: "lot-1",
        onSlotStatusChanged: (event) => {
            const lower = event.effectiveStatus.toLowerCase();
            const mapped: SlotStatus =
                lower === "occupied"
                    ? "occupied"
                    : lower === "maintenance"
                      ? "maintenance"
                      : lower === "unavailable"
                        ? "unavailable"
                        : lower === "assigned"
                          ? "assigned"
                          : lower === "needsverification"
                            ? "needs_verification"
                            : "available";

            updateSlotStatus(event.slotId, mapped);
            if (event.slotCode) {
                updateSlotStatus(event.slotCode, mapped);
            }

            const ownerStatusKey: SlotStatusKey =
                mapped === "needs_verification"
                    ? "verify"
                    : mapped === "unavailable"
                      ? "maintenance"
                      : mapped === "assigned"
                        ? "reserved"
                        : mapped;

            updateOwnerSpotStatus(event.slotCode, ownerStatusKey);
            updateOwnerSpotStatus(event.slotId, ownerStatusKey);
        },
    });

    useEffect(() => {
        if (slots) {
            setSlots(slots);
        }
    }, [slots, setSlots]);

    if (isLoading) {
        return (
            <div className="absolute inset-0 flex items-center justify-center text-owner-text-secondary bg-white rounded-xl">
                Đang tải dữ liệu bãi đỗ...
            </div>
        );
    }

    return (
        <div className="relative w-full h-full rounded-xl overflow-hidden border border-owner-border">
            {/* Top right controls */}
            <div className="absolute top-4 right-4 z-10 flex space-x-2 bg-white/90 backdrop-blur rounded-lg p-1 shadow-sm border border-owner-border">
                <button
                    className={`px-3 py-1 text-sm font-medium rounded-md transition-colors ${
                        viewMode === "2d"
                            ? "bg-owner-dark text-white"
                            : "text-owner-text-secondary hover:text-owner-text-primary hover:bg-gray-100"
                    }`}
                    onClick={() => setViewMode("2d")}
                >
                    2D
                </button>
                <button
                    className={`px-3 py-1 text-sm font-medium rounded-md transition-colors ${
                        viewMode === "3d"
                            ? "bg-owner-dark text-white"
                            : "text-owner-text-secondary hover:text-owner-text-primary hover:bg-gray-100"
                    }`}
                    onClick={() => setViewMode("3d")}
                >
                    3D
                </button>
            </div>

            {viewMode === "3d" ? <ParkingScene /> : <Plan2D />}
        </div>
    );
}
