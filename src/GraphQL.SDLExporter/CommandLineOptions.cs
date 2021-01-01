using System;
using System.Collections.Generic;
using System.IO;
using CommandLine;
using CommandLine.Text;

namespace GraphQL.SDLExporter
{
    /// <summary> Command line options for 'sdlexport' tool. </summary>
    public sealed class CommandLineOptions
    {
        /// <summary> Gets or sets a value indicating that detailed log output is required. </summary>
        [Option("verbose", Required = false, HelpText = "Enables verbose log output")]
        public bool Verbose { get; set; }

        /// <summary>
        /// Gets or sets the source of the schema. This is the full name of the executable file to run
        /// or URL to the GraphQL Web API.
        /// </summary>
        [Option("source", Required = true, HelpText = "Schema source - executable file or URL")]
        public string Source { get; set; }

        /// <summary> Gets or sets the URL where the process will be launched if an executable file has been specified as --source. </summary>
        [Option("url", Required = false, Default = "http://localhost:8088", HelpText = "URL to start process")]
        public string ServiceUrl { get; set; }

        /// <summary> Gets or sets additional command line arguments in case of using the executable file. </summary>
        [Option("args", Required = false, HelpText = "Additional command line arguments in case of using the executable file")]
        public string AdditionalCommandLineArgs { get; set; }

        /// <summary> Gets or sets the relative path for the GraphQL API when using the --url option. </summary>
        [Option("api-path", Required = false, Default = "/graphql", HelpText = "Relative path for GraphQL API when using --url option")]
        public string GraphQLRelativePath { get; set; }

        /// <summary> Gets or sets the authentication method. The value is specified as schema|parameter. </summary>
        /// <example> bearer|05c9b6cddb96df2bf854d13acc2fcaf85ca181ec </example>
        /// <example> basic|user:secret </example>
        [Option("auth", Required = false, HelpText = "Authentication method in schema|parameter format")]
        public string Authentication { get; set; }

        internal bool FromURL => Source.StartsWith("http://", StringComparison.InvariantCultureIgnoreCase) || Source.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase);

        /// <summary> Gets or sets the output SDL file. </summary>
        [Option("out", Required = false, HelpText = "The output SDL file. If not specified, then the schema will be saved as <Source>.graphql")]
        public string GeneratedFileName { get; set; }

        /// <summary> Gets or sets a value indicating whether to include descriptions of types and fields in the output file. </summary>
        [Option("include-descriptions", Required = false, HelpText = "Include descriptions as comments in output file")]
        public bool IncludeDescriptions { get; set; }

        /// <summary>
        /// Examples.
        /// </summary>
        [Usage]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Generating SDL from an executable file", new CommandLineOptions
                {
                    Source = "C:\\MyFiles\\MyService.dll",
                    GeneratedFileName = "D:\\MySchema.graphql",
                    IncludeDescriptions = true,
                    ServiceUrl = "http://localhost:5000",
                    GraphQLRelativePath = "/api/graphql"
                });

                yield return new Example("Generating SDL from URL", new CommandLineOptions
                {
                    Source = "https://api.github.com/graphql",
                    GeneratedFileName = "D:\\github.graphql",
                    IncludeDescriptions = true,
                    Authentication = "bearer|04b8b7cddb76df2bf353d23ccc1fcaf85ca132ec",
                    Verbose = true
                });
            }
        }

        internal int Validate()
        {
            if (!FromURL)
            {
                if (!File.Exists(Source))
                {
                    ColoredConsole.WriteError($"Unknown source: {Source}. Only http:// and https:// protocols are supported. You can also specify the full path to the executable file.");
                    return 1;
                }

                if (string.IsNullOrEmpty(ServiceUrl))
                {
                    ColoredConsole.WriteError("--url parameter not set");
                    return 2;
                }
            }

            if (!string.IsNullOrEmpty(Authentication))
            {
                string[] parts = Authentication.Split('|');
                if (parts.Length != 2 || string.IsNullOrEmpty(parts[0]) || string.IsNullOrEmpty(parts[1]))
                {
                    ColoredConsole.WriteError("The value of the --auth option must be specified in the schema|parameter format.");
                    return 3;
                }
            }

            return 0;
        }
    }
}
