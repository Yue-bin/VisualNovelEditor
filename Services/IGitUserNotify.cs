using System.Threading.Tasks;

namespace VNEditor.Services;

/// <summary>MVVM 层用于提示（由 View 注入实现）。</summary>
public interface IGitUserNotify
{
    Task ShowMessageAsync(string title, string message);
    Task<bool> ShowYesNoAsync(string title, string message);
}
