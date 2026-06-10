using Jellyfin.Plugin.DoesTheDogDie.Scoring;
using Xunit;

namespace Jellyfin.Plugin.DoesTheDogDie.Tests.Scoring;

public class BetaConfidenceCalculatorTests
{
    [Fact]
    public void CalculateConfidence_ZeroVotes_ReturnsZero()
    {
        // Act
        var result = BetaConfidenceCalculator.CalculateConfidence(0, 0);

        // Assert
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void CalculateConfidence_ZeroPositiveVotes_ReturnsZero()
    {
        // Act
        var result = BetaConfidenceCalculator.CalculateConfidence(0, 10);

        // Assert
        Assert.Equal(0.0, result);
    }

    [Theory]
    [InlineData(1, 1, 0.206543)] // Single unanimous vote - low confidence
    [InlineData(10, 10, 0.722460)] // 10 unanimous votes - high confidence
    [InlineData(100, 100, 0.963005)] // 100 unanimous votes - very high confidence
    [InlineData(50, 100, 0.403830)] // Equal split - below 50%
    [InlineData(3, 3, 0.438494)] // Default vote threshold, unanimous
    [InlineData(75, 100, 0.656954)] // 75% agreement with good sample size
    [InlineData(1336, 1454, 0.903680)] // Real-world example from DTDD
    public void CalculateConfidence_KnownValues_ReturnsWilsonLowerBound(
        int positiveVotes,
        int totalVotes,
        double expected)
    {
        // Act
        var result = BetaConfidenceCalculator.CalculateConfidence(positiveVotes, totalVotes);

        // Assert
        Assert.Equal(expected, result, precision: 6);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 10)]
    [InlineData(1000, 1000)]
    public void CalculateConfidence_AnyInput_ReturnsValueBetweenZeroAndOne(
        int positiveVotes,
        int totalVotes)
    {
        // Act
        var result = BetaConfidenceCalculator.CalculateConfidence(positiveVotes, totalVotes);

        // Assert
        Assert.InRange(result, 0.0, 1.0);
    }

    [Fact]
    public void CalculateConfidence_MoreVotesSameRatio_IncreasesConfidence()
    {
        // Arrange - same 100% yes ratio with increasing sample sizes
        var lowSample = BetaConfidenceCalculator.CalculateConfidence(2, 2);
        var midSample = BetaConfidenceCalculator.CalculateConfidence(20, 20);
        var highSample = BetaConfidenceCalculator.CalculateConfidence(200, 200);

        // Assert
        Assert.True(lowSample < midSample);
        Assert.True(midSample < highSample);
    }

    [Fact]
    public void CalculateConfidence_NegativeVotes_ReturnsZero()
    {
        // Act
        var result = BetaConfidenceCalculator.CalculateConfidence(-1, 10);

        // Assert
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void CalculateConfidence_PositiveExceedsTotal_ClampsToTotal()
    {
        // Arrange - defensive: malformed data where yes > total
        var clamped = BetaConfidenceCalculator.CalculateConfidence(15, 10);
        var unanimous = BetaConfidenceCalculator.CalculateConfidence(10, 10);

        // Assert
        Assert.Equal(unanimous, clamped, precision: 6);
    }
}
