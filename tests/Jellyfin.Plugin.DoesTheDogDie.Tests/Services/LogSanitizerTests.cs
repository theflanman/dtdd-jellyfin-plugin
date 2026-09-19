using Jellyfin.Plugin.DoesTheDogDie.Services;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests.Services;

public class LogSanitizerTests
{
    [Fact]
    public void Sanitize_RedactsConfiguredKey()
    {
        var result = LogSanitizer.Sanitize("request failed for key ddd_abc123", "ddd_abc123");

        Assert.Equal("request failed for key ***REDACTED***", result);
    }

    [Fact]
    public void Sanitize_RedactsKeyPatternEvenWhenConfiguredKeyIsUnknown()
    {
        var result = LogSanitizer.Sanitize("X-API-KEY: ddd_zzz999 rejected", apiKey: null);

        Assert.Equal("X-API-KEY: ***REDACTED*** rejected", result);
    }

    [Fact]
    public void Sanitize_LeavesUnrelatedTextAlone()
    {
        var result = LogSanitizer.Sanitize("item 1234 not found", "ddd_abc123");

        Assert.Equal("item 1234 not found", result);
    }

    [Fact]
    public void Sanitize_HandlesNullMessage()
    {
        Assert.Equal(string.Empty, LogSanitizer.Sanitize(null, "ddd_abc123"));
    }

    [Fact]
    public void Sanitize_IgnoresEmptyConfiguredKey()
    {
        var result = LogSanitizer.Sanitize("nothing secret here", string.Empty);

        Assert.Equal("nothing secret here", result);
    }
}
