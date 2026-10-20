namespace ParkingSystem.Identity.Application.Accounts.OwnerApplications;

public interface IOwnerApplicationQueries
{
    Task<(IReadOnlyList<OwnerApplicationListItem> Items, int TotalCount)> ListOwnerApplicationsAsync(
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}

public sealed record OwnerApplicationListItem(
    Guid AccountId,
    string Email,
    string? PhoneNumber,
    string? FullName,
    string Status,
    string? TaxCode,
    string? BusinessName,
    string? RejectionReason,
    DateTimeOffset? ReviewedAtUtc,
    Guid? ReviewedBy,
    DateTimeOffset CreatedAtUtc);
