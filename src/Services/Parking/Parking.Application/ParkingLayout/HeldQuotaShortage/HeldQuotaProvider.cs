using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ParkingSystem.BuildingBlocks.Time;

namespace ParkingSystem.Parking.Application.ParkingLayout.HeldQuotaShortage;

/// <summary>
/// The reservations Booking holds for a lot, for deciding walk-in car entry (US-PARK-007, BR-02.d).
/// When Booking fails or times out: returns the most recent cached answer (≤ 60s); if none is available,
/// falls back to 0 with a warning log so that the gate does not reject walk-in cars.
/// </summary>
public interface IHeldQuotaProvider
{
    Task<int?> GetHeldReservationsAsync(Guid lotId, CancellationToken cancellationToken);
}

// Team decision 2026-10-09 for a Booking that does not answer: ask with a deadline, otherwise reuse the last answer while
// it is recent enough (≤ 60s), otherwise fall back to 0 with a warning log so the gate does not reject walk-in cars.
public sealed class HeldQuotaOptions
{
    public TimeSpan Deadline { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan MaxAge { get; init; } = TimeSpan.FromSeconds(60);
}

// Last answer per lot, kept in this process only (no Redis in this project). Registered as a singleton.
public sealed class HeldQuotaCache
{
    private readonly ConcurrentDictionary<Guid, (int Held, DateTimeOffset ReadAt)> lastAnswers = new();

    public void Remember(Guid lotId, int held, DateTimeOffset readAt) => lastAnswers[lotId] = (held, readAt);

    public int? FindSince(Guid lotId, DateTimeOffset oldestAccepted) =>
        lastAnswers.TryGetValue(lotId, out var answer) && answer.ReadAt >= oldestAccepted ? answer.Held : null;
}

public sealed class HeldQuotaProvider(
    IHeldQuotaReader reader,
    HeldQuotaCache cache,
    HeldQuotaOptions options,
    IClock clock,
    ILogger<HeldQuotaProvider> logger)
    : IHeldQuotaProvider
{
    public async Task<int?> GetHeldReservationsAsync(Guid lotId, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(options.Deadline);
        try
        {
            // WaitAsync also stops waiting for a reader that ignores the token.
            var held = await reader.CountHeldReservationsAsync(lotId, deadline.Token).WaitAsync(options.Deadline, cancellationToken);
            cache.Remember(lotId, held, clock.UtcNow);
            return held;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Booking failed or was too slow: an answer from the last MaxAge is still good enough.
            var cached = cache.FindSince(lotId, clock.UtcNow - options.MaxAge);
            if (cached.HasValue)
            {
                logger.LogWarning(ex, "Failed to read held quota for lot {LotId} from Booking. Reusing cached answer {HeldQuota} (max age: {MaxAgeSeconds}s).",
                    lotId, cached.Value, options.MaxAge.TotalSeconds);
                return cached.Value;
            }

            logger.LogWarning(ex, "Failed to read held quota for lot {LotId} from Booking and no recent cache is available. Defaulting held quota to 0.",
                lotId);
            return 0;
        }
    }
}
