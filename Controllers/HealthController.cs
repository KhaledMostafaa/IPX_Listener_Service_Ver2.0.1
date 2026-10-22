using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace IpxListenerService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class HealthController : ControllerBase
    {
        private readonly IpxListenerService _listenerService;
        private readonly HealthCheckService _healthCheckService;

        public HealthController(IpxListenerService listenerService, HealthCheckService healthCheckService)
        {
            _listenerService = listenerService;
            _healthCheckService = healthCheckService;
        }

        /// <summary>
        /// Comprehensive system health check endpoint.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetHealth()
        {
            var isHealthy = await _healthCheckService.IsHealthyAsync();
            var process = Process.GetCurrentProcess();

            var response = new
            {
                status = isHealthy ? "Healthy" : "Degraded",
                serviceRunning = _listenerService.IsRunning,
                workerCount = _listenerService.Workers.Count,
                uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime(),
                processId = process.Id,
                memoryUsageMb = Math.Round((double)process.WorkingSet64 / (1024 * 1024), 2),
                timestamp = DateTime.UtcNow
            };

            return isHealthy ? Ok(response) : StatusCode(503, response);
        }

        /// <summary>
        /// Liveness probe - returns 200 OK if the web server process is responsive.
        /// </summary>
        [HttpGet("live")]
        public IActionResult Live()
        {
            return Ok(new { status = "Live", timestamp = DateTime.UtcNow });
        }

        /// <summary>
        /// Readiness probe - returns 200 OK if the listener service is currently active and running.
        /// </summary>
        [HttpGet("ready")]
        public IActionResult Ready()
        {
            if (_listenerService.IsRunning)
            {
                return Ok(new { status = "Ready", serviceRunning = true, timestamp = DateTime.UtcNow });
            }

            return StatusCode(503, new { status = "Not Ready", serviceRunning = false, timestamp = DateTime.UtcNow });
        }
    }
}
