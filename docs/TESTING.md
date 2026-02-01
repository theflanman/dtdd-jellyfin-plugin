# DoesTheDogDie Plugin - Testing Guide

## Test Framework

| Component | Package | Version |
|-----------|---------|---------|
| Test Framework | xUnit | 2.9.2 |
| Mocking | Moq | 4.20.72 |
| Coverage | coverlet.collector | 6.0.2 |

---

## Running Tests

### Basic Test Run
```bash
dotnet test
```

### With Coverage Collection
```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Verbose Output
```bash
dotnet test --verbosity normal
```

---

## Test Organization

```
tests/Jellyfin.Plugin.DoesTheDogDie.Tests/
├── Api/
│   └── DtddApiClientTests.cs       # API client unit tests (20 tests)
├── Providers/
│   ├── DtddMovieProviderTests.cs   # Movie provider tests (12 tests)
│   ├── DtddSeriesProviderTests.cs  # Series provider tests (10 tests)
│   ├── DtddSeasonProviderTests.cs  # Season provider tests (6 tests)
│   └── DtddEpisodeProviderTests.cs # Episode provider tests (6 tests)
└── TestData/
    └── *.json                       # Mock API response files
```

---

## Test Data Location

Real API responses are stored for reference:

```
tests/api-exploration/results/
├── search-title.json      # Search by title response
├── search-imdb.json       # Search by IMDB ID response
└── media-detail-johnwick.json  # Full media details with triggers
```

These can be used to verify mock data accuracy.

---

## Coverage

### Collecting Coverage
```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

### Generating HTML Report

First install ReportGenerator:
```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
```

Then generate report:
```bash
reportgenerator \
  -reports:"TestResults/**/coverage.cobertura.xml" \
  -targetdir:"TestResults/CoverageReport" \
  -reporttypes:Html
```

Open `TestResults/CoverageReport/index.html` in browser.

### Coverage Targets

| Component | Target | Notes |
|-----------|--------|-------|
| API Client | 80%+ | Core functionality |
| Providers | 70%+ | Some paths require Jellyfin runtime |
| Bootstrap (Plugin, ServiceRegistrator) | Excluded | Can't be unit tested |

---

## Known Limitations

### 1. Parent Entity Relationships

Season and Episode providers get IMDB ID from parent Series:
```csharp
var series = item.Series;  // Always null in unit tests
var imdbId = series.GetProviderId(MetadataProvider.Imdb);
```

The `Series` property can't be set in unit tests because Jellyfin doesn't expose a setter. Only the "no parent series" path is testable.

**Impact:** ~44% coverage on Season/Episode providers

### 2. Plugin Bootstrap Code

`Plugin.cs` and `PluginServiceRegistrator.cs` require Jellyfin runtime to test. These are marked with `[ExcludeFromCodeCoverage]`.

### 3. Integration Testing

No automated integration tests against a real Jellyfin instance. Manual testing required for:
- Plugin installation
- Configuration page
- Tag application
- External URL display

---

## Mutation Testing (Optional)

For additional test quality assurance, use Stryker.NET:

```bash
# Install
dotnet tool install -g dotnet-stryker

# Run mutation testing
cd tests/Jellyfin.Plugin.DoesTheDogDie.Tests
dotnet stryker
```

Mutation testing verifies that tests actually catch bugs by introducing small changes to the code and ensuring tests fail.

---

## Writing New Tests

### Test Pattern

```csharp
public class MyClassTests
{
    private readonly Mock<IDependency> _dependencyMock;
    private readonly MyClass _sut;  // System Under Test

    public MyClassTests()
    {
        _dependencyMock = new Mock<IDependency>();
        _sut = new MyClass(_dependencyMock.Object);
    }

    [Fact]
    public async Task MethodName_Scenario_ExpectedResult()
    {
        // Arrange
        _dependencyMock.Setup(x => x.Method()).Returns(value);

        // Act
        var result = await _sut.MethodName();

        // Assert
        Assert.Equal(expected, result);
    }
}
```

### Mocking DtddApiClient

The API client methods are virtual for mocking:
```csharp
_apiClientMock
    .Setup(x => x.GetMediaDetailsByImdbIdAsync("tt2911666", It.IsAny<CancellationToken>()))
    .ReturnsAsync(details);
```

### Mocking Configuration

Use `IPluginConfigurationAccessor`:
```csharp
_configAccessorMock
    .Setup(x => x.GetConfiguration())
    .Returns(new PluginConfiguration { EnableMovies = true });
```
