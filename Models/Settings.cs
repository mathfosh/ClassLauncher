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

    /// <summary>
    /// 命令行参数。需以 - 开头，多个参数用空格分隔。
    /// </summary>
    [ObservableProperty]
    private string _arguments = "";

    /// <summary>
    /// 是否启用此规则。用户可手动切换，系统也可自动禁用。
    /// </summary>
    [ObservableProperty]
    private bool _isEnabled = true;

    /// <summary>
    /// 是否允许系统在启动失败时自动禁用此规则。
    /// </summary>
    [ObservableProperty]
    private bool _autoDisableOnFail = true;

    /// <summary>
    /// 累计启动失败次数。
    /// </summary>
    [ObservableProperty]
    private int _failCount;

    /// <summary>
    /// 系统自动禁用此规则的时间。null 表示未被系统自动禁用。
    /// </summary>
    [ObservableProperty]
    private DateTime? _autoDisabledAt;

    /// <summary>
    /// 状态描述文本（用于 UI 显示）。
    /// </summary>
    public string StatusText => AutoDisabledAt.HasValue
        ? $"已自动禁用（{AutoDisabledAt:MM-dd HH:mm}）"
        : !IsEnabled
            ? "已手动禁用"
            : "启用中";

    partial void OnAdvanceMinutesChanged(double value)
    {
        if (value < 0)
            AdvanceMinutes = 0;
    }

    /// <summary>
    /// 重置失败计数和自动禁用状态（用户手动重新启用时调用）。
    /// </summary>
    public void ResetAutoDisableState()
    {
        FailCount = 0;
        AutoDisabledAt = null;
    }
}

public partial class Settings : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<AppEntry> _apps = new();

    /// <summary>
    /// 是否启用全局自动禁用机制。
    /// </summary>
    [ObservableProperty]
    private bool _autoDisableEnabled = true;

    /// <summary>
    /// 启动失败多少次后自动禁用规则。默认 1 次。
    /// </summary>
    [ObservableProperty]
    private int _autoDisableFailThreshold = 1;

partial void OnAutoDisableFailThresholdChanged(int value)
    {
        if (value < 1)
            AutoDisableFailThreshold = 1;
    }
}