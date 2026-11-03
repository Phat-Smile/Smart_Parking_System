import { gatewayFetch } from "@/shared/api/client";

export type RegisteredVehicle = {
    vehicleId: string;
    licensePlate: string;
    vehicleType: "Car" | "Motorbike";
    nickName: string | null;
    createdAtUtc: string;
    isDefault: boolean;
};

export function getMyVehicles(signal?: AbortSignal) {
    return gatewayFetch<{ vehicles: RegisteredVehicle[] }>(
        "/api/identity/accounts/me/vehicles",
        { signal },
    );
}

export function addMyVehicle(
    licensePlate: string,
    vehicleType: "Car" | "Motorbike",
    nickName: string | null,
) {
    return gatewayFetch<RegisteredVehicle>(
        "/api/identity/accounts/me/vehicles",
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ licensePlate, vehicleType, nickName }),
        },
    );
}

export function deleteMyVehicle(vehicleId: string) {
    return gatewayFetch<{ vehicleId: string; message: string }>(
        `/api/identity/accounts/me/vehicles/${encodeURIComponent(vehicleId)}`,
        { method: "DELETE" },
    );
}

export function setDefaultVehicle(vehicleId: string) {
    return gatewayFetch<void>(
        `/api/identity/accounts/me/vehicles/${encodeURIComponent(vehicleId)}/default`,
        { method: "PATCH" },
    );
}
