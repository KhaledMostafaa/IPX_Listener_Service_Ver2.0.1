using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace IpxListenerService.Services
{
    public class LogFileService : ILogFileService
    {
        private readonly string _logDirectory;
        private readonly ILogger<LogFileService> _logger;

        public string LogDirectory => _logDirectory;

        public LogFileService(ILogger<LogFileService> logger)
        {
            _logger = logger;
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _logDirectory = Path.Combine(baseDir, "logs");

            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
        }

        public IEnumerable<LogFileInfo> GetLogFiles()
        {
            if (!Directory.Exists(_logDirectory))
                return Enumerable.Empty<LogFileInfo>();

            var directoryInfo = new DirectoryInfo(_logDirectory);
            var files = directoryInfo.GetFiles("*.log")
                .OrderByDescending(f => f.LastWriteTime)
                .ToList();

            var todayPrefix = DateTime.UtcNow.ToString("yyyyMMdd");
            var result = new List<LogFileInfo>();

            foreach (var file in files)
            {
                var isActive = file.Name.Contains(todayPrefix) || 
                               (files.Count > 0 && files[0].FullName == file.FullName);

                result.Add(new LogFileInfo
                {
                    FileName = file.Name,
                    FilePath = file.FullName,
                    SizeBytes = file.Length,
                    FormattedSize = FormatBytes(file.Length),
                    CreatedAt = file.CreationTime,
                    LastModified = file.LastWriteTime,
                    IsActive = isActive
                });
            }

            return result;
        }

        public async Task<LogContentResult> ReadLogFileAsync(string? fileName = null, int maxLines = 200, string? search = null, string? level = null)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                var latestFile = GetLogFiles().FirstOrDefault();
                if (latestFile == null)
                {
                    return new LogContentResult
                    {
                        FileName = "None",
                        Lines = new List<string> { "No log files found in " + _logDirectory }
                    };
                }
                fileName = latestFile.FileName;
            }

            // Sanitize filename to prevent directory traversal
            var safeFileName = Path.GetFileName(fileName);
            var filePath = Path.Combine(_logDirectory, safeFileName);

            if (!File.Exists(filePath))
            {
                return new LogContentResult
                {
                    FileName = safeFileName,
                    Lines = new List<string> { $"Log file '{safeFileName}' not found." }
                };
            }

            var allMatchingLines = new List<string>();

            try
            {
                // Open with FileShare.ReadWrite to allow concurrent reading while Serilog is appending
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(fileStream, Encoding.UTF8))
                {
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        // Filter by log level if specified (e.g. INF, WRN, ERR, DBG)
                        if (!string.IsNullOrWhiteSpace(level))
                        {
                            var levelTag = $"[{level.Trim().ToUpperInvariant()}]";
                            if (!line.Contains(levelTag, StringComparison.OrdinalIgnoreCase))
                                continue;
                        }

                        // Filter by search string if specified
                        if (!string.IsNullOrWhiteSpace(search))
                        {
                            if (!line.Contains(search, StringComparison.OrdinalIgnoreCase))
                                continue;
                        }

                        allMatchingLines.Add(line);
                    }
                }

                var totalCount = allMatchingLines.Count;
                var returnedLines = maxLines > 0 && maxLines < totalCount
                    ? allMatchingLines.Skip(totalCount - maxLines).ToList()
                    : allMatchingLines;

                return new LogContentResult
                {
                    FileName = safeFileName,
                    TotalLines = totalCount,
                    ReturnedLines = returnedLines.Count,
                    FilterLevel = level,
                    SearchTerm = search,
                    Lines = returnedLines
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read log file {FileName}", safeFileName);
                return new LogContentResult
                {
                    FileName = safeFileName,
                    Lines = new List<string> { $"Error reading log file: {ex.Message}" }
                };
            }
        }

        public FileStream? GetLogFileStream(string fileName)
        {
            var safeFileName = Path.GetFileName(fileName);
            var filePath = Path.Combine(_logDirectory, safeFileName);

            if (!File.Exists(filePath))
                return null;

            return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        public bool DeleteLogFile(string fileName, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                var safeFileName = Path.GetFileName(fileName);
                var filePath = Path.Combine(_logDirectory, safeFileName);

                if (!File.Exists(filePath))
                {
                    errorMessage = $"File '{safeFileName}' does not exist.";
                    return false;
                }

                // Check if this is the newest active file
                var newest = GetLogFiles().FirstOrDefault();
                if (newest != null && newest.FileName.Equals(safeFileName, StringComparison.OrdinalIgnoreCase) && newest.IsActive)
                {
                    errorMessage = "Cannot delete the currently active log file.";
                    return false;
                }

                File.Delete(filePath);
                _logger.LogInformation("Deleted log file: {FileName}", safeFileName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting log file {FileName}", fileName);
                errorMessage = ex.Message;
                return false;
            }
        }

        public Task<LogSummaryDto> GetSummaryAsync()
        {
            var files = GetLogFiles().ToList();
            long totalBytes = files.Sum(f => f.SizeBytes);
            var activeFile = files.FirstOrDefault(f => f.IsActive)?.FileName ?? files.FirstOrDefault()?.FileName;
            var lastLogTime = files.FirstOrDefault()?.LastModified;

            var summary = new LogSummaryDto
            {
                TotalLogFiles = files.Count,
                TotalSizeBytes = totalBytes,
                FormattedTotalSize = FormatBytes(totalBytes),
                ActiveLogFile = activeFile,
                LastLogTime = lastLogTime,
                LogDirectory = _logDirectory
            };

            return Task.FromResult(summary);
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n2} {suffixes[counter]}";
        }
    }
}
