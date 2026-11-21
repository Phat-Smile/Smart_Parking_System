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

    it("ignores events for another lot even when hub forwards them", async () => {
        const lotId = "11111111-1111-1111-1111-111111111111";
        const onSlotStatusChanged = vi.fn();
        const onLotOccupancyChanged = vi.fn();
        const { result } = renderHook(() =>
            useParkingRealtime({
                lotId,
                onSlotStatusChanged,
                onLotOccupancyChanged,
            }),
        );
        await act(async () => {
            await Promise.resolve();
        });
        act(() => {
            mockOn["SlotStatusChanged"]?.({
                lotId: "22222222-2222-2222-2222-222222222222",
            });
            mockOn["LotOccupancyChanged"]?.({
                lotId: "22222222-2222-2222-2222-222222222222",
            });
        });
        expect(onSlotStatusChanged).not.toHaveBeenCalled();
        expect(onLotOccupancyChanged).not.toHaveBeenCalled();
        expect(result.current.lastSlotChange).toBeNull();
        expect(result.current.lastOccupancyChange).toBeNull();
    });

    it("clears prior lot event state when switching lots", async () => {
        const firstLotId = "11111111-1111-1111-1111-111111111111";
        const secondLotId = "22222222-2222-2222-2222-222222222222";
        const { result, rerender } = renderHook(
            ({ lotId }: { lotId: string }) => useParkingRealtime({ lotId }),
            { initialProps: { lotId: firstLotId } },
        );
        await act(async () => {
            await Promise.resolve();
        });
        act(() => {
            mockOn["LotOccupancyChanged"]?.({
                lotId: firstLotId,
                availableCarSlots: 3,
                availableMotorbikeSlots: 4,
                timestamp: "2026-11-20T03:00:00Z",
            });
        });
        expect(result.current.lastOccupancyChange).not.toBeNull();
        rerender({ lotId: secondLotId });
        expect(result.current.lastOccupancyChange).toBeNull();
        expect(result.current.lastSlotChange).toBeNull();
    });

    it("public mode sends no access token and never joins the operations group", async () => {
        const lotId = "11111111-1111-1111-1111-111111111111";
        renderHook(() => useParkingRealtime({ lotId }));

        await act(async () => {
            await Promise.resolve();
        });

        const options = vi.mocked(realtimeConn.createRealtimeConnection).mock
            .calls[0][0];
        expect(options.accessTokenFactory).toBeUndefined();
        expect(mockInvoke).not.toHaveBeenCalledWith(
            "JoinLotOperationsGroup",
            expect.anything(),
        );
    });

    it("operations mode sends the access token and joins the operations group", async () => {
        const lotId = "11111111-1111-1111-1111-111111111111";
        const { unmount } = renderHook(() =>
            useParkingRealtime({ lotId, operations: true }),
        );

        await act(async () => {
            await Promise.resolve();
        });

        const options = vi.mocked(realtimeConn.createRealtimeConnection).mock
            .calls[0][0];
        expect(options.hubPath).toBe("/hubs/parking");
        expect(typeof options.accessTokenFactory).toBe("function");
        expect(mockInvoke).toHaveBeenCalledWith("JoinLotGroup", lotId);
        expect(mockInvoke).toHaveBeenCalledWith(
            "JoinLotOperationsGroup",
            lotId,
        );

        unmount();
        expect(mockInvoke).toHaveBeenCalledWith(
            "LeaveLotOperationsGroup",
            lotId,
        );
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
