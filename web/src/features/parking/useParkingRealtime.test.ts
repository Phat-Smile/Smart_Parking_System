import { renderHook, act } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { useParkingRealtime } from "./useParkingRealtime";
import * as realtimeConn from "@/shared/realtime/connection";

describe("useParkingRealtime", () => {
    let mockOn: Record<string, (data: unknown) => void>;
    let mockStart: ReturnType<typeof vi.fn>;
    let mockStop: ReturnType<typeof vi.fn>;
    let mockInvoke: ReturnType<typeof vi.fn>;

    beforeEach(() => {
        mockOn = {};
        mockStart = vi.fn().mockResolvedValue(undefined);
        mockStop = vi.fn().mockResolvedValue(undefined);
        mockInvoke = vi.fn().mockResolvedValue(undefined);

        vi.spyOn(realtimeConn, "createRealtimeConnection").mockReturnValue({
            start: mockStart,
            stop: mockStop,
            invoke: mockInvoke,
            on: vi.fn((event: string, cb: (data: unknown) => void) => {
                mockOn[event] = cb;
            }),
        } as unknown as ReturnType<
            typeof realtimeConn.createRealtimeConnection
        >);
    });

    it("connects and joins lot group when lotId is provided", async () => {
        const lotId = "11111111-1111-1111-1111-111111111111";
        const { result } = renderHook(() => useParkingRealtime({ lotId }));

        await act(async () => {
            await Promise.resolve();
        });

        expect(realtimeConn.createRealtimeConnection).toHaveBeenCalledWith({
            hubPath: "/hubs/parking",
        });
        expect(mockStart).toHaveBeenCalled();
        expect(mockInvoke).toHaveBeenCalledWith("JoinLotGroup", lotId);
        expect(result.current.isConnected).toBe(true);
    });

    it("handles SlotStatusChanged event and fires callback", async () => {
        const lotId = "11111111-1111-1111-1111-111111111111";
        const onSlotStatusChanged = vi.fn();
        const { result } = renderHook(() =>
            useParkingRealtime({ lotId, onSlotStatusChanged }),
        );

        await act(async () => {
            await Promise.resolve();
        });

        const eventData = {
            lotId,
            slotId: "22222222-2222-2222-2222-222222222222",
            slotCode: "A-01",
            effectiveStatus: "Occupied",
            previousStatus: "Available",
            timestamp: new Date().toISOString(),
        };

        act(() => {
            mockOn["SlotStatusChanged"]?.(eventData);
        });

        expect(result.current.lastSlotChange).toEqual(eventData);
        expect(onSlotStatusChanged).toHaveBeenCalledWith(eventData);
    });

    it("handles LotOccupancyChanged event and fires callback", async () => {
        const lotId = "11111111-1111-1111-1111-111111111111";
        const onLotOccupancyChanged = vi.fn();
        const { result } = renderHook(() =>
            useParkingRealtime({ lotId, onLotOccupancyChanged }),
        );

        await act(async () => {
            await Promise.resolve();
        });

        const eventData = {
            lotId,
            availableCarSlots: 5,
            availableMotorbikeSlots: 20,
            timestamp: new Date().toISOString(),
        };

        act(() => {
            mockOn["LotOccupancyChanged"]?.(eventData);
        });

        expect(result.current.lastOccupancyChange).toEqual(eventData);
        expect(onLotOccupancyChanged).toHaveBeenCalledWith(eventData);
    });

    it("leaves group and stops connection on unmount", async () => {
        const lotId = "11111111-1111-1111-1111-111111111111";
        const { unmount } = renderHook(() => useParkingRealtime({ lotId }));

        await act(async () => {
            await Promise.resolve();
        });

        unmount();

        expect(mockInvoke).toHaveBeenCalledWith("LeaveLotGroup", lotId);
        expect(mockStop).toHaveBeenCalled();
    });
});
