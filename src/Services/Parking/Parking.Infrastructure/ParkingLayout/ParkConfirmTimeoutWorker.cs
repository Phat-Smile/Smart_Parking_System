using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ParkingSystem.Parking.Application.ParkingLayout.SlotConfirmation;

namespace ParkingSystem.Parking.Infrastructure.ParkingLayout;

// US-PARK-005 (BR-04.e): checks the 10 and 30 minute limits once a minute.
public sealed class ParkConfirmTimeoutWorker(IServiceScopeFactory scopeFactory, ILogger<ParkConfirmTimeoutWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ISender>().Send(new CheckParkConfirmTimeoutsCommand(), stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The next tick tries again; one failed run must not stop the worker.
                logger.LogError(exception, "Checking park confirmation timeouts failed.");
            }
        }
    }
}
