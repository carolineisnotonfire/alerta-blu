using Microsoft.Extensions.DependencyInjection;

namespace AlertaBlu;

// The base type is fully qualified because the referenced Application layer contributes an
// "AlertaBlu.Application" namespace: from inside "AlertaBlu", the bare name "Application" binds to
// that nested namespace and shadows Microsoft.Maui.Controls.Application.
public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();

        // The screen is dark-only by design, so the app never follows the system theme: pinning
        // it here keeps native controls (search bar, refresh spinner, scrollbars) in the same
        // palette as the hand-styled surfaces.
        UserAppTheme = AppTheme.Dark;

        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        new(_services.GetRequiredService<AppShell>());
}
