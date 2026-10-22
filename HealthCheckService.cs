using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace IpxListenerService
{
    /// <summary>
    /// A service to perform health checks on the IpxListenerService.
    /// </summary>
    public class HealthCheckService
    {
        private readonly IpxListenerService _service;
        private readonly ILogger<HealthCheckService> _healthLogger;

        public HealthCheckService(IpxListenerService service, ILogger<HealthCheckService> logger)
        {
            _service = service;
            _healthLogger = logger;
        }

        public async Task<bool> IsHealthyAsync()
        {
            _healthLogger.LogInformation("Performing health check...");
            await Task.Yield();
            if (_service.IsRunning)
            {
                _healthLogger.LogInformation("Service is currently running and healthy.");
                return true;
            }
            _healthLogger.LogWarning("Service is not running.");
            return false;
        }
    }
}
