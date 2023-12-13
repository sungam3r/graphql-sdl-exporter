#if NET8_0

using PublicApiGenerator;
using Shouldly;
using Xunit;

namespace GraphQL.SDLExporter.Tests.Approval;

/// <summary> Tests for checking changes to the public API. </summary>
public class ApiApprovalTests
{
    /// <summary> Check for changes to the public APIs. </summary>
    /// <param name="type"> The type used as a marker for the assembly whose public API change you want to check. </param>
    [Theory]
    [InlineData(typeof(CommandLineOptions))]
    public void Public_Api_Should_Not_Change_Unexpectedly(Type type)
    {
        string publicApi = type.Assembly.GeneratePublicApi(new ApiGeneratorOptions
        {
            IncludeAssemblyAttributes = false,
            AllowNamespacePrefixes = ["Microsoft.Extensions.DependencyInjection"],
            ExcludeAttributes = ["System.Diagnostics.DebuggerDisplayAttribute"],
        });

        publicApi.ShouldMatchApproved(options => options!.WithFilenameGenerator((testMethodInfo, discriminator, fileType, fileExtension) => $"{type.Assembly.GetName().Name!}.{fileType}.{fileExtension}"));
    }
}

#endif
