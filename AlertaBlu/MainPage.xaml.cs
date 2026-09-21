using System.ComponentModel;
using AlertaBlu.ViewModels;

namespace AlertaBlu;

public partial class MainPage : ContentPage
{
    /// <summary>Handoff asks for 150-200ms transitions; these sit in that band.</summary>
    private const uint ChevronDuration = 180;
    private const uint FadeDuration = 150;

    private readonly MainViewModel _viewModel;

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        BindingContext = viewModel;

        // The expand/collapse animation is the one piece of the design that cannot be expressed
        // as a binding: rotation and opacity are transient states, not view-model data.
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Fire-and-forget: LoadAsync owns its own error handling and no-ops while a refresh is
        // already running, so returning to the page never stacks up requests.
        _ = _viewModel.LoadAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _viewModel.Cancel();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsRiverExpanded))
        {
            _ = AnimateRiverCardAsync(_viewModel.IsRiverExpanded);
        }
    }

    private async Task AnimateRiverCardAsync(bool expanded)
    {
        await Task.WhenAll(
            RiverChevron.RotateToAsync(expanded ? 180d : 0d, ChevronDuration, Easing.CubicInOut),
            RiverBands.FadeToAsync(expanded ? 1d : 0d, FadeDuration))
            .ConfigureAwait(false);
    }
}
