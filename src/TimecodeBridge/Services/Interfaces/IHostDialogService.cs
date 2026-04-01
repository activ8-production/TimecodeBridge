using TimecodeBridge.Models;

namespace TimecodeBridge.Services.Interfaces;

/// <summary>
/// ホスト関連のダイアログ表示を担当するサービス
/// </summary>
public interface IHostDialogService
{
    OscHost? ShowEditDialog(OscHost template);
}
