using System;
using System.IO;
using TaleWorlds.Library;

namespace KingdomFragmentation.Helpers
{
    /// <summary>
    /// Thin wrapper around Bannerlord's <see cref="Debug"/> class that adds a
    /// consistent "KF" prefix and respects the MCM debug-logging toggle.
    /// Also writes to a log file for reliable diagnostics.
    /// </summary>
    public static class LogHelper
    {
        private const string Prefix = "[KingdomFragmentation] ";
        private static readonly object _lock = new object();
        private static string _logFilePath;

        private static string LogFilePath
        {
            get
            {
                if (_logFilePath == null)
                {
                    try
                    {
                        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        string logDir = Path.Combine(docs, "Mount and Blade II Bannerlord", "Configs", "ModLogs");
                        if (!Directory.Exists(logDir))
                            Directory.CreateDirectory(logDir);
                        _logFilePath = Path.Combine(logDir, "KingdomFragmentation.log");
                    }
                    catch
                    {
                        _logFilePath = "KingdomFragmentation.log";
                    }
                }
                return _logFilePath;
            }
        }

        private static void WriteToFile(string level, string message)
        {
            try
            {
                lock (_lock)
                {
                    File.AppendAllText(LogFilePath,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + level + "] " + message + Environment.NewLine);
                }
            }
            catch
            {
                // Swallow file write errors to avoid crashing the game
            }
        }

        // -----------------------------------------------------------------------
        // Public helpers
        // -----------------------------------------------------------------------

        public static void Info(string message)
        {
            TaleWorlds.Library.Debug.Print(Prefix + message, color: TaleWorlds.Library.Debug.DebugColor.White);
            WriteToFile("INF", message);
        }

        public static void Warn(string message)
        {
            TaleWorlds.Library.Debug.Print(Prefix + "WARNING: " + message, color: TaleWorlds.Library.Debug.DebugColor.Yellow);
            WriteToFile("WRN", message);
        }

        public static void Error(string message)
        {
            TaleWorlds.Library.Debug.Print(Prefix + "ERROR: " + message, color: TaleWorlds.Library.Debug.DebugColor.Red);
            WriteToFile("ERR", message);
        }

        /// <summary>
        /// Only prints when MCM debug logging is enabled.
        /// </summary>
        public static void Debug(string message)
        {
            var settings = Settings.KingdomFragmentationSettings.Instance;
            if (settings?.DebugLogging != true)
                return;

            TaleWorlds.Library.Debug.Print(
                Prefix + "[DEBUG] " + message,
                color: TaleWorlds.Library.Debug.DebugColor.Cyan);
            WriteToFile("DBG", message);
        }
    }
}
