using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using IpxListenerService.Services;
using Microsoft.AspNetCore.Mvc;

namespace IpxListenerService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class LogsController : ControllerBase
    {
        private readonly ILogFileService _logFileService;

        public LogsController(ILogFileService logFileService)
        {
            _logFileService = logFileService;
        }

        /// <summary>
        /// Gets a summary of all log files and disk usage.
        /// </summary>
        [HttpGet("summary")]
        public async Task<ActionResult<LogSummaryDto>> GetSummary()
        {
            var summary = await _logFileService.GetSummaryAsync();
            return Ok(summary);
        }

        /// <summary>
        /// Lists all available log files with size and timestamp metadata.
        /// </summary>
        [HttpGet]
        public ActionResult<IEnumerable<LogFileInfo>> GetLogFiles()
        {
            var files = _logFileService.GetLogFiles();
            return Ok(files);
        }

        /// <summary>
        /// Reads recent log entries from the latest active log file.
        /// </summary>
        /// <param name="lines">Maximum number of lines to return (default: 100).</param>
        /// <param name="search">Optional search term to filter log entries.</param>
        /// <param name="level">Optional log level filter (e.g. INF, WRN, ERR, DBG).</param>
        [HttpGet("latest")]
        public async Task<ActionResult<LogContentResult>> GetLatestLogs(
            [FromQuery] int lines = 100,
            [FromQuery] string? search = null,
            [FromQuery] string? level = null)
        {
            var result = await _logFileService.ReadLogFileAsync(null, lines, search, level);
            return Ok(result);
        }

        /// <summary>
        /// Reads entries from a specific log file.
        /// </summary>
        /// <param name="fileName">Name of the log file (e.g. ipx-listener-20261022.log).</param>
        /// <param name="lines">Maximum number of lines to return (default: 200, 0 for all).</param>
        /// <param name="search">Optional search term.</param>
        /// <param name="level">Optional log level filter (e.g. INF, WRN, ERR).</param>
        [HttpGet("{fileName}")]
        public async Task<ActionResult<LogContentResult>> GetLogFileContent(
            string fileName,
            [FromQuery] int lines = 200,
            [FromQuery] string? search = null,
            [FromQuery] string? level = null)
        {
            var result = await _logFileService.ReadLogFileAsync(fileName, lines, search, level);
            return Ok(result);
        }

        /// <summary>
        /// Downloads the specified log file.
        /// </summary>
        /// <param name="fileName">Name of the log file.</param>
        [HttpGet("{fileName}/download")]
        [Produces("text/plain", "application/octet-stream")]
        public IActionResult DownloadLogFile(string fileName)
        {
            var stream = _logFileService.GetLogFileStream(fileName);
            if (stream == null)
            {
                return NotFound(new { message = $"Log file '{fileName}' was not found." });
            }

            return File(stream, "text/plain; charset=utf-8", fileName);
        }

        /// <summary>
        /// Deletes an archived log file. The currently active log file cannot be deleted.
        /// </summary>
        /// <param name="fileName">Name of the log file to delete.</param>
        [HttpDelete("{fileName}")]
        public IActionResult DeleteLogFile(string fileName)
        {
            var deleted = _logFileService.DeleteLogFile(fileName, out var error);
            if (!deleted)
            {
                return BadRequest(new { message = error });
            }

            return Ok(new { message = $"Log file '{fileName}' deleted successfully." });
        }
    }
}
