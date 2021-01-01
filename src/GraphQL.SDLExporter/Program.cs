using System;
using CommandLine;

namespace GraphQL.SDLExporter
{
    internal static class Program
    {
        internal static DateTime Start = DateTime.Now;

        // DO NOT REMOVE: explicit cctor to initialize Start - https://csharpindepth.com/Articles/BeforeFieldInit
        static Program() { }

        internal static int Main(string[] args) =>
            Parser.Default.ParseArguments<CommandLineOptions>(args).MapResult(
                  (CommandLineOptions opt) => new SDLWriter { Options = opt }.Execute(),
                  errors => -1);
    }
}
