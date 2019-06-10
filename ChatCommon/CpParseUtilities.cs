// ==================================================================
// Copyright (c) 2019 Alexander Freed
// Language: C# 6.0 (.NET Framework 4.6.1)
// ==================================================================

using System;
using System.Linq;

using System.Diagnostics;


namespace ChatCommon
{
    /// <summary>
    /// This static class holds some common Chat Protocol string manipulation functions
    /// </summary>
    public static class CpParseUtilities
    {
        public static string[] SplitMessage(string commandString, char delimiter, int maxSplit)
        {
            string[] commandSplit = commandString.Split(new char[] { delimiter }, maxSplit);
            // strip whitespace
            commandSplit = commandSplit.Select(str => str.Trim()).ToArray();
            return commandSplit;
        }
        public static string[] SplitMessage(string commandString, char delimiter)
        {
            string[] commandSplit = commandString.Split(delimiter);
            // strip whitespace
            commandSplit = commandSplit.Select(str => str.Trim()).ToArray();
            return commandSplit;
        }

        public static bool CheckArgCount(string[] commandSplit, int expectedMin, ref string errMessage)
        {
            if (commandSplit.Length < expectedMin)
            {
                errMessage = String.Format("Command expects at least {0} arguments.", expectedMin);
                return false;
            }
            return true;
        }
        public static bool CheckArgCount(string[] commandSplit, int expectedMin, int expectedMax, ref string errMessage)
        {
            if (!CheckArgCount(commandSplit, expectedMin, ref errMessage))
            {
                return false;
            }
            if (commandSplit.Length > expectedMin)
            {
                errMessage = String.Format("Command expects no more than {0} arguments.", expectedMax);
                return false;
            }
            return true;
        }

        public static void RecombineSplitMessage(ref string[] commandSplit, int firstIndex)
        {
            if (firstIndex >= commandSplit.Length)
            {
                Debug.Assert(false, "Call checkArgCount first.");
                return;
            }

            // combine strings [index..end] into a new string
            string recombined = String.Join(":", commandSplit.Skip(firstIndex));
            // remake the commandSplit array with the new string instead
            commandSplit = commandSplit.Take(firstIndex).Concat(new string[] { recombined }).ToArray();
        }

        public static bool ConvertType(string arg, ref Int32 converted, ref string errMessage)
        {
            try
            {
                converted = Convert.ToInt32(arg);
            }
            catch
            {
                errMessage = String.Format("Unable to convert argument '{0}' to Int32.", arg);
                return false;
            }
            return true;
        }

        public static bool CheckConstraint_NonNegative(int i, ref string errMessage)
        {
            if (i < 0)
            {
                errMessage = String.Format("Argument '{0}' must be non-negative.", i);
                return false;
            }
            return true;
        }
    }
}
