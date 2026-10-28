using MediatR;
using ParkingSystem.BuildingBlocks.Results;

namespace ParkingSystem.Payment.Application.Features.Wallets.Queries.GetWalletTopupStatus;

// Truy van trang thai nap tien theo ma giao dich va tai khoan
public record GetWalletTopupStatusQuery(Guid UserId, string TopupId) : IRequest<Result<WalletTopupStatusDto>>;
