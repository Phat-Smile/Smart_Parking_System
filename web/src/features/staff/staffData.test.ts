import { renderHook, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

const { LOT } = vi.hoisted(() => ({
    LOT: "10000000-0000-0000-0000-000000000002",
}));

vi.mock("./staffApi", () => ({
    getAssignedLot: vi.fn().mockResolvedValue({
        lotId: LOT,
        lotName: "Bãi B",
        address: "2 Đường Mẫu",
        availableMotorbikeSlots: 58,
        motorbikeCapacity: 60,
        availableCarSlots: null,
    }),
    listLotInsideSessions: vi.fn().mockResolvedValue({
        items: [
            {
                sessionId: "30000000-0000-0000-0000-000000000001",
                plateNumber: "59A-12345",
                vehicleType: "Motorbike",
                status: "Active",
                entryTime: "2026-10-30T01:00:00Z",
                slotCode: null,
            },
            {
                sessionId: "30000000-0000-0000-0000-000000000002",
                plateNumber: "59B-67890",
                vehicleType: "Motorbike",
                status: "Paid",
                entryTime: "2026-10-30T01:05:00Z",
                slotCode: null,
            },
        ],
        totalCount: 2,
        page: 1,
        pageSize: 100,
    }),
}));

import { useStaffData } from "./staffData";

// Demo 4 (D04.2, D04.6): the staff dashboard counts what Parking returns; no sample sessions or issues.
describe("useStaffData", () => {
    it("loads the assigned lot, its free spaces and its active sessions from Parking", async () => {
        const { result } = renderHook(() => useStaffData());

        await waitFor(() =>
            expect(result.current.assignedLot?.lotId).toBe(LOT),
        );
        // Paid sessions are paid but the vehicle has not left, so they still count as inside.
        expect(result.current.activeSessions).toHaveLength(2);
        expect(result.current.activeSessions[0].plateNumber).toBe("59A-12345");
        expect(result.current.activeSessions[1].status).toBe("Paid");
        expect(result.current.issues).toEqual([]);
        expect(result.current.occupancy).toMatchObject({
            motorbikeAvailable: 58,
            totalCapacity: 60,
            carAvailable: null,
            activeSessionsCount: 2,
        });
    });
});
