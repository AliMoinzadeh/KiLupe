using KiLupeDemo.Models;
using KiLupeDemo.Services;
using Xunit;

namespace KiLupeDemo.Tests;

public sealed class InferenceProviderResolverTests
{
    [Theory]
    [InlineData(InferenceProviderKind.Cpu, "CPU")]
    [InlineData(InferenceProviderKind.DirectMl, "DirectML")]
    [InlineData(InferenceProviderKind.Auto, "Auto")]
    public void ProviderResolverReportsTheRequestedProvider(
        InferenceProviderKind requested,
        string expected)
    {
        Assert.Equal(expected, InferenceProviderResolver.GetRequestedName(requested));
    }
}