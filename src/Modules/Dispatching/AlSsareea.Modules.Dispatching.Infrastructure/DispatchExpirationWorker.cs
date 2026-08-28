using AlSsareea.Modules.Dispatching.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AlSsareea.Modules.Dispatching.Infrastructure;

internal sealed class DispatchExpirationWorker(IServiceScopeFactory scopes) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken)) { await using AsyncServiceScope scope = scopes.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<IDispatchService>().ExpireAsync(stoppingToken); }
    }
}
