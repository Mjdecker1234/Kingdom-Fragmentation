using TaleWorlds.Library;

namespace KingdomFragmentation.Helpers
{
    /// <summary>
    /// Thin wrapper around Bannerlord's <see cref="Debug"/> class that adds a
    /// consistent "KF" prefix and respects the MCM debug-logging toggle.
    /// </summary>
    public static class LogHelper
    {
        private const string Prefix = "[KingdomFragmentation] ";

        // -----------------------------------------------------------------------
        // Public helpers
        // -----------------------------------------------------------------------

        public static void Info(string message)
        {
            Debug.Print(Prefix + message, color: Debug.DebugColor.White);
        }

        public static void Warn(string message)
        {
            Debug.Print(Prefix + "WARNING: " + message, color: Debug.DebugColor.Yellow);
        }

        public static void Error(string message)
        {
            Debug.Print(Prefix + "ERROR: " + message, color: Debug.DebugColor.Red);
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
        }
    }
}
