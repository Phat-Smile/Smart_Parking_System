using MediatR;

namespace ParkingSystem.Parking.Application.ParkingLots.ListStaffLots;

// Nhân viên / chủ bãi đăng nhập thấy các bãi đã duyệt mình được gán, để chọn bãi vào ca (NFR-07, Demo A luồng 4 bước 2).
public sealed record ListStaffLotsQuery(Guid AccountId) : IRequest<IReadOnlyList<StaffLot>>;

public sealed record StaffLot(Guid Id, string Name, string Address, string Type);

public sealed class ListStaffLotsQueryHandler(IParkingLotAssignmentRepository assignments)
    : IRequestHandler<ListStaffLotsQuery, IReadOnlyList<StaffLot>>
{
    // Chưa được gán bãi nào thì trả danh sách rỗng, không phải lỗi.
    public Task<IReadOnlyList<StaffLot>> Handle(ListStaffLotsQuery request, CancellationToken cancellationToken) =>
        assignments.ListApprovedLotsOfMemberAsync(request.AccountId, cancellationToken);
}
