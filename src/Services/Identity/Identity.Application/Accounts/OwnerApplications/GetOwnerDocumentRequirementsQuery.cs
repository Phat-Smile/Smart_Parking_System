using MediatR;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.Identity.Domain.Accounts;

namespace ParkingSystem.Identity.Application.Accounts.OwnerApplications;

public sealed record OwnerDocumentRequirementsDto(
    string OwnerType,
    IReadOnlyList<OwnerDocumentRequirementItem> Documents);

public sealed record GetOwnerDocumentRequirementsQuery(
    OwnerType OwnerType) : IRequest<Result<OwnerDocumentRequirementsDto>>;

public sealed class GetOwnerDocumentRequirementsQueryHandler(
    IOwnerDocumentRequirements requirements) : IRequestHandler<GetOwnerDocumentRequirementsQuery, Result<OwnerDocumentRequirementsDto>>
{
    public Task<Result<OwnerDocumentRequirementsDto>> Handle(GetOwnerDocumentRequirementsQuery request, CancellationToken cancellationToken)
    {
        var items = requirements.GetRequirements(request.OwnerType);
        var result = new OwnerDocumentRequirementsDto(request.OwnerType.ToString(), items);
        return Task.FromResult(Result<OwnerDocumentRequirementsDto>.Success(result));
    }
}
