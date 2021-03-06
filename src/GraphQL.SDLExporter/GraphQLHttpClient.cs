using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace GraphQL.SDLExporter
{
    internal sealed class GraphQLHttpClient : IDisposable
    {
        private readonly HttpClient _client = new HttpClient();
        private readonly Uri _endPoint;

        public GraphQLHttpClient(string endPoint, string authentication)
        {
            _endPoint = new Uri(endPoint);

            // UserAgent is always filled, some APIs require it to be specified (https://developer.github.com/v3/#user-agent-required)
            var asmName = Assembly.GetExecutingAssembly().GetName();
            _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(asmName.Name, asmName.Version.ToString()));

            if (!string.IsNullOrEmpty(authentication))
            {
                string[] parts = authentication.Split('|');
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(parts[0], parts[1]);
            }
        }

        public void Dispose() => _client.Dispose();

        public async Task<GraphQLResponse> SendQueryAsync(string query, string operationName)
        {
            using (var httpContent = new StringContent(JsonConvert.SerializeObject(new { query, operationName }), Encoding.UTF8, "application/json"))
            {
                using (var postResponse = await _client.PostAsync(_endPoint, httpContent))
                {
                    ColoredConsole.WriteInfo($"POST request to {_endPoint} returned {(int)postResponse.StatusCode} ({postResponse.StatusCode})");
                    PrintHeaders(postResponse);

                    if (postResponse.StatusCode == HttpStatusCode.MethodNotAllowed)
                    {
                        ColoredConsole.WriteInfo("Switching to GET method");

                        // execute GET if POST not allowed
                        using (var getResponse = await _client.GetAsync($"{_endPoint}?query={query}"))
                        {
                            ColoredConsole.WriteInfo($"GET request to {_endPoint} returned {(int)postResponse.StatusCode} ({getResponse.StatusCode})");
                            PrintHeaders(getResponse);

                            return await ReadHttpResponseMessageAsync(getResponse);
                        }
                    }

                    return await ReadHttpResponseMessageAsync(postResponse);
                }
            }
        }

        private async Task<GraphQLResponse> ReadHttpResponseMessageAsync(HttpResponseMessage httpResponseMessage)
        {
            string content = await httpResponseMessage.Content.ReadAsStringAsync();

            if (!httpResponseMessage.IsSuccessStatusCode)
                ColoredConsole.WriteWarning($"Server returned HTTP response code {(int)httpResponseMessage.StatusCode} ({httpResponseMessage.StatusCode}){(string.IsNullOrEmpty(content) ? " with empty body" : ": " + content)}");

            return JsonConvert.DeserializeObject<GraphQLResponse>(content, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });
        }

        private void PrintHeaders(HttpResponseMessage response)
        {
            ColoredConsole.WriteInfo("Response headers:");
            foreach (var header in response.Headers)
                ColoredConsole.WriteInfo($"  {header.Key}: {string.Join(";", header.Value)}");
        }
    }
}
