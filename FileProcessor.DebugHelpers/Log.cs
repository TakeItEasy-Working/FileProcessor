using System.Diagnostics;

namespace FileProcessor.DebugHelpers
{
    public static class Log
    {
        [Conditional("DEBUG")]
        public static void Debug(string message)
        {
            System.Diagnostics.Debug.WriteLine(message);
        }
    }
}
