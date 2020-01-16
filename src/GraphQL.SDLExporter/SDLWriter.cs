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

            Console.WriteLine("Start exporting SDL");

            var outDirectory = new FileInfo(Options.GeneratedFileName).Directory;
            outDirectory.Create(); // if there is no necessary subfolder, then this creates it

            Console.WriteLine($"Output directory: {outDirectory.FullName}");

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
                    Console.WriteLine($"Starting process {processName} at {Options.ServiceUrl}");

                    // Currently, only ASP.NET Core apps are supported.
                    var procStartInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = Options.Source + " API_ONLY_RESTRICTED_ENVIRONMENT --server.urls " + Options.ServiceUrl,
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
                            Console.WriteLine($"[{processName}] {e.Data}");
                    };
                    targetService.ErrorDataReceived += (o, e) =>
                    {
                        if (e.Data != null)
                            Console.WriteLine($"[{processName}] ERROR {e.Data}");
                    };

                    targetService.BeginOutputReadLine();
                    targetService.BeginErrorReadLine();

                    if (targetService.HasExited)
                        throw new ApplicationException($"Process could not start, exit code: {targetService.ExitCode}");

                    Console.WriteLine($"The process {processName} was started at {Options.ServiceUrl}");

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

                    Console.WriteLine($"The process {processName} was stopped");
                }
            }

            if (response == null)
            {
                Console.WriteLine("Failed to get introspection response");
                return 100;
            }

            string sdl = ConvertIntrospectionResponseToSDL(response);

            if (string.IsNullOrEmpty(sdl))
            {
                Console.WriteLine("Failed to generate SDL");
                return 200;
            }

            File.WriteAllText(Options.GeneratedFileName, sdl);
            
            Console.WriteLine($"SDL was successfully written to {Options.GeneratedFileName}, generation completed in {DateTime.Now - Program.Start:hh\\:mm\\:ss\\.ff} sec.");

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
                    Console.WriteLine($"Sending introspection request to {serviceUrl}");

                    try
                    {
                        try
                        {
                            response = client.SendQueryAsync(IntrospectionQuery.Modern).GetAwaiter().GetResult();
                            if (response != null)
                                Console.WriteLine($"Received modern introspection response from {serviceUrl}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to send modern introspection request with directives: {ex.Message}");
                        }

                        if (response == null || response.Errors?.Any() == true)
                        {
                            if (response != null)
                            {
                                Console.WriteLine("Modern introspection request with directives contains errors:");
                                foreach (var error in response.Errors)
                                    Console.WriteLine(error.Message);
                            }

                            Console.WriteLine("Fallback to classic introspection request without directives");
                            response = client.SendQueryAsync(IntrospectionQuery.Classic).GetAwaiter().GetResult();
                            if (response != null)
                                Console.WriteLine($"Received classic introspection response from {serviceUrl}");
                        }

                        return response;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"An error occurred while executing an HTTP request ({retry}): {e.Message}");

                        if (Options.FromURL)
                            return null;

                        Console.WriteLine($"Make sure that it is possible to start the process at {serviceUrl} and the required port is not used by another process. Perhaps the process has not yet started and cannot serve the request.");

                        if (retry == MAX_RETRY)
                        {
                            Console.WriteLine($"Failed to load data from {serviceUrl} for {MAX_RETRY} attempts");
                            return null; // that's enough
                        }

                        ++retry;
                        Console.WriteLine($"Wait 2 seconds and try {retry} from {MAX_RETRY}.");
                        Thread.Sleep(2000);
                    }
                }
            }
        }

        private string ConvertIntrospectionResponseToSDL(GraphQLResponse response)
        {
            if (response.Errors?.Any() == true)
            {
                Console.WriteLine("Introspection response contains errors:");
                foreach (var error in response.Errors)
                    Console.WriteLine(error.Message);

                return null;
            }

            var data = response.Data["__schema"];
            var schema = data.ToObject<GraphQLSchema>();

            if (Options.Verbose)
            {
                Console.WriteLine($"Introspection response contains {schema.Types?.Count() ?? 0} types and {schema.Directives?.Count ?? 0} directives:");
                Console.WriteLine(data.ToString(Newtonsoft.Json.Formatting.Indented));
                Console.WriteLine("Starting transformation from introspection response (json) to SDL.");
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
                Console.WriteLine("SDL generated successfully:");
                Console.WriteLine(sdl);
            }

            return sdl;
        }
    }
}
