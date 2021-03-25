using System.IO;
using Shouldly;
using Xunit;

namespace GraphQL.SDLExporter.Tests
{
    /// <summary>
    /// Tests for <see cref="SDLWriter"/>.
    /// </summary>
    public class SDLWriterTests
    {
        /// <summary>
        /// Basic export test.
        /// </summary>
        [Fact]
        public void Export_Should_Work()
        {
            var writer = new SDLWriter
            {
                Options = new CommandLineOptions
                {
                    Source = "https://swapi-graphql.netlify.com/.netlify/functions/index",
                    IncludeDescriptions = true,
                    GeneratedFileName = "swapi-generated.graphql",
                    // swapi-graphql.netlify.com does not support __Schema.description field!
                    ConfigureIntrospectionQuery = query => query.Replace("__schema {\n      description", "__schema {"),
                }
            };

            writer.Execute().ShouldBe(0);
            File.Exists("swapi-generated.graphql").ShouldBeTrue();
        }
    }
}
