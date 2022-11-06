using PublicApiGenerator;
using Shouldly;
using Xunit;

namespace GraphQL.SDLExporter.Tests.Approval;

/// <summary> Tests for checking changes to the public API. </summary>
/// <see href="https://github.com/JakeGinnivan/ApiApprover"/>
public class ApiApprovalTests
{
    /// <summary> Check for changes to the public APIs. </summary>
    /// <param name="type"> The type used as a marker for the assembly whose public API change you want to check. </param>
    [Theory]
    [InlineData(typeof(CommandLineOptions))]
    public void PublicApi(Type type)
    {
        string publicApi = type.Assembly.GeneratePublicApi(new ApiGeneratorOptions
        {
            IncludeAssemblyAttributes = false,
            WhitelistedNamespacePrefixes = new[] { "Microsoft.Extensions.DependencyInjection" },
            ExcludeAttributes = new[] { "System.Diagnostics.DebuggerDisplayAttribute" },
        });

        publicApi.ShouldMatchApproved(options => options!.WithFilenameGenerator((testMethodInfo, discriminator, fileType, fileExtension) => $"{type.Assembly.GetName().Name!}.{fileType}.{fileExtension}"));
    }
}
