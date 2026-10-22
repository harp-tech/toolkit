using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Harp.Toolkit.Tests;

/// <summary>
/// Provides access to test assets downloaded outside the repository, such as published firmware
/// images.
/// </summary>
/// <remarks>
/// A test that needs an asset skips itself when the asset folder has not been configured, so the
/// ordinary suite runs on a machine with no downloads. Under continuous integration a missing
/// asset fails the test instead, so a misconfigured workflow cannot pass by skipping.
/// </remarks>
static class TestAssetHelper
{
    public const string SoundCardImageFw20 = "SoundCard.X-fw2.0-harp1.4-hw2.1-ass0.hex";
    public const string SoundCardImageFw22 = "SoundCard.PIC32-fw2.2-harp1.13-hw2.2-ass0.hex";

    const string AssetsVariable = "HARP_TOOLKIT_TEST_ASSETS";
    const string ContinuousIntegrationVariable = "CI";

    /// <summary>
    /// Returns the path of the specified asset, or skips the calling test when the asset
    /// is not available.
    /// </summary>
    public static string RequireAsset(string fileName)
    {
        var directory = Environment.GetEnvironmentVariable(AssetsVariable);
        if (string.IsNullOrEmpty(directory))
        {
            Unavailable($"Set {AssetsVariable} to the folder holding '{fileName}' to run this test.");
        }

        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
        {
            Unavailable($"No test asset was found at '{path}'.");
        }

        return path;
    }

    [DoesNotReturn]
    static void Unavailable(string message)
    {
        var continuousIntegration = Environment.GetEnvironmentVariable(ContinuousIntegrationVariable);
        if (string.Equals(continuousIntegration, "true", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Fail(message);
        }

        Assert.Inconclusive(message);
    }
}
