using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace IpxListenerService.Controllers
{
    public class InstanceDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public int Channel { get; set; }
        public int Gpu { get; set; }
        public string LogLevel { get; set; } = string.Empty;
        public bool AutoStart { get; set; }
        public int ReconnectInterval { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsWorkerActive { get; set; }
        public string? XmlSourceFile { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class InstancesController : ControllerBase
    {
        private readonly IpxListenerService _listenerService;

        public InstancesController(IpxListenerService listenerService)
        {
            _listenerService = listenerService;
        }

        /// <summary>
        /// Gets all configured instances along with their active worker status.
        /// </summary>
        [HttpGet]
        public ActionResult<IEnumerable<InstanceDto>> GetInstances()
        {
            try
            {
                var instancesPath = _listenerService.InstancesPath;
                if (!System.IO.File.Exists(instancesPath))
                {
                    return Ok(new List<InstanceDto>());
                }

                var root = JsonLoader.LoadInstances(instancesPath);
                var activeWorkerIds = _listenerService.Workers.Select(w => w.InstanceId).ToHashSet();

                var list = new List<InstanceDto>();
                foreach (var (id, cfg) in root.Instances)
                {
                    list.Add(new InstanceDto
                    {
                        Id = id,
                        Name = cfg.Name,
                        Host = cfg.Host,
                        Port = cfg.Port,
                        Channel = cfg.Channel,
                        Gpu = cfg.Gpu,
                        LogLevel = cfg.LogLevel,
                        AutoStart = cfg.AutoStart,
                        ReconnectInterval = cfg.ReconnectInterval,
                        Status = cfg.Status,
                        Description = cfg.Description,
                        IsWorkerActive = activeWorkerIds.Contains(id),
                        XmlSourceFile = cfg.XmlSourceFile
                    });
                }

                return Ok(list);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error loading instances: {ex.Message}" });
            }
        }

        /// <summary>
        /// Gets detailed information for a specific instance.
        /// </summary>
        [HttpGet("{id}")]
        public ActionResult<InstanceDto> GetInstance(string id)
        {
            try
            {
                var instancesPath = _listenerService.InstancesPath;
                if (!System.IO.File.Exists(instancesPath))
                {
                    return NotFound(new { message = "Instances configuration file not found." });
                }

                var root = JsonLoader.LoadInstances(instancesPath);
                if (!root.Instances.TryGetValue(id, out var cfg))
                {
                    return NotFound(new { message = $"Instance '{id}' was not found." });
                }

                var isWorkerActive = _listenerService.Workers.Any(w => w.InstanceId == id);

                return Ok(new InstanceDto
                {
                    Id = id,
                    Name = cfg.Name,
                    Host = cfg.Host,
                    Port = cfg.Port,
                    Channel = cfg.Channel,
                    Gpu = cfg.Gpu,
                    LogLevel = cfg.LogLevel,
                    AutoStart = cfg.AutoStart,
                    ReconnectInterval = cfg.ReconnectInterval,
                    Status = cfg.Status,
                    Description = cfg.Description,
                    IsWorkerActive = isWorkerActive,
                    XmlSourceFile = cfg.XmlSourceFile
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Reloads instances configuration and restarts active workers.
        /// </summary>
        [HttpPost("reload")]
        public async Task<IActionResult> ReloadWorkers()
        {
            try
            {
                await _listenerService.ReloadWorkersAsync();
                return Ok(new
                {
                    message = "Workers reloaded successfully.",
                    workerCount = _listenerService.Workers.Count,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error reloading workers: {ex.Message}" });
            }
        }

        /// <summary>
        /// Triggers regeneration of instances.json from XML analytics files.
        /// </summary>
        [HttpPost("regenerate-xml")]
        public async Task<IActionResult> RegenerateFromXml()
        {
            try
            {
                await _listenerService.RegenerateInstancesAsync();
                return Ok(new
                {
                    message = "Instance regeneration from XML completed.",
                    workerCount = _listenerService.Workers.Count,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error regenerating instances: {ex.Message}" });
            }
        }
    }
}
