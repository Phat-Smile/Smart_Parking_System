import { create } from "zustand";
import { Slot, SlotStatus, Vehicle } from "../domain/types";

interface ParkingState {
  slots: Record<string, Slot>;
  selectedSlotId: string | null;
  hoverSlotId: string | null;
  activeFloor: string | "all";
  exploded: boolean;
  viewMode: "2d" | "3d";
  
  // Actions
  setSlots: (slots: Slot[]) => void;
  updateSlotStatus: (slotId: string, status: SlotStatus, vehicle?: Vehicle) => void;
  selectSlot: (slotId: string | null) => void;
  hoverSlot: (slotId: string | null) => void;
  setActiveFloor: (floorId: string | "all") => void;
  setExploded: (exploded: boolean) => void;
  setViewMode: (mode: "2d" | "3d") => void;
}

export const useParkingStore = create<ParkingState>((set) => ({
  slots: {},
  selectedSlotId: null,
  hoverSlotId: null,
  activeFloor: "all",
  exploded: false,
  viewMode: "3d",

  setSlots: (slots) =>
    set(() => {
      const slotsRecord: Record<string, Slot> = {};
      slots.forEach((s) => {
        slotsRecord[s.id] = s;
      });
      return { slots: slotsRecord };
    }),

  updateSlotStatus: (slotId, status, vehicle) =>
    set((state) => {
      const slot = state.slots[slotId];
      if (!slot) return state;
      return {
        slots: {
          ...state.slots,
          [slotId]: { ...slot, status, vehicle: vehicle ?? slot.vehicle },
        },
      };
    }),

  selectSlot: (slotId) => set({ selectedSlotId: slotId }),
  hoverSlot: (slotId) => set({ hoverSlotId: slotId }),
  setActiveFloor: (floorId) => set({ activeFloor: floorId }),
  setExploded: (exploded) => set({ exploded }),
  setViewMode: (viewMode) => set({ viewMode }),
}));
