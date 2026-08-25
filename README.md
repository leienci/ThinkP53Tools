# Power Plan Switcher

Windows 电源计划快速切换工具。

## 功能

- 全局快捷键 `Win+F4` 显示电源计划菜单
- 按住 Win 键继续按 F4 循环选择，松开 Win 键确认切换
- Esc 取消选择
- 托盘图标显示当前计划，左键显示菜单，右键退出
- 切换后显示半透明 OSD 提示
- 支持通过 `setting.ini` 配置

## 配置

编辑 `setting.ini` 文件：

```ini
[Hotkey]
Enabled=true
Key=Win+F4

[OSD]
Enabled=true
DurationMs=1500
Opacity=0.82

[Behavior]
RefreshIntervalMs=1000
CancelOnEscape=true

[DisplayNames]
Balanced=平衡
High performance=高性能
Power saver=节能
Ultimate Performance=卓越性能
```

## 构建

```bash
dotnet build -c Release
```

发布产物在 `bin/Release/net8.0-windows/win-x64/publish/`。

## 许可

MIT
