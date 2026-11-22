## Stutter Fix 中文补丁 v0.1.10

同步 Stutter Fix 2.8.2 的界面文案。

### 新增
- `0.5 ms timer resolution`（0.5 毫秒计时器分辨率）及其新说明：0.5 毫秒已改为默认开启，并补充输入处理与 ISLC 相关的说明
- `Verbose log`（详细日志）及其说明
- `Stutter Fix v{0} available · press {1} → Home to update`（新版本提示条）
- `Instant moves`（瞬时移动）、`On`（开）

### 移除
- 已被上游替换的 `0.5 ms timer request (experimental)` 及其旧说明

### 校验
- 词库 740 条，覆盖上游 2.8.2 全部 756 处可静态求值的 `T()` 调用，0 处遗漏
- 其中 1 条（编辑器播放加速的说明）依赖乱码还原路径匹配，已单独验证

### 安装
UMM 中 Install Mod 选择 ZIP，或解压到 `A Dance of Fire and Ice/UMMMods/StutterFixZh/`。
