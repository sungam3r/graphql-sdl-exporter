using System;

namespace GraphQL.SDLExporter
{
    internal static class ColoredConsole
    {
        public static ConsoleColor ErrorColor { get; set; } = ConsoleColor.Red;

        public static ConsoleColor InfoColor { get; set; } = ConsoleColor.Gray;

        public static ConsoleColor WarningColor { get; set; } = ConsoleColor.Yellow;

        public static void WriteError(string text = null) => Write(text, ErrorColor);

        public static void WriteInfo(string text = null) => Write(text, InfoColor);

        public static void WriteWarning(string text = null) => Write(text, WarningColor);

        private static void Write(string text, ConsoleColor color)
        {
            var old = Console.ForegroundColor;
            try
            {
                Console.ForegroundColor = color;
                Console.WriteLine(text);
            }
            finally
            {
                Console.ForegroundColor = old;
            }
        }
    }
}
