# ClassLauncher

在上课时自动提醒并询问是否打开指定的软件，适合需要在上课时启动特定教学工具的场景。

## 功能

- 上课时弹出提示框，询问是否要打开配置的软件
- 支持自定义软件名称和路径
- 同一节课内已确认的软件不会重复询问
- 在 ClassIsland 设置页面中管理软件列表

## 使用方法

1. 在 ClassIsland 中打开【应用设置】→【插件】→【ClassLauncher】
2. 点击「+ 添加软件」，填写软件名称和路径
3. 上课时，插件会依次弹出对话框询问是否打开列表中的软件

### 软件路径填写说明

- 可直接填写可执行文件的完整路径，例如：`C:\Program Files\Notepad++\notepad++.exe`
- 如果软件已添加到系统 PATH 环境变量，也可直接填写文件名，例如：`notepad.exe`
- 支持填入参数，例如：`C:\path\to\app.exe --argument`

## 兼容性

- ClassIsland 2.x（API v2.0.0.0）
- .NET 8.0 Windows