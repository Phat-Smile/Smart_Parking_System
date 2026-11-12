import { gatewayFetch, gatewayFetchBlob } from "@/shared/api/client";

export interface CustomerProfile {
    accountId: string;
    fullName: string;
    dateOfBirth: string | null;
    citizenIdNumber: string | null;
    driverLicenseNumber: string | null;
    avatarUrl: string | null;
    phoneNumber: string | null;
    address: string | null;
    identityDocumentType: "citizenId" | "driverLicense" | null;
    identityIssueDate: string | null;
    identityIssuedBy: string | null;
    identityFrontMediaId: string | null;
    identityBackMediaId: string | null;
}

export type SaveCustomerProfile = Pick<
    CustomerProfile,
    | "fullName"
    | "dateOfBirth"
    | "citizenIdNumber"
    | "driverLicenseNumber"
    | "avatarUrl"
    | "phoneNumber"
    | "address"
    | "identityDocumentType"
    | "identityIssueDate"
    | "identityIssuedBy"
    | "identityFrontMediaId"
    | "identityBackMediaId"
>;

export interface CustomerMedia {
    id: string;
    kind: string;
    url: string;
}

export function getMyProfile() {
    return gatewayFetch<CustomerProfile>("/api/identity/accounts/me/profile");
}

export function saveMyProfile(profile: SaveCustomerProfile) {
    return gatewayFetch<CustomerProfile>("/api/identity/accounts/me/profile", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(profile),
    });
}

export function uploadCustomerMedia(
    kind: "avatar" | "identity-front" | "identity-back" | "vehicle-photo",
    file: File,
) {
    const form = new FormData();
    form.set("kind", kind);
    form.set("file", file);
    return gatewayFetch<CustomerMedia>("/api/identity/accounts/me/media", {
        method: "POST",
        body: form,
    });
}

export function getCustomerMediaBlob(url: string) {
    return gatewayFetchBlob(url);
}

export function deleteCustomerMedia(mediaId: string) {
    return gatewayFetch<void>(
        `/api/identity/accounts/me/media/${encodeURIComponent(mediaId)}`,
        {
            method: "DELETE",
        },
    );
}

export function deleteMyProfileIdentity() {
    return gatewayFetch<void>("/api/identity/accounts/me/profile/identity", {
        method: "DELETE",
    });
}
