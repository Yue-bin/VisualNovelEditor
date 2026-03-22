using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VNEditor.Models;
using VNEditor.Services;

namespace VNEditor.ViewModels;

public partial class ScenePreviewViewModel : ViewModelBase
{
    private readonly DialogueScene _scene;
    private readonly string _resourcesRoot;
    private readonly string _gameResourcesRoot;
    private readonly string _projectRoot;
    private readonly Dictionary<string, string> _roleCharacterImageMap;
    private readonly Dictionary<string, string> _roleNameMap;
    private int _playingIndex;
    private string _activeBackgroundPath = string.Empty;

    [ObservableProperty] private string windowTitle = "场景预览";
    [ObservableProperty] private Bitmap? previewBackground;
    [ObservableProperty] private Bitmap? previewPortrait1;
    [ObservableProperty] private Bitmap? previewPortrait2;
    [ObservableProperty] private bool previewPortrait1Visible;
    [ObservableProperty] private bool previewPortrait2Visible;
    [ObservableProperty] private bool previewPortrait1Dim;
    [ObservableProperty] private bool previewPortrait2Dim;
    [ObservableProperty] private bool previewUseSinglePortrait;
    [ObservableProperty] private bool previewUseDualPortrait;
    [ObservableProperty] private Bitmap? previewSinglePortrait;
    [ObservableProperty] private bool previewSinglePortraitDim;
    [ObservableProperty] private string previewSpeaker = "旁白";
    [ObservableProperty] private string previewText = string.Empty;
    [ObservableProperty] private string previewHint = "鼠标左键下一句";
    [ObservableProperty] private bool previewChoice1Visible;
    [ObservableProperty] private bool previewChoice2Visible;
    [ObservableProperty] private bool previewChoice3Visible;
    [ObservableProperty] private bool previewChoice4Visible;
    [ObservableProperty] private string previewChoice1Text = "选项1";
    [ObservableProperty] private string previewChoice2Text = "选项2";
    [ObservableProperty] private string previewChoice3Text = "选项3";
    [ObservableProperty] private string previewChoice4Text = "选项4";
    [ObservableProperty] private bool isFinished;

    public ScenePreviewViewModel(
        DialogueScene scene,
        string resourcesRoot,
        string gameResourcesRoot,
        string projectRoot,
        Dictionary<string, string> roleCharacterImageMap,
        Dictionary<string, string> roleNameMap)
    {
        _scene = scene;
        _resourcesRoot = resourcesRoot;
        _gameResourcesRoot = gameResourcesRoot;
        _projectRoot = projectRoot;
        _roleCharacterImageMap = new Dictionary<string, string>(roleCharacterImageMap, StringComparer.OrdinalIgnoreCase);
        _roleNameMap = new Dictionary<string, string>(roleNameMap, StringComparer.OrdinalIgnoreCase);
        _playingIndex = 0;
        WindowTitle = $"场景预览 - {_scene.Name}";
        ApplyCurrentLine();
    }

    [RelayCommand]
    private void LeftClick()
    {
        if (IsFinished)
        {
            return;
        }

        if (PreviewChoice1Visible || PreviewChoice2Visible || PreviewChoice3Visible || PreviewChoice4Visible)
        {
            return;
        }

        var line = GetCurrentLine();
        if (line == null)
        {
            EndPreview("场景播放完成。");
            return;
        }

        if (string.IsNullOrWhiteSpace(line.EndScript))
        {
            MoveTo(_playingIndex + 1);
            return;
        }

        _ = VisualNovelScriptExecutorParser.TryParseFirstAction(line.EndScript, out var endAction, out var endError);
        if (TryResolveJumpFromAction(endAction, out var target))
        {
            MoveTo(target);
            return;
        }

        if (endAction.Type == DialogueScriptActionType.EndDialogue)
        {
            EndPreview("预览结束（EndDialogue）。");
            return;
        }

        PreviewHint = string.IsNullOrWhiteSpace(endError) ? "该 EndScript 无法模拟（无详细错误）" : $"EndScript 模拟失败: {endError}";
    }

    [RelayCommand] private void SelectChoice1() => ApplyChoice(1);
    [RelayCommand] private void SelectChoice2() => ApplyChoice(2);
    [RelayCommand] private void SelectChoice3() => ApplyChoice(3);
    [RelayCommand] private void SelectChoice4() => ApplyChoice(4);

    private void ApplyChoice(int idx)
    {
        if (IsFinished)
        {
            return;
        }

        var line = GetCurrentLine();
        if (line == null)
        {
            return;
        }

        HideChoices();
        var script = GetChoiceScriptByIndex(line, idx);
        if (string.IsNullOrWhiteSpace(script))
        {
            MoveTo(_playingIndex + 1);
            return;
        }

        _ = VisualNovelScriptExecutorParser.TryParseFirstAction(script, out var choiceAction, out var choiceError);
        if (choiceAction.Type == DialogueScriptActionType.EndDialogue)
        {
            ExecuteEndScriptAfterChoice(line);
            return;
        }

        if (TryResolveJumpFromAction(choiceAction, out var target))
        {
            MoveTo(target);
            return;
        }

        PreviewHint = string.IsNullOrWhiteSpace(choiceError) ? "该 ChoiceScript 无法模拟（无详细错误）" : $"ChoiceScript 模拟失败: {choiceError}";
    }

    private void ExecuteEndScriptAfterChoice(DialogueLine line)
    {
        if (string.IsNullOrWhiteSpace(line.EndScript))
        {
            MoveTo(_playingIndex + 1);
            return;
        }

        _ = VisualNovelScriptExecutorParser.TryParseFirstAction(line.EndScript, out var endAction, out var endError);
        if (TryResolveJumpFromAction(endAction, out var target))
        {
            MoveTo(target);
            return;
        }

        if (endAction.Type == DialogueScriptActionType.EndDialogue)
        {
            EndPreview("预览结束（EndScript 触发 EndDialogue）。");
            return;
        }

        EndPreview(string.IsNullOrWhiteSpace(endError) ? "EndScript 无法模拟。" : $"EndScript 模拟失败: {endError}");
    }

    private void MoveTo(int index)
    {
        if (index < 0 || index >= _scene.Lines.Count)
        {
            EndPreview("场景播放完成。");
            return;
        }

        _playingIndex = index;
        ApplyCurrentLine();
    }

    private DialogueLine? GetCurrentLine()
    {
        if (_playingIndex < 0 || _playingIndex >= _scene.Lines.Count)
        {
            return null;
        }

        return _scene.Lines[_playingIndex];
    }

    private void ApplyCurrentLine()
    {
        var line = GetCurrentLine();
        if (line == null)
        {
            EndPreview("场景播放完成。");
            return;
        }

        PreviewText = line.Text;
        SetPreviewBackground(line.BackgroundPath, keepWhenEmpty: true);
        SetPortraits(line.Roles, line.IsNarrator);
        SetupChoices(line);
        if (!PreviewChoice1Visible && !PreviewChoice2Visible && !PreviewChoice3Visible && !PreviewChoice4Visible)
        {
            PreviewHint = "鼠标左键下一句";
        }
    }

    private void SetupChoices(DialogueLine line)
    {
        var count = Math.Clamp(line.ChoiceCount, 0, 4);
        PreviewChoice1Visible = count >= 1;
        PreviewChoice2Visible = count >= 2;
        PreviewChoice3Visible = count >= 3;
        PreviewChoice4Visible = count >= 4;
        PreviewChoice1Text = string.IsNullOrWhiteSpace(line.ChoiceText1) ? "选项1" : line.ChoiceText1;
        PreviewChoice2Text = string.IsNullOrWhiteSpace(line.ChoiceText2) ? "选项2" : line.ChoiceText2;
        PreviewChoice3Text = string.IsNullOrWhiteSpace(line.ChoiceText3) ? "选项3" : line.ChoiceText3;
        PreviewChoice4Text = string.IsNullOrWhiteSpace(line.ChoiceText4) ? "选项4" : line.ChoiceText4;
        if (count > 0)
        {
            PreviewHint = "请选择一个选项";
        }
    }

    private void HideChoices()
    {
        PreviewChoice1Visible = false;
        PreviewChoice2Visible = false;
        PreviewChoice3Visible = false;
        PreviewChoice4Visible = false;
    }

    private void SetPreviewBackground(string rawPath, bool keepWhenEmpty)
    {
        var resolved = ResolveResourcePath(rawPath);
        if (string.IsNullOrWhiteSpace(resolved) || !File.Exists(resolved))
        {
            if (!keepWhenEmpty)
            {
                _activeBackgroundPath = string.Empty;
                var old = PreviewBackground;
                PreviewBackground = null;
                old?.Dispose();
            }
            return;
        }

        if (_activeBackgroundPath.Equals(resolved, StringComparison.OrdinalIgnoreCase) && PreviewBackground != null)
        {
            return;
        }

        _activeBackgroundPath = resolved;
        var bmp = LoadBitmapSafe(resolved);
        if (bmp == null)
        {
            return;
        }

        var oldBg = PreviewBackground;
        PreviewBackground = bmp;
        oldBg?.Dispose();
    }

    private void SetPortraits(string rolesRaw, bool isNarrator)
    {
        var roles = ParseRoles(rolesRaw);
        if (roles.Count == 0)
        {
            ClearPortraits();
            PreviewSpeaker = isNarrator ? string.Empty : "旁白";
            return;
        }

        var speakerId = roles.FirstOrDefault(x => x.isSpeaker).id;
        if (string.IsNullOrWhiteSpace(speakerId))
        {
            speakerId = roles[0].id;
        }
        PreviewSpeaker = isNarrator ? string.Empty : ResolveRoleName(speakerId);

        SetPortraitSlot(1, roles.ElementAtOrDefault(0));
        SetPortraitSlot(2, roles.ElementAtOrDefault(1));
    }

    private void SetPortraitSlot(int slot, (string id, bool isSpeaker) role)
    {
        if (string.IsNullOrWhiteSpace(role.id))
        {
            SetPortrait(slot, null, false, false);
            return;
        }

        var path = ResolvePortraitPathByRoleId(role.id);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            SetPortrait(slot, null, false, false);
            return;
        }

        var bmp = LoadBitmapSafe(path);
        if (bmp == null)
        {
            SetPortrait(slot, null, false, false);
            return;
        }

        SetPortrait(slot, bmp, true, !role.isSpeaker);
    }

    private void SetPortrait(int slot, Bitmap? bmp, bool visible, bool dim)
    {
        if (slot == 1)
        {
            var old = PreviewPortrait1;
            PreviewPortrait1 = bmp;
            PreviewPortrait1Visible = visible;
            PreviewPortrait1Dim = dim;
            old?.Dispose();
            RefreshPortraitLayout();
            return;
        }

        var old2 = PreviewPortrait2;
        PreviewPortrait2 = bmp;
        PreviewPortrait2Visible = visible;
        PreviewPortrait2Dim = dim;
        old2?.Dispose();
        RefreshPortraitLayout();
    }

    private void RefreshPortraitLayout()
    {
        var count = (PreviewPortrait1Visible ? 1 : 0) + (PreviewPortrait2Visible ? 1 : 0);
        PreviewUseSinglePortrait = count == 1;
        PreviewUseDualPortrait = count >= 2;

        if (!PreviewUseSinglePortrait)
        {
            PreviewSinglePortrait = null;
            PreviewSinglePortraitDim = false;
            return;
        }

        if (PreviewPortrait1Visible)
        {
            PreviewSinglePortrait = PreviewPortrait1;
            PreviewSinglePortraitDim = PreviewPortrait1Dim;
        }
        else
        {
            PreviewSinglePortrait = PreviewPortrait2;
            PreviewSinglePortraitDim = PreviewPortrait2Dim;
        }
    }

    private void ClearPortraits()
    {
        var old1 = PreviewPortrait1;
        var old2 = PreviewPortrait2;
        PreviewPortrait1 = null;
        PreviewPortrait2 = null;
        PreviewPortrait1Visible = false;
        PreviewPortrait2Visible = false;
        PreviewPortrait1Dim = false;
        PreviewPortrait2Dim = false;
        PreviewUseSinglePortrait = false;
        PreviewUseDualPortrait = false;
        PreviewSinglePortrait = null;
        PreviewSinglePortraitDim = false;
        old1?.Dispose();
        old2?.Dispose();
    }

    private string ResolveRoleName(string roleId)
    {
        if (IsNarratorRole(roleId))
        {
            return string.Empty;
        }

        if (_roleNameMap.TryGetValue(roleId, out var name) && !string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var key = roleId.StartsWith("role_", StringComparison.OrdinalIgnoreCase) ? roleId[5..] : roleId;
        if (_roleNameMap.TryGetValue(key, out var n2) && !string.IsNullOrWhiteSpace(n2))
        {
            return n2;
        }

        return key;
    }

    private static bool IsNarratorRole(string? roleId)
    {
        if (string.IsNullOrWhiteSpace(roleId))
        {
            return true;
        }

        var key = roleId.StartsWith("role_", StringComparison.OrdinalIgnoreCase) ? roleId[5..] : roleId;
        return key.Equals("narrator", StringComparison.OrdinalIgnoreCase);
    }

    private string ResolvePortraitPathByRoleId(string roleId)
    {
        if (_roleCharacterImageMap.TryGetValue(roleId, out var direct))
        {
            return ResolveResourcePath(direct);
        }

        var key = roleId.StartsWith("role_", StringComparison.OrdinalIgnoreCase) ? roleId[5..] : roleId;
        return _roleCharacterImageMap.TryGetValue(key, out var path)
            ? ResolveResourcePath(path)
            : string.Empty;
    }

    private string ResolveResourcePath(string? rawPath) =>
        ResourcePathResolver.Resolve(rawPath, _projectRoot, _resourcesRoot, _gameResourcesRoot);

    private bool TryResolveJumpFromAction(DialogueScriptAction action, out int targetIndex)
    {
        targetIndex = -1;
        if (action.Type != DialogueScriptActionType.Jump)
        {
            return false;
        }

        var fullId = action.TargetId.Trim();
        if (string.IsNullOrWhiteSpace(fullId))
        {
            return false;
        }

        var part = NormalizeIdPartFromFullId(_scene.Name, fullId);
        for (var i = 0; i < _scene.Lines.Count; i++)
        {
            if (_scene.Lines[i].IdPart.Equals(part, StringComparison.OrdinalIgnoreCase))
            {
                targetIndex = i;
                return true;
            }
        }

        return false;
    }

    private static string NormalizeIdPartFromFullId(string sceneName, string fullId)
    {
        var id = fullId.Trim();
        var prefix = sceneName + "_";
        if (id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            id = id[prefix.Length..];
        }

        if (id.StartsWith('*'))
        {
            id = id[1..];
        }

        return id;
    }

    private static string GetChoiceScriptByIndex(DialogueLine line, int index)
    {
        return index switch
        {
            1 => line.ChoiceScript1,
            2 => line.ChoiceScript2,
            3 => line.ChoiceScript3,
            4 => line.ChoiceScript4,
            _ => string.Empty
        };
    }

    private static List<(string id, bool isSpeaker)> ParseRoles(string rolesRaw)
    {
        var result = new List<(string id, bool isSpeaker)>();
        if (string.IsNullOrWhiteSpace(rolesRaw))
        {
            return result;
        }

        var tokens = rolesRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var tokenRaw in tokens)
        {
            var token = tokenRaw.Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            var isSpeaker = true;
            if (token.StartsWith('*'))
            {
                token = token[1..].Trim();
                isSpeaker = false;
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            result.Add((token, isSpeaker));
            if (result.Count >= 2)
            {
                break;
            }
        }

        return result;
    }

    private void EndPreview(string hint)
    {
        IsFinished = true;
        HideChoices();
        PreviewHint = hint;
    }

    private static Bitmap? LoadBitmapSafe(string file)
    {
        try
        {
            return new Bitmap(file);
        }
        catch
        {
            return null;
        }
    }
}
