using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using VNEditor.ViewModels;

namespace VNEditor.Views;

public partial class GitHubDeviceLoginWindow : Window
{
    private readonly string _verificationUri;

    public GitHubDeviceLoginWindow()
    {
        InitializeComponent();
        _verificationUri = "https://github.com/login/device";
        WireControls(string.Empty);
    }

    public GitHubDeviceLoginWindow(string userCode, string verificationUri, MainWindowViewModel? theme)
    {
        InitializeComponent();
        if (theme != null)
        {
            DataContext = theme;
        }

        if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime d
            && d.MainWindow != null)
        {
            RequestedThemeVariant = d.MainWindow.ActualThemeVariant;
        }

        _verificationUri = string.IsNullOrWhiteSpace(verificationUri) ? "https://github.com/login/device" : verificationUri;
        WireControls(userCode);
    }

    private void WireControls(string userCode)
    {
        var userCodeBlock = this.FindControl<TextBlock>("UserCodeBlock")!;
        var openBtn = this.FindControl<Button>("OpenBrowserButton")!;
        userCodeBlock.Text = string.IsNullOrEmpty(userCode) ? "----" : userCode;
        openBtn.Click += (_, _) => OpenBrowser();
        Opened += (_, _) => OpenBrowser();
    }

    public void SetStatus(string text)
    {
        var st = this.FindControl<TextBlock>("StatusText");
        if (st != null)
        {
            st.Text = text;
        }
    }

    public void OpenBrowser()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _verificationUri,
                UseShellExecute = true
            });
        }
        catch
        {
            // ignore
        }
    }
}
