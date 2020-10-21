using GraphQL.IntrospectionModel;
using GraphQL.IntrospectionModel.SDL;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace GraphQL.SDLExporter
{
    internal sealed class SDLWriter
    {
        public CommandLineOptions Options { get; set; }

        internal int Execute()
        {
            var validated = Options.Validate();
            if (validated != 0)
                return validated;

            Options.GeneratedFileName = Options.GeneratedFileName ?? (Options.FromURL ? "service" : Options.Source) + ".graphql";

            ColoredConsole.WriteInfo($"Start exporting SDL from {Options.Source}");

            var outDirectory = new FileInfo(Options.GeneratedFileName).Directory;
            outDirectory.Create(); // if there is no necessary subfolder, then this creates it

            ColoredConsole.WriteInfo($"Output directory: {outDirectory.FullName}");

            GraphQLResponse response = null;

            if (Options.FromURL)
            {
                response = GetIntrospectionResponseFromUrl();
            }
            else
            {
                Process targetService = null;
                string processName = null;
                try
                {
                    processName = Path.GetFileNameWithoutExtension(Options.Source);

                    var args = $"{Options.Source} API_ONLY_RESTRICTED_ENVIRONMENT --server.urls {Options.ServiceUrl} --urls {Options.ServiceUrl} {Options.AdditionalCommandLineArgs}";
                    ColoredConsole.WriteInfo($"Executing command: dotnet {args}");

                    // Currently, only ASP.NET Core apps are supported.
                    var procStartInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = args,
                        // there may be problems with services that did not configure the working directory on their own
                        WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(Options.Source)),
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    };

                    targetService = Process.Start(procStartInfo);
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

                    response = GetIntrospectionResponseFromUrl();
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
                ColoredConsole.WriteError("Failed to get introspection response");
                return 100;
            }

            string sdl = ConvertIntrospectionResponseToSDL(response);

            if (string.IsNullOrEmpty(sdl))
            {
                ColoredConsole.WriteError("Failed to generate SDL");
                return 200;
            }

            File.WriteAllText(Options.GeneratedFileName, sdl);

            ColoredConsole.WriteInfo($"SDL was successfully written to {Options.GeneratedFileName}, generation completed in {DateTime.Now - Program.Start:hh\\:mm\\:ss\\.ff} sec.");

            return 0;
        }

        private GraphQLResponse GetIntrospectionResponseFromUrl()
        {
            string serviceUrl = Options.FromURL ? Options.Source : Options.ServiceUrl + Options.GraphQLRelativePath;

            using (var client = new GraphQLHttpClient(serviceUrl, Options.Authentication))
            {
                // There should be enough time to start. If necessary, this can be moved to the options.
                int retry = 1;
                const int MAX_RETRY = 10;

                while (true)
                {
                    GraphQLResponse response = null;
                    ColoredConsole.WriteInfo($"Sending introspection request to {serviceUrl}");

                    try
                    {
                        try
                        {
                            response = client.SendQueryAsync(IntrospectionQuery.Modern, "IntrospectionQuery").GetAwaiter().GetResult();
                            if (response?.Data != null)
                                ColoredConsole.WriteInfo($"Received modern introspection response from {serviceUrl}");
                        }
                        catch (Exception ex)
                        {
                            ColoredConsole.WriteError($"Failed to send modern introspection request with directives: {ex.Message}");
                        }

                        if (response?.Data == null || response?.Errors?.Any() == true)
                        {
                            if (response?.Errors?.Any() == true)
                            {
                                ColoredConsole.WriteError("Modern introspection request with directives contains errors:");
                                foreach (var error in response.Errors)
                                    ColoredConsole.WriteError(error.Message);
                            }

                            ColoredConsole.WriteInfo("Fallback to classic introspection request without directives");
                            response = client.SendQueryAsync(IntrospectionQuery.Classic, "IntrospectionQuery").GetAwaiter().GetResult();
                            if (response?.Data != null)
                                ColoredConsole.WriteInfo($"Received classic introspection response from {serviceUrl}");
                        }

                        return response;
                    }
                    catch (Exception e)
                    {
                        ColoredConsole.WriteError($"An error occurred while executing an HTTP request ({retry}): {e.Message}");

                        if (Options.FromURL)
                            return null;

                        ColoredConsole.WriteError($"Make sure that it is possible to start the process at {serviceUrl} and the required port is not used by another process. Perhaps the process has not yet started and cannot serve the request.");

                        if (retry == MAX_RETRY)
                        {
                            ColoredConsole.WriteError($"Failed to load data from {serviceUrl} for {MAX_RETRY} attempts");
                            return null; // that's enough
                        }

                        ++retry;
                        ColoredConsole.WriteWarning($"Wait 2 seconds and try {retry} from {MAX_RETRY}.");
                        Thread.Sleep(2000);
                    }
                }
            }
        }

        private string ConvertIntrospectionResponseToSDL(GraphQLResponse response)
        {
            if (response.Errors?.Any() == true)
            {
                ColoredConsole.WriteError("Introspection response contains errors:");
                foreach (var error in response.Errors)
                    ColoredConsole.WriteError(error.Message);

                return null;
            }

            var data = response.Data["__schema"];
            var schema = data.ToObject<GraphQLSchema>();

            if (Options.Verbose)
            {
                ColoredConsole.WriteInfo($"Introspection response contains {schema.Types?.Count() ?? 0} types and {schema.Directives?.Count ?? 0} directives:");
                ColoredConsole.WriteInfo(data.ToString(Newtonsoft.Json.Formatting.Indented));
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
}
