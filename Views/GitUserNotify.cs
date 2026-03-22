using System.Threading.Tasks;
using Avalonia.Controls;
using VNEditor.Services;
using VNEditor.ViewModels;

namespace VNEditor.Views;

public sealed class AvaloniaGitUserNotify : IGitUserNotify
{
    private readonly Window _owner;
    private readonly MainWindowViewModel _theme;

    public AvaloniaGitUserNotify(Window owner, MainWindowViewModel theme)
    {
        _owner = owner;
        _theme = theme;
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        var win = AppDialogWindow.CreateMessage(_theme, _owner, title, message);
        await win.ShowDialog<bool>(_owner);
    }

    public async Task<bool> ShowYesNoAsync(string title, string message)
    {
        var win = AppDialogWindow.CreateYesNo(_theme, _owner, title, message);
        return await win.ShowDialog<bool>(_owner);
    }
}
