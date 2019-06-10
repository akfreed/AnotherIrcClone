// ==================================================================
// Copyright (c) 2019 Alexander Freed
// Language: C# 6.0 (.NET Framework 4.6.1)
// ==================================================================

using System;


namespace ChatCommon
{
    /// <summary>
    /// Provides a standard way to timestamp and print messages to the screen and output.
    /// Debug message can be squelched.
    /// Logging to file is not implemented.
    /// </summary>
    public static class Logging
    {
        public static string Tag      = "AppLog";
        public static string DebugTag = "AppLog (debug)";
        public static bool   DebugToStdout = true;
        public static bool   DebugToOutput = true;


        public static string GetTimestamp()
        {
            return DateTime.Now.ToString("'['yyyy'_'MM'_'dd HH:mm:ss']'");
        }

        public static void DebugWriteLine(string message)
        {
            string timestamp = GetTimestamp();

            if (DebugToOutput)
                System.Diagnostics.Debug.WriteLine(timestamp + " " + message, DebugTag);
            if (DebugToStdout)
                Console.WriteLine(String.Format("{0} {1}: {2}", timestamp, DebugTag, message));
        }

        public static void WriteLine(string message)
        {
            string timestamp = GetTimestamp();

            System.Diagnostics.Debug.WriteLine(timestamp + " " + message, Tag);
            Console.WriteLine(String.Format("{0} {1}: {2}", timestamp, Tag, message));
        }
    }
}
