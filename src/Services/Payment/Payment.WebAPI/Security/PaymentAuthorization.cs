namespace ParkingSystem.Payment.WebAPI.Security;

internal static class PaymentAuthorization
{
    public const string AdminPolicy = "payment-system-admin";
    public const string RoleClaimType = "role";
    public const string AdminRole = "SystemAdmin";

    public static IServiceCollection AddPaymentAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(AdminPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(RoleClaimType, AdminRole));

        return services;
    }
}
