namespace ParkingSystem.Parking.WebAPI.Security;

// Opening and cancelling sessions is for the gate or staff only, never for customers (MAIN FLOW MF-08/09A/09B).
// Token contract with Identity (owner BE-1): the staff role arrives as claim "role" = "Staff".
// Limiting staff to their assigned lot is an Open Decision and is not checked yet.
internal static class ParkingAuthorization
{
    public const string StaffPolicy = "parking-staff";
    public const string RoleClaimType = "role";
    public const string StaffRole = "Staff";

    public static IServiceCollection AddParkingAuthorization(this IServiceCollection services)
    {
        // Checks the raw claim because the shared JWT setup keeps inbound claim names (MapInboundClaims = false).
        services.AddAuthorizationBuilder()
            .AddPolicy(StaffPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(RoleClaimType, StaffRole));
        return services;
    }
}
