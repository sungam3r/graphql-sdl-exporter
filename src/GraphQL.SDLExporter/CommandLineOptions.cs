using System.Net.Http.Headers;
using System.Reflection;
using CommandLine;
using CommandLine.Text;

namespace GraphQL.SDLExporter;

/// <summary> Command line options for 'sdlexport' tool. </summary>
public sealed class CommandLineOptions
{
    /// <summary>
    /// A delegate to modify introspection query sent by client to the GraphQL server.
    /// </summary>
    public Func<string, string> ConfigureIntrospectionQuery { get; set; } = query => query;

    /// <summary>
    /// Allows you to specify a file with your own introspection query.
    /// </summary>
    [Option("introspection-file", Required = false, HelpText = "Allows you to specify a file with your own introspection query")]
    public string? IntrospectionQueryFile { get; set; }

    /// <summary>
    /// A factory to create <see cref="HttpClient"/> used to send an introspection query.
    /// </summary>
    public Func<CommandLineOptions, HttpClient> HttpClientFactory { get; set; } = options =>
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true });
        // UserAgent is always filled, some APIs require it to be specified (https://developer.github.com/v3/#user-agent-required)
        var asmName = Assembly.GetExecutingAssembly().GetName();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(asmName.Name!, asmName.Version?.ToString() ?? "1.0.0"));

        if (!string.IsNullOrEmpty(options.Authentication))
        {
            string[] parts = options.Authentication.Split('|');
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(parts[0], parts[1]);
        }

        return client;
    };

    /// <summary> A value indicating that detailed log output is required. </summary>
    [Option("verbose", Required = false, HelpText = "Enables verbose log output")]
    public bool Verbose { get; set; }

    /// <summary>
    /// The source of the schema. This is the full name of the executable file to run
    /// or URL to the GraphQL Web API.
    /// </summary>
    [Option("source", Required = true, HelpText = "Schema source - executable file or URL")]
    public string Source { get; set; } = null!;

    /// <summary> The URL where the process will be launched if an executable file has been specified as --source. </summary>
    [Option("url", Required = false, Default = "http://localhost:8088", HelpText = "URL to start process")]
    public string? ServiceUrl { get; set; }

    /// <summary> Additional command line arguments in case of using the executable file. </summary>
    [Option("args", Required = false, HelpText = "Additional command line arguments in case of using the executable file")]
    public string? AdditionalCommandLineArgs { get; set; }

    /// <summary> The relative path for the GraphQL API when using the --url option. </summary>
    [Option("api-path", Required = false, Default = "/graphql", HelpText = "Relative path for GraphQL API when using --url option")]
    public string? GraphQLRelativePath { get; set; }

    /// <summary>
    /// The authentication method. The value is specified as schema|parameter.
    /// Also in this parameter you can set the path to the file with this data.
    /// </summary>
    /// <example> bearer|05c9b6cddb96df2bf854d13acc2fcaf85ca181ec </example>
    /// <example> basic|user:secret </example>
    [Option("auth", Required = false, HelpText = "Authentication method in schema|parameter format")]
    public string? Authentication { get; set; }

    internal bool FromURL => Source.StartsWith("http://", StringComparison.InvariantCultureIgnoreCase) || Source.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase);

    /// <summary> The output SDL file. </summary>
    [Option("out", Required = false, HelpText = "The output SDL file. If not specified, then the schema will be saved as <Source>.graphql")]
    public string? GeneratedFileName { get; set; }

    /// <summary> A value indicating whether to include descriptions in the output file. </summary>
    [Option("include-descriptions", Required = false, HelpText = "Include descriptions as comments in output file")]
    public bool IncludeDescriptions { get; set; }

    /// <summary> A timeout in seconds for generating SDL. By default 0, i.e. no timeout.</summary>
    [Option("timeout", Required = false, HelpText = "Timeout in seconds for generating SDL; 0 - no timeout")]
    public int Timeout { get; set; }

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
                GraphQLRelativePath = "/api/graphql",
                Timeout = 10,
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
            if (File.Exists(Authentication))
                Authentication = File.ReadAllText(Authentication);

            string[] parts = Authentication.Split('|');
            if (parts.Length != 2 || string.IsNullOrEmpty(parts[0]) || string.IsNullOrEmpty(parts[1]))
            {
                ColoredConsole.WriteError("The value of the --auth option must be specified in the schema|parameter format.");
                return 3;
            }
        }

        if (Timeout < 0)
        {
            ColoredConsole.WriteError("The value of the --timeout option should be non-negative integer");
        }

        return 0;
    }
}
