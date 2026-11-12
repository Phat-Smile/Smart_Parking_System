import { act, renderHook, waitFor } from "@testing-library/react";
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

import { getAssignedLot, listLotInsideSessions } from "./staffApi";
import type { AssignedLotResponse } from "./types";
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

    it("preserves selected lot across subsequent fetchAssignedLot() calls without arguments", async () => {
        const mockedGetAssignedLot = vi.mocked(getAssignedLot);
        mockedGetAssignedLot.mockImplementation(async (preferredLotId) => ({
            lotId: preferredLotId ?? LOT,
            lotName: preferredLotId === "lot-2" ? "Bãi 2" : "Bãi B",
            address: "2 Đường Mẫu",
            availableMotorbikeSlots: 58,
            motorbikeCapacity: 60,
            availableCarSlots: 5,
        }));

        const { result } = renderHook(() => useStaffData());
        await waitFor(() => expect(result.current.assignedLot).not.toBeNull());

        await act(async () => {
            await result.current.fetchAssignedLot("lot-2");
        });
        expect(result.current.assignedLot?.lotId).toBe("lot-2");

        await act(async () => {
            await result.current.fetchAssignedLot();
        });
        expect(mockedGetAssignedLot).toHaveBeenLastCalledWith("lot-2");
        expect(result.current.assignedLot?.lotId).toBe("lot-2");
    });

    it("ignores stale out-of-order fetchAssignedLot responses when switching lots quickly", async () => {
        let resolveLot1!: (value: AssignedLotResponse) => void;
        let resolveLot2!: (value: AssignedLotResponse) => void;
        let lot1Pending = true;
        let lot2Pending = true;

        const mockedGetAssignedLot = vi.mocked(getAssignedLot);
        const mockedListSessions = vi.mocked(listLotInsideSessions);

        mockedGetAssignedLot.mockImplementation((preferredLotId) => {
            if (preferredLotId === "lot-1" && lot1Pending) {
                lot1Pending = false;
                return new Promise<AssignedLotResponse>((resolve) => {
                    resolveLot1 = resolve;
                });
            }
            if (preferredLotId === "lot-2" && lot2Pending) {
                lot2Pending = false;
                return new Promise<AssignedLotResponse>((resolve) => {
                    resolveLot2 = resolve;
                });
            }
            return Promise.resolve({
                lotId: preferredLotId ?? LOT,
                lotName: preferredLotId === "lot-2" ? "Bãi 2" : "Bãi B",
                address: "2 Đường Mẫu",
                availableMotorbikeSlots: 58,
                motorbikeCapacity: 60,
                availableCarSlots: 5,
            });
        });

        mockedListSessions.mockImplementation(async (lotId) => ({
            items: [
                {
                    sessionId: `ses-${lotId}`,
                    plateNumber: lotId === "lot-2" ? "51B-22222" : "51A-11111",
                    vehicleType: "Car",
                    status: "Active",
                    entryTime: "2026-10-30T01:00:00Z",
                    slotCode: null,
                },
            ],
            totalCount: 1,
            page: 1,
            pageSize: 100,
        }));

        const { result } = renderHook(() => useStaffData());
        await waitFor(() => expect(result.current.assignedLot).not.toBeNull());

        let p1!: Promise<AssignedLotResponse | null>;
        let p2!: Promise<AssignedLotResponse | null>;
        await act(async () => {
            p1 = result.current.fetchAssignedLot("lot-1");
            p2 = result.current.fetchAssignedLot("lot-2");
            await Promise.resolve();
        });

        await act(async () => {
            resolveLot2({
                lotId: "lot-2",
                lotName: "Bãi 2",
                address: "2 Đường 2",
                availableMotorbikeSlots: 10,
                motorbikeCapacity: 20,
                availableCarSlots: 8,
            });
            await p2;
        });

        expect(result.current.assignedLot?.lotId).toBe("lot-2");
        expect(result.current.activeSessions[0]?.plateNumber).toBe("51B-22222");

        await act(async () => {
            resolveLot1({
                lotId: "lot-1",
                lotName: "Bãi 1",
                address: "1 Đường 1",
                availableMotorbikeSlots: 5,
                motorbikeCapacity: 10,
                availableCarSlots: 1,
            });
            await p1;
        });

        expect(result.current.assignedLot?.lotId).toBe("lot-2");
        expect(result.current.activeSessions[0]?.plateNumber).toBe("51B-22222");

        await act(async () => {
            await result.current.fetchAssignedLot();
        });
        expect(mockedGetAssignedLot).toHaveBeenLastCalledWith("lot-2");
    });
});
