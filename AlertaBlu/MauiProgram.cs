using AlertaBlu.Infrastructure;
using AlertaBlu.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AlertaBlu;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddAlertaBluInfrastructure(FileSystem.AppDataDirectory);

		builder.Services.AddSingleton<MainViewModel>();
		// Transient, not singleton: a Page can only ever belong to one parent, so a singleton
		// breaks the moment a second route or ContentTemplate needs its own instance.
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddSingleton<AppShell>();

#if DEBUG
		builder.Logging.AddDebug();
#else
		builder.Logging.AddAlertaBluFileLogging(FileSystem.AppDataDirectory);
#endif

		return builder.Build();
	}
}
