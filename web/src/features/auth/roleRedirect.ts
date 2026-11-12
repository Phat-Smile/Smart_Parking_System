export type UserRole = "Customer" | "Staff" | "ParkingLotOwner" | "SystemAdmin";

/**
 * Returns the default dashboard destination path based on account role.
 */
export function getRedirectPathForRole(role?: string): string {
    switch (role) {
        case "SystemAdmin":
            return "/admin/dashboard";
        case "Staff":
            return "/staff/dashboard";
        case "ParkingLotOwner":
            return "/owner";
        case "Customer":
        default:
            return "/customer/dashboard";
    }
}

/**
 * Resolves the destination URL after successful authentication,
 * prioritizing explicit valid relative redirect query parameter if provided.
 */
export function resolvePostLoginRedirect(
    account: { role?: string },
    explicitRedirect?: string | null,
): string {
    if (
        explicitRedirect &&
        explicitRedirect.startsWith("/") &&
        !explicitRedirect.startsWith("//")
    ) {
        return explicitRedirect;
    }
    return getRedirectPathForRole(account.role);
}
