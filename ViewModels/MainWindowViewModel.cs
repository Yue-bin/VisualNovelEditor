using System;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using Avalonia.Media;
using Avalonia.Threading;
using VNEditor.Models;
using VNEditor.Services;

namespace VNEditor.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const string ThemeModeNormal = "正常";
    private const string ThemeModeNight = "黑夜";
    private DialogueScene? _sceneNameTracking;
    private bool _loadingSettings;
    private string _projectRoot = string.Empty;
    private string _openedDataDialogueDir = string.Empty;
    private string _openedTextDialogueDir = string.Empty;
    private string _resourcesRoot = string.Empty;
    private string _gameResourcesRoot = string.Empty;
    private Dictionary<string, string> _roleCharacterImageMap = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _roleNameMap = new(StringComparer.OrdinalIgnoreCase);
    private bool _updatingRoleSelectors;
    private int _lastMainTabIndex;
    private DialogueScene? _playingScene;
    private int _playingIndex = -1;
    private string _activeBackgroundPath = string.Empty;

    public ObservableCollection<DialogueScene> Scenes { get; } = new();
    public ObservableCollection<RoleEntry> RoleEntries { get; } = new();
    public ObservableCollection<RoleOption> RoleOptions { get; } = new();
    public ObservableCollection<string> RoleCategories { get; } = new();
    public ObservableCollection<RoleEntry> FilteredRoleEntries { get; } = new();
    public ObservableCollection<string> ThemeModeOptions { get; } = [ThemeModeNormal, ThemeModeNight];
    public ObservableCollection<string> WindowBlurLevelOptions { get; } = new() { "无", "模糊", "亚克力" };
    public ObservableCollection<string> BackgroundImageOptions { get; } = new();

    [ObservableProperty] private DialogueScene? selectedScene;
    [ObservableProperty] private DialogueLine? selectedLine;
    [ObservableProperty] private RoleEntry? selectedRoleEntry;
    [ObservableProperty] private string? selectedRoleCategory;
    [ObservableProperty] private string newRoleCategoryName = string.Empty;
    [ObservableProperty] private RoleOption? selectedRole1Option;
    [ObservableProperty] private RoleOption? selectedRole2Option;
    [ObservableProperty] private bool selectedRole1Muted;
    [ObservableProperty] private bool selectedRole2Muted;
    [ObservableProperty] private string statusText = "请选择并打开 Data/Text 对话工程目录。";
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
    [ObservableProperty] private bool isPlayingScene;
    [ObservableProperty] private string previewHint = "点击预览区可查看当前行";
    [ObservableProperty] private bool previewChoice1Visible;
    [ObservableProperty] private bool previewChoice2Visible;
    [ObservableProperty] private bool previewChoice3Visible;
    [ObservableProperty] private bool previewChoice4Visible;
    [ObservableProperty] private string previewChoice1Text = "选项1";
    [ObservableProperty] private string previewChoice2Text = "选项2";
    [ObservableProperty] private string previewChoice3Text = "选项3";
    [ObservableProperty] private string previewChoice4Text = "选项4";
    [ObservableProperty] private bool optionEditor1Visible;
    [ObservableProperty] private bool optionEditor2Visible;
    [ObservableProperty] private bool optionEditor3Visible;
    [ObservableProperty] private bool optionEditor4Visible;
    [ObservableProperty] private double globalFontSize = 14;
    [ObservableProperty] private Bitmap? editorBackgroundImage;
    [ObservableProperty] private string editorBackgroundPath = string.Empty;
    [ObservableProperty] private Color editorBackgroundTint = Colors.Black;
    [ObservableProperty] private string editorBackgroundTintColorText = "#000000";
    [ObservableProperty] private double editorBackgroundTintOpacity = 0.25;
    [ObservableProperty] private string themeMode = ThemeModeNight;
    [ObservableProperty] private string requestedThemeVariantText = "Dark";
    [ObservableProperty] private string themeWindowBackground = "#1E1E1E";
    [ObservableProperty] private string themeTopBarBackground = "#B3252526";
    [ObservableProperty] private string themePanelBackground = "#A0252526";
    [ObservableProperty] private string themePanelAltBackground = "#9A2A2D2E";
    [ObservableProperty] private string themeEditorPanelBackground = "#8F242424";
    [ObservableProperty] private string themeListBackground = "#701E1E1E";
    [ObservableProperty] private string themeListAltBackground = "#901E1E1E";
    [ObservableProperty] private string themeCardBackground = "#802A2D2E";
    [ObservableProperty] private string themeDialogBackground = "#AA1F1F1F";
    [ObservableProperty] private string themeBorderColor = "#3C3C3C";
    [ObservableProperty] private string themeDialogBorder = "#808080";
    [ObservableProperty] private string themeTextPrimary = "#D4D4D4";
    [ObservableProperty] private string themeTextMuted = "#8A8A8A";
    [ObservableProperty] private double windowOpacity = 1.0;
    [ObservableProperty] private int windowBlurLevel; // 0=无 1=模糊 2=亚克力
    [ObservableProperty] private int selectedMainTabIndex;
    [ObservableProperty] private double sceneTabOffsetX;
    [ObservableProperty] private double roleTabOffsetX;
    [ObservableProperty] private double sceneTabOpacity = 1.0;
    [ObservableProperty] private double roleTabOpacity = 1.0;
    [ObservableProperty] private bool isSceneGalleryMode = true;
    [ObservableProperty] private double sceneGalleryOpacity = 1.0;
    [ObservableProperty] private double sceneDetailOpacity = 0.0;
    [ObservableProperty] private bool sceneGalleryHitTestVisible = true;
    [ObservableProperty] private bool sceneDetailHitTestVisible;
    [ObservableProperty] private bool isStartupUpdateChecking;
    [ObservableProperty] private string startupUpdateCheckingText = "正在检测更新……";
    [ObservableProperty] private double startupSpinnerDashOffset;
    [ObservableProperty] private bool isStartupUpdateDownloading;
    [ObservableProperty] private double startupUpdateDownloadProgress;
    [ObservableProperty] private bool gitPanelEnabled;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GitUiIdle))]
    private bool gitBusy;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnpushedCommits))]
    private int gitAheadBy;
    [ObservableProperty] private string gitStatusHint = string.Empty;
    /// <summary>当前会话内已对远端执行过「签出」的场景名（Git 工程下需签出后才可编辑）。</summary>
    private readonly HashSet<string> _gitCheckedOutSceneNames = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Git 工程下角色数据是否已签出（可编辑角色表）。</summary>
    private bool _gitRolesCheckedOut;

    /// <summary>已写入磁盘、待合并为单次 Git 提交的路径（与 <see cref="GitAheadBy"/> 分离直至执行提交）。</summary>
    private readonly HashSet<string> _pendingGitCommitPaths = new(StringComparer.OrdinalIgnoreCase);

    public bool IsSceneDetailMode => !IsSceneGalleryMode;
    public bool HasSelectedScene => SelectedScene != null;
    public bool HasUnpushedCommits => GitAheadBy > 0 || _pendingGitCommitPaths.Count > 0;
    public bool GitUiIdle => !GitBusy;

    /// <summary>非 Git 工程或未选场景时为 true；Git 工程下仅当当前场景已签出后为 true。</summary>
    public bool DialogueEditingEnabled =>
        !GitPanelEnabled || SelectedScene == null
        || _gitCheckedOutSceneNames.Contains(SelectedScene.Name);

    /// <summary>Git 工程下仅当角色数据已签出后可编辑角色。</summary>
    public bool RoleEditingEnabled => !GitPanelEnabled || _gitRolesCheckedOut;

    /// <summary>Git 工程且角色尚未签出。</summary>
    public bool GitCheckoutRequiredBeforeRoleEdit => GitPanelEnabled && !_gitRolesCheckedOut;

    /// <summary>Git 工程且当前场景尚未签出，需提示先签出。</summary>
    public bool GitCheckoutRequiredBeforeEdit =>
        GitPanelEnabled && SelectedScene != null && !_gitCheckedOutSceneNames.Contains(SelectedScene.Name);

    public MainWindowViewModel()
    {
        LoadEditorSettings();
    }

    partial void OnIsSceneGalleryModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsSceneDetailMode));
        SceneGalleryOpacity = value ? 1.0 : 0.0;
        SceneDetailOpacity = value ? 0.0 : 1.0;
        SceneGalleryHitTestVisible = value;
        SceneDetailHitTestVisible = !value;
    }

    partial void OnSelectedMainTabIndexChanged(int value)
    {
        AnimateMainTabSwitch(value);
    }

    partial void OnGlobalFontSizeChanged(double value)
    {
        if (_loadingSettings)
        {
            return;
        }

        if (value < 10)
        {
            GlobalFontSize = 10;
            return;
        }

        if (value > 28)
        {
            GlobalFontSize = 28;
            return;
        }

        SaveEditorSettings();
    }

    partial void OnSelectedSceneChanged(DialogueScene? value)
    {
        OnPropertyChanged(nameof(HasSelectedScene));
        if (_sceneNameTracking != null)
        {
            _sceneNameTracking.PropertyChanged -= OnSelectedScenePropertyChanged;
            _sceneNameTracking = null;
        }

        if (IsPlayingScene && value != _playingScene)
        {
            StopScenePlay();
        }

        if (value == null)
        {
            SelectedLine = null;
            return;
        }

        value.PropertyChanged += OnSelectedScenePropertyChanged;
        _sceneNameTracking = value;
        SelectedLine = value.Lines.Count > 0 ? value.Lines[0] : null;
        GitCheckoutSceneFilesCommand.NotifyCanExecuteChanged();
        RefreshDialogueEditingState();
    }

    partial void OnSelectedLineChanged(DialogueLine? oldValue, DialogueLine? newValue)
    {
        if (oldValue != null)
        {
            oldValue.PropertyChanged -= OnSelectedLinePropertyChanged;
        }

        if (newValue != null)
        {
            newValue.PropertyChanged += OnSelectedLinePropertyChanged;
        }

        SyncRoleSelectorsFromLine();
        UpdatePreview();
        DuplicateLineCommand.NotifyCanExecuteChanged();
        RemoveLineCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedRole1OptionChanged(RoleOption? value) => UpdateLineRolesFromSelectors();
    partial void OnSelectedRole2OptionChanged(RoleOption? value) => UpdateLineRolesFromSelectors();
    partial void OnSelectedRole1MutedChanged(bool value) => UpdateLineRolesFromSelectors();
    partial void OnSelectedRole2MutedChanged(bool value) => UpdateLineRolesFromSelectors();
    partial void OnSelectedRoleCategoryChanged(string? value) => RefreshFilteredRoleEntries();

    partial void OnThemeModeChanged(string value)
    {
        var normalized = NormalizeThemeMode(value);
        if (!string.Equals(normalized, value, StringComparison.Ordinal))
        {
            ThemeMode = normalized;
            return;
        }

        RequestedThemeVariantText = IsNightTheme() ? "Dark" : "Light";
        UpdateThemePalette(EditorBackgroundTint);
        SaveEditorSettings();
    }


    [RelayCommand]
    private void AddScene()
    {
        var scene = new DialogueScene { Name = $"NewScene{Scenes.Count + 1}" };
        scene.Lines.Add(new DialogueLine { IdPart = "1", Roles = "role_narrator" });
        RefreshScenePreview(scene);
        scene.IsDirty = true;
        Scenes.Add(scene);
        SelectedScene = scene;
        IsSceneGalleryMode = false;
        StatusText = "已新增场景。";
    }

    [RelayCommand(CanExecute = nameof(CanRemoveScene))]
    private void RemoveScene()
    {
        if (SelectedScene == null)
        {
            return;
        }

        var index = Scenes.IndexOf(SelectedScene);
        SelectedScene.PropertyChanged -= OnSelectedScenePropertyChanged;
        Scenes.Remove(SelectedScene);
        SelectedScene = Scenes.Count == 0 ? null : Scenes[Math.Clamp(index - 1, 0, Scenes.Count - 1)];
        if (Scenes.Count == 0)
        {
            IsSceneGalleryMode = true;
        }
        StatusText = "已删除场景。";
    }

    [RelayCommand]
    private void OpenSceneEditor(DialogueScene? scene)
    {
        if (scene == null)
        {
            return;
        }

        SelectedScene = scene;
        IsSceneGalleryMode = false;
    }

    [RelayCommand]
    private void OpenSelectedScene()
    {
        if (SelectedScene == null)
        {
            return;
        }

        IsSceneGalleryMode = false;
    }

    [RelayCommand]
    private void BackToSceneGallery()
    {
        IsSceneGalleryMode = true;
        StopScenePlay();
    }

    [RelayCommand]
    private void ApplyEditorBackground()
    {
        SetEditorBackgroundByPath(EditorBackgroundPath);
        SaveEditorSettings();
    }

    [RelayCommand]
    private void ClearEditorBackground()
    {
        var old = EditorBackgroundImage;
        EditorBackgroundImage = null;
        EditorBackgroundPath = string.Empty;
        old?.Dispose();
        SaveEditorSettings();
    }

    [RelayCommand]
    private void RefreshBackgroundOptions()
    {
        PopulateBackgroundImageOptions();
    }

    public IReadOnlyList<string> GetBackgroundImageOptions()
    {
        return BackgroundImageOptions.ToList();
    }

    public string ResolveBackgroundImagePath(string relativePath)
    {
        return ResolveResourcePath(relativePath);
    }

    partial void OnEditorBackgroundTintChanged(Color value)
    {
        var hex = ColorToHex(value);
        if (!string.Equals(EditorBackgroundTintColorText, hex, StringComparison.OrdinalIgnoreCase))
        {
            EditorBackgroundTintColorText = hex;
        }

        UpdateThemePalette(value);
        SaveEditorSettings();
    }
    partial void OnEditorBackgroundTintColorTextChanged(string value)
    {
        if (_loadingSettings)
        {
            return;
        }

        if (TryParseColor(value, out var parsed))
        {
            EditorBackgroundTint = parsed;
            SaveEditorSettings();
        }
    }
    partial void OnEditorBackgroundTintOpacityChanged(double value)
    {
        if (_loadingSettings)
        {
            return;
        }

        if (value < 0)
        {
            EditorBackgroundTintOpacity = 0;
            return;
        }

        if (value > 1)
        {
            EditorBackgroundTintOpacity = 1;
            return;
        }

        SaveEditorSettings();
    }

    [RelayCommand(CanExecute = nameof(CanAddLine))]
    private void AddLine()
    {
        if (SelectedScene == null)
        {
            return;
        }

        var line = new DialogueLine
        {
            IdPart = NextLineId(SelectedScene),
            Roles = "role_narrator"
        };

        if (SelectedLine == null)
        {
            SelectedScene.Lines.Add(line);
        }
        else
        {
            var idx = SelectedScene.Lines.IndexOf(SelectedLine);
            SelectedScene.Lines.Insert(idx + 1, line);
        }

        RefreshScenePreview(SelectedScene);
        SelectedScene.IsDirty = true;
        SelectedLine = line;
        StatusText = "已新增对话行。";
    }

    [RelayCommand(CanExecute = nameof(CanDuplicateLine))]
    private void DuplicateLine()
    {
        if (SelectedScene == null || SelectedLine == null)
        {
            return;
        }

        var copy = new DialogueLine
        {
            IdPart = NextLineId(SelectedScene),
            BaseScript = SelectedLine.BaseScript,
            EndScript = SelectedLine.EndScript,
            Roles = SelectedLine.Roles,
            IsNarrator = SelectedLine.IsNarrator,
            EventName = SelectedLine.EventName,
            ChoiceCount = SelectedLine.ChoiceCount,
            ChoiceScript1 = SelectedLine.ChoiceScript1,
            ChoiceScript2 = SelectedLine.ChoiceScript2,
            ChoiceScript3 = SelectedLine.ChoiceScript3,
            ChoiceScript4 = SelectedLine.ChoiceScript4,
            Text = SelectedLine.Text,
            TextEn = SelectedLine.TextEn,
            TextJa = SelectedLine.TextJa,
            ChoiceText1 = SelectedLine.ChoiceText1,
            ChoiceText1En = SelectedLine.ChoiceText1En,
            ChoiceText1Ja = SelectedLine.ChoiceText1Ja,
            ChoiceText2 = SelectedLine.ChoiceText2,
            ChoiceText2En = SelectedLine.ChoiceText2En,
            ChoiceText2Ja = SelectedLine.ChoiceText2Ja,
            ChoiceText3 = SelectedLine.ChoiceText3,
            ChoiceText3En = SelectedLine.ChoiceText3En,
            ChoiceText3Ja = SelectedLine.ChoiceText3Ja,
            ChoiceText4 = SelectedLine.ChoiceText4,
            ChoiceText4En = SelectedLine.ChoiceText4En,
            ChoiceText4Ja = SelectedLine.ChoiceText4Ja,
            BackgroundPath = SelectedLine.BackgroundPath
        };

        var idx = SelectedScene.Lines.IndexOf(SelectedLine);
        SelectedScene.Lines.Insert(idx + 1, copy);
        RefreshScenePreview(SelectedScene);
        SelectedScene.IsDirty = true;
        SelectedLine = copy;
        StatusText = "已复制对话行。";
    }

    [RelayCommand(CanExecute = nameof(CanRemoveLine))]
    private void RemoveLine()
    {
        if (SelectedScene == null || SelectedLine == null)
        {
            return;
        }

        var idx = SelectedScene.Lines.IndexOf(SelectedLine);
        SelectedScene.Lines.Remove(SelectedLine);
        RefreshScenePreview(SelectedScene);
        SelectedScene.IsDirty = true;
        if (SelectedScene.Lines.Count == 0)
        {
            SelectedLine = null;
        }
        else
        {
            SelectedLine = SelectedScene.Lines[Math.Clamp(idx, 0, SelectedScene.Lines.Count - 1)];
        }

        StatusText = "已删除对话行。";
    }

    public void OpenProject(string selectedPath)
    {
        var resolved = DialogueProjectService.ResolveProjectDirs(selectedPath);
        if (resolved == null)
        {
            StatusText = "目录无效：需要存在 DataConfigs/Data/Dialogue 与 DataConfigs/Text/Dialogue（.git 与 DataConfigs 同级）。";
            return;
        }

        var (dataDir, textDir, projectRoot) = resolved.Value;
        var loadedScenes = DialogueProjectService.LoadScenes(dataDir, textDir);
        Scenes.Clear();
        foreach (var scene in loadedScenes)
        {
            scene.IsDirty = false;
            RefreshScenePreview(scene);
            Scenes.Add(scene);
        }

        _projectRoot = projectRoot;
        _openedDataDialogueDir = dataDir;
        _openedTextDialogueDir = textDir;
        var assetsRoot = Path.Combine(projectRoot, "Assets");
        if (!Directory.Exists(assetsRoot))
        {
            assetsRoot = projectRoot;
        }

        _resourcesRoot = Path.Combine(assetsRoot, "Resources");
        _gameResourcesRoot = Path.Combine(assetsRoot, "GameResources");
        LoadRoleEntries(projectRoot);
        RefreshRoleMapsAndOptions();
        RefreshAllScenePreviews();
        PopulateBackgroundImageOptions();

        SelectedScene = Scenes.Count > 0 ? Scenes[0] : null;
        IsSceneGalleryMode = true;
        StopScenePlay();
        SaveEditorSettings();
        StatusText = $"已打开工程：{projectRoot}，共 {Scenes.Count} 个场景。";
        _gitCheckedOutSceneNames.Clear();
        _gitRolesCheckedOut = false;
        _pendingGitCommitPaths.Clear();
        AfterProjectOpenedForGit();
        RefreshDialogueEditingState();
        RefreshRoleEditingState();
    }

    [RelayCommand(CanExecute = nameof(CanSaveScene))]
    private void SaveScene(DialogueScene? scene)
    {
        if (scene == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_openedDataDialogueDir) || string.IsNullOrWhiteSpace(_openedTextDialogueDir))
        {
            StatusText = "请先打开工程后再保存场景。";
            return;
        }

        DialogueProjectService.ExportScene(
            scene,
            _openedDataDialogueDir,
            _openedTextDialogueDir,
            BuildValidRoleIdSet());
        scene.IsDirty = false;
        StatusText = $"已保存场景：{scene.Name}";
        TryGitCommitAfterSaveScene(scene);
    }

    private bool CanSaveScene(DialogueScene? scene) => DialogueEditingEnabled && scene != null;

    private bool CanRemoveScene() => DialogueEditingEnabled && SelectedScene != null;

    private bool CanAddLine() => DialogueEditingEnabled && SelectedScene != null;

    private bool CanDuplicateLine() => DialogueEditingEnabled && SelectedScene != null && SelectedLine != null;

    private bool CanRemoveLine() => DialogueEditingEnabled && SelectedScene != null && SelectedLine != null;

    private void RefreshDialogueEditingState()
    {
        OnPropertyChanged(nameof(DialogueEditingEnabled));
        OnPropertyChanged(nameof(GitCheckoutRequiredBeforeEdit));
        AddLineCommand.NotifyCanExecuteChanged();
        DuplicateLineCommand.NotifyCanExecuteChanged();
        RemoveLineCommand.NotifyCanExecuteChanged();
        SaveSceneCommand.NotifyCanExecuteChanged();
        RemoveSceneCommand.NotifyCanExecuteChanged();
    }

    private void RefreshRoleEditingState()
    {
        OnPropertyChanged(nameof(RoleEditingEnabled));
        OnPropertyChanged(nameof(GitCheckoutRequiredBeforeRoleEdit));
        AddRoleCommand.NotifyCanExecuteChanged();
        RemoveRoleCommand.NotifyCanExecuteChanged();
        SaveRolesCommand.NotifyCanExecuteChanged();
        AddRoleCategoryCommand.NotifyCanExecuteChanged();
        RemoveRoleCategoryCommand.NotifyCanExecuteChanged();
        GitCheckoutRoleFilesCommand.NotifyCanExecuteChanged();
    }

    private bool CanModifyRoles() => RoleEditingEnabled;

    [RelayCommand(CanExecute = nameof(CanModifyRoles))]
    private void AddRole()
    {
        var category = string.IsNullOrWhiteSpace(SelectedRoleCategory) ? "role" : SelectedRoleCategory!;
        var role = new RoleEntry
        {
            Category = category,
            Id = NextRoleId(category),
            Name = "新角色"
        };
        RoleEntries.Add(role);
        EnsureCategoryExists(category);
        SelectedRoleCategory = category;
        RefreshFilteredRoleEntries();
        SelectedRoleEntry = role;
        StatusText = "已新增角色。";
    }

    [RelayCommand(CanExecute = nameof(CanModifyRoles))]
    private void RemoveRole()
    {
        if (SelectedRoleEntry == null)
        {
            return;
        }

        var idx = RoleEntries.IndexOf(SelectedRoleEntry);
        UnsubscribeRoleEntry(SelectedRoleEntry);
        var removedCategory = SelectedRoleEntry.Category;
        RoleEntries.Remove(SelectedRoleEntry);
        RefreshRoleCategories();
        if (!string.IsNullOrWhiteSpace(removedCategory)
            && !RoleCategories.Any(x => x.Equals(removedCategory, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedRoleCategory = RoleCategories.FirstOrDefault();
        }
        RefreshFilteredRoleEntries();
        SelectedRoleEntry = FilteredRoleEntries.Count == 0 ? null : FilteredRoleEntries[Math.Clamp(idx, 0, FilteredRoleEntries.Count - 1)];
        RefreshRoleMapsAndOptions();
        StatusText = "已删除角色。";
    }

    [RelayCommand(CanExecute = nameof(CanModifyRoles))]
    private void AddRoleCategory()
    {
        var category = string.IsNullOrWhiteSpace(NewRoleCategoryName) ? string.Empty : NewRoleCategoryName.Trim();
        if (string.IsNullOrWhiteSpace(category))
        {
            StatusText = "分类名不能为空。";
            return;
        }

        EnsureCategoryExists(category);
        SelectedRoleCategory = category;
        NewRoleCategoryName = string.Empty;
        RefreshFilteredRoleEntries();
        StatusText = $"已新增分类：{category}";
    }

    [RelayCommand(CanExecute = nameof(CanModifyRoles))]
    private void RemoveRoleCategory()
    {
        if (string.IsNullOrWhiteSpace(SelectedRoleCategory))
        {
            return;
        }

        var category = SelectedRoleCategory;
        var toRemove = RoleEntries.Where(r => r.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var role in toRemove)
        {
            UnsubscribeRoleEntry(role);
            RoleEntries.Remove(role);
        }

        RoleCategories.Remove(category);
        SelectedRoleCategory = RoleCategories.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(_projectRoot))
        {
            DialogueProjectService.DeleteRoleCategoryCsvFiles(_projectRoot, category);
        }

        RefreshFilteredRoleEntries();
        RefreshRoleMapsAndOptions();
        StatusText = $"已删除分类：{category}";
    }

    [RelayCommand(CanExecute = nameof(CanModifyRoles))]
    private void SaveRoles()
    {
        if (string.IsNullOrWhiteSpace(_projectRoot))
        {
            StatusText = "请先打开工程后再保存角色。";
            return;
        }

        DialogueProjectService.SaveRoleEntries(_projectRoot, RoleEntries);
        RefreshRoleMapsAndOptions();
        StatusText = "角色已保存。";
        TryGitCommitAfterSaveRoles();
    }

    public void ExportProject(string outputRoot)
    {
        if (Scenes.Count == 0)
        {
            StatusText = "没有可导出的场景。";
            return;
        }

        var dataDir = DialogueProjectService.GetDialogueDataDir(outputRoot);
        var textDir = DialogueProjectService.GetDialogueTextDir(outputRoot);
        var validRoleIds = BuildValidRoleIdSet();
        foreach (var scene in Scenes)
        {
            DialogueProjectService.ExportScene(scene, dataDir, textDir, validRoleIds);
        }
        StatusText = $"导出完成：{outputRoot}";
    }

    public ScenePreviewViewModel? CreateScenePreviewViewModel()
    {
        if (SelectedScene == null || SelectedScene.Lines.Count == 0)
        {
            return null;
        }

        return new ScenePreviewViewModel(
            SelectedScene,
            _resourcesRoot,
            _gameResourcesRoot,
            _projectRoot,
            _roleCharacterImageMap,
            _roleNameMap);
    }

    private void LoadRoleEntries(string projectRoot)
    {
        RoleEntries.CollectionChanged -= OnRoleEntriesCollectionChanged;
        foreach (var role in RoleEntries)
        {
            UnsubscribeRoleEntry(role);
        }
        RoleEntries.Clear();

        foreach (var role in DialogueProjectService.LoadRoleEntries(projectRoot))
        {
            role.Category = string.IsNullOrWhiteSpace(role.Category) ? InferCategoryFromRoleId(role.Id) : role.Category;
            RoleEntries.Add(role);
            SubscribeRoleEntry(role);
        }

        RoleEntries.CollectionChanged += OnRoleEntriesCollectionChanged;
        RefreshRoleCategories();
        SelectedRoleCategory = RoleCategories.FirstOrDefault();
        RefreshFilteredRoleEntries();
        SelectedRoleEntry = FilteredRoleEntries.Count > 0 ? FilteredRoleEntries[0] : null;
    }

    private void OnRoleEntriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems.OfType<RoleEntry>())
            {
                SubscribeRoleEntry(item);
            }
        }

        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems.OfType<RoleEntry>())
            {
                UnsubscribeRoleEntry(item);
            }
        }

        RefreshRoleCategories();
        RefreshFilteredRoleEntries();
        RefreshRoleMapsAndOptions();
    }

    private void SubscribeRoleEntry(RoleEntry role)
    {
        role.PropertyChanged += OnRoleEntryPropertyChanged;
    }

    private void UnsubscribeRoleEntry(RoleEntry role)
    {
        role.PropertyChanged -= OnRoleEntryPropertyChanged;
    }

    private void OnRoleEntryPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RoleEntry.Category) || e.PropertyName == nameof(RoleEntry.Id))
        {
            RefreshRoleCategories();
            var syncedFilter = false;
            if (ReferenceEquals(sender, SelectedRoleEntry) && SelectedRoleEntry != null)
            {
                var resolved = string.IsNullOrWhiteSpace(SelectedRoleEntry.Category)
                    ? InferCategoryFromRoleId(SelectedRoleEntry.Id)
                    : SelectedRoleEntry.Category.Trim();
                if (!string.IsNullOrWhiteSpace(resolved)
                    && !string.Equals(SelectedRoleCategory, resolved, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedRoleCategory = resolved;
                    syncedFilter = true;
                }
            }

            if (!syncedFilter)
            {
                RefreshFilteredRoleEntries();
            }
        }

        RefreshRoleMapsAndOptions();
    }

    private void RefreshRoleMapsAndOptions()
    {
        _roleCharacterImageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _roleNameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        RoleOptions.Clear();
        RoleOptions.Add(new RoleOption { Id = string.Empty, DisplayName = "(空)" });

        foreach (var role in RoleEntries.OrderBy(r => r.Category).ThenBy(r => r.Id))
        {
            if (string.IsNullOrWhiteSpace(role.Id))
            {
                continue;
            }

            var optionId = BuildRoleOptionId(role);
            var rawId = ExtractSuffixId(optionId);

            if (!string.IsNullOrWhiteSpace(role.CharacterImage))
            {
                _roleCharacterImageMap[optionId] = role.CharacterImage;
                _roleCharacterImageMap[role.Id] = role.CharacterImage;
                if (!string.Equals(rawId, optionId, StringComparison.OrdinalIgnoreCase))
                {
                    _roleCharacterImageMap[rawId] = role.CharacterImage;
                }
            }

            var displayName = string.IsNullOrWhiteSpace(role.Name) ? rawId : role.Name;
            _roleNameMap[optionId] = displayName;
            _roleNameMap[role.Id] = displayName;
            if (!string.Equals(rawId, optionId, StringComparison.OrdinalIgnoreCase))
            {
                _roleNameMap[rawId] = displayName;
            }
            RoleOptions.Add(new RoleOption
            {
                Id = optionId,
                DisplayName = $"{displayName} ({optionId})"
            });
        }

        SyncRoleSelectorsFromLine();
        RefreshAllScenePreviews();
    }

    private void RefreshRoleCategories()
    {
        var categories = RoleEntries
            .Select(r => string.IsNullOrWhiteSpace(r.Category) ? InferCategoryFromRoleId(r.Id) : r.Category.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Union(RoleCategories, StringComparer.OrdinalIgnoreCase)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        RoleCategories.Clear();
        foreach (var category in categories)
        {
            RoleCategories.Add(category);
        }
    }

    private void RefreshFilteredRoleEntries()
    {
        // ListBox 在 Clear 时会通过绑定把 SelectedRoleEntry 置空，必须先记下再重建列表
        var preferred = SelectedRoleEntry;
        FilteredRoleEntries.Clear();
        var category = SelectedRoleCategory;
        IEnumerable<RoleEntry> query = RoleEntries;
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(r =>
                (string.IsNullOrWhiteSpace(r.Category) ? InferCategoryFromRoleId(r.Id) : r.Category)
                .Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var role in query
                     .OrderBy(r =>
                         string.IsNullOrWhiteSpace(r.Category) ? InferCategoryFromRoleId(r.Id) : r.Category,
                         StringComparer.OrdinalIgnoreCase)
                     .ThenBy(r => r.Id, StringComparer.OrdinalIgnoreCase))
        {
            FilteredRoleEntries.Add(role);
        }

        ReconcileSelectedRoleAfterFilter(preferred);
    }

    private void ReconcileSelectedRoleAfterFilter(RoleEntry? preferred)
    {
        if (preferred != null && FilteredRoleEntries.Contains(preferred))
        {
            SelectedRoleEntry = preferred;
            return;
        }

        if (SelectedRoleEntry != null && FilteredRoleEntries.Contains(SelectedRoleEntry))
        {
            return;
        }

        SelectedRoleEntry = FilteredRoleEntries.FirstOrDefault();
    }

    private void EnsureCategoryExists(string category)
    {
        if (RoleCategories.Any(x => x.Equals(category, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        RoleCategories.Add(category);
    }

    private void OnSelectedLinePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (SelectedScene != null)
        {
            SelectedScene.IsDirty = true;
            RefreshScenePreview(SelectedScene);
        }

        if (e.PropertyName == nameof(DialogueLine.ChoiceCount))
        {
            UpdateOptionEditorVisibility();
        }

        UpdatePreview();
    }

    private void OnSelectedScenePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not DialogueScene scene)
        {
            return;
        }

        if (e.PropertyName == nameof(DialogueScene.Name))
        {
            if (!scene.IsDirty)
            {
                scene.IsDirty = true;
            }

            RefreshDialogueEditingState();
        }
    }

    [RelayCommand]
    private void PlayCurrentScene()
    {
        if (SelectedScene == null || SelectedScene.Lines.Count == 0)
        {
            StatusText = "当前场景没有可播放行。";
            return;
        }

        _playingScene = SelectedScene;
        _playingIndex = 0;
        _activeBackgroundPath = string.Empty;
        IsPlayingScene = true;
        ApplyCurrentPlayLine();
        StatusText = $"开始预览场景：{_playingScene.Name}";
    }

    [RelayCommand]
    private void StopScenePlay()
    {
        IsPlayingScene = false;
        _playingScene = null;
        _playingIndex = -1;
        HideChoices();
        PreviewHint = "点击预览区可查看当前行";
        UpdatePreview();
    }

    [RelayCommand]
    private void PreviewLeftClick()
    {
        if (!IsPlayingScene)
        {
            UpdatePreview();
            return;
        }

        if (PreviewChoice1Visible || PreviewChoice2Visible || PreviewChoice3Visible || PreviewChoice4Visible)
        {
            return;
        }

        var line = GetCurrentPlayLine();
        if (line == null)
        {
            StopScenePlay();
            return;
        }

        if (string.IsNullOrWhiteSpace(line.EndScript))
        {
            MoveToPlayIndex(_playingIndex + 1);
            return;
        }

        _ = VisualNovelScriptExecutorParser.TryParseFirstAction(line.EndScript, out var endAction, out var endError);
        if (TryResolveJumpFromAction(endAction, out var target))
        {
            MoveToPlayIndex(target);
            return;
        }

        if (endAction.Type == DialogueScriptActionType.EndDialogue)
        {
            StatusText = "预览结束（EndDialogue）。";
            StopScenePlay();
            return;
        }

        PreviewHint = string.IsNullOrWhiteSpace(endError)
            ? "该 EndScript 无法在编辑器模拟，等待手动修改或点击停止"
            : $"EndScript 模拟失败: {endError}";
    }

    [RelayCommand]
    private void SelectPreviewChoice1()
    {
        ApplyChoice(1);
    }

    [RelayCommand]
    private void SelectPreviewChoice2()
    {
        ApplyChoice(2);
    }

    [RelayCommand]
    private void SelectPreviewChoice3()
    {
        ApplyChoice(3);
    }

    [RelayCommand]
    private void SelectPreviewChoice4()
    {
        ApplyChoice(4);
    }

    private void ApplyChoice(int choiceIndex)
    {
        if (!IsPlayingScene)
        {
            return;
        }

        var line = GetCurrentPlayLine();
        if (line == null)
        {
            return;
        }

        var script = GetChoiceScriptByIndex(line, choiceIndex);
        HideChoices();

        if (string.IsNullOrWhiteSpace(script))
        {
            MoveToPlayIndex(_playingIndex + 1);
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
            MoveToPlayIndex(target);
            return;
        }

        PreviewHint = string.IsNullOrWhiteSpace(choiceError)
            ? "该 ChoiceScript 无法在编辑器模拟，等待手动修改或点击停止"
            : $"ChoiceScript 模拟失败: {choiceError}";
    }

    private void ExecuteEndScriptAfterChoice(DialogueLine line)
    {
        var endScript = line.EndScript;
        if (string.IsNullOrWhiteSpace(endScript))
        {
            MoveToPlayIndex(_playingIndex + 1);
            return;
        }

        _ = VisualNovelScriptExecutorParser.TryParseFirstAction(endScript, out var endAction, out var endError);
        if (TryResolveJumpFromAction(endAction, out var target))
        {
            MoveToPlayIndex(target);
            return;
        }

        if (endAction.Type == DialogueScriptActionType.EndDialogue)
        {
            StatusText = "预览结束（EndScript 触发 EndDialogue）。";
            StopScenePlay();
            return;
        }

        StatusText = string.IsNullOrWhiteSpace(endError)
            ? "ChoiceScript 触发 EndDialogue，已尝试执行 EndScript（包含不可模拟内容）。"
            : $"EndScript 模拟失败: {endError}";
        StopScenePlay();
    }

    private void UpdatePreview()
    {
        if (IsPlayingScene)
        {
            ApplyCurrentPlayLine();
            return;
        }

        if (SelectedLine == null)
        {
            PreviewSpeaker = "旁白";
            PreviewText = string.Empty;
            ClearPreviewPortrait();
            ClearPreviewBackground();
            OptionEditor1Visible = false;
            OptionEditor2Visible = false;
            OptionEditor3Visible = false;
            OptionEditor4Visible = false;
            return;
        }

        HideChoices();
        PreviewHint = "点击预览区可查看当前行";
        UpdateOptionEditorVisibility();
        ApplyPreviewFromLine(SelectedLine, keepBackgroundWhenEmpty: false);
    }

    private void UpdateOptionEditorVisibility()
    {
        if (SelectedLine == null)
        {
            OptionEditor1Visible = false;
            OptionEditor2Visible = false;
            OptionEditor3Visible = false;
            OptionEditor4Visible = false;
            return;
        }

        var count = Math.Clamp(SelectedLine.ChoiceCount, 0, 4);
        OptionEditor1Visible = count >= 1;
        OptionEditor2Visible = count >= 2;
        OptionEditor3Visible = count >= 3;
        OptionEditor4Visible = count >= 4;
    }

    private void ApplyCurrentPlayLine()
    {
        var line = GetCurrentPlayLine();
        if (line == null)
        {
            StatusText = "场景播放完成。";
            StopScenePlay();
            return;
        }

        SelectedLine = line;
        ApplyPreviewFromLine(line, keepBackgroundWhenEmpty: true);
        SetupChoices(line);
    }

    private DialogueLine? GetCurrentPlayLine()
    {
        if (!IsPlayingScene || _playingScene == null)
        {
            return null;
        }

        if (_playingIndex < 0 || _playingIndex >= _playingScene.Lines.Count)
        {
            return null;
        }

        return _playingScene.Lines[_playingIndex];
    }

    private void MoveToPlayIndex(int index)
    {
        if (_playingScene == null)
        {
            StopScenePlay();
            return;
        }

        if (index < 0 || index >= _playingScene.Lines.Count)
        {
            StatusText = "场景播放完成。";
            StopScenePlay();
            return;
        }

        _playingIndex = index;
        ApplyCurrentPlayLine();
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
        PreviewHint = count > 0 ? "请选择一个选项" : "鼠标左键下一句";
    }

    private void HideChoices()
    {
        PreviewChoice1Visible = false;
        PreviewChoice2Visible = false;
        PreviewChoice3Visible = false;
        PreviewChoice4Visible = false;
    }

    private void ApplyPreviewFromLine(DialogueLine line, bool keepBackgroundWhenEmpty)
    {
        PreviewSpeaker = string.IsNullOrWhiteSpace(line.Roles) ? "旁白" : line.Roles;
        PreviewText = line.Text;

        SetPreviewBackgroundByRaw(line.BackgroundPath, keepBackgroundWhenEmpty);
        SetPreviewPortraitByRole(line);
        if (line.IsNarrator)
        {
            PreviewSpeaker = string.Empty;
        }
    }

    private void SetPreviewBackgroundByRaw(string rawPath, bool keepWhenEmpty)
    {
        var resolved = ResolveResourcePath(rawPath);
        if (string.IsNullOrWhiteSpace(resolved) || !File.Exists(resolved))
        {
            if (!keepWhenEmpty)
            {
                _activeBackgroundPath = string.Empty;
                ClearPreviewBackground();
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

        var old = PreviewBackground;
        PreviewBackground = bmp;
        old?.Dispose();
    }

    private void SetPreviewPortraitByRole(DialogueLine line)
    {
        var roles = ParseRoles(line.Roles);
        if (roles.Count == 0)
        {
            ClearPreviewPortrait();
            return;
        }

        var speaker = roles.FirstOrDefault(x => x.isSpeaker).id;
        if (string.IsNullOrWhiteSpace(speaker))
        {
            speaker = roles[0].id;
        }
        PreviewSpeaker = ResolveSpeakerName(speaker);

        SetPortraitSlot(1, roles.ElementAtOrDefault(0));
        SetPortraitSlot(2, roles.ElementAtOrDefault(1));
    }

    private void ClearPreviewBackground()
    {
        var old = PreviewBackground;
        PreviewBackground = null;
        old?.Dispose();
    }

    private void ClearPreviewPortrait()
    {
        var old1 = PreviewPortrait1;
        var old2 = PreviewPortrait2;
        PreviewSinglePortrait = null;
        PreviewPortrait1 = null;
        PreviewPortrait2 = null;
        PreviewPortrait1Visible = false;
        PreviewPortrait2Visible = false;
        PreviewPortrait1Dim = false;
        PreviewPortrait2Dim = false;
        PreviewUseSinglePortrait = false;
        PreviewUseDualPortrait = false;
        PreviewSinglePortraitDim = false;
        old1?.Dispose();
        old2?.Dispose();
    }

    private string ResolvePortraitPathByRoleId(string roleId)
    {
        if (string.IsNullOrWhiteSpace(roleId))
        {
            return string.Empty;
        }

        if (_roleCharacterImageMap.TryGetValue(roleId, out var direct))
        {
            return ResolveResourcePath(direct);
        }

        var roleKey = ExtractSuffixId(roleId);
        return _roleCharacterImageMap.TryGetValue(roleKey, out var path)
            ? ResolveResourcePath(path)
            : string.Empty;
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

        var dim = !role.isSpeaker;
        SetPortrait(slot, bmp, true, dim);
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
            RefreshPortraitLayoutMode();
            return;
        }

        var old2 = PreviewPortrait2;
        PreviewPortrait2 = bmp;
        PreviewPortrait2Visible = visible;
        PreviewPortrait2Dim = dim;
        old2?.Dispose();
        RefreshPortraitLayoutMode();
    }

    private void RefreshPortraitLayoutMode()
    {
        var visibleCount = (PreviewPortrait1Visible ? 1 : 0) + (PreviewPortrait2Visible ? 1 : 0);
        PreviewUseSinglePortrait = visibleCount == 1;
        PreviewUseDualPortrait = visibleCount >= 2;

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
                isSpeaker = false;
                token = token[1..].Trim();
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

    private void SyncRoleSelectorsFromLine()
    {
        _updatingRoleSelectors = true;
        try
        {
            if (SelectedLine == null)
            {
                SelectedRole1Option = null;
                SelectedRole2Option = null;
                SelectedRole1Muted = false;
                SelectedRole2Muted = false;
                return;
            }

            var roles = ParseRoles(SelectedLine.Roles);
            SelectedRole1Option = roles.Count > 0 ? FindOrCreateRoleOptionForDisplay(roles[0].id) : null;
            SelectedRole2Option = roles.Count > 1 ? FindOrCreateRoleOptionForDisplay(roles[1].id) : null;
            SelectedRole1Muted = roles.Count > 0 && !roles[0].isSpeaker;
            SelectedRole2Muted = roles.Count > 1 && !roles[1].isSpeaker;
        }
        finally
        {
            _updatingRoleSelectors = false;
        }
    }

    private void UpdateLineRolesFromSelectors()
    {
        if (_updatingRoleSelectors || SelectedLine == null)
        {
            return;
        }

        var parts = new List<string>();
        if (SelectedRole1Option != null && !string.IsNullOrWhiteSpace(SelectedRole1Option.Id))
        {
            parts.Add((SelectedRole1Muted ? "*" : "") + SelectedRole1Option.Id.Trim());
        }

        if (SelectedRole2Option != null && !string.IsNullOrWhiteSpace(SelectedRole2Option.Id))
        {
            parts.Add((SelectedRole2Muted ? "*" : "") + SelectedRole2Option.Id.Trim());
        }

        SelectedLine.Roles = string.Join(",", parts);
    }

    private RoleOption? FindOrCreateRoleOptionForDisplay(string roleId)
    {
        if (string.IsNullOrWhiteSpace(roleId))
        {
            return null;
        }

        var option = RoleOptions.FirstOrDefault(x => x.Id.Equals(roleId, StringComparison.OrdinalIgnoreCase));
        if (option != null)
        {
            return option;
        }

        option = RoleOptions.FirstOrDefault(x => ExtractSuffixId(x.Id).Equals(roleId, StringComparison.OrdinalIgnoreCase));
        if (option != null)
        {
            return option;
        }

        var missing = new RoleOption
        {
            Id = roleId,
            DisplayName = $"{roleId}<Missing>"
        };
        RoleOptions.Add(missing);
        return missing;
    }

    private string ResolveSpeakerName(string roleId)
    {
        if (string.IsNullOrWhiteSpace(roleId))
        {
            return string.Empty;
        }

        var key = ExtractSuffixId(roleId);
        if (key.Equals("narrator", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (_roleNameMap.TryGetValue(roleId, out var direct) && !string.IsNullOrWhiteSpace(direct))
        {
            return direct;
        }

        if (_roleNameMap.TryGetValue(key, out var name) && !string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return key;
    }

    private string ResolveResourcePath(string? rawPath) =>
        ResourcePathResolver.Resolve(rawPath, _projectRoot, _resourcesRoot, _gameResourcesRoot);

    private bool TryResolveJumpFromAction(DialogueScriptAction action, out int targetIndex)
    {
        targetIndex = -1;
        if (action.Type != DialogueScriptActionType.Jump || _playingScene == null)
        {
            return false;
        }

        var fullId = action.TargetId.Trim();
        if (string.IsNullOrWhiteSpace(fullId))
        {
            return false;
        }

        var part = NormalizeIdPartFromFullId(_playingScene.Name, fullId);
        if (string.IsNullOrWhiteSpace(part))
        {
            return false;
        }

        for (var i = 0; i < _playingScene.Lines.Count; i++)
        {
            if (_playingScene.Lines[i].IdPart.Equals(part, StringComparison.OrdinalIgnoreCase))
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

    public void SetEditorBackgroundByPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var resolved = ResolveResourcePath(path);
        if (string.IsNullOrWhiteSpace(resolved) || !File.Exists(resolved))
        {
            return;
        }

        var bmp = LoadBitmapSafe(resolved);
        if (bmp == null)
        {
            return;
        }

        var old = EditorBackgroundImage;
        EditorBackgroundImage = bmp;
        EditorBackgroundPath = BuildStoredBackgroundPath(path, resolved);
        old?.Dispose();
    }

    private string BuildStoredBackgroundPath(string inputPath, string resolvedPath)
    {
        if (!Path.IsPathRooted(inputPath))
        {
            return inputPath;
        }

        var appBase = AppContext.BaseDirectory;
        try
        {
            if (!string.IsNullOrWhiteSpace(appBase))
            {
                var relative = Path.GetRelativePath(appBase, resolvedPath);
                if (!string.IsNullOrWhiteSpace(relative) && !relative.StartsWith(".."))
                {
                    return relative;
                }
            }
        }
        catch
        {
            // ignore and fallback to absolute
        }

        return resolvedPath;
    }

    private void LoadEditorSettings()
    {
        string? lastProjectPathToRestore = null;
        _loadingSettings = true;
        try
        {
            var settings = EditorSettingsService.Load();
            GlobalFontSize = Math.Clamp(settings.GlobalFontSize, 10, 28);
            ThemeMode = NormalizeThemeMode(settings.ThemeMode);
            EditorBackgroundTint = ParseColorOrDefault(settings.EditorBackgroundTintColor, Colors.Black);
            EditorBackgroundTintColorText = ColorToHex(EditorBackgroundTint);
            EditorBackgroundTintOpacity = Math.Clamp(settings.EditorBackgroundTintOpacity, 0, 1);
            if (!string.IsNullOrWhiteSpace(settings.EditorBackgroundPath))
            {
                try { SetEditorBackgroundByPath(settings.EditorBackgroundPath); } catch { /* 背景图加载失败时忽略 */ }
            }
            WindowOpacity = Math.Clamp(settings.WindowOpacity, 0.2, 1.0);
            WindowBlurLevel = Math.Clamp(settings.WindowBlurLevel, 0, 2);
            if (!string.IsNullOrWhiteSpace(settings.LastOpenedProjectPath))
            {
                lastProjectPathToRestore = settings.LastOpenedProjectPath;
            }
        }
        finally
        {
            _loadingSettings = false;
        }

        if (!string.IsNullOrWhiteSpace(lastProjectPathToRestore) && Directory.Exists(lastProjectPathToRestore))
        {
            try { OpenProject(lastProjectPathToRestore); } catch { /* 打开上次项目失败时忽略 */ }
        }
    }

    private void SaveEditorSettings()
    {
        if (_loadingSettings)
        {
            return;
        }

        EditorSettingsService.Save(new EditorSettings
        {
            LastOpenedProjectPath = _projectRoot,
            EditorBackgroundPath = EditorBackgroundPath,
            GlobalFontSize = GlobalFontSize,
            EditorBackgroundTintColor = ColorToHex(EditorBackgroundTint),
            EditorBackgroundTintOpacity = EditorBackgroundTintOpacity,
            ThemeMode = ThemeMode,
            WindowOpacity = WindowOpacity,
            WindowBlurLevel = WindowBlurLevel
        });
    }

    /// <summary>根据 WindowBlurLevel 返回 Avalonia 窗口模糊等级，供各窗口绑定或应用。</summary>
    public WindowTransparencyLevel GetWindowTransparencyLevel()
    {
        return WindowBlurLevel switch
        {
            1 => WindowTransparencyLevel.Blur,
            2 => WindowTransparencyLevel.AcrylicBlur,
            _ => WindowTransparencyLevel.None
        };
    }

    partial void OnWindowOpacityChanged(double value)
    {
        if (_loadingSettings) return;
        SaveEditorSettings();
    }

    partial void OnWindowBlurLevelChanged(int value)
    {
        if (_loadingSettings) return;
        SaveEditorSettings();
    }

    private static string ColorToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static string ColorToHexArgb(Color color)
    {
        return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private void UpdateThemePalette(Color tint)
    {
        var tintRgb = Color.FromRgb(tint.R, tint.G, tint.B);
        if (IsNightTheme())
        {
            ThemeWindowBackground = ColorToHex(Blend(Color.FromRgb(0x1E, 0x1E, 0x1E), tintRgb, 0.28));
            ThemeTopBarBackground = ColorToHexArgb(WithAlpha(Blend(Color.FromRgb(0x25, 0x25, 0x26), tintRgb, 0.25), 0xB3));
            ThemePanelBackground = ColorToHexArgb(WithAlpha(Blend(Color.FromRgb(0x25, 0x25, 0x26), tintRgb, 0.30), 0xA0));
            ThemePanelAltBackground = ColorToHexArgb(WithAlpha(Blend(Color.FromRgb(0x2A, 0x2D, 0x2E), tintRgb, 0.33), 0x9A));
            ThemeEditorPanelBackground = ColorToHexArgb(WithAlpha(Blend(Color.FromRgb(0x24, 0x24, 0x24), tintRgb, 0.31), 0x8F));
            ThemeListBackground = ColorToHexArgb(WithAlpha(Blend(Color.FromRgb(0x1E, 0x1E, 0x1E), tintRgb, 0.24), 0x70));
            ThemeListAltBackground = ColorToHexArgb(WithAlpha(Blend(Color.FromRgb(0x1E, 0x1E, 0x1E), tintRgb, 0.30), 0x90));
            ThemeCardBackground = ColorToHexArgb(WithAlpha(Blend(Color.FromRgb(0x2A, 0x2D, 0x2E), tintRgb, 0.36), 0x80));
            ThemeDialogBackground = ColorToHexArgb(WithAlpha(Blend(Color.FromRgb(0x1F, 0x1F, 0x1F), tintRgb, 0.32), 0xAA));
            ThemeBorderColor = ColorToHex(Blend(Color.FromRgb(0x3C, 0x3C, 0x3C), tintRgb, 0.36));
            ThemeDialogBorder = ColorToHex(Blend(Color.FromRgb(0x80, 0x80, 0x80), tintRgb, 0.30));
            ThemeTextPrimary = ColorToHex(Blend(Color.FromRgb(0xD4, 0xD4, 0xD4), tintRgb, 0.20));
            ThemeTextMuted = ColorToHex(Blend(Color.FromRgb(0x8A, 0x8A, 0x8A), tintRgb, 0.18));
            return;
        }

        ThemeWindowBackground = ColorToHex(Blend(Color.FromRgb(0xF4, 0xF6, 0xFA), tintRgb, 0.10));
        ThemeTopBarBackground = ColorToHexArgb(WithAlpha(Blend(Color.FromRgb(0xEE, 0xF1, 0xF6), tintRgb, 0.12), 0xA8));
        ThemePanelBackground = ColorToHexArgb(WithAlpha(Blend(Color.FromRgb(0xF3, 0xF6, 0xFB), tintRgb, 0.10), 0x92));
        ThemePanelAltBackground = ColorToHexArgb(WithAlpha(Blend(Color.FromRgb(0xE9, 0xEE, 0xF6), tintRgb, 0.14), 0x86));
        ThemeEditorPanelBackground = ColorToHexArgb(WithAlpha(Blend(Color.FromRgb(0xF1, 0xF5, 0xFB), tintRgb, 0.12), 0x82));
        ThemeListBackground = ColorToHexArgb(WithAlpha(Blend(Color.FromRgb(0xF8, 0xFA, 0xFE), tintRgb, 0.08), 0x64));
        ThemeListAltBackground = ColorToHexArgb(WithAlpha(Blend(Color.FromRgb(0xF1, 0xF5, 0xFC), tintRgb, 0.10), 0x7A));
        ThemeCardBackground = ColorToHexArgb(WithAlpha(Blend(Color.FromRgb(0xEA, 0xF0, 0xFA), tintRgb, 0.14), 0x72));
        ThemeDialogBackground = ColorToHexArgb(WithAlpha(Blend(Color.FromRgb(0xF7, 0xFA, 0xFF), tintRgb, 0.10), 0x92));
        ThemeBorderColor = ColorToHex(Blend(Color.FromRgb(0xB8, 0xC2, 0xD1), tintRgb, 0.24));
        ThemeDialogBorder = ColorToHex(Blend(Color.FromRgb(0x9D, 0xAA, 0xBE), tintRgb, 0.24));
        ThemeTextPrimary = ColorToHex(Blend(Color.FromRgb(0x1F, 0x27, 0x33), tintRgb, 0.08));
        ThemeTextMuted = ColorToHex(Blend(Color.FromRgb(0x5A, 0x67, 0x7A), tintRgb, 0.08));
    }

    private bool IsNightTheme()
    {
        return string.Equals(ThemeMode, ThemeModeNight, StringComparison.Ordinal);
    }

    private static string NormalizeThemeMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return ThemeModeNight;
        }

        return mode.Trim() switch
        {
            ThemeModeNormal => ThemeModeNormal,
            ThemeModeNight => ThemeModeNight,
            "Normal" => ThemeModeNormal,
            "Night" => ThemeModeNight,
            _ => ThemeModeNight
        };
    }

    private void PopulateBackgroundImageOptions()
    {
        BackgroundImageOptions.Clear();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddFromRoot(string? gameResRoot)
        {
            if (string.IsNullOrWhiteSpace(gameResRoot) || !Directory.Exists(gameResRoot))
            {
                return;
            }

            var backgroundDir = Path.Combine(gameResRoot, "Images", "Dialogue", "Background");
            if (!Directory.Exists(backgroundDir))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(backgroundDir, "*.*", SearchOption.TopDirectoryOnly)
                         .Where(IsSupportedBackgroundFile)
                         .OrderBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase))
            {
                var relative = Path.GetRelativePath(gameResRoot, file).Replace('\\', '/');
                if (seen.Add(relative))
                {
                    BackgroundImageOptions.Add(relative);
                }
            }
        }

        AddFromRoot(_gameResourcesRoot);
        if (!string.IsNullOrWhiteSpace(_projectRoot))
        {
            AddFromRoot(Path.Combine(_projectRoot, "DataConfigs", "GameResources"));
        }
    }

    private static bool IsSupportedBackgroundFile(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".webp", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase);
    }


    private static Color Blend(Color baseColor, Color tintColor, double tintAmount)
    {
        var t = Math.Clamp(tintAmount, 0, 1);
        var r = (byte)Math.Round(baseColor.R + (tintColor.R - baseColor.R) * t);
        var g = (byte)Math.Round(baseColor.G + (tintColor.G - baseColor.G) * t);
        var b = (byte)Math.Round(baseColor.B + (tintColor.B - baseColor.B) * t);
        return Color.FromRgb(r, g, b);
    }

    private static Color WithAlpha(Color color, byte alpha)
    {
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    private static Color ParseColorOrDefault(string? value, Color fallback)
    {
        if (TryParseColor(value, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private static bool TryParseColor(string? value, out Color parsed)
    {
        parsed = Colors.Black;
        return !string.IsNullOrWhiteSpace(value) && Color.TryParse(value, out parsed);
    }

    private static string NextLineId(DialogueScene scene)
    {
        var max = 0;
        foreach (var line in scene.Lines)
        {
            if (int.TryParse(line.IdPart, out var val) && val > max)
            {
                max = val;
            }
        }

        return (max + 1).ToString();
    }

    private HashSet<string> BuildValidRoleIdSet()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in RoleEntries)
        {
            if (string.IsNullOrWhiteSpace(role.Id))
            {
                continue;
            }

            var id = role.Id.Trim();
            set.Add(id);
            var optionId = BuildRoleOptionId(role);
            set.Add(optionId);
            var suffix = ExtractSuffixId(optionId);
            if (!string.Equals(suffix, optionId, StringComparison.OrdinalIgnoreCase))
            {
                set.Add(suffix);
            }
        }

        return set;
    }

    private string BuildRoleOptionId(RoleEntry role)
    {
        var id = role.Id?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(id))
        {
            return string.Empty;
        }

        if (id.Contains('_'))
        {
            return id;
        }

        var category = string.IsNullOrWhiteSpace(role.Category) ? InferCategoryFromRoleId(id) : role.Category.Trim();
        return string.IsNullOrWhiteSpace(category) ? id : $"{category}_{id}";
    }

    private static string ExtractSuffixId(string roleId)
    {
        var idx = roleId.IndexOf('_');
        return idx > 0 && idx + 1 < roleId.Length ? roleId[(idx + 1)..] : roleId;
    }

    private static string InferCategoryFromRoleId(string roleId)
    {
        if (string.IsNullOrWhiteSpace(roleId))
        {
            return "role";
        }

        var trimmed = roleId.Trim();
        var idx = trimmed.IndexOf('_');
        return idx > 0 ? trimmed[..idx] : "role";
    }

    private string NextRoleId(string category)
    {
        var prefix = string.IsNullOrWhiteSpace(category) ? "role" : category.Trim();
        var max = 0;
        foreach (var role in RoleEntries.Where(r =>
                     (string.IsNullOrWhiteSpace(r.Category) ? InferCategoryFromRoleId(r.Id) : r.Category)
                     .Equals(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            var id = role.Id?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            var suffix = id;
            var idx = id.IndexOf('_');
            if (idx > 0 && idx + 1 < id.Length)
            {
                suffix = id[(idx + 1)..];
            }

            if (int.TryParse(suffix, out var num) && num > max)
            {
                max = num;
            }
        }

        return $"{prefix}_{max + 1}";
    }

    private void RefreshScenePreview(DialogueScene scene)
    {
        if (scene.Lines.Count == 0)
        {
            scene.PreviewText = "（空场景）";
            SetSceneThumbnailBackground(scene, null);
            SetSceneThumbnailPortrait(scene, 1, null, false, false);
            SetSceneThumbnailPortrait(scene, 2, null, false, false);
            RefreshSceneThumbnailPortraitLayout(scene);
            return;
        }

        var first = scene.Lines[0];
        var text = string.IsNullOrWhiteSpace(first.Text) ? "（首句为空）" : first.Text.Trim();
        if (text.Length > 80)
        {
            text = text[..80] + "...";
        }

        scene.PreviewText = text;
        SetSceneThumbnailBackground(scene, first.BackgroundPath);
        SetSceneThumbnailPortraits(scene, first.Roles);
    }

    private void RefreshAllScenePreviews()
    {
        foreach (var scene in Scenes)
        {
            RefreshScenePreview(scene);
        }
    }

    private void SetSceneThumbnailBackground(DialogueScene scene, string? rawPath)
    {
        var old = scene.GalleryBackground;
        scene.GalleryBackground = null;
        old?.Dispose();

        var resolved = ResolveResourcePath(rawPath);
        if (string.IsNullOrWhiteSpace(resolved) || !File.Exists(resolved))
        {
            return;
        }

        scene.GalleryBackground = LoadBitmapSafe(resolved);
    }

    private void SetSceneThumbnailPortraits(DialogueScene scene, string rolesRaw)
    {
        var roles = ParseRoles(rolesRaw);
        SetSceneThumbnailPortrait(scene, 1, null, false, false);
        SetSceneThumbnailPortrait(scene, 2, null, false, false);

        if (roles.Count > 0)
        {
            SetSceneThumbnailPortraitByRole(scene, 1, roles[0]);
        }
        if (roles.Count > 1)
        {
            SetSceneThumbnailPortraitByRole(scene, 2, roles[1]);
        }

        RefreshSceneThumbnailPortraitLayout(scene);
    }

    private void SetSceneThumbnailPortraitByRole(DialogueScene scene, int slot, (string id, bool isSpeaker) role)
    {
        if (string.IsNullOrWhiteSpace(role.id))
        {
            return;
        }

        var path = ResolvePortraitPathByRoleId(role.id);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        var bmp = LoadBitmapSafe(path);
        if (bmp == null)
        {
            return;
        }

        SetSceneThumbnailPortrait(scene, slot, bmp, true, !role.isSpeaker);
    }

    private static void SetSceneThumbnailPortrait(DialogueScene scene, int slot, Bitmap? bmp, bool visible, bool dim)
    {
        if (slot == 1)
        {
            var old = scene.GalleryPortrait1;
            scene.GalleryPortrait1 = bmp;
            scene.GalleryPortrait1Visible = visible;
            scene.GalleryPortrait1Dim = dim;
            old?.Dispose();
            return;
        }

        var old2 = scene.GalleryPortrait2;
        scene.GalleryPortrait2 = bmp;
        scene.GalleryPortrait2Visible = visible;
        scene.GalleryPortrait2Dim = dim;
        old2?.Dispose();
    }

    private static void RefreshSceneThumbnailPortraitLayout(DialogueScene scene)
    {
        var count = (scene.GalleryPortrait1Visible ? 1 : 0) + (scene.GalleryPortrait2Visible ? 1 : 0);
        scene.GalleryUseSinglePortrait = count == 1;
        scene.GalleryUseDualPortrait = count >= 2;

        if (!scene.GalleryUseSinglePortrait)
        {
            scene.GallerySinglePortrait = null;
            scene.GallerySinglePortraitDim = false;
            return;
        }

        if (scene.GalleryPortrait1Visible)
        {
            scene.GallerySinglePortrait = scene.GalleryPortrait1;
            scene.GallerySinglePortraitDim = scene.GalleryPortrait1Dim;
        }
        else
        {
            scene.GallerySinglePortrait = scene.GalleryPortrait2;
            scene.GallerySinglePortraitDim = scene.GalleryPortrait2Dim;
        }
    }

    private void AnimateMainTabSwitch(int newIndex)
    {
        if (newIndex == _lastMainTabIndex)
        {
            return;
        }

        var goingRight = newIndex > _lastMainTabIndex;
        const double distance = 90.0;

        if (goingRight)
        {
            SceneTabOffsetX = 0;
            RoleTabOffsetX = distance;
        }
        else
        {
            SceneTabOffsetX = -distance;
            RoleTabOffsetX = 0;
        }

        SceneTabOpacity = 1.0;
        RoleTabOpacity = 1.0;

        Dispatcher.UIThread.Post(() =>
        {
            if (goingRight)
            {
                SceneTabOffsetX = -distance;
                RoleTabOffsetX = 0;
            }
            else
            {
                SceneTabOffsetX = 0;
                RoleTabOffsetX = distance;
            }
        }, DispatcherPriority.Background);

        _lastMainTabIndex = newIndex;
    }

}
