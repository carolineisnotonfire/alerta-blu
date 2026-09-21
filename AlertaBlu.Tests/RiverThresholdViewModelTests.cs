using AlertaBlu.Domain;
using AlertaBlu.ViewModels;
using Xunit;

namespace AlertaBlu.Tests;

public class RiverThresholdViewModelTests
{
    [Fact]
    public void RangeDisplay_Should_UseAte_When_TheBandStartsAtZero()
    {
        // Arrange
        var viewModel = new RiverThresholdViewModel(
            new RiverThreshold { Label = "Normalidade", MinMeters = 0, MaxMeters = 3.0 });

        // Assert
        Assert.Equal("até 3,00 m", viewModel.RangeDisplay);
    }

    [Fact]
    public void RangeDisplay_Should_ShowBothBounds_When_TheBandDoesNotStartAtZero()
    {
        // Arrange
        var viewModel = new RiverThresholdViewModel(
            new RiverThreshold { Label = "Atenção", MinMeters = 3.0, MaxMeters = 4.5 });

        // Assert
        Assert.Equal("3,00 – 4,50 m", viewModel.RangeDisplay);
    }

    [Fact]
    public void RangeDisplay_Should_UseAcimaDe_When_TheBandIsOpenEnded()
    {
        // Arrange
        var viewModel = new RiverThresholdViewModel(
            new RiverThreshold { Label = "Alerta", MinMeters = 4.5, MaxMeters = null });

        // Assert
        Assert.Equal("acima de 4,50 m", viewModel.RangeDisplay);
    }

    [Fact]
    public void Label_And_IsCurrent_Should_PassThrough_FromTheWrappedDomainValue()
    {
        // Arrange
        var viewModel = new RiverThresholdViewModel(
            new RiverThreshold { Label = "Alerta", MinMeters = 4.5, IsCurrent = true });

        // Assert
        Assert.Equal("Alerta", viewModel.Label);
        Assert.True(viewModel.IsCurrent);
    }
}
