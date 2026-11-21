using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Payment.Application.Common.Constants;
using ParkingSystem.Payment.Application.Common.Interfaces;
using ParkingSystem.Payment.Domain.Enums;

namespace ParkingSystem.Payment.Application.Features.RefundRequests.Queries.ListRefundRequests;

public sealed class ListRefundRequestsQueryHandler(
    IRefundRequestRepository refundRequestRepository,
    IWalletRepository walletRepository,
    IParkingLotMembershipGateway membershipGateway)
    : IRequestHandler<ListRefundRequestsQuery, Result<RefundRequestsPagedDto>>
{
    private const string AdminRole = "SystemAdmin";
    private const string CustomerRole = "Customer";
    private static readonly string[] OwnerRoles = ["ParkingLotOwner", "LotOwner"];

    public async Task<Result<RefundRequestsPagedDto>> Handle(
        ListRefundRequestsQuery request,
        CancellationToken cancellationToken)
    {
        var isAdmin = string.Equals(request.CallerRole, AdminRole, StringComparison.OrdinalIgnoreCase);
        var isOwner = OwnerRoles.Any(r => string.Equals(request.CallerRole, r, StringComparison.OrdinalIgnoreCase));
        var isCustomer = string.Equals(request.CallerRole, CustomerRole, StringComparison.OrdinalIgnoreCase);

        RefundRequestListFilter filter;

        if (isAdmin)
        {
            filter = new RefundRequestListFilter(
                Status: request.Status,
                LotId: request.LotId,
                WalletId: null,
                Page: request.Page,
                PageSize: request.PageSize);
        }
        else if (isOwner)
        {
            if (!request.LotId.HasValue || request.LotId.Value == Guid.Empty)
            {
                return Result<RefundRequestsPagedDto>.Failure(RefundRequestErrors.LotIdRequired);
            }

            var membership = await membershipGateway.GetMembershipAsync(request.LotId.Value, request.CallerId, cancellationToken);
            if (!membership.IsSuccess || membership.Value is null)
            {
                return Result<RefundRequestsPagedDto>.Failure(RefundRequestErrors.ParkingServiceUnavailable);
            }

            if (!membership.Value.IsMember || !string.Equals(membership.Value.Role, "Owner", StringComparison.OrdinalIgnoreCase))
            {
                return Result<RefundRequestsPagedDto>.Failure(RefundRequestErrors.ForbiddenLotAccess);
            }

            filter = new RefundRequestListFilter(
                Status: request.Status,
                LotId: request.LotId.Value,
                WalletId: null,
                Page: request.Page,
                PageSize: request.PageSize);
        }
        else if (isCustomer)
        {
            var wallet = await walletRepository.GetByUserIdAsync(request.CallerId, cancellationToken);
            if (wallet is null)
            {
                return Result<RefundRequestsPagedDto>.Success(
                    new RefundRequestsPagedDto(Array.Empty<RefundRequestDto>(), 0, request.Page, request.PageSize));
            }

            filter = new RefundRequestListFilter(
                Status: request.Status,
                LotId: request.LotId,
                WalletId: wallet.Id,
                Page: request.Page,
                PageSize: request.PageSize);
        }
        else
        {
            return Result<RefundRequestsPagedDto>.Failure(RefundRequestErrors.ForbiddenRole);
        }

        var pageResult = await refundRequestRepository.ListAsync(filter, cancellationToken);

        var dtos = pageResult.Items.Select(r => new RefundRequestDto(
            r.Id,
            r.WalletId,
            r.LotId,
            r.Amount,
            r.Reason,
            r.ReferenceType?.ToString(),
            r.ReferenceId,
            r.Status.ToString(),
            r.CreatedBy,
            r.OwnerConfirmedBy,
            r.ReviewedBy,
            r.ReviewedAt,
            r.CreatedAt
        )).ToList();

        return Result<RefundRequestsPagedDto>.Success(
            new RefundRequestsPagedDto(dtos, pageResult.TotalCount, pageResult.Page, pageResult.PageSize));
    }
}
