using System.Net;
using System.Text;
using Jellyfin.Plugin.DoesTheDogDie.Api;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests.Api;

public class DtddApiClientTests
{
    private readonly Mock<ILogger<DtddApiClient>> _loggerMock;
    private readonly string _testDataPath;

    public DtddApiClientTests()
    {
        _loggerMock = new Mock<ILogger<DtddApiClient>>();
        _testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData");
    }

    private DtddApiClient CreateClient(MockHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(f => f.CreateClient(Constants.HttpClientName))
            .Returns(httpClient);

        return new DtddApiClient(httpClientFactory.Object, _loggerMock.Object);
    }

    private string LoadTestData(string filename)
    {
        return File.ReadAllText(Path.Combine(_testDataPath, filename));
    }

    #region SearchByImdbIdAsync Tests

    [Fact]
    public async Task SearchByImdbIdAsync_ValidId_ReturnsResults()
    {
        // Arrange
        var responseJson = LoadTestData("search-imdb-success.json");
        var handler = new MockHttpMessageHandler(responseJson, "application/json");
        var client = CreateClient(handler);

        // Act
        var result = await client.SearchByImdbIdAsync("tt2911666");

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal(15713, result.Items[0].Id);
        Assert.Equal("John Wick", result.Items[0].Name);
        Assert.Equal("tt2911666", result.Items[0].ImdbId);
        Assert.Contains("dddsearch?imdb=tt2911666", handler.LastRequestUri?.ToString());
    }

    [Fact]
    public async Task SearchByImdbIdAsync_InvalidId_ReturnsEmptyResults()
    {
        // Arrange
        var responseJson = LoadTestData("search-imdb-empty.json");
        var handler = new MockHttpMessageHandler(responseJson, "application/json");
        var client = CreateClient(handler);

        // Act
        var result = await client.SearchByImdbIdAsync("tt9999999");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Items);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchByImdbIdAsync_NullOrEmpty_ReturnsNull(string? imdbId)
    {
        // Arrange
        var handler = new MockHttpMessageHandler("{}", "application/json");
        var client = CreateClient(handler);

        // Act
        var result = await client.SearchByImdbIdAsync(imdbId!);

        // Assert
        Assert.Null(result);
        Assert.Null(handler.LastRequestUri); // No request should be made
    }

    [Fact]
    public async Task SearchByImdbIdAsync_HttpError_ReturnsNull()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.InternalServerError);
        var client = CreateClient(handler);

        // Act
        var result = await client.SearchByImdbIdAsync("tt2911666");

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region SearchByTitleAsync Tests

    [Fact]
    public async Task SearchByTitleAsync_ValidTitle_ReturnsResults()
    {
        // Arrange
        var responseJson = LoadTestData("search-imdb-success.json");
        var handler = new MockHttpMessageHandler(responseJson, "application/json");
        var client = CreateClient(handler);

        // Act
        var result = await client.SearchByTitleAsync("John Wick");

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal("John Wick", result.Items[0].Name);
        var requestUrl = handler.LastRequestUri?.ToString();
        Assert.NotNull(requestUrl);
        Assert.Contains("dddsearch?q=", requestUrl);
        Assert.Contains("John", requestUrl);
        Assert.Contains("Wick", requestUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchByTitleAsync_NullOrEmpty_ReturnsNull(string? title)
    {
        // Arrange
        var handler = new MockHttpMessageHandler("{}", "application/json");
        var client = CreateClient(handler);

        // Act
        var result = await client.SearchByTitleAsync(title!);

        // Assert
        Assert.Null(result);
        Assert.Null(handler.LastRequestUri);
    }

    #endregion

    #region GetMediaDetailsAsync Tests

    [Fact]
    public async Task GetMediaDetailsAsync_ValidId_ReturnsDetails()
    {
        // Arrange
        var responseJson = LoadTestData("media-detail-success.json");
        var handler = new MockHttpMessageHandler(responseJson, "application/json");
        var client = CreateClient(handler);

        // Act
        var result = await client.GetMediaDetailsAsync(15713);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(15713, result.Item.Id);
        Assert.Equal("John Wick", result.Item.Name);
        Assert.Equal(3, result.TopicItemStats.Count);
        Assert.Contains("media/15713", handler.LastRequestUri?.ToString());
    }

    [Fact]
    public async Task GetMediaDetailsAsync_ValidId_ParsesTriggersCorrectly()
    {
        // Arrange
        var responseJson = LoadTestData("media-detail-success.json");
        var handler = new MockHttpMessageHandler(responseJson, "application/json");
        var client = CreateClient(handler);

        // Act
        var result = await client.GetMediaDetailsAsync(15713);

        // Assert
        Assert.NotNull(result);

        var dogDiesTrigger = result.TopicItemStats.FirstOrDefault(t => t.TopicId == 153);
        Assert.NotNull(dogDiesTrigger);
        Assert.Equal(1336, dogDiesTrigger.YesSum);
        Assert.Equal(118, dogDiesTrigger.NoSum);
        Assert.True(dogDiesTrigger.IsPositive);
        Assert.Equal("a dog dies", dogDiesTrigger.Topic?.Name);
        Assert.Equal("Animal", dogDiesTrigger.TopicCategory?.Name);
    }

    [Fact]
    public async Task GetMediaDetailsAsync_ValidId_CalculatesConfidenceCorrectly()
    {
        // Arrange
        var responseJson = LoadTestData("media-detail-success.json");
        var handler = new MockHttpMessageHandler(responseJson, "application/json");
        var client = CreateClient(handler);

        // Act
        var result = await client.GetMediaDetailsAsync(15713);

        // Assert
        Assert.NotNull(result);

        var dogDiesTrigger = result.TopicItemStats.First(t => t.TopicId == 153);
        var expectedConfidence = (double)1336 / (1336 + 118) * 100;
        Assert.Equal(expectedConfidence, dogDiesTrigger.Confidence, precision: 2);
    }

    [Fact]
    public async Task GetMediaDetailsAsync_InvalidId_Html404_ReturnsNull()
    {
        // Arrange
        var responseHtml = LoadTestData("media-detail-404.html");
        var handler = new MockHttpMessageHandler(responseHtml, "text/html", HttpStatusCode.OK);
        var client = CreateClient(handler);

        // Act
        var result = await client.GetMediaDetailsAsync(99999999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetMediaDetailsAsync_HttpError_ReturnsNull()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.NotFound);
        var client = CreateClient(handler);

        // Act
        var result = await client.GetMediaDetailsAsync(99999999);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region GetMediaDetailsByImdbIdAsync Tests

    [Fact]
    public async Task GetMediaDetailsByImdbIdAsync_Found_ReturnsDetails()
    {
        // Arrange
        var searchJson = LoadTestData("search-imdb-success.json");
        var detailJson = LoadTestData("media-detail-success.json");

        var handler = new MockHttpMessageHandler(new Dictionary<string, (string Content, string ContentType)>
        {
            { "dddsearch", (searchJson, "application/json") },
            { "media/15713", (detailJson, "application/json") }
        });
        var client = CreateClient(handler);

        // Act
        var result = await client.GetMediaDetailsByImdbIdAsync("tt2911666");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(15713, result.Item.Id);
        Assert.Equal("John Wick", result.Item.Name);
    }

    [Fact]
    public async Task GetMediaDetailsByImdbIdAsync_NotFound_ReturnsNull()
    {
        // Arrange
        var searchJson = LoadTestData("search-imdb-empty.json");
        var handler = new MockHttpMessageHandler(searchJson, "application/json");
        var client = CreateClient(handler);

        // Act
        var result = await client.GetMediaDetailsByImdbIdAsync("tt9999999");

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region GetPositiveTriggers / GetNegativeTriggers Tests

    [Fact]
    public async Task GetPositiveTriggers_ReturnsOnlyPositive()
    {
        // Arrange
        var responseJson = LoadTestData("media-detail-success.json");
        var handler = new MockHttpMessageHandler(responseJson, "application/json");
        var client = CreateClient(handler);

        // Act
        var result = await client.GetMediaDetailsAsync(15713);
        var positiveTriggers = result!.GetPositiveTriggers().ToList();

        // Assert
        Assert.Equal(2, positiveTriggers.Count);
        Assert.All(positiveTriggers, t => Assert.True(t.IsPositive));
        Assert.Contains(positiveTriggers, t => t.Topic?.Name == "a dog dies");
        Assert.Contains(positiveTriggers, t => t.Topic?.Name == "an animal is abused");
    }

    [Fact]
    public async Task GetNegativeTriggers_ReturnsOnlyNegative()
    {
        // Arrange
        var responseJson = LoadTestData("media-detail-success.json");
        var handler = new MockHttpMessageHandler(responseJson, "application/json");
        var client = CreateClient(handler);

        // Act
        var result = await client.GetMediaDetailsAsync(15713);
        var negativeTriggers = result!.GetNegativeTriggers().ToList();

        // Assert
        Assert.Single(negativeTriggers);
        Assert.All(negativeTriggers, t => Assert.False(t.IsPositive));
        Assert.Contains(negativeTriggers, t => t.Topic?.Name == "a negative trigger");
    }

    [Fact]
    public async Task GetPositiveTriggers_WithMinVotes_FiltersCorrectly()
    {
        // Arrange
        var responseJson = LoadTestData("media-detail-success.json");
        var handler = new MockHttpMessageHandler(responseJson, "application/json");
        var client = CreateClient(handler);

        // Act
        var result = await client.GetMediaDetailsAsync(15713);
        var positiveTriggers = result!.GetPositiveTriggers(minVotes: 500).ToList();

        // Assert
        Assert.Single(positiveTriggers);
        Assert.Equal("a dog dies", positiveTriggers[0].Topic?.Name);
    }

    #endregion
}

/// <summary>
/// Mock HTTP message handler for testing.
/// </summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly string _responseContent;
    private readonly string _contentType;
    private readonly HttpStatusCode _statusCode;
    private readonly Dictionary<string, (string Content, string ContentType)>? _routedResponses;

    public Uri? LastRequestUri { get; private set; }

    public MockHttpMessageHandler(string responseContent, string contentType, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _responseContent = responseContent;
        _contentType = contentType;
        _statusCode = statusCode;
        _routedResponses = null;
    }

    public MockHttpMessageHandler(HttpStatusCode statusCode)
    {
        _responseContent = string.Empty;
        _contentType = "application/json";
        _statusCode = statusCode;
        _routedResponses = null;
    }

    public MockHttpMessageHandler(Dictionary<string, (string Content, string ContentType)> routedResponses)
    {
        _responseContent = string.Empty;
        _contentType = "application/json";
        _statusCode = HttpStatusCode.OK;
        _routedResponses = routedResponses;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;

        string content;
        string contentType;
        HttpStatusCode statusCode;

        if (_routedResponses != null && request.RequestUri != null)
        {
            var matchingRoute = _routedResponses.Keys.FirstOrDefault(k => request.RequestUri.ToString().Contains(k));
            if (matchingRoute != null)
            {
                var response = _routedResponses[matchingRoute];
                content = response.Content;
                contentType = response.ContentType;
                statusCode = HttpStatusCode.OK;
            }
            else
            {
                content = "{}";
                contentType = "application/json";
                statusCode = HttpStatusCode.NotFound;
            }
        }
        else
        {
            content = _responseContent;
            contentType = _contentType;
            statusCode = _statusCode;
        }

        var httpResponse = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, contentType)
        };

        return Task.FromResult(httpResponse);
    }
}
