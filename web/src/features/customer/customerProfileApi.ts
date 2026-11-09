import { gatewayFetch } from "@/shared/api/client";

export interface CustomerProfile {
    accountId: string;
    fullName: string;
    dateOfBirth: string | null;
    avatarUrl: string | null;
    phoneNumber: string | null;
    address: string | null;
    createdAtUtc: string;
    updatedAtUtc: string | null;
}

export interface UpdateCustomerProfileRequest {
    fullName: string;
    phoneNumber?: string | null;
    address?: string | null;
    dateOfBirth?: string | null;
    avatarUrl?: string | null;
}

/** Endpoint: GET /api/identity/accounts/me/profile */
export async function getMyProfile(
    signal?: AbortSignal,
): Promise<CustomerProfile> {
    return await gatewayFetch<CustomerProfile>(
        "/api/identity/accounts/me/profile",
        { signal },
    );
}

/** Endpoint: PUT /api/identity/accounts/me/profile */
export async function updateMyProfile(
    data: UpdateCustomerProfileRequest,
): Promise<CustomerProfile> {
    return await gatewayFetch<CustomerProfile>(
        "/api/identity/accounts/me/profile",
        {
            method: "PUT",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(data),
        },
    );
}
