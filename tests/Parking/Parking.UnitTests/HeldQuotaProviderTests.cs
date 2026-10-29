using ParkingSystem.BuildingBlocks.Time;
using ParkingSystem.Parking.Application.ParkingLayout.HeldQuotaShortage;
using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

// US-PARK-007, team decision 2026-10-09: when Booking does not answer, reuse its last answer for up to 60 seconds,
// otherwise report "unknown" so the gate refuses walk-in cars.
public sealed class HeldQuotaProviderTests
{
    private static readonly Guid LotId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly HeldQuotaOptions Options = new() { Deadline = TimeSpan.FromMilliseconds(100), MaxAge = TimeSpan.FromSeconds(60) };

    private readonly MovableClock clock = new();
    private readonly HeldQuotaCache cache = new();

    [Fact]
    public async Task Returns_what_booking_answers()
    {
        var provider = NewProvider(new FakeReader { Held = 4 });

        Assert.Equal(4, await provider.GetHeldReservationsAsync(LotId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Reuses_the_last_answer_when_booking_fails_within_60_seconds()
    {
        var reader = new FakeReader { Held = 3 };
        await NewProvider(reader).GetHeldReservationsAsync(LotId, TestContext.Current.CancellationToken);

        reader.Fails = true;
        clock.Advance(TimeSpan.FromSeconds(60));

        Assert.Equal(3, await NewProvider(reader).GetHeldReservationsAsync(LotId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Is_unknown_when_booking_fails_and_the_last_answer_is_older_than_60_seconds()
    {
        var reader = new FakeReader { Held = 3 };
        await NewProvider(reader).GetHeldReservationsAsync(LotId, TestContext.Current.CancellationToken);

        reader.Fails = true;
        clock.Advance(TimeSpan.FromSeconds(61));

        Assert.Null(await NewProvider(reader).GetHeldReservationsAsync(LotId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Is_unknown_when_booking_never_answered_this_lot()
    {
        Assert.Null(await NewProvider(new FakeReader { Fails = true }).GetHeldReservationsAsync(LotId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Stops_waiting_at_the_deadline_even_when_the_reader_ignores_the_token()
    {
        var reader = new FakeReader { Hangs = true };

        var held = await NewProvider(reader).GetHeldReservationsAsync(LotId, TestContext.Current.CancellationToken);

        Assert.Null(held);
    }

    [Fact]
    public async Task An_answer_for_one_lot_is_not_used_for_another()
    {
        var reader = new FakeReader { Held = 2 };
        await NewProvider(reader).GetHeldReservationsAsync(LotId, TestContext.Current.CancellationToken);

        reader.Fails = true;

        Assert.Null(await NewProvider(reader).GetHeldReservationsAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    private HeldQuotaProvider NewProvider(FakeReader reader) => new(reader, cache, Options, clock);

    private sealed class FakeReader : IHeldQuotaReader
    {
        public int Held { get; init; }
        public bool Fails { get; set; }
        public bool Hangs { get; init; }

        public Task<int> CountHeldReservationsAsync(Guid lotId, CancellationToken cancellationToken)
        {
            if (Hangs)
                return new TaskCompletionSource<int>().Task;
            return Fails ? Task.FromException<int>(new InvalidOperationException("Booking is down.")) : Task.FromResult(Held);
        }
    }

    private sealed class MovableClock : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = new(2026, 10, 29, 8, 0, 0, TimeSpan.Zero);

        public void Advance(TimeSpan by) => UtcNow += by;
    }
}
