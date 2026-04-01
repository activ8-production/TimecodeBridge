namespace TimecodeBridge.Services.Interfaces;

/// <summary>
/// アプリケーション設定の読み書きを担当するサービス
/// </summary>
public interface IAppSettingsService
{
    List<string> LoadRecentProjects();
    void SaveRecentProjects(List<string> projects);
}
