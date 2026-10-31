using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace 窗格.Util
{
    /// <summary>
    /// Minimal rolling file log used for diagnostics. Logging never throws: a failure to write a
    /// log entry must not affect the application.
    /// </summary>
    internal static class AppLogger
    {
        private const int RetainedLogDays = 7;
        private const int MaximumMessageLength = 1000;
        private const int MaximumStackTraceLength = 6000;

        private static readonly object SyncRoot = new object();
        private static bool trimmedOldLogs;

        public static string LogDirectory { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NoFences", "logs");

        public static void Info(string message)
        {
            Write("INFO", message, null);
        }

        public static void Error(string context, Exception exception)
        {
            Write("ERROR", context, exception);
        }

        public static string Truncate(string value, int maximumLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maximumLength)
                return value;

            return value.Substring(0, maximumLength) + "...";
        }

        private static void Write(string level, string message, Exception exception)
        {
            try
            {
                lock (SyncRoot)
                {
                    Directory.CreateDirectory(LogDirectory);
                    TrimOldLogs();

                    var file = Path.Combine(LogDirectory,
                        "window-" + DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log");
                    var builder = new StringBuilder();
                    builder.Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
                        .Append(" [")
                        .Append(level)
                        .Append("] ")
                        .AppendLine(Truncate(message, MaximumMessageLength));

                    if (exception != null)
                    {
                        builder.AppendLine(exception.GetType().FullName + ": " + exception.Message);
                        if (!string.IsNullOrEmpty(exception.StackTrace))
                            builder.AppendLine(Truncate(exception.StackTrace, MaximumStackTraceLength));
                    }

                    File.AppendAllText(file, builder.ToString(), Encoding.UTF8);
                }
            }
            catch
            {
                // Diagnostics must never take the application down.
            }
        }

        private static void TrimOldLogs()
        {
            if (trimmedOldLogs)
                return;

            trimmedOldLogs = true;
            try
            {
                var cutoff = DateTime.UtcNow.AddDays(-RetainedLogDays);
                foreach (var file in Directory.EnumerateFiles(LogDirectory, "window-*.log"))
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoff)
                        File.Delete(file);
                }
            }
            catch
            {
                // Best effort only.
            }
        }
    }
}
