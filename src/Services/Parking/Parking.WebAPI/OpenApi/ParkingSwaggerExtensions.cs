using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ParkingSystem.Parking.WebAPI.OpenApi;

internal static class ParkingSwaggerExtensions
{
    private const string BearerSchemeId = "Bearer";

    // Swagger UI shows the Authorize button and sends "Authorization: Bearer <token>" (tester report 2026-10-08, DOC-001).
    public static IServiceCollection AddParkingSwagger(this IServiceCollection services) =>
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition(BearerSchemeId, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "JWT access token. Staff endpoints need the claim role = Staff."
            });
            options.OperationFilter<BearerSecurityOperationFilter>();
            options.SchemaFilter<AllowedValuesSchemaFilter>();
        });

    // Swashbuckle ignores [AllowedValues]; publish the values as a schema enum (DOC-002).
    private sealed class AllowedValuesSchemaFilter : ISchemaFilter
    {
        public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
        {
            var allowedValues = context.MemberInfo?.GetCustomAttribute<AllowedValuesAttribute>();
            if (allowedValues is null || schema is not OpenApiSchema concreteSchema)
                return;

            concreteSchema.Enum = allowedValues.Values
                .Select(JsonNode (value) => JsonValue.Create(value?.ToString() ?? string.Empty))
                .ToList();
        }
    }

    // Marks only endpoints that require authorization, so anonymous endpoints (lot catalog) stay unlocked in the UI.
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
