using System.Net.Http.Headers;
using AlertaBlu.Application;
using AlertaBlu.Infrastructure;
using AlertaBlu.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AlertaBlu;

public static class MauiProgram
{
	/// <summary>Per-request ceiling; the whole refresh is additionally bounded by the view model.</summary>
	private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

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

		// A single long-lived HttpClient: the app talks to two hosts for the lifetime of the
		// process, so connection reuse is what matters and DNS rotation is a non-issue.
		builder.Services.AddSingleton(_ =>
		{
			var client = new HttpClient { Timeout = RequestTimeout };
			client.DefaultRequestHeaders.UserAgent.Add(
				new ProductInfoHeaderValue("AlertaBluApp", "1.0"));
			return client;
		});

		builder.Services.AddSingleton<IAlertaBluService, AlertaBluService>();
		builder.Services.AddSingleton<MainViewModel>();
		builder.Services.AddSingleton<MainPage>();
		builder.Services.AddSingleton<AppShell>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
