using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace IpxListenerService.Services
{
    public class LogFileInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string FormattedSize { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastModified { get; set; }
        public bool IsActive { get; set; }
    }

    public class LogContentResult
    {
        public string FileName { get; set; } = string.Empty;
        public int TotalLines { get; set; }
        public int ReturnedLines { get; set; }
        public string? FilterLevel { get; set; }
        public string? SearchTerm { get; set; }
        public List<string> Lines { get; set; } = new();
    }

    public class LogSummaryDto
    {
        public int TotalLogFiles { get; set; }
        public long TotalSizeBytes { get; set; }
        public string FormattedTotalSize { get; set; } = string.Empty;
        public string? ActiveLogFile { get; set; }
        public DateTime? LastLogTime { get; set; }
        public string LogDirectory { get; set; } = string.Empty;
    }

    public interface ILogFileService
    {
        string LogDirectory { get; }
        IEnumerable<LogFileInfo> GetLogFiles();
        Task<LogContentResult> ReadLogFileAsync(string? fileName = null, int maxLines = 200, string? search = null, string? level = null);
        FileStream? GetLogFileStream(string fileName);
        bool DeleteLogFile(string fileName, out string errorMessage);
        Task<LogSummaryDto> GetSummaryAsync();
    }
}
