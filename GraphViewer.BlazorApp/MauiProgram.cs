using Blazorise;
using Blazorise.Tailwind;
using Blazorise.Icons.FontAwesome;
using Microsoft.Extensions.Logging;
using GraphViewer.BlazorApp.Services;

namespace GraphViewer.BlazorApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder.UseMauiApp<App>().ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });
            builder.Services.AddBlazorise().AddFontAwesomeIcons().AddTailwindProviders().AddTailwindComponents();
            builder.Services.AddMauiBlazorWebView();
            builder.Services.AddScoped<GraphConsole>();
            builder.Services.AddScoped<GraphDebugger>();
#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif
            return builder.Build();
        }
    }
}