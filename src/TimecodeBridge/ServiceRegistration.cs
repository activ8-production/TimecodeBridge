using Microsoft.Extensions.DependencyInjection;
using TimecodeBridge.Models;
using TimecodeBridge.Services;
using TimecodeBridge.Services.Interfaces;
using TimecodeBridge.ViewModels;

namespace TimecodeBridge;

public static class ServiceRegistration
{
    public static void ConfigureServices(IServiceCollection services)
    {
        // Services (Singleton)
        services.AddSingleton<IHostRegistry, HostRegistry>();
        services.AddSingleton<ITimecodeEngine>(_ => new TimecodeEngine(FrameRate.Fps30));
        services.AddSingleton<ITimecodeGenerator, TimecodeGenerator>();
        services.AddSingleton<ILtcEncoder, LtcEncoder>();

        services.AddSingleton<IOscTransport, OscTransport>();
        services.AddSingleton<IOscSender, OscSender>();
        services.AddSingleton<ICueManager, CueManager>();
        services.AddSingleton<ITimecodeRelay, TimecodeRelay>();
        services.AddSingleton<IProjectService>(_ =>
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var settingsFilePath = System.IO.Path.Combine(appDataPath, "TimecodeBridge", "settings.json");
            return new ProjectService(settingsFilePath);
        });

        // ViewModels
        services.AddSingleton<TimecodeViewModel>();
        services.AddSingleton<CueListViewModel>();
        services.AddSingleton<RelayViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<HostManagerViewModel>();
        services.AddTransient<LogViewModel>();
        services.AddTransient<AudioWaveformViewModel>();
    }
}
