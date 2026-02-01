using System.Text.Json;
using Jellyfin.Plugin.DoesTheDogDie.Api.Models;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests.Api.Models;

public class BooleanOrIntConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Theory]
    [InlineData("{\"verified\":true}", true)]
    [InlineData("{\"verified\":false}", false)]
    [InlineData("{\"verified\":1}", true)]
    [InlineData("{\"verified\":0}", false)]
    [InlineData("{\"verified\":42}", true)]
    public void Deserialize_HandlesAllFormats(string json, bool expected)
    {
        // Act
        var result = JsonSerializer.Deserialize<TestModel>(json, Options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expected, result.Verified);
    }

    [Fact]
    public void Serialize_WritesBooleanValue()
    {
        // Arrange
        var model = new TestModel { Verified = true };

        // Act
        var json = JsonSerializer.Serialize(model, Options);

        // Assert
        Assert.Contains("true", json);
    }

    [Fact]
    public void DtddMediaItem_DeserializesIntegerVerified()
    {
        // Arrange - JSON with integer verified fields (as returned by some API endpoints)
        var json = @"{
            ""id"": 123,
            ""name"": ""Test Movie"",
            ""verified"": 1,
            ""staffVerified"": 0,
            ""ItemTypeId"": 15,
            ""numRatings"": 100
        }";

        // Act
        var result = JsonSerializer.Deserialize<DtddMediaItem>(json, Options);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Verified);
        Assert.False(result.StaffVerified);
    }

    [Fact]
    public void DtddMediaItem_DeserializesBooleanVerified()
    {
        // Arrange - JSON with boolean verified fields
        var json = @"{
            ""id"": 123,
            ""name"": ""Test Movie"",
            ""verified"": true,
            ""staffVerified"": true,
            ""ItemTypeId"": 15,
            ""numRatings"": 100
        }";

        // Act
        var result = JsonSerializer.Deserialize<DtddMediaItem>(json, Options);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Verified);
        Assert.True(result.StaffVerified);
    }

    private class TestModel
    {
        [System.Text.Json.Serialization.JsonConverter(typeof(BooleanOrIntConverter))]
        public bool Verified { get; set; }
    }
}
