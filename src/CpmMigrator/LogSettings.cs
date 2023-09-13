using System.ComponentModel;

using Spectre.Console.Cli;

namespace CpmMigrator
{
    public class LogSettings : CommandSettings
    {
        [Description("Level of logging output.")]
        [CommandOption("-v|--verbosity")]
        public LogLevel LogVerbosity { get; set; } = LogLevel.Info;

        [Description("Path to log file.")]
        [CommandOption("-l|--log")]
        public string? LogFile { get; set; }
    }

    public enum LogLevel { Info, Debug, Warning }
}
