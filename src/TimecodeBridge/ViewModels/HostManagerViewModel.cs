using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimecodeBridge.Models;
using TimecodeBridge.Services.Interfaces;
using TimecodeBridge.Views;

namespace TimecodeBridge.ViewModels;

public partial class HostManagerViewModel : ObservableObject
{
    private readonly IHostRegistry _hostRegistry;
    private readonly IOscSender _oscSender;
    private readonly ITimecodeEngine _timecodeEngine;

    /// <summary>
    /// Shows a host edit dialog. Returns the edited OscHost if confirmed, null if cancelled.
    /// Replaceable for testing.
    /// </summary>
    internal Func<OscHost, OscHost?> ShowHostEditDialog { get; set; }

    public ObservableCollection<OscHost> Hosts { get; } = [];

    public HostManagerViewModel(IHostRegistry hostRegistry, IOscSender oscSender, ITimecodeEngine timecodeEngine)
    {
        _hostRegistry = hostRegistry;
        _oscSender = oscSender;
        _timecodeEngine = timecodeEngine;

        ShowHostEditDialog = DefaultShowHostEditDialog;

        // Sync existing hosts
        foreach (var host in _hostRegistry.Hosts)
        {
            Hosts.Add(host);
        }

        // Subscribe to changes
        _hostRegistry.HostChanged += OnHostChanged;
    }

    private static OscHost? DefaultShowHostEditDialog(OscHost template)
    {
        var dialog = new HostEditDialog(template);
        if (Application.Current?.MainWindow is { } mainWindow)
            dialog.Owner = mainWindow;
        return dialog.ShowDialog() == true ? dialog.ResultHost : null;
    }

    private void OnHostChanged(object? sender, HostChangedEventArgs e)
    {
        switch (e.ChangeType)
        {
            case HostChangeType.Added:
                var addedHost = _hostRegistry.Hosts.FirstOrDefault(h => h.Id == e.HostId);
                if (addedHost is not null && !Hosts.Any(h => h.Id == e.HostId))
                {
                    Hosts.Add(addedHost);
                }
                break;

            case HostChangeType.Removed:
                var toRemove = Hosts.FirstOrDefault(h => h.Id == e.HostId);
                if (toRemove is not null)
                {
                    Hosts.Remove(toRemove);
                }
                break;

            case HostChangeType.Updated:
                var existingIndex = -1;
                for (int i = 0; i < Hosts.Count; i++)
                {
                    if (Hosts[i].Id == e.HostId)
                    {
                        existingIndex = i;
                        break;
                    }
                }
                if (existingIndex >= 0)
                {
                    var updatedHost = _hostRegistry.Hosts.FirstOrDefault(h => h.Id == e.HostId);
                    if (updatedHost is not null)
                    {
                        Hosts[existingIndex] = updatedHost;
                    }
                }
                break;
        }
    }

    [RelayCommand]
    private void AddHost()
    {
        var template = new OscHost
        {
            Id = string.Empty,
            Name = "New Host",
            IpAddress = "127.0.0.1",
            Port = 9000,
        };

        var result = ShowHostEditDialog(template);
        if (result is not null)
        {
            result.Id = Guid.NewGuid().ToString();
            _hostRegistry.AddHost(result);
        }
    }

    [RelayCommand]
    private void EditHost(string hostId)
    {
        var host = _hostRegistry.Hosts.FirstOrDefault(h => h.Id == hostId);
        if (host is null) return;

        var result = ShowHostEditDialog(host);
        if (result is not null)
        {
            result.Id = hostId;
            _hostRegistry.UpdateHost(hostId, result);
        }
    }

    [RelayCommand]
    private void RemoveHost(string hostId)
    {
        _hostRegistry.RemoveHost(hostId);
    }

    [RelayCommand]
    private void ToggleHostEnabled(string hostId)
    {
        var host = _hostRegistry.Hosts.FirstOrDefault(h => h.Id == hostId);
        if (host is not null)
        {
            _hostRegistry.SetHostEnabled(hostId, !host.IsEnabled);
        }
    }

    [RelayCommand]
    private async Task PingHost(string hostId)
    {
        var fps = _timecodeEngine.FrameRate.FramesPerSecond();
        await _oscSender.SendIcmpPingAsync(hostId, fps);
    }
}
