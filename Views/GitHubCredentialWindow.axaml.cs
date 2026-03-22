using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using VNEditor.Services;

namespace VNEditor.Views;

public partial class GitHubCredentialWindow : Window
{
    public GitHubCredentialWindow()
        : this(string.Empty, null)
    {
    }

    public GitHubCredentialWindow(string remoteUrl, string? errorHint)
    {
        InitializeComponent();
        var remoteUrlText = this.FindControl<TextBlock>("RemoteUrlText")!;
        var errorHintText = this.FindControl<TextBlock>("ErrorHintText")!;
        var userNameBox = this.FindControl<TextBox>("UserNameBox")!;
        var tokenBox = this.FindControl<TextBox>("TokenBox")!;
        var tokenLinkText = this.FindControl<TextBlock>("TokenLinkText")!;
        var okButton = this.FindControl<Button>("OkButton")!;
        var cancelButton = this.FindControl<Button>("CancelButton")!;

        remoteUrlText.Text = string.IsNullOrWhiteSpace(remoteUrl) ? "（未读取到远程 URL）" : $"远程：{remoteUrl}";
        if (!string.IsNullOrWhiteSpace(errorHint))
        {
            errorHintText.Text = errorHint;
            errorHintText.IsVisible = true;
        }

        tokenLinkText.PointerPressed += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/settings/tokens",
                    UseShellExecute = true
                });
            }
            catch
            {
                // ignore
            }
        };

        okButton.Click += (_, _) =>
        {
            var token = tokenBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(token))
            {
                return;
            }

            var user = userNameBox.Text?.Trim() ?? string.Empty;
            Close(new GitHubCredentials(user, token));
        };
        cancelButton.Click += (_, _) => Close(null);
    }
}
