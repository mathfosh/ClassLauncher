using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassLauncher.Models;

public partial class AppEntry : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _path = "";

    /// <summary>
    /// 限定课程名称，多个用逗号分隔。为空时匹配所有课程。
    /// </summary>
    [ObservableProperty]
    private string _courseNames = "";

    /// <summary>
    /// 提前提示的分钟数。0=上课时提示，>0=提前N分钟提示。
    /// </summary>
    [ObservableProperty]
    private double _advanceMinutes;
}

public partial class Settings : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<AppEntry> _apps = new();
}