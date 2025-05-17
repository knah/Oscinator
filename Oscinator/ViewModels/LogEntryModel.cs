using System;
using Microsoft.Extensions.Logging;

namespace Oscinator.ViewModels;

public class LogEntryModel
{
    public required LogLevel Level;
    
    public required string Severity { get; set; }
    public required DateTime Time { get; set; }
    public required string Category { get; set; }
    public required string Message { get; set; }

    public string SeverityColorName
    {
        get
        {
            return Level switch
            {
                LogLevel.Trace => "SystemBaseLowColor",
                LogLevel.Debug => "SystemBaseLowColor",
                LogLevel.Information => "SystemBaseHighColor",
                LogLevel.Warning => "WarningTextColor",
                LogLevel.Error => "SystemErrorTextColor",
                LogLevel.Critical => "SystemErrorTextColor",
                LogLevel.None => "SystemBaseHighColor",
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}