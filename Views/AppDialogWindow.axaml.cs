using Avalonia.Controls;
using Avalonia.Interactivity;
using VNEditor.ViewModels;

namespace VNEditor.Views;

public enum AppDialogButtons
{
    OK,
    YesNo
}

/// <summary>统一主题的信息框：标题、正文、确定 或 是/否；不可拉伸，窄版布局。</summary>
public partial class AppDialogWindow : Window
{
    public AppDialogWindow()
    {
        InitializeComponent();
    }

    public AppDialogWindow(MainWindowViewModel? theme, Window? owner, string title, string message, AppDialogButtons buttons)
    {
        InitializeComponent();
        if (theme != null)
        {
            DataContext = theme;
        }

        if (owner != null)
        {
            RequestedThemeVariant = owner.ActualThemeVariant;
        }

        Title = title;
        MessageBlock.Text = message;

        if (buttons == AppDialogButtons.OK)
        {
            ButtonRowOk.IsVisible = true;
            OkButton.Click += OnOkClick;
        }
        else
        {
            ButtonRowYesNo.IsVisible = true;
            YesButton.Click += (_, _) => Close(true);
            NoButton.Click += (_, _) => Close(false);
        }
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close(true);

    public static AppDialogWindow CreateMessage(MainWindowViewModel? theme, Window? owner, string title, string message) =>
        new(theme, owner, title, message, AppDialogButtons.OK);

    public static AppDialogWindow CreateYesNo(MainWindowViewModel? theme, Window? owner, string title, string message) =>
        new(theme, owner, title, message, AppDialogButtons.YesNo);
}
