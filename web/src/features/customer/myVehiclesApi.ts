import { gatewayFetch } from "@/shared/api/client";

export type RegisteredVehicle = {
    vehicleId: string;
    licensePlate: string;
    vehicleType: "Car" | "Motorbike" | "Bicycle";
    nickName: string | null;
    createdAtUtc: string;
    isDefault: boolean;
    details?: VehicleDetails | null;
};

type ApiRegisteredVehicle = Omit<RegisteredVehicle, "licensePlate"> & {
    licensePlate: string | null;
};

function normalizeRegisteredVehicle(
    vehicle: ApiRegisteredVehicle,
): RegisteredVehicle {
    return { ...vehicle, licensePlate: vehicle.licensePlate ?? "" };
}

export type VehicleDetails = {
    brand: string | null;
    model: string | null;
    motorbikeKind: string | null;
    seats: number | null;
    fuelType: string | null;
    color: string | null;
    manufacturingYear: number | null;
    size: string | null;
    photoUrl: string | null;
};

export function getMyVehicle(vehicleId: string) {
    return gatewayFetch<ApiRegisteredVehicle>(
        `/api/identity/accounts/me/vehicles/${encodeURIComponent(vehicleId)}`,
    ).then(normalizeRegisteredVehicle);
}

export function updateMyVehicle(
    vehicleId: string,
    nickName: string | null,
    details: VehicleDetails,
) {
    return gatewayFetch<Omit<ApiRegisteredVehicle, "createdAtUtc">>(
        `/api/identity/accounts/me/vehicles/${encodeURIComponent(vehicleId)}`,
        {
            method: "PUT",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ nickName, details }),
        },
    ).then((vehicle) => ({
        ...vehicle,
        licensePlate: vehicle.licensePlate ?? "",
    }));
}

export function getMyVehicles(signal?: AbortSignal) {
    return gatewayFetch<{ vehicles: ApiRegisteredVehicle[] }>(
        "/api/identity/accounts/me/vehicles",
        { signal },
    ).then((result) => ({
        vehicles: result.vehicles.map(normalizeRegisteredVehicle),
    }));
}

export function addMyVehicle(
    licensePlate: string | null,
    vehicleType: "Car" | "Motorbike" | "Bicycle",
    nickName: string | null,
    details?: VehicleDetails,
) {
    return gatewayFetch<ApiRegisteredVehicle>(
        "/api/identity/accounts/me/vehicles",
        {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                licensePlate,
                vehicleType,
                nickName,
                details,
            }),
        },
    ).then(normalizeRegisteredVehicle);
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
