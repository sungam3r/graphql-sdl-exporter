using System.Diagnostics;
using GraphQL.IntrospectionModel;
using GraphQL.IntrospectionModel.SDL;

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
            ColoredConsole.WriteError("Failed to get introspection response for both modern and classic introspection query.");
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
        cancellationToken.ThrowIfCancellationRequested();

        string serviceUrl = Options.FromURL ? Options.Source : Options.ServiceUrl + Options.GraphQLRelativePath;

        using (var client = new GraphQLHttpClient(Options.HttpClientFactory(Options)))
        {
            // There should be enough time to start. If necessary, this can be moved to the options.
            int retry = 1;
            const int MAX_RETRY = 10;
            ColoredConsole.WriteInfo($"Starting to poll {serviceUrl} with max {MAX_RETRY} attempts.");

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                GraphQLResponse? response = null;
                ColoredConsole.WriteInfo($"Sending introspection request #{retry} to {serviceUrl}");

                try
                {
                    // skip modern introspection request with directives if custom introspection query was specified
                    if (Options.IntrospectionQueryFile == null)
                    {
                        try
                        {
                            response = client.SendQueryAsync(serviceUrl, Options.ConfigureIntrospectionQuery(IntrospectionQuery.Modern), "IntrospectionQuery", cancellationToken).GetAwaiter().GetResult();
                            if (response?.Data != null)
                                ColoredConsole.WriteInfo($"Received modern introspection response from {serviceUrl}");
                        }
                        catch (Exception ex)
                        {
                            ColoredConsole.WriteError($"Failed to send modern introspection request with directives: {(Options.Verbose ? ex.ToString() : ex.Message)}");
                        }
                    }

                    if (response?.Data == null || response?.Errors?.Any() == true)
                    {
                        void TryPrintErrors(string header)
                        {
                            if (response?.Errors?.Any() == true)
                            {
                                ColoredConsole.WriteError(header);
                                foreach (var error in response.Errors)
                                    ColoredConsole.WriteError(error.Message);
                            }
                        }

                        TryPrintErrors("Modern introspection request with directives contains errors:");

                        if (Options.IntrospectionQueryFile == null)
                        {
                            ColoredConsole.WriteInfo("Fallback to classic introspection request without directives");
                            response = client.SendQueryAsync(serviceUrl, Options.ConfigureIntrospectionQuery(IntrospectionQuery.Classic), "IntrospectionQuery", cancellationToken).GetAwaiter().GetResult();
                            if (response?.Data != null)
                                ColoredConsole.WriteInfo($"Received classic introspection response from {serviceUrl}");
                            TryPrintErrors("Classic introspection request without directives contains errors:");
                        }
                        else
                        {
                            ColoredConsole.WriteInfo($"Calling custom introspection query from '{Options.IntrospectionQueryFile}'.");
                            response = client.SendQueryAsync(serviceUrl, Options.ConfigureIntrospectionQuery(File.ReadAllText(Options.IntrospectionQueryFile)), "IntrospectionQuery", cancellationToken).GetAwaiter().GetResult();
                            if (response?.Data != null)
                                ColoredConsole.WriteInfo($"Received introspection response from {serviceUrl}");
                            TryPrintErrors($"Custom introspection query from '{Options.IntrospectionQueryFile}' contains errors:");
                        }
                    }

                    return response;
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
    }

    private string? ConvertIntrospectionResponseToSDL(GraphQLResponse response)
    {
        if (response.Errors?.Any() == true)
        {
            ColoredConsole.WriteError("Introspection response contains errors:");
            foreach (var error in response.Errors)
                ColoredConsole.WriteError(error.Message);

            return null;
        }

        if (response.Data == null)
        {
            Console.WriteLine("GraphQLResponse.Data is null.");
            return null;
        }

        var schemaToken = response.Data["__schema"];
        if (schemaToken == null)
        {
            Console.WriteLine("GraphQLResponse.Data does not contain '__schema' property.");
            return null;
        }

        var schema = schemaToken.ToObject<GraphQLSchema>();
        if (schema == null)
        {
            Console.WriteLine("Could not deserialize '__schema' property from GraphQLResponse.Data to GraphQLSchema object.");
            return null;
        }

        if (Options.Verbose)
        {
            ColoredConsole.WriteInfo($"Introspection response contains {schema.Types?.Count ?? 0} types and {schema.Directives?.Count ?? 0} directives:");
            ColoredConsole.WriteInfo(schemaToken.ToString(Newtonsoft.Json.Formatting.Indented));
            ColoredConsole.WriteInfo("Starting transformation from introspection response (json) to SDL.");
        }

        string sdl = SDLBuilder.Build(schema, new SDLBuilderOptions
        {
            ArgumentComments = Options.IncludeDescriptions,
            EnumValuesComments = Options.IncludeDescriptions,
            FieldComments = Options.IncludeDescriptions,
            TypeComments = Options.IncludeDescriptions,
        });

        if (Options.Verbose)
        {
            ColoredConsole.WriteInfo("SDL generated successfully:");
            ColoredConsole.WriteInfo(sdl);
        }

        return sdl;
    }
}
