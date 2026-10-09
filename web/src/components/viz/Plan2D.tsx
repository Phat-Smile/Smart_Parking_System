"use client";

import React, { useMemo } from "react";
import { useParkingStore } from "../../store/useParkingStore";
import { SlotStatus } from "../../domain/types";

const STATUS_COLORS: Record<SlotStatus, string> = {
  available: "bg-[#6fcf2e]",
  assigned: "bg-[#2f80ed]",
  occupied: "bg-[#e5484d]",
  maintenance: "bg-[#f2a007]",
  unavailable: "bg-[#a8b0a8]",
  needs_verification: "bg-[#8a5cf6]",
};

export function Plan2D() {
  const { slots, selectedSlotId, selectSlot, hoverSlot, hoverSlotId } = useParkingStore();

  const slotsList = useMemo(() => Object.values(slots), [slots]);

  if (slotsList.length === 0) {
    return <div className="p-8 text-center text-owner-text-secondary">No slots available to display.</div>;
  }

  // Assuming simple grid for demonstration
  return (
    <div className="p-6 h-full flex flex-col">
      <div className="flex-1 overflow-auto bg-white border border-owner-border rounded-xl shadow-sm p-8">
        <div className="grid grid-cols-10 gap-2 min-w-max mx-auto">
          {slotsList.map((slot) => {
            const isSelected = selectedSlotId === slot.id;
            const isHovered = hoverSlotId === slot.id;
            
            return (
              <button
                key={slot.id}
                onClick={() => selectSlot(slot.id)}
                onMouseEnter={() => hoverSlot(slot.id)}
                onMouseLeave={() => hoverSlot(null)}
                className={`
                  relative w-12 h-20 rounded-md border-2 transition-all duration-200
                  ${STATUS_COLORS[slot.status]}
                  ${isSelected ? "border-owner-dark scale-110 shadow-lg z-10" : "border-transparent"}
                  ${isHovered && !isSelected ? "scale-105 shadow-md z-10 opacity-90" : ""}
                  opacity-80 hover:opacity-100
                `}
                aria-label={`Slot ${slot.code} is ${slot.status}`}
              >
                <div className="absolute inset-0 flex items-center justify-center text-xs font-bold text-white drop-shadow-md">
                  {slot.code}
                </div>
                {slot.vehicle && (
                  <div className="absolute bottom-1 w-full text-[10px] leading-tight text-white font-mono bg-black/30 px-1 rounded truncate">
                    {slot.vehicle.plate}
                  </div>
                )}
              </button>
            );
          })}
        </div>
      </div>
    </div>
  );
}
