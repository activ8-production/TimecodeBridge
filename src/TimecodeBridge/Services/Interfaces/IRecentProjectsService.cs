namespace TimecodeBridge.Services.Interfaces;

/// <summary>
/// 最近使用したプロジェクトのMRUリスト管理を提供するサービス。
/// </summary>
public interface IRecentProjectsService
{
    IReadOnlyList<string> GetRecentProjects();
    void AddRecentProject(string filePath);
}
