using System;
using System.IO;

namespace GraphQL.SDLExporter
{
    internal static class ColoredConsole
    {
        public static ConsoleColor ErrorColor { get; set; } = ConsoleColor.Red;

        public static ConsoleColor InfoColor { get; set; } = ConsoleColor.Gray;

        public static ConsoleColor WarningColor { get; set; } = ConsoleColor.Yellow;

        public static void WriteError(string? text = null) => Write(text, ErrorColor, Console.Error);

        public static void WriteInfo(string? text = null) => Write(text, InfoColor, Console.Out);

        public static void WriteWarning(string? text = null) => Write(text, WarningColor, Console.Error);

        private static void Write(string? text, ConsoleColor color, TextWriter to)
        {
            var old = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = color;

                // If the text is empty, then the intention of the caller is very likely
                // a visual separation of blocks of text, so there is no need to display the time.
                if (string.IsNullOrEmpty(text))
                    to.WriteLine();
                else
                    to.WriteLine($"[{DateTime.Now:HH:mm:ss}] {text}");
            }
            finally
            {
                Console.ForegroundColor = old;
            }
        }
    }
}
