namespace ParkingSystem.Parking.WebAPI.Security;

// Parking authorization policies.
internal static class ParkingAuthorization
{
    public const string StaffPolicy = "parking-staff";
    public const string ParkingLotOwnerPolicy = "parking-lot-owner";
    public const string AdminPolicy = "parking-system-admin";
    public const string SystemAdminPolicy = "parking-admin";
    public const string OwnerPolicy = ParkingLotOwnerPolicy;
    public const string RoleClaimType = "role";

    public const string StaffRole = "Staff";
    public const string OwnerRole = "ParkingLotOwner";
    public const string ParkingLotOwnerRole = OwnerRole;
    public const string AdminRole = "SystemAdmin";
    public const string SystemAdminRole = AdminRole;

    public static IServiceCollection AddParkingAuthorization(
        this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(StaffPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(RoleClaimType, StaffRole, OwnerRole))
            .AddPolicy(ParkingLotOwnerPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(RoleClaimType, OwnerRole))
            .AddPolicy(AdminPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(RoleClaimType, AdminRole))
            .AddPolicy(SystemAdminPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(RoleClaimType, SystemAdminRole));
        return services;
    }
}