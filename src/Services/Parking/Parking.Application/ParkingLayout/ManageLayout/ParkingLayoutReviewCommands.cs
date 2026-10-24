using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Parking.Application.ParkingLayout.HeldQuotaShortage;

namespace ParkingSystem.Parking.Application.ParkingLayout.ManageLayout;

public sealed record ListPendingLayoutReviewsQuery() : IRequest<IReadOnlyList<LayoutReviewRequest>>;

public sealed class ListPendingLayoutReviewsQueryHandler(IManageParkingLayoutRepository repository)
    : IRequestHandler<ListPendingLayoutReviewsQuery, IReadOnlyList<LayoutReviewRequest>>
{
    public Task<IReadOnlyList<LayoutReviewRequest>> Handle(ListPendingLayoutReviewsQuery request, CancellationToken ct) =>
        repository.ListPendingReviewsAsync(ct);
}

public sealed record ReviewLayoutChangeCommand(Guid LotId, Guid RequestId, Guid AdminId, bool Approve, string? Reason)
    : IRequest<Result<ReviewedLayoutChange>>;

public sealed class ReviewLayoutChangeCommandHandler(
    IManageParkingLayoutRepository repository, IHeldQuotaShortageCheck shortageCheck)
    : IRequestHandler<ReviewLayoutChangeCommand, Result<ReviewedLayoutChange>>
{
    public async Task<Result<ReviewedLayoutChange>> Handle(ReviewLayoutChangeCommand request, CancellationToken ct)
    {
        if (request.LotId == Guid.Empty || request.RequestId == Guid.Empty || request.AdminId == Guid.Empty ||
            (request.Approve ? !string.IsNullOrWhiteSpace(request.Reason) :
                string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500))
            return Result<ReviewedLayoutChange>.Failure(LayoutReviewErrors.InvalidDecision);

        var outcome = await repository.ReviewAsync(request.LotId, request.RequestId,
            request.AdminId, request.Approve, request.Reason, ct);
        if (outcome.Error is not null)
            return Result<ReviewedLayoutChange>.Failure(outcome.Error);

        ParkingLotLayout? activeLayout = null;
        IReadOnlyList<CapacityWarning> warnings = [];
        if (request.Approve)
        {
            activeLayout = await repository.ReadAsync(request.LotId, ct);
            var warning = await shortageCheck.CheckAsync(request.LotId, ct);
            if (warning is not null) warnings = [warning];
        }
        return Result<ReviewedLayoutChange>.Success(new ReviewedLayoutChange(outcome.Review!, activeLayout, warnings));
    }
}
