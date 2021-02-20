namespace EmissiveClothing
{
    public static class Log
    {
        public static void Error(string message)
        {
            SuperController.LogError($"{nameof(EmissiveClothing)}: {message}");
        }

        public static void Message(string message)
        {
            SuperController.LogMessage($"{nameof(EmissiveClothing)}: {message}");
        }
    }
}
