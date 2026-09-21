using AlertaBlu.Domain;
using AlertaBlu.ViewModels;
using Xunit;

namespace AlertaBlu.Tests;

public class DamCardViewModelTests
{
    [Fact]
    public void CapacityFraction_Should_ConvertPercentToA0To1Value()
    {
        // Arrange
        var viewModel = new DamCardViewModel(new Barragem { Estacao = "Barragem Sul Ituporanga", CapacityPercent = 17.30 });

        // Assert
        Assert.Equal(0.173, viewModel.CapacityFraction, precision: 3);
    }

    [Theory]
    [InlineData(-5.0)]
    [InlineData(150.0)]
    public void CapacityFraction_Should_ClampOutOfRangeValues_So_TheProgressBarNeverBreaks(double percent)
    {
        // Arrange
        var viewModel = new DamCardViewModel(new Barragem { Estacao = "x", CapacityPercent = percent });

        // Assert
        Assert.InRange(viewModel.CapacityFraction, 0d, 1d);
    }

    [Fact]
    public void IsAlert_Should_BeDrivenByOpenGates_Not_ByCapacity()
    {
        // Arrange: high capacity but every gate closed is not the alert signal AlertaBLU cares about.
        var viewModel = new DamCardViewModel(
            new Barragem { Estacao = "x", CapacityPercent = 95, GatesOpen = 0, GatesClosed = 7 });

        // Assert
        Assert.False(viewModel.IsAlert);
        Assert.Equal("Regular", viewModel.StatusLabel);
    }

    [Fact]
    public void IsAlert_Should_BeTrue_When_AnyGateIsOpen()
    {
        // Arrange
        var viewModel = new DamCardViewModel(new Barragem { Estacao = "x", GatesOpen = 1, GatesClosed = 6 });

        // Assert
        Assert.True(viewModel.IsAlert);
        Assert.Equal("Atenção", viewModel.StatusLabel);
    }

    [Fact]
    public void GatesDisplay_Should_ReportNoInformation_When_NeitherGateCountIsPublished()
    {
        // Arrange
        var viewModel = new DamCardViewModel(new Barragem { Estacao = "x" });

        // Assert
        Assert.False(viewModel.HasGates);
        Assert.Equal("Comportas: sem informação", viewModel.GatesDisplay);
    }

    [Fact]
    public void DetailDisplay_Should_CombineCapacityAndGates()
    {
        // Arrange
        var viewModel = new DamCardViewModel(
            new Barragem { Estacao = "x", CapacityPercent = 2.90, GatesOpen = 0, GatesClosed = 7 });

        // Assert
        Assert.Equal("Volume: 2,90%  ·  Comportas abertas: 0  ·  fechadas: 7", viewModel.DetailDisplay);
    }
}
