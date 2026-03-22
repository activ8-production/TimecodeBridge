using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimecodeBridge.Models;
using TimecodeBridge.Services;
using TimecodeBridge.Services.Interfaces;
using TimecodeBridge.Views;

namespace TimecodeBridge.ViewModels;

public partial class CueListViewModel : DispatcherViewModel
{
    private readonly ICueManager _cueManager;
    private readonly ITimecodeEngine _timecodeEngine;
    private readonly IHostRegistry _hostRegistry;

    /// <summary>
    /// Shows a cue edit dialog. Returns the edited Cue if confirmed, null if cancelled.
    /// Replaceable for testing.
    /// </summary>
    internal Func<Cue, IReadOnlyList<OscHost>, FrameRate, string, Cue?> ShowCueEditDialog { get; set; }

    public ObservableCollection<CueItemViewModel> CueItems { get; } = [];

    public int TriggerWindowFrames
    {
        get => _cueManager.TriggerWindowFrames;
        set
        {
            if (_cueManager.TriggerWindowFrames != value)
            {
                _cueManager.TriggerWindowFrames = value;
                OnPropertyChanged();
            }
        }
    }

    public CueListViewModel(ICueManager cueManager, ITimecodeEngine timecodeEngine, IHostRegistry hostRegistry)
    {
        _cueManager = cueManager;
        _timecodeEngine = timecodeEngine;
        _hostRegistry = hostRegistry;

        ShowCueEditDialog = DefaultShowCueEditDialog;

        // Populate from existing cues
        foreach (var cue in _cueManager.Cues)
        {
            CueItems.Add(new CueItemViewModel(cue));
        }

        _cueManager.CueTriggered += OnCueTriggered;
        _timecodeEngine.TimecodeUpdated += OnTimecodeUpdated;
    }

    public void SyncFromService()
    {
        CueItems.Clear();
        foreach (var cue in _cueManager.Cues)
        {
            CueItems.Add(new CueItemViewModel(cue));
        }
    }

    private static Cue? DefaultShowCueEditDialog(Cue template, IReadOnlyList<OscHost> hosts, FrameRate frameRate, string title)
    {
        var dialog = new CueEditDialog(template, hosts, frameRate) { Title = title };
        if (Application.Current?.MainWindow is { } mainWindow)
            dialog.Owner = mainWindow;
        return dialog.ShowDialog() == true ? dialog.ResultCue : null;
    }

    [RelayCommand]
    private void AddCue()
    {
        var template = new Cue
        {
            Id = string.Empty,
            Name = $"Cue {_cueManager.Cues.Count + 1}",
            TriggerTime = new TimecodeValue(0, 0, 0, 0, _timecodeEngine.FrameRate),
            OscAddress = "/cue",
            TargetHostIds = _hostRegistry.Hosts.Select(h => h.Id).ToList(),
        };

        var result = ShowCueEditDialog(template, _hostRegistry.Hosts, _timecodeEngine.FrameRate, "キュー追加");
        if (result is not null)
        {
            result.Id = Guid.NewGuid().ToString();
            _cueManager.AddCue(result);
            CueItems.Add(new CueItemViewModel(result));
        }
    }

    [RelayCommand]
    private void EditCue(string? cueId)
    {
        if (cueId is null) return;
        var cue = _cueManager.Cues.FirstOrDefault(c => c.Id == cueId);
        if (cue is null) return;

        var result = ShowCueEditDialog(cue, _hostRegistry.Hosts, _timecodeEngine.FrameRate, "キュー編集");
        if (result is not null)
        {
            result.Id = cueId;
            _cueManager.UpdateCue(cueId, result);

            var index = -1;
            for (int i = 0; i < CueItems.Count; i++)
            {
                if (CueItems[i].Id == cueId) { index = i; break; }
            }
            if (index >= 0)
            {
                CueItems[index] = new CueItemViewModel(result);
            }
        }
    }

    [RelayCommand]
    private void DuplicateCue(string? cueId)
    {
        if (cueId is null) return;
        var source = _cueManager.Cues.FirstOrDefault(c => c.Id == cueId);
        if (source is null) return;

        var duplicate = CloneCue(source, source.TriggerTime, source.Name + " (コピー)");
        AddCueInternal(duplicate);
    }

    [RelayCommand]
    private void BatchDuplicateCue(string? cueId)
    {
        if (cueId is null) return;
        var source = _cueManager.Cues.FirstOrDefault(c => c.Id == cueId);
        if (source is null) return;

        var dialog = new BatchDuplicateDialog();
        if (Application.Current?.MainWindow is { } mainWindow)
            dialog.Owner = mainWindow;
        if (dialog.ShowDialog() != true) return;

        int fps = source.TriggerTime.FrameRate.FramesPerSecond();
        long framesPerInterval = (long)dialog.IntervalHours * 3600 * fps;
        long baseFrames = source.TriggerTime.TotalFrames();

        for (int i = 1; i <= dialog.Count; i++)
        {
            long newTotalFrames = baseFrames + framesPerInterval * i;
            var newTriggerTime = TimecodeValue.FromTotalFrames(newTotalFrames, source.TriggerTime.FrameRate);

            var duplicate = CloneCue(source, newTriggerTime);
            AddCueInternal(duplicate);
        }
    }

    private static Cue CloneCue(Cue source, TimecodeValue triggerTime, string? nameOverride = null)
    {
        return new Cue
        {
            Id = Guid.NewGuid().ToString(),
            Name = nameOverride ?? source.Name,
            Memo = source.Memo,
            TriggerTime = triggerTime,
            OscAddress = source.OscAddress,
            Arguments = source.Arguments.ToList(),
            TargetHostIds = source.TargetHostIds.ToList(),
            IsEnabled = source.IsEnabled,
            SendTriggerTimeAsSeconds = source.SendTriggerTimeAsSeconds,
            CueOffset = source.CueOffset,
        };
    }

    private void AddCueInternal(Cue cue)
    {
        _cueManager.AddCue(cue);
        CueItems.Add(new CueItemViewModel(cue));
    }

    [RelayCommand]
    private void RemoveCue(string? cueId)
    {
        if (cueId is null) return;
        _cueManager.RemoveCue(cueId);
        var item = CueItems.FirstOrDefault(c => c.Id == cueId);
        if (item != null)
        {
            CueItems.Remove(item);
        }
    }

    [RelayCommand]
    private void ManualTrigger(string cueId)
    {
        _cueManager.ManualTrigger(cueId);
    }

    [RelayCommand]
    private void ToggleCueEnabled(string cueId)
    {
        var item = CueItems.FirstOrDefault(c => c.Id == cueId);
        if (item != null)
        {
            var newEnabled = !item.IsEnabled;
            _cueManager.SetCueEnabled(cueId, newEnabled);
            item.IsEnabled = newEnabled;
        }
    }

    private void OnCueTriggered(object? sender, CueTriggeredEventArgs e)
    {
        RunOnUiThread(() =>
        {
            var item = CueItems.FirstOrDefault(c => c.Id == e.Cue.Id);
            if (item == null) return;

            item.IsTriggered = true;

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500),
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                item.IsTriggered = false;
            };
            timer.Start();
        });
    }

    private void OnTimecodeUpdated(object? sender, TimecodeUpdatedEventArgs e)
    {
        RunOnUiThread(() => UpdateNextCue(e.OffsetTimecode));
    }

    private void UpdateNextCue(TimecodeValue currentTimecode)
    {
        long currentOrd = currentTimecode.ToOrdinal();
        CueItemViewModel? nextCue = null;
        long nextOrd = long.MaxValue;

        foreach (var item in CueItems)
        {
            item.IsNextCue = false;

            if (!item.IsEnabled) continue;

            long cueOrd = item.TriggerTime.ToOrdinal();
            if (cueOrd > currentOrd && cueOrd < nextOrd)
            {
                nextCue = item;
                nextOrd = cueOrd;
            }
        }

        if (nextCue != null)
        {
            nextCue.IsNextCue = true;
        }
    }

}
