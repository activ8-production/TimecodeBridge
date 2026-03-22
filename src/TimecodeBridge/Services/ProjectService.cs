using System.IO;
using System.Text.Json;
using TimecodeBridge.Models;
using TimecodeBridge.Services.Interfaces;

namespace TimecodeBridge.Services;

public class ProjectService : IProjectService
{
    private const int MaxRecentProjects = 10;

    private readonly string _settingsFilePath;
    private readonly List<string> _recentProjects;
    private bool _hasUnsavedChanges;

    public string? CurrentFilePath { get; private set; }

    public bool HasUnsavedChanges => _hasUnsavedChanges;

    public event EventHandler<EventArgs>? UnsavedChangesStatusChanged;

    public ProjectService(string settingsFilePath)
    {
        _settingsFilePath = settingsFilePath;
        _recentProjects = LoadRecentProjectsFromDisk();
    }

    public ProjectData LoadProject(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Project file not found.", filePath);
        }

        var json = File.ReadAllText(filePath);
        var options = ProjectData.CreateJsonOptions();
        var data = JsonSerializer.Deserialize<ProjectData>(json, options)
            ?? throw new InvalidOperationException("Failed to deserialize project data.");

        CurrentFilePath = filePath;
        SetHasUnsavedChanges(false);
        AddToRecentProjects(filePath);

        return data;
    }

    public void SaveProject(string filePath, ProjectData data)
    {
        var options = ProjectData.CreateJsonOptions();
        var json = JsonSerializer.Serialize(data, options);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(filePath, json);

        CurrentFilePath = filePath;
        SetHasUnsavedChanges(false);
        AddToRecentProjects(filePath);
    }

    public void MarkAsChanged()
    {
        SetHasUnsavedChanges(true);
    }

    public IReadOnlyList<string> GetRecentProjects()
    {
        return _recentProjects.AsReadOnly();
    }

    private void SetHasUnsavedChanges(bool value)
    {
        if (_hasUnsavedChanges == value) return;

        _hasUnsavedChanges = value;
        UnsavedChangesStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AddToRecentProjects(string filePath)
    {
        // Remove existing entry if present (to move it to front)
        _recentProjects.Remove(filePath);

        // Insert at the beginning (most recent first)
        _recentProjects.Insert(0, filePath);

        // Trim to max size
        while (_recentProjects.Count > MaxRecentProjects)
        {
            _recentProjects.RemoveAt(_recentProjects.Count - 1);
        }

        SaveRecentProjectsToDisk();
    }

    private List<string> LoadRecentProjectsFromDisk()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                return settings?.RecentProjects ?? [];
            }
        }
        catch
        {
            // If the settings file is corrupted, start fresh
        }

        return [];
    }

    private void SaveRecentProjectsToDisk()
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var settings = new AppSettings { RecentProjects = _recentProjects };
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
        }
        catch
        {
            // Settings persistence is best-effort
        }
    }

    public BackgroundSettings LoadBackgroundSettings()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                return settings?.BackgroundSettings ?? new BackgroundSettings();
            }
        }
        catch
        {
            // Best-effort
        }
        return new BackgroundSettings();
    }

    public void SaveBackgroundSettings(BackgroundSettings backgroundSettings)
    {
        try
        {
            AppSettings settings;
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            else
            {
                settings = new AppSettings();
            }

            settings.BackgroundSettings = backgroundSettings;
            settings.RecentProjects = _recentProjects;

            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var outputJson = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, outputJson);
        }
        catch
        {
            // Best-effort
        }
    }

    private class AppSettings
    {
        public List<string> RecentProjects { get; set; } = [];
        public BackgroundSettings BackgroundSettings { get; set; } = new();
    }
}
