using MediatR;
using ParkingSystem.BuildingBlocks.Identity;
using ParkingSystem.BuildingBlocks.Results;
using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.Common.Plate;
using ParkingSystem.Parking.Application.ParkingLots;
using ParkingSystem.Parking.Domain.ParkingSessions;

namespace ParkingSystem.Parking.Application.ParkingSessions.RecognizePlate;

public sealed class RecognizePlateCommandHandler(
    IParkingLotDetailReader parkingLots,
    IParkingLotAccess lotAccess,
    ILicensePlateOcrEngine ocrEngine,
    IParkingSessionRepository sessions,
    IClock clock) : IRequestHandler<RecognizePlateCommand, Result<RecognizePlateResponse>>
{
    public async Task<Result<RecognizePlateResponse>> Handle(RecognizePlateCommand request, CancellationToken cancellationToken)
    {
        // 1. Kiểm tra sự tồn tại và trạng thái duyệt của bãi đỗ xe
        var lot = await parkingLots.GetApprovedByIdAsync(request.LotId, cancellationToken);
        if (lot is null)
        {
            return Result<RecognizePlateResponse>.Failure(RecognizePlateErrors.LotNotFound);
        }

        // 2. TECH-12: Kiểm tra quyền vận hành bãi đỗ của nhân viên/chủ bãi
        if (!await lotAccess.CanOperateAsync(request.ActorUserId, request.LotId, cancellationToken))
        {
            return Result<RecognizePlateResponse>.Failure(RecognizePlateErrors.LotAccessDenied);
        }

        // 3. Trích xuất biển số xe từ hình ảnh qua AI OCR Engine
        var ocrResult = await ocrEngine.RecognizePlateAsync(request.ImagePayload, request.LotId.ToString(), cancellationToken);
        if (string.IsNullOrWhiteSpace(ocrResult.NormalizedPlate))
        {
            return Result<RecognizePlateResponse>.Failure(RecognizePlateErrors.UnreadablePlate);
        }

        var normalizedPlate = VietnamesePlateNormalizer.Normalize(ocrResult.NormalizedPlate);
        var isValidFormat = VietnamesePlateNormalizer.IsValidFormat(normalizedPlate);
        var vehicleType = string.IsNullOrWhiteSpace(ocrResult.VehicleType)
            ? VietnamesePlateNormalizer.DetectVehicleType(normalizedPlate)
            : ocrResult.VehicleType;

        var isConfident = ocrResult.Confidence >= 0.85 && isValidFormat;
        MatchedSessionDto? matchedSession = null;
        string suggestedAction;

        var isEntry = string.Equals(request.GateDirection, "Entry", StringComparison.OrdinalIgnoreCase);

        if (isEntry)
        {
            var hasOpen = await sessions.HasOpenSessionAsync(request.LotId, normalizedPlate, cancellationToken);
            if (hasOpen)
            {
                suggestedAction = "SessionAlreadyOpen";
            }
            else
            {
                suggestedAction = isConfident ? "AutoCheckIn" : "RequiresManualReview";
            }
        }
        else // Exit gate
        {
            // Tìm chính xác phiên đỗ xe đang mở (Active hoặc Paid)
            var openSession = await sessions.FindOpenByPlateAsync(request.LotId, normalizedPlate, cancellationToken);
            if (openSession is not null)
            {
                matchedSession = new MatchedSessionDto(
                    openSession.Id,
                    openSession.PlateNumber,
                    openSession.EntryTime,
                    openSession.Status.ToString(),
                    null);
                suggestedAction = isConfident ? "AutoCheckOut" : "ConfirmCheckOut";
            }
            else
            {
                // So khớp mờ (Fuzzy Matching) với các phiên Active/Paid của bãi để bù trừ lỗi OCR
                var (activeSessions, _) = await sessions.ListByLotAsync(request.LotId, ParkingSessionStatus.Active, 0, 100, cancellationToken);
                var (paidSessions, _) = await sessions.ListByLotAsync(request.LotId, ParkingSessionStatus.Paid, 0, 100, cancellationToken);
                var allCandidates = activeSessions.Concat(paidSessions).ToList();

                var (bestMatchPlate, distance) = FuzzyPlateMatcher.FindBestMatch(
                    normalizedPlate,
                    allCandidates.Select(s => s.PlateNumber),
                    maxAllowedDistance: 1);

                if (bestMatchPlate is not null)
                {
                    var session = allCandidates.First(s => string.Equals(
                        VietnamesePlateNormalizer.Normalize(s.PlateNumber),
                        VietnamesePlateNormalizer.Normalize(bestMatchPlate),
                        StringComparison.OrdinalIgnoreCase));

                    matchedSession = new MatchedSessionDto(
                        session.Id,
                        session.PlateNumber,
                        session.EntryTime,
                        session.Status.ToString(),
                        null);

                    suggestedAction = "ConfirmCheckOut";
                }
                else
                {
                    suggestedAction = "RequiresManualReview";
                }
            }
        }

        var response = new RecognizePlateResponse(
            DetectedPlate: string.IsNullOrWhiteSpace(ocrResult.RawPlate) ? normalizedPlate : ocrResult.RawPlate,
            NormalizedPlate: normalizedPlate,
            VehicleType: vehicleType,
            Confidence: ocrResult.Confidence,
            IsConfident: isConfident,
            DetectedAtUtc: clock.UtcNow,
            SuggestedAction: suggestedAction,
            MatchedBooking: null,
            MatchedSession: matchedSession,
            BoundingBox: ocrResult.BoundingBox,
            Provider: ocrResult.ProcessingProvider);

        return Result<RecognizePlateResponse>.Success(response);
    }
}
