using MediatR;
using ParkingSystem.Identity.Application.Accounts.RegisterAccount;

namespace ParkingSystem.Identity.WebAPI.Endpoints;

internal static class AccountEndpoints
{
    public sealed record RegisterAccountRequest(string Email, string Password, string? PhoneNumber);

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        // MF-01: registration is a public endpoint. Validation failures become 400 in the shared exception handler.
        app.MapPost("/api/identity/accounts", async (RegisterAccountRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new RegisterAccountCommand(request.Email, request.Password, request.PhoneNumber),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.Created((string?)null, result.Value)
                    : Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "The email address is already registered.",
                        detail: result.Error!.Message,
                        extensions: new Dictionary<string, object?> { ["errorCode"] = result.Error.Code });
            })
            .WithName("RegisterAccount")
            .Produces<RegisteredAccount>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
