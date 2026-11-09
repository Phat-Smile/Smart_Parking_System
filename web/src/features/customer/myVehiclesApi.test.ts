import { afterEach, describe, expect, it, vi } from "vitest";
import {
    getMyVehicles,
    addMyVehicle,
    deleteMyVehicle,
    setDefaultVehicle,
    type RegisteredVehicle,
} from "./myVehiclesApi";

afterEach(() => {
    vi.unstubAllGlobals();
});

describe("myVehiclesApi", () => {
    it("getMyVehicles fetches account vehicles with isDefault status", async () => {
        const mockVehicles: RegisteredVehicle[] = [
            {
                vehicleId: "v-01",
                licensePlate: "51A-123.45",
                vehicleType: "Car",
                nickName: "Xe chính",
                createdAtUtc: "2026-10-31T00:00:00Z",
                isDefault: true,
            },
            {
                vehicleId: "v-02",
                licensePlate: "51B-678.90",
                vehicleType: "Motorbike",
                nickName: "Xe máy",
                createdAtUtc: "2026-10-31T01:00:00Z",
                isDefault: false,
            },
        ];

        const fetchMock = vi.fn().mockResolvedValue(
            new Response(JSON.stringify({ vehicles: mockVehicles }), {
                status: 200,
                headers: { "Content-Type": "application/json" },
            }),
        );
        vi.stubGlobal("fetch", fetchMock);

        const result = await getMyVehicles();

        expect(fetchMock).toHaveBeenCalledWith(
            expect.objectContaining({
                pathname: "/api/identity/accounts/me/vehicles",
            }),
            expect.anything(),
        );
        expect(result.vehicles).toHaveLength(2);
        expect(result.vehicles[0].isDefault).toBe(true);
        expect(result.vehicles[1].isDefault).toBe(false);
    });

    it("addMyVehicle posts vehicle data and receives RegisteredVehicle with isDefault", async () => {
        const created: RegisteredVehicle = {
            vehicleId: "v-03",
            licensePlate: "51C-111.22",
            vehicleType: "Car",
            nickName: "Xe mới",
            createdAtUtc: "2026-10-31T02:00:00Z",
            isDefault: true,
        };

        const fetchMock = vi.fn().mockResolvedValue(
            new Response(JSON.stringify(created), {
                status: 201,
                headers: { "Content-Type": "application/json" },
            }),
        );
        vi.stubGlobal("fetch", fetchMock);

        const result = await addMyVehicle("51C-111.22", "Car", "Xe mới");

        expect(fetchMock).toHaveBeenCalledWith(
            expect.objectContaining({
                pathname: "/api/identity/accounts/me/vehicles",
            }),
            expect.objectContaining({
                method: "POST",
                body: JSON.stringify({
                    licensePlate: "51C-111.22",
                    vehicleType: "Car",
                    nickName: "Xe mới",
                }),
            }),
        );
        expect(result.isDefault).toBe(true);
    });

    it("setDefaultVehicle calls PATCH /api/identity/accounts/me/vehicles/{id}/default", async () => {
        const fetchMock = vi.fn().mockResolvedValue(
            new Response(null, {
                status: 204,
            }),
        );
        vi.stubGlobal("fetch", fetchMock);

        await setDefaultVehicle("v-02");

        expect(fetchMock).toHaveBeenCalledWith(
            expect.objectContaining({
                pathname: "/api/identity/accounts/me/vehicles/v-02/default",
            }),
            expect.objectContaining({
                method: "PATCH",
            }),
        );
    });

    it("deleteMyVehicle calls DELETE /api/identity/accounts/me/vehicles/{id}", async () => {
        const fetchMock = vi.fn().mockResolvedValue(
            new Response(
                JSON.stringify({
                    vehicleId: "v-01",
                    message: "Vehicle deleted",
                }),
                {
                    status: 200,
                    headers: { "Content-Type": "application/json" },
                },
            ),
        );
        vi.stubGlobal("fetch", fetchMock);

        const result = await deleteMyVehicle("v-01");

        expect(fetchMock).toHaveBeenCalledWith(
            expect.objectContaining({
                pathname: "/api/identity/accounts/me/vehicles/v-01",
            }),
            expect.objectContaining({
                method: "DELETE",
            }),
        );
        expect(result.vehicleId).toBe("v-01");
    });
});
