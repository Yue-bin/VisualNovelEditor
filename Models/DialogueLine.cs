using CommunityToolkit.Mvvm.ComponentModel;

namespace VNEditor.Models;

public partial class DialogueLine : ObservableObject
{
    [ObservableProperty] private string idPart = "1";
    [ObservableProperty] private string baseScript = string.Empty;
    [ObservableProperty] private string endScript = string.Empty;
    [ObservableProperty] private string roles = "role_narrator";
    [ObservableProperty] private bool isNarrator;
    [ObservableProperty] private string eventName = string.Empty;
    [ObservableProperty] private int choiceCount;
    [ObservableProperty] private string choiceScript1 = string.Empty;
    [ObservableProperty] private string choiceScript2 = string.Empty;
    [ObservableProperty] private string choiceScript3 = string.Empty;
    [ObservableProperty] private string choiceScript4 = string.Empty;

    [ObservableProperty] private string text = string.Empty;
    [ObservableProperty] private string textEn = string.Empty;
    [ObservableProperty] private string textJa = string.Empty;
    [ObservableProperty] private string choiceText1 = string.Empty;
    [ObservableProperty] private string choiceText1En = string.Empty;
    [ObservableProperty] private string choiceText1Ja = string.Empty;
    [ObservableProperty] private string choiceText2 = string.Empty;
    [ObservableProperty] private string choiceText2En = string.Empty;
    [ObservableProperty] private string choiceText2Ja = string.Empty;
    [ObservableProperty] private string choiceText3 = string.Empty;
    [ObservableProperty] private string choiceText3En = string.Empty;
    [ObservableProperty] private string choiceText3Ja = string.Empty;
    [ObservableProperty] private string choiceText4 = string.Empty;
    [ObservableProperty] private string choiceText4En = string.Empty;
    [ObservableProperty] private string choiceText4Ja = string.Empty;
    // Editor-only metadata, serialized into BaseScript comments.
    [ObservableProperty] private string backgroundPath = string.Empty;
    /// <summary>Git 拉取合并冲突已按远端解决时，标记需人工核对的行。</summary>
    [ObservableProperty] private bool hasGitSyncWarning;

    public string CsvId => IdPart;
}
