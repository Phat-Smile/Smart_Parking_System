namespace ParkingSystem.Identity.WebAPI.Security;

// Identity issues the role claim itself ("role" = AccountRole name); the shared JWT setup keeps raw claim names.
internal static class IdentityAuthorization
{
    public const string AdminPolicy = "identity-system-admin";
    public const string RoleClaimType = "role";
    public const string AdminRole = "SystemAdmin";

    public static IServiceCollection AddIdentityAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(AdminPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(RoleClaimType, AdminRole));
        return services;
    }
}
