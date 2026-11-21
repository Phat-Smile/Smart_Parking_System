namespace ParkingSystem.Payment.WebAPI.Security;

internal static class PaymentAuthorization
{
    public const string AdminPolicy = "payment-system-admin";
    public const string ViewSessionFeePolicy = "payment-view-session-fee";
    public const string PaySessionFeePolicy = "payment-pay-session-fee";
    public const string PayVnPayPolicy = "payment-pay-vnpay";
    public const string ViewFeePaymentPolicy = "payment-view-fee-payment";
    public const string CustomerPolicy = "payment-customer-only";
    public const string OwnerPolicy = "payment-owner-only";
    public const string ConfirmCashPaymentPolicy = "payment-confirm-cash";
    public const string OwnerRevenueReportPolicy = "payment-owner-revenue-report";
    public const string RoleClaimType = "role";
    public const string AdminRole = "SystemAdmin";
    public const string AuthenticationMethodsClaimType = "amr";
    public const string MfaMethod = "mfa";

    public static IServiceCollection AddPaymentAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(AdminPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(RoleClaimType, AdminRole)
                .RequireClaim(AuthenticationMethodsClaimType, MfaMethod))
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
            .AddPolicy(OwnerPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(RoleClaimType, "ParkingLotOwner", "LotOwner"))
            .AddPolicy(ConfirmCashPaymentPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(RoleClaimType, "Staff", "ParkingLotOwner", "LotOwner"))
            .AddPolicy(OwnerRevenueReportPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim(RoleClaimType, "ParkingLotOwner", "LotOwner", "SystemAdmin"));

        return services;
    }
}
