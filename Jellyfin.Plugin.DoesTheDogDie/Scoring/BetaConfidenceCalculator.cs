using System;

namespace Jellyfin.Plugin.DoesTheDogDie.Scoring;

/// <summary>
/// Calculates statistical confidence that a trigger applies using the
/// Wilson score interval lower bound.
/// </summary>
public static class BetaConfidenceCalculator
{
    /// <summary>
    /// Z-score for a 95% confidence interval.
    /// </summary>
    private const double Z = 1.96;

    /// <summary>
    /// Calculates the lower bound of the 95% Wilson score confidence interval
    /// for the proportion of positive votes.
    /// </summary>
    /// <param name="positiveVotes">Number of votes agreeing the trigger applies.</param>
    /// <param name="totalVotes">Total number of votes cast.</param>
    /// <returns>Confidence score between 0.0 and 1.0.</returns>
    public static double CalculateConfidence(int positiveVotes, int totalVotes)
    {
        if (totalVotes <= 0 || positiveVotes <= 0)
        {
            return 0.0;
        }

        var n = (double)totalVotes;
        var p = Math.Min(positiveVotes, totalVotes) / n;
        var zSquared = Z * Z;

        var numerator = p + (zSquared / (2 * n))
            - (Z * Math.Sqrt(((p * (1 - p)) + (zSquared / (4 * n))) / n));
        var lowerBound = numerator / (1 + (zSquared / n));

        return Math.Clamp(lowerBound, 0.0, 1.0);
    }
}
