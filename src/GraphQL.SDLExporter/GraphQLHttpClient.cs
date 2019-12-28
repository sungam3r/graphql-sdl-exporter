using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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

		public async Task<GraphQLResponse> SendQueryAsync(string query)
		{
			using (var httpContent = new StringContent(JsonConvert.SerializeObject(new { query }), Encoding.UTF8, "application/json"))
			{
				using (var postResponse = await _client.PostAsync(_endPoint, httpContent))
				{
					Console.WriteLine($"POST request to {_endPoint}: {postResponse.StatusCode}");

					if (postResponse.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed)
					{
						Console.WriteLine("Switching to GET method");

						// execute GET if POST not allowed
						using (var getResponse = await _client.GetAsync($"{_endPoint}?query={query}"))
						{
							Console.WriteLine($"GET request to {_endPoint}: {getResponse.StatusCode}");

							return await ReadHttpResponseMessageAsync(getResponse);
						}
					}

					return await ReadHttpResponseMessageAsync(postResponse);
				}
			}
		}

		public async Task<GraphQLResponse> ReadHttpResponseMessageAsync(HttpResponseMessage httpResponseMessage)
		{
			if (httpResponseMessage.StatusCode == System.Net.HttpStatusCode.NotFound)
				return null;

			var content = await httpResponseMessage.Content.ReadAsStringAsync();

			if (!httpResponseMessage.IsSuccessStatusCode)
				Console.WriteLine($"[{httpResponseMessage.StatusCode}]: {content}");

			return JsonConvert.DeserializeObject<GraphQLResponse>(content, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });
		}
	}
}
