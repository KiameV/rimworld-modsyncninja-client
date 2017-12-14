namespace RimWorld_ModSyncNinja
{
    class MSLog
    {
        private static int numLog = 0;
        public enum Level
        {
            All,
            User,
            None
        }
        public static Level LogLevel = Level.User;

        public static void Log(string message, Level logLevel = MSLog.Level.All, bool error = false)
        {
            // dont log
            if (LogLevel == Level.None) return;
            if (LogLevel == logLevel || LogLevel == Level.All)
            {
                if (error)
                    Verse.Log.Error("[ModSync Ninja LOG] " + message);
                else Verse.Log.Message("[ModSync Ninja LOG] " + message);
            }
        }

        public static void NLog(string message)
        {
            Log(message + " " + numLog);
            numLog++;
        }
        public static void ShowErrorScreen()
        {
            Verse.Log.TryOpenLogWindow();
        }
    }
}
