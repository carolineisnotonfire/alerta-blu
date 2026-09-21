using AlertaBlu.Domain;
using Xunit;

namespace AlertaBlu.Tests;

public class AlertPolicyTests
{
    private static readonly RiverThreshold[] Bands =
    [
        new RiverThreshold { Label = "Normalidade", MinMeters = 0, MaxMeters = 3.0 },
        new RiverThreshold { Label = "Atenção", MinMeters = 3.0, MaxMeters = 4.5 },
        new RiverThreshold { Label = "Alerta", MinMeters = 4.5 },
    ];

    [Fact]
    public void Evaluate_Should_IdentifyTheBandContainingTheCurrentReading()
    {
        // Arrange
        var current = new RiverLevel { LevelMeters = 3.5 };

        // Act
        var evaluation = AlertPolicy.Evaluate(current, Bands, previousBand: null, watchedStreets: []);

        // Assert
        Assert.Equal("Atenção", evaluation.CurrentBand!.Label);
    }

    [Fact]
    public void Evaluate_Should_NotFlagEscalation_When_ThereIsNoPreviousBandToCompareAgainst()
    {
        // Arrange: first-ever evaluation this session, even if the river is already at Alerta.
        var current = new RiverLevel { LevelMeters = 5.0 };

        // Act
        var evaluation = AlertPolicy.Evaluate(current, Bands, previousBand: null, watchedStreets: []);

        // Assert
        Assert.False(evaluation.IsEscalation);
        Assert.False(evaluation.RequiresAlert);
    }

    [Fact]
    public void Evaluate_Should_FlagEscalation_When_TheBandIsHigherThanBefore()
    {
        // Arrange
        var current = new RiverLevel { LevelMeters = 3.2 };
        var previous = Bands[0]; // Normalidade

        // Act
        var evaluation = AlertPolicy.Evaluate(current, Bands, previous, watchedStreets: []);

        // Assert
        Assert.True(evaluation.IsEscalation);
        Assert.True(evaluation.RequiresAlert);
    }

    [Fact]
    public void Evaluate_Should_NotFlagEscalation_When_TheBandIsTheSameAsBefore()
    {
        // Arrange
        var current = new RiverLevel { LevelMeters = 3.2 };
        var previous = Bands[1]; // Atenção, same band the reading falls in now

        // Act
        var evaluation = AlertPolicy.Evaluate(current, Bands, previous, watchedStreets: []);

        // Assert
        Assert.False(evaluation.IsEscalation);
    }

    [Fact]
    public void Evaluate_Should_NotFlagEscalation_When_TheLevelFell()
    {
        // Arrange
        var current = new RiverLevel { LevelMeters = 1.0 };
        var previous = Bands[2]; // was Alerta, now back to Normalidade

        // Act
        var evaluation = AlertPolicy.Evaluate(current, Bands, previous, watchedStreets: []);

        // Assert: a falling level is good news, not an alert.
        Assert.False(evaluation.IsEscalation);
        Assert.False(evaluation.RequiresAlert);
    }

    [Fact]
    public void Evaluate_Should_ListWatchedStreets_WhoseThresholdTheCurrentLevelHasReached()
    {
        // Arrange
        var current = new RiverLevel { LevelMeters = 7.40 };
        var watched = new[]
        {
            new CotaEnchente { Logradouro = "Rua Sao Rafael", CotaMeters = 7.40 },
            new CotaEnchente { Logradouro = "Rua Alta", CotaMeters = 10.00 },
        };

        // Act
        var evaluation = AlertPolicy.Evaluate(current, Bands, previousBand: null, watched);

        // Assert: 7,40 reaches the first street's threshold but not the second's.
        Assert.Single(evaluation.AffectedStreets);
        Assert.Equal("Rua Sao Rafael", evaluation.AffectedStreets[0].Logradouro);
        Assert.True(evaluation.RequiresAlert);
    }

    [Fact]
    public void Evaluate_Should_IgnoreWatchedStreets_WithNoPublishedThreshold()
    {
        // Arrange
        var current = new RiverLevel { LevelMeters = 100.0 };
        var watched = new[] { new CotaEnchente { Logradouro = "Rua Sem Cota", CotaMeters = null } };

        // Act
        var evaluation = AlertPolicy.Evaluate(current, Bands, previousBand: null, watched);

        // Assert
        Assert.Empty(evaluation.AffectedStreets);
    }

    [Fact]
    public void Evaluate_Should_LeaveCurrentBandNull_When_NoBandContainsTheReading()
    {
        // Arrange: a negative/re-baselined reading below every published band.
        var current = new RiverLevel { LevelMeters = -1.0 };

        // Act
        var evaluation = AlertPolicy.Evaluate(current, Bands, previousBand: null, watchedStreets: []);

        // Assert
        Assert.Null(evaluation.CurrentBand);
    }
}
