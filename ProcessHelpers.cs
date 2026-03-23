using System;
using System.Diagnostics;

/*
    ProcessHelpers.cs

    Utility helpers for process detection.

    Responsibilities:
    - Detects whether Guild Wars 2 appears to be running
    - Scans active processes for known GW2 executable names
    - Provides a simple yes/no result for runtime and UI status checks
*/

namespace GW2Telemetry
{
    internal static class ProcessHelpers
    {
        public static bool IsGw2Running()
        {
            try
            {
                foreach (var process in Process.GetProcesses())
                {
                    try
                    {
                        string name = process.ProcessName;

                        if (name.Equals("Gw2-64", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("Gw2", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("GuildWars2", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            catch
            {
            }

            return false;
        }
    }
}