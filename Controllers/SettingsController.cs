using System;
using System.IO;
using Microsoft.AspNetCore.Mvc;

namespace IpxListenerService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class SettingsController : ControllerBase
    {
        private readonly IpxListenerService _listenerService;

        public SettingsController(IpxListenerService listenerService)
        {
            _listenerService = listenerService;
        }

        /// <summary>
        /// Gets current global settings with sensitive database credentials masked.
        /// </summary>
        [HttpGet]
        public ActionResult<GlobalSettingsRoot> GetSettings()
        {
            try
            {
                var settingsPath = _listenerService.SettingsPath;
                if (!System.IO.File.Exists(settingsPath))
                {
                    return NotFound(new { message = "Settings file not found." });
                }

                var settings = JsonLoader.LoadGlobalSettings(settingsPath);
                
                // Mask password for safety
                if (settings.GlobalSettings?.Database != null)
                {
                    if (!string.IsNullOrEmpty(settings.GlobalSettings.Database.Password))
                    {
                        settings.GlobalSettings.Database.Password = "********";
                    }
                    if (!string.IsNullOrEmpty(settings.GlobalSettings.Database.ConnectionString))
                    {
                        settings.GlobalSettings.Database.ConnectionString = Program.MaskConnectionString(settings.GlobalSettings.Database.ConnectionString);
                    }
                    if (!string.IsNullOrEmpty(settings.GlobalSettings.Database.Url))
                    {
                        settings.GlobalSettings.Database.Url = Program.MaskConnectionString(settings.GlobalSettings.Database.Url);
                    }
                }

                return Ok(settings);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error reading settings: {ex.Message}" });
            }
        }

        /// <summary>
        /// Gets the settings file path on disk.
        /// </summary>
        [HttpGet("path")]
        public IActionResult GetSettingsPath()
        {
            return Ok(new
            {
                settingsPath = _listenerService.SettingsPath,
                instancesPath = _listenerService.InstancesPath,
                baseDirectory = _listenerService.BaseDirectory,
                settingsExists = System.IO.File.Exists(_listenerService.SettingsPath),
                instancesExists = System.IO.File.Exists(_listenerService.InstancesPath)
            });
        }
    }
}
