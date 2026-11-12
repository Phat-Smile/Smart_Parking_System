using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ParkingSystem.Booking.WebAPI.OpenApi;

internal static class BookingSwaggerExtensions
{
    private const string BearerSchemeId = "Bearer";

    public static IServiceCollection AddBookingSwagger(this IServiceCollection services) =>
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition(BearerSchemeId, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "JWT access token."
            });
            options.OperationFilter<BearerSecurityOperationFilter>();
        });

    private sealed class BearerSecurityOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
            if (metadata.OfType<IAllowAnonymous>().Any() || !metadata.OfType<IAuthorizeData>().Any())
                return;

            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(BearerSchemeId, context.Document)] = []
            });
        }
    }
}

