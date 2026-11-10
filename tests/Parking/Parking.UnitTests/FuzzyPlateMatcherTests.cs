using ParkingSystem.Parking.Application.Common.Plate;
using Xunit;

namespace ParkingSystem.Parking.UnitTests;

public sealed class FuzzyPlateMatcherTests
{
    [Theory]
    [InlineData("51F78291", "51F78291", 0)]
    [InlineData("51F78291", "51F78290", 1)]
    [InlineData("51F78291", "51F78200", 2)]
    public void ComputeLevenshteinDistance_CalculatesCorrectly(string s1, string s2, int expected)
    {
        var result = FuzzyPlateMatcher.ComputeLevenshteinDistance(s1, s2);
        Assert.Equal(expected, result);
    }

    [Theory]
    // Exact match
    [InlineData("51F-782.91", "51F78291", true)]
    // Confusable pair: 8 <-> B
    [InlineData("51F-7B2.91", "51F78291", true)]
    // Confusable pair: 1 <-> I
    [InlineData("51F-782.9I", "51F78291", true)]
    // Confusable pair: 0 <-> D
    [InlineData("51F-782.90", "51F7829D", true)]
    // Distance 1 different digit
    [InlineData("51F-782.91", "51F78292", true)]
    // Completely different plate
    [InlineData("51F-782.91", "29A11111", false)]
    public void ArePlatesSimilar_EvaluatesSimilarityWithConfusableRules(string p1, string p2, bool expected)
    {
        var result = FuzzyPlateMatcher.ArePlatesSimilar(p1, p2, maxAllowedDistance: 1);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FindBestMatch_ReturnsCorrectMatchFromCandidateList()
    {
        var targetPlate = "51F-7B2.91"; // OCR misread '8' as 'B'
        var candidates = new[]
        {
            "29A-123.45",
            "59X1-999.88",
            "51F-782.91", // target
            "43D1-555.55"
        };

        var (bestMatch, distance) = FuzzyPlateMatcher.FindBestMatch(targetPlate, candidates, maxAllowedDistance: 1);

        Assert.NotNull(bestMatch);
        Assert.Equal("51F-782.91", bestMatch);
    }
}
