using GraphQL.IntrospectionModel;
using Shouldly;
using Xunit;

namespace GraphQL.SDLExporter.Tests;

/// <summary>
/// Tests for <see cref="SDLWriter"/>.
/// </summary>
public class SDLWriterTests
{
    /// <summary>
    /// Basic export test 1.
    /// </summary>
    [Fact(Skip = "swapi-graphql.netlify.com is gone")]
    public void Export_Should_Work_1()
    {
        const string name = "swapi.graphql";
        var writer = new SDLWriter
        {
            Options = new CommandLineOptions
            {
                Source = "http://swapi-graphql.netlify.com/.netlify/functions/index",
                IncludeDescriptions = true,
                GeneratedFileName = name,
                // swapi-graphql.netlify.com does not support __Schema.description field!
                ConfigureIntrospectionQuery = query => query.Replace("__schema {\n      description", "__schema {"),
            }
        };

        writer.Execute().ShouldBe(0);
        File.Exists(name).ShouldBeTrue();
        File.ReadAllText(name).ShouldMatchApproved(opt => opt.SubFolder("Examples").NoDiff().WithFilenameGenerator((_, _, fileType, _) => $"swapi.{fileType}.graphql"));
    }

    /// <summary>
    /// Basic export test 2.
    /// </summary>
    [Fact]
    public void Export_Should_Work_2()
    {
        const string name = "hivdb.graphql";
        var writer = new SDLWriter
        {
            Options = new CommandLineOptions
            {
                Source = "https://hivdb.stanford.edu/graphql?someparam=abc", // query string passed
                IncludeDescriptions = true,
                GeneratedFileName = name,
                // hivdb.stanford.edu does not support __Schema.description field!
                ConfigureIntrospectionQuery = query => query.Replace("__schema {\n      description", "__schema {"),
                Timeout = 10,
            }
        };

        writer.Execute().ShouldBe(0);
        File.Exists(name).ShouldBeTrue();
        File.ReadAllText(name).ShouldMatchApproved(opt => opt.SubFolder("Examples").NoDiff().WithFilenameGenerator((_, _, fileType, _) => $"hivdb.{fileType}.graphql"));
    }

    /// <summary>
    /// Basic export test 3.
    /// </summary>
    [Fact]
    public void Export_Should_Work_3()
    {
        const string name = "countries.trevorblades.graphql";
        var writer = new SDLWriter
        {
            Options = new CommandLineOptions
            {
                Source = "http://countries.trevorblades.com/",
                IncludeDescriptions = true,
                GeneratedFileName = name,
                // countries.trevorblades.com does not support __Schema.description field and hangs!
                ConfigureIntrospectionQuery = query => IntrospectionQuery.Classic,
            }
        };

        writer.Execute().ShouldBe(0);
        File.Exists(name).ShouldBeTrue();
        File.ReadAllText(name).ShouldMatchApproved(opt => opt.SubFolder("Examples").NoDiff().WithFilenameGenerator((_, _, fileType, _) => $"countries.trevorblades.{fileType}.graphql"));
    }

    /// <summary>
    /// Basic export test 4.
    /// </summary>
    [SkipOnCI]
    public void Export_Should_Work_4()
    {
        const string name = "github.graphql";
        var writer = new SDLWriter
        {
            Options = new CommandLineOptions
            {
                Source = "https://api.github.com/graphql",
                IncludeDescriptions = true,
                GeneratedFileName = name,
                // github.com does not support __Schema.description field!
                ConfigureIntrospectionQuery = query => query.Replace("__schema {\n      description", "__schema {"),
                Authentication = "..//..//..//..//..//.githubtoken",
                Timeout = 20,
            }
        };

        writer.Execute().ShouldBe(0);
        File.Exists(name).ShouldBeTrue();
        File.ReadAllText(name).ShouldMatchApproved(opt => opt.SubFolder("Examples").NoDiff().WithFilenameGenerator((_, _, fileType, _) => $"github.{fileType}.graphql"));
    }

    private sealed class SkipOnCIAttribute : FactAttribute
    {
        public SkipOnCIAttribute()
        {
            if (Environment.GetEnvironmentVariable("CI") == "true")
                Skip = "Skipped on CI: add your GitHub personal access token (PAT) in .githubtoken and run test locally.";
        }
    }
}
