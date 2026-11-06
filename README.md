# Stutter Fix 中文补丁

这是 Stutter Fix 2.8.1 的独立 UMM 伴随模组。它不会修改或覆盖原来的
`StutterFix.dll`，只在游戏运行时通过 Harmony 添加中文语言按钮和中文文案。

## 安装

1. 关闭游戏。
2. 在 UMM 中使用 `Install Mod` 选择 `StutterFixZh-0.1.9.zip`。
3. 或把 ZIP 中的 `StutterFixZh` 文件夹解压到：

```text
A Dance of Fire and Ice/UMMMods/StutterFixZh/
```

4. 确认 `StutterFix` 和 `Stutter Fix 中文补丁` 都已启用，然后启动游戏。

## 使用

打开 Stutter Fix 设置窗口后，标题栏会显示：

```text
中文   English   한국어   ×
```

选择“中文”后，语言会写入原模组的 `Settings.xml`，下次启动仍然生效。
翻译资源内嵌在 `StutterFixZh.dll` 中，不需要额外词库文件。

## 实现边界

- 不写入、不替换原 `StutterFix.dll`。
- 只修补 `StutterFix.SettingsWindow.T`、语言按钮状态和标题栏按钮绘制。
- 卸载本模组时只移除 Harmony ID `StutterFixZh`，不会移除其它模组的补丁。
- 词库覆盖原 DLL 全部 585 处静态 `T()` 调用（735 个词条），并包含动态效果名、滤镜分组和提示模板。

## 构建

```powershell
dotnet build StutterFixZh\StutterFixZh.csproj -c Release --nologo -v minimal
```

输出为 `StutterFixZh\bin\Release\StutterFixZh.dll`。

## 许可证

[MIT](LICENSE)。原版 [Stutter Fix](https://github.com/pding4569/StutterFix) 版权归其作者所有。
