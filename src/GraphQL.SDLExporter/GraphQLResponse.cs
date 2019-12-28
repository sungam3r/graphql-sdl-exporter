using Newtonsoft.Json.Linq;

namespace GraphQL.SDLExporter
{
    internal sealed class GraphQLResponse
    {
        public JObject Data { get; set; }

        public GraphQLError[] Errors { get; set; }
    }

    internal sealed class GraphQLError
    {
        public string Message { get; set; }
    }
}
