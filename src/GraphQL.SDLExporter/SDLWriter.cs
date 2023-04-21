using System.Diagnostics;
using System.Text.Json;
using GraphQL.IntrospectionModel;
using GraphQL.IntrospectionModel.SDL;
using GraphQLParser.Visitors;

namespace GraphQL.SDLExporter;

/// <summary>
/// Writes SDL to destination.
/// </summary>
public sealed class SDLWriter
{
    /// <summary>
    /// Options used.
    /// </summary>
    public CommandLineOptions Options { get; init; } = null!;

    /// <summary>
    /// Writes SDL to destination.
    /// </summary>
    /// <returns> Exit code. 0 for success, otherwise one of the error codes. </returns>
    public int Execute()
    {
        int validated = Options.Validate();
        if (validated != 0)
            return validated;

        var cts = new CancellationTokenSource();
        if (Options.Timeout > 0)
            cts.CancelAfter(TimeSpan.FromSeconds(Options.Timeout));

        Options.GeneratedFileName = Options.GeneratedFileName ?? (Options.FromURL ? "service" : Options.Source) + ".graphql";

        ColoredConsole.WriteInfo($"Start exporting SDL from {Options.Source}");

        var outDirectory = new FileInfo(Options.GeneratedFileName).Directory!;
        outDirectory.Create(); // if there is no necessary subdirectory, then this creates it

        ColoredConsole.WriteInfo($"Output directory: {outDirectory.FullName}");

        GraphQLResponse? response = null;

        if (Options.FromURL)
        {
            response = GetIntrospectionResponseFromUrl(cts.Token);
        }
        else
        {
            Process? targetService = null;
            string? processName = null;
            try
            {
                processName = Path.GetFileNameWithoutExtension(Options.Source);

                string args = $"{Options.Source} API_ONLY_RESTRICTED_ENVIRONMENT --server.urls {Options.ServiceUrl} --urls {Options.ServiceUrl} {Options.AdditionalCommandLineArgs}";
                ColoredConsole.WriteInfo($"Executing command: dotnet {args}");

                // Currently, only ASP.NET Core apps are supported.
                var procStartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = args,
                    // there may be problems with services that did not configure the working directory on their own
                    WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(Options.Source))!,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                targetService = Process.Start(procStartInfo)!;
                targetService.Exited += (o, e) => ColoredConsole.WriteInfo($"The process {processName} exited with code {targetService.ExitCode}");
                targetService.OutputDataReceived += (o, e) =>
                {
                    if (e.Data != null)
                        ColoredConsole.WriteInfo($"[{processName}] {e.Data}");
                };
                targetService.ErrorDataReceived += (o, e) =>
                {
                    if (e.Data != null)
                        ColoredConsole.WriteInfo($"[{processName}] ERROR {e.Data}");
                };

                targetService.BeginOutputReadLine();
                targetService.BeginErrorReadLine();

                if (targetService.HasExited)
                    throw new ApplicationException($"Process could not start, exit code: {targetService.ExitCode}");

                ColoredConsole.WriteInfo($"The process {processName} was started at {Options.ServiceUrl}");

                if (Options.Timeout > 0)
                {
                    cts.Token.Register(() =>
                    {
                        try
                        {
                            targetService?.Kill();
                        }
                        catch (InvalidOperationException)
                        {
                            // the process has already completed
                        }
                    });
                }

                response = GetIntrospectionResponseFromUrl(cts.Token);
            }
            finally
            {
                try
                {
                    targetService?.Kill();
                }
                catch (InvalidOperationException)
                {
                    // the process has already completed
                }

                ColoredConsole.WriteInfo($"The process {processName} was stopped");
            }
        }

        if (response?.Data == null)
        {
            ColoredConsole.WriteError("Failed to get introspection query response");
            return 100;
        }

        cts.Token.ThrowIfCancellationRequested();
        string? sdl = ConvertIntrospectionResponseToSDL(response);

        if (string.IsNullOrEmpty(sdl))
        {
            ColoredConsole.WriteError("Failed to generate SDL");
            return 200;
        }

        File.WriteAllText(Options.GeneratedFileName, sdl);

        ColoredConsole.WriteInfo($"SDL was successfully written to {Options.GeneratedFileName}, generation completed in {DateTime.Now - Program.Start:hh\\:mm\\:ss\\.ff} sec.");

        return 0;
    }

    private GraphQLResponse? GetIntrospectionResponseFromUrl(CancellationToken cancellationToken)
    {
        void PrintErrors(GraphQLResponse? response, string header)
        {
            if (response?.Errors?.Any() == true)
            {
                ColoredConsole.WriteError(header);
                foreach (var error in response.Errors)
                    ColoredConsole.WriteError(error.Message);
            }
        }

        GraphQLResponse? ExecuteIntrospectionVariation(GraphQLHttpClient client, string serviceUrl, string query, string type)
        {
            GraphQLResponse? response = null;

            try
            {
                ColoredConsole.WriteInfo($"Sending {type} introspection query");
                response = client.SendQueryAsync(serviceUrl, Options.ConfigureIntrospectionQuery(query), "IntrospectionQuery", cancellationToken).GetAwaiter().GetResult();
                if (response?.Data != null)
                    ColoredConsole.WriteInfo($"Received {type} introspection query response from {serviceUrl}");
            }
            catch (Exception ex)
            {
                ColoredConsole.WriteError($"Failed to get data for {type} introspection query: {(Options.Verbose ? ex.ToString() : ex.Message)}");
            }

            PrintErrors(response, $"Errors for {type} introspection query:");

            return response?.Data == null ? null : response;
        }

        cancellationToken.ThrowIfCancellationRequested();

        string serviceUrl = Options.FromURL ? Options.Source : Options.ServiceUrl + Options.GraphQLRelativePath;

        using var client = new GraphQLHttpClient(Options.HttpClientFactory(Options));

        // There should be enough time to start. If necessary, this can be moved to the options.
        int retry = 1;
        const int MAX_RETRY = 10;
        ColoredConsole.WriteInfo($"Starting to poll {serviceUrl} with max {MAX_RETRY} attempts.");

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ColoredConsole.WriteInfo($"Attempt #{retry} to {serviceUrl} begins");

            try
            {
                return Options.IntrospectionQueryFile == null
                    ? ExecuteIntrospectionVariation(client, serviceUrl, Options.ConfigureIntrospectionQuery(IntrospectionQuery.ModernDraft), "modern/draft") ??
                      ExecuteIntrospectionVariation(client, serviceUrl, Options.ConfigureIntrospectionQuery(IntrospectionQuery.Modern), "modern") ??
                      ExecuteIntrospectionVariation(client, serviceUrl, Options.ConfigureIntrospectionQuery(IntrospectionQuery.ClassicDraft), "classic/draft") ??
                      ExecuteIntrospectionVariation(client, serviceUrl, Options.ConfigureIntrospectionQuery(IntrospectionQuery.Classic), "classic")
                    : ExecuteIntrospectionVariation(client, serviceUrl, Options.ConfigureIntrospectionQuery(File.ReadAllText(Options.IntrospectionQueryFile)), "custom:" + Options.IntrospectionQueryFile);
            }
            catch (Exception e)
            {
                ColoredConsole.WriteError($"An error occurred while executing an HTTP request ({retry}): {(Options.Verbose ? e.ToString() : e.Message)}");

                if (Options.FromURL)
                    return null;

                ColoredConsole.WriteError($"Make sure that it is possible to start the process at {serviceUrl} and the required port is not used by another process. Perhaps the process has not yet started and cannot serve the request.");

                if (retry == MAX_RETRY)
                {
                    ColoredConsole.WriteError($"Failed to load data from {serviceUrl} for {MAX_RETRY} attempts");
                    return null; // that's enough
                }

                ++retry;
                ColoredConsole.WriteInfo($"Waiting 2 seconds and try again ({retry} of {MAX_RETRY}).");
                Thread.Sleep(2000);
            }
        }
    }

    private string? ConvertIntrospectionResponseToSDL(GraphQLResponse response)
    {
        if (response.Errors?.Any() == true)
        {
            ColoredConsole.WriteError("Introspection query response contains errors:");
            foreach (var error in response.Errors)
                ColoredConsole.WriteError(error.Message);

            return null;
        }

        if (response.Data?.__Schema == null)
        {
            Console.WriteLine("Could not obtain GraphQLSchema object.");
            return null;
        }

        if (Options.Verbose)
        {
            ColoredConsole.WriteInfo($"Introspection query response contains {response.Data.__Schema.Types?.Count ?? 0} types and {response.Data.__Schema.Directives?.Count ?? 0} directives:");
            ColoredConsole.WriteInfo(JsonSerializer.Serialize<object>(response.Data.__Schema));
            ColoredConsole.WriteInfo("Starting transformation from introspection query response (json) to SDL.");
        }

        string sdl = response.Data.__Schema.Print(new ASTConverterOptions
        {
            PrintDescriptions = Options.IncludeDescriptions,
            EachDirectiveLocationOnNewLine = true,
            EachUnionMemberOnNewLine = true,
        });

        if (Options.Verbose)
        {
            ColoredConsole.WriteInfo("SDL generated successfully:");
            ColoredConsole.WriteInfo(sdl);
        }

        return sdl;
    }
}
