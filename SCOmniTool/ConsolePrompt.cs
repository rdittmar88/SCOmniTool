using System;

namespace SCOmniTool;

internal static class ConsolePrompt
{
    public static bool AskYesNo(string question)
    {
        while (true)
        {
            Console.Write(question);
            var response = Console.ReadLine()?.Trim();
            if (string.Equals(response, "y", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(response, "n", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Console.WriteLine("Please enter y or n.");
        }
    }
}
