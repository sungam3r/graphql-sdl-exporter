using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace GraphQL.SDLExporter
{
    internal sealed class GraphQLHttpClient : IDisposable
    {
        private readonly HttpClient _client;

        public GraphQLHttpClient(HttpClient client)
        {
            _client = client;
        }

        public void Dispose() => _client.Dispose();

        public async Task<GraphQLResponse?> SendQueryAsync(string requestUri, string query, string operationName)
        {
            using (var httpContent = new StringContent(JsonConvert.SerializeObject(new { query, operationName }), Encoding.UTF8, "application/json"))
            {
                using (var postResponse = await _client.PostAsync(requestUri, httpContent))
                {
                    ColoredConsole.WriteInfo($"POST request to {requestUri} returned {(int)postResponse.StatusCode} ({postResponse.StatusCode})");
                    PrintHeaders(postResponse);

                    Console.WriteLine("======TEST=====");
                    if (postResponse.StatusCode == HttpStatusCode.MethodNotAllowed)
                    {
                        ColoredConsole.WriteInfo("Switching to GET method");

                        // execute GET if POST not allowed
                        using (var getResponse = await _client.GetAsync($"{requestUri}?query={query}"))
                        {
                            ColoredConsole.WriteInfo($"GET request to {requestUri} returned {(int)postResponse.StatusCode} ({getResponse.StatusCode})");
                            PrintHeaders(getResponse);

                            return await ReadHttpResponseMessageAsync(getResponse);
                        }
                    }

                    return await ReadHttpResponseMessageAsync(postResponse);
                }
            }
        }

        private static async Task<GraphQLResponse?> ReadHttpResponseMessageAsync(HttpResponseMessage httpResponseMessage)
        {
            string content = await httpResponseMessage.Content.ReadAsStringAsync();

            Console.WriteLine("////////////////////////////");
            Console.WriteLine(content);

            if (!httpResponseMessage.IsSuccessStatusCode)
                ColoredConsole.WriteWarning($"Server returned HTTP response code {(int)httpResponseMessage.StatusCode} ({httpResponseMessage.StatusCode}){(string.IsNullOrEmpty(content) ? " with empty body" : ": " + content)}");



            return JsonConvert.DeserializeObject<GraphQLResponse>(content, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });
        }

        private static void PrintHeaders(HttpResponseMessage response)
        {
            ColoredConsole.WriteInfo("Response headers:");
            foreach (var header in response.Headers)
                ColoredConsole.WriteInfo($"  {header.Key}: {string.Join(";", header.Value)}");
        }
    }
}
