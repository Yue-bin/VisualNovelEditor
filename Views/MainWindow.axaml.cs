using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using System.Collections.Generic;
using System.Linq;
using VNEditor.Models;
using VNEditor.Services;
using VNEditor.ViewModels;

namespace VNEditor.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _boundVm;
    private bool _forceCloseSkipGitPrompt;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Closing += OnMainWindowClosing;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_boundVm != null)
        {
            _boundVm.PropertyChanged -= OnVmPropertyChanged;
            _boundVm.GitNotify = null;
            _boundVm = null;
        }
        if (DataContext is MainWindowViewModel vm)
        {
            _boundVm = vm;
            vm.GitNotify = new AvaloniaGitUserNotify(this, vm);
            vm.PropertyChanged += OnVmPropertyChanged;
            try { ApplyTransparencyLevel(); } catch { /* 透明/模糊不可用时跳过，避免启动崩溃 */ }
        }
    }

    private async void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_forceCloseSkipGitPrompt)
        {
            return;
        }

        if (DataContext is not MainWindowViewModel vm || !vm.HasUnpushedCommits)
        {
            return;
        }

        e.Cancel = true;
        var allowClose = await vm.ConfirmCloseWithOptionalPushAsync();
        if (!allowClose)
        {
            return;
        }

        _forceCloseSkipGitPrompt = true;
        Close();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.WindowBlurLevel))
            try { ApplyTransparencyLevel(); } catch { }
    }

    /// <summary>模糊/亚克力时用半透明深色底，减少背后窗口对效果的影响，视觉更稳定。</summary>
    private static readonly Avalonia.Media.IBrush BlurBaseBrush = new Avalonia.Media.SolidColorBrush(
        Avalonia.Media.Color.FromArgb(217, 0x1E, 0x1E, 0x1E));

    private void ApplyTransparencyLevel()
    {
        if (DataContext is not MainWindowViewModel vm)
            return;
        var level = vm.GetWindowTransparencyLevel();
        TransparencyLevelHint = new[] { level };
        ClearValue(BackgroundProperty); // 清除 XAML 绑定，避免覆盖亚克力/模糊
        if (level != Avalonia.Controls.WindowTransparencyLevel.None)
            Background = BlurBaseBrush;
        else
            this.Bind(Window.BackgroundProperty, new Avalonia.Data.Binding(nameof(MainWindowViewModel.ThemeWindowBackground)) { Source = vm });
    }

    private async void OnOpenProjectClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择工程目录（含 DataConfigs，或与 .git 同级）",
            AllowMultiple = false
        });

        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            vm.OpenProject(path);
        }
    }

    private async void OnExportClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择导出目标目录（将创建 DataConfigs/Data 与 DataConfigs/Text）",
            AllowMultiple = false
        });

        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            vm.ExportProject(path);
        }
    }

    private void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            vm.PreviewLeftClickCommand.Execute(null);
        }
    }

    private async void OnPlaySceneClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        var previewVm = vm.CreateScenePreviewViewModel();
        if (previewVm == null)
        {
            return;
        }

        var win = new ScenePreviewWindow
        {
            DataContext = previewVm
        };
        await win.ShowDialog(this);
    }

    private void OnSceneCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (sender is Control { DataContext: DialogueScene scene })
        {
            vm.OpenSceneEditorCommand.Execute(scene);
        }
    }

    private void OnTopBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || IsInteractiveElement(e.Source))
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        BeginMoveDrag(e);
    }

    private static bool IsInteractiveElement(object? source)
    {
        var element = source as StyledElement;
        while (element != null)
        {
            if (element is Button or Menu or MenuItem)
            {
                return true;
            }

            element = element.Parent as StyledElement;
        }

        return false;
    }

    private void OnMinimizeClicked(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaxRestoreClicked(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void OnPickBackgroundClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择编辑器背景图",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("图片")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"]
                }
            ]
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            vm.SetEditorBackgroundByPath(path);
        }
    }

    private void OnOpenSettingsClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        var win = new SettingsWindow
        {
            DataContext = vm
        };
        win.Show(this);
    }

    private async void OnOpenBackgroundGalleryClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.SelectedLine == null)
        {
            return;
        }

        if (!vm.DialogueEditingEnabled)
        {
            vm.StatusText = "请先签出本场景后再编辑。";
            return;
        }

        vm.RefreshBackgroundOptionsCommand.Execute(null);
        var items = new List<BackgroundGalleryItem>();
        items.Add(new BackgroundGalleryItem
        {
            RelativePath = "(无背景)",
            FullPath = string.Empty,
            PreviewImage = null
        });

        foreach (var relativePath in vm.GetBackgroundImageOptions())
        {
            var fullPath = vm.ResolveBackgroundImagePath(relativePath);
            if (string.IsNullOrWhiteSpace(fullPath))
            {
                continue;
            }

            items.Add(new BackgroundGalleryItem
            {
                RelativePath = relativePath,
                FullPath = fullPath,
                PreviewImage = LoadBitmapSafe(fullPath)
            });
        }

        if (items.Count == 1)
        {
            return;
        }

        var galleryWindow = new BackgroundGalleryWindow(items);
        var result = await galleryWindow.ShowDialog<bool>(this);
        if (result && !string.IsNullOrWhiteSpace(galleryWindow.SelectedBackgroundPath))
        {
            vm.SelectedLine.BackgroundPath = galleryWindow.SelectedBackgroundPath == "(无背景)"
                ? string.Empty
                : galleryWindow.SelectedBackgroundPath;
        }
    }

    private void OnClearLineBackgroundClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.SelectedLine == null)
        {
            return;
        }

        if (!vm.DialogueEditingEnabled)
        {
            vm.StatusText = "请先签出本场景后再编辑。";
            return;
        }

        vm.SelectedLine.BackgroundPath = string.Empty;
    }

    private async void OnEditBaseScriptClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.SelectedLine == null)
        {
            return;
        }

        if (!vm.DialogueEditingEnabled)
        {
            vm.StatusText = "请先签出本场景后再编辑。";
            return;
        }

        var win = new ScriptEditorWindow("编辑 BaseScript", vm.SelectedLine.BaseScript) { ThemeSource = vm };
        var ok = await win.ShowDialog<bool>(this);
        if (ok)
        {
            vm.SelectedLine.BaseScript = win.ScriptText;
        }
    }

    private async void OnEditEndScriptClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.SelectedLine == null)
        {
            return;
        }

        if (!vm.DialogueEditingEnabled)
        {
            vm.StatusText = "请先签出本场景后再编辑。";
            return;
        }

        var win = new ScriptEditorWindow("编辑 EndScript", vm.SelectedLine.EndScript) { ThemeSource = vm };
        var ok = await win.ShowDialog<bool>(this);
        if (ok)
        {
            vm.SelectedLine.EndScript = win.ScriptText;
        }
    }

    private async void OnEditChoiceScript1Clicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.SelectedLine == null)
        {
            return;
        }

        if (!vm.DialogueEditingEnabled)
        {
            vm.StatusText = "请先签出本场景后再编辑。";
            return;
        }

        var win = new ScriptEditorWindow("编辑 ChoiceScript1", vm.SelectedLine.ChoiceScript1) { ThemeSource = vm };
        var ok = await win.ShowDialog<bool>(this);
        if (ok)
        {
            vm.SelectedLine.ChoiceScript1 = win.ScriptText;
        }
    }

    private async void OnEditChoiceScript2Clicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.SelectedLine == null)
        {
            return;
        }

        if (!vm.DialogueEditingEnabled)
        {
            vm.StatusText = "请先签出本场景后再编辑。";
            return;
        }

        var win = new ScriptEditorWindow("编辑 ChoiceScript2", vm.SelectedLine.ChoiceScript2) { ThemeSource = vm };
        var ok = await win.ShowDialog<bool>(this);
        if (ok)
        {
            vm.SelectedLine.ChoiceScript2 = win.ScriptText;
        }
    }

    private async void OnEditChoiceScript3Clicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.SelectedLine == null)
        {
            return;
        }

        if (!vm.DialogueEditingEnabled)
        {
            vm.StatusText = "请先签出本场景后再编辑。";
            return;
        }

        var win = new ScriptEditorWindow("编辑 ChoiceScript3", vm.SelectedLine.ChoiceScript3) { ThemeSource = vm };
        var ok = await win.ShowDialog<bool>(this);
        if (ok)
        {
            vm.SelectedLine.ChoiceScript3 = win.ScriptText;
        }
    }

    private async void OnEditChoiceScript4Clicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.SelectedLine == null)
        {
            return;
        }

        if (!vm.DialogueEditingEnabled)
        {
            vm.StatusText = "请先签出本场景后再编辑。";
            return;
        }

        var win = new ScriptEditorWindow("编辑 ChoiceScript4", vm.SelectedLine.ChoiceScript4) { ThemeSource = vm };
        var ok = await win.ShowDialog<bool>(this);
        if (ok)
        {
            vm.SelectedLine.ChoiceScript4 = win.ScriptText;
        }
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