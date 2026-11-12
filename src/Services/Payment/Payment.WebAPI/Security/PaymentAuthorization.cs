namespace ParkingSystem.Payment.WebAPI.Security;

internal static class PaymentAuthorization
{
    public const string AdminPolicy = "payment-system-admin";
    public const string ViewSessionFeePolicy = "payment-view-session-fee";
    public const string PaySessionFeePolicy = "payment-pay-session-fee";
    public const string PayVnPayPolicy = "payment-pay-vnpay";
    public const string ViewFeePaymentPolicy = "payment-view-fee-payment";
    public const string CustomerPolicy = "payment-customer-only";
    public const string ConfirmCashPaymentPolicy = "payment-confirm-cash";
    public const string RoleClaimType = "role";
    public const string AdminRole = "SystemAdmin";

    public static IServiceCollection AddPaymentAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(AdminPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(RoleClaimType, AdminRole))
            .AddPolicy(ViewSessionFeePolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(RoleClaimType, "Customer", "Staff", "ParkingLotOwner", "LotOwner"))
            .AddPolicy(PaySessionFeePolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(RoleClaimType, "Customer"))
            .AddPolicy(PayVnPayPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(RoleClaimType, "Customer", "Staff", "ParkingLotOwner", "LotOwner"))
            .AddPolicy(ViewFeePaymentPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(RoleClaimType, "Customer", "Staff", "ParkingLotOwner", "LotOwner"))
            .AddPolicy(CustomerPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(RoleClaimType, "Customer"))
            .AddPolicy(ConfirmCashPaymentPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(RoleClaimType, "Staff", "ParkingLotOwner", "LotOwner"));

        return services;
    }
}
