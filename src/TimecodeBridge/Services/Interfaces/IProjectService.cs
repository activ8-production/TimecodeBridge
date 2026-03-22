using TimecodeBridge.Models;

namespace TimecodeBridge.Services.Interfaces;

public interface IProjectService
{
    string? CurrentFilePath { get; }
    bool HasUnsavedChanges { get; }

    ProjectData LoadProject(string filePath);
    void SaveProject(string filePath, ProjectData data);
    void MarkAsChanged();
    IReadOnlyList<string> GetRecentProjects();

    BackgroundSettings LoadBackgroundSettings();
    void SaveBackgroundSettings(BackgroundSettings settings);

    event EventHandler<EventArgs> UnsavedChangesStatusChanged;
}
