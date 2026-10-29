using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Payment.Application.Features.Wallets.Queries.GetWalletTransactions;

public record GetWalletTransactionsQuery(
    Guid UserId,
    string? Role,
    string? RawPage,
    string? RawPageSize,
    string? RawType,
    string? RawFromUtc,
    string? RawToUtc
) : IRequest<Result<WalletTransactionsPagedDto>>;
