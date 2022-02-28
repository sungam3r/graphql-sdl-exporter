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
        /// Basic export test 1.
        /// </summary>
        [Fact(Skip = "Weird errors https://github.com/sungam3r/graphql-sdl-exporter/pull/43")]
        public void Export_Should_Work_1()
        {
            var writer = new SDLWriter
            {
                Options = new CommandLineOptions
                {
                    Source = "http://swapi-graphql.netlify.com/.netlify/functions/index",
                    IncludeDescriptions = true,
                    GeneratedFileName = "swapi-generated.graphql",
                    // swapi-graphql.netlify.com does not support __Schema.description field!
                    ConfigureIntrospectionQuery = query => query.Replace("__schema {\n      description", "__schema {"),
                }
            };

            writer.Execute().ShouldBe(0);
            File.Exists("swapi-generated.graphql").ShouldBeTrue();
            File.ReadAllText("swapi-generated.graphql").ShouldMatchApproved(opt => opt.WithFilenameGenerator((_, __, ___, ____) => "swapi-generated.approved.graphql"));
        }

        /// <summary>
        /// Basic export test 2.
        /// </summary>
        [Fact]
        public void Export_Should_Work_2()
        {
            var writer = new SDLWriter
            {
                Options = new CommandLineOptions
                {
                    Source = "https://hivdb.stanford.edu/graphql?someparam=abc", // query string passed
                    IncludeDescriptions = true,
                    GeneratedFileName = "hivdb-generated.graphql",
                    // hivdb.stanford.edu does not support __Schema.description field!
                    ConfigureIntrospectionQuery = query => query.Replace("__schema {\n      description", "__schema {"),
                }
            };

            writer.Execute().ShouldBe(0);
            File.Exists("hivdb-generated.graphql").ShouldBeTrue();
            File.ReadAllText("hivdb-generated.graphql").ShouldMatchApproved(opt => opt.WithFilenameGenerator((_, __, ___, ____) => "hivdb-generated.approved.graphql"));
        }

        /// <summary>
        /// Basic export test 3.
        /// </summary>
        [Fact]
        public void Export_Should_Work_3()
        {
            var writer = new SDLWriter
            {
                Options = new CommandLineOptions
                {
                    Source = "http://countries.trevorblades.com/",
                    IncludeDescriptions = true,
                    GeneratedFileName = "countries.trevorblades.graphql",
                    // countries.trevorblades.com does not support __Schema.description field!
                    ConfigureIntrospectionQuery = query => query.Replace("__schema {\n      description", "__schema {"),
                }
            };

            writer.Execute().ShouldBe(0);
            File.Exists("countries.trevorblades.graphql").ShouldBeTrue();
            File.ReadAllText("countries.trevorblades.graphql").ShouldMatchApproved(opt => opt.WithFilenameGenerator((_, __, ___, ____) => "countries.trevorblades.approved.graphql"));
        }

        /// <summary>
        /// Basic export test 4.
        /// </summary>
        [Fact(Skip = "Add your auth token in .githubtoken.txt and run manually")]
        public void Export_Should_Work_4()
        {
            var name = "github.graphql";
            var writer = new SDLWriter
            {
                Options = new CommandLineOptions
                {
                    Source = "https://api.github.com/graphql",
                    IncludeDescriptions = true,
                    GeneratedFileName = name,
                    // github.com does not support __Schema.description field!
                    ConfigureIntrospectionQuery = query => query.Replace("__schema {\n      description", "__schema {"),
                    Authentication = "..//..//..//..//..//.githubtoken.txt"
                }
            };

            writer.Execute().ShouldBe(0);
            File.Exists(name).ShouldBeTrue();
            File.ReadAllText(name).ShouldMatchApproved(opt => opt.WithFilenameGenerator((_, __, ___, ____) => "github.approved.graphql"));
        }
    }
}
