using Resturanyar.Data;

namespace resturanyar.Utility
{
    public class WarmupService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;

        public WarmupService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();

           
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.CanConnectAsync(cancellationToken);

           
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
