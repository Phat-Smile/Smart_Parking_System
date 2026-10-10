namespace ParkingSystem.Parking.WebAPI.Security;

// Opening and cancelling sessions is for the gate or staff only, never for customers (MAIN FLOW MF-08/09A/09B).
// Token contract with Identity (owner BE-1): the role arrives as claim "role" = AccountRole name.
// Role chỉ là bước đầu; handler còn kiểm người gọi có được gắn với đúng bãi (TECH-12, IParkingLotAccess).
internal static class ParkingAuthorization
{
    public const string StaffPolicy = "parking-staff";
    public const string AdminPolicy = "parking-system-admin";
    public const string RoleClaimType = "role";
    public const string StaffRole = "Staff";
    public const string OwnerRole = "ParkingLotOwner";
    public const string AdminRole = "SystemAdmin";

    public static IServiceCollection AddParkingAuthorization(this IServiceCollection services)
    {
        // Checks the raw claim because the shared JWT setup keeps inbound claim names (MapInboundClaims = false).
        // Chủ bãi được vận hành cổng bãi của mình như nhân viên.
        services.AddAuthorizationBuilder()
            .AddPolicy(StaffPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(RoleClaimType, StaffRole, OwnerRole))
            .AddPolicy(AdminPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(RoleClaimType, AdminRole));
        return services;
    }
}
