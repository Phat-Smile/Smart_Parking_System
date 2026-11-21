namespace ParkingSystem.Payment.Application.Features.RefundRequests.Queries.ListRefundRequests;

public sealed record RefundRequestDto(
    Guid Id,
    Guid WalletId,
    Guid LotId,
    decimal Amount,
    string Reason,
    string? ReferenceType,
    string? ReferenceId,
    string Status,
    Guid CreatedBy,
    Guid? OwnerConfirmedBy,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    DateTimeOffset CreatedAt);
