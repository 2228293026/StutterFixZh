## Stutter Fix 中文补丁 v0.1.9

### 修复
- 修复语言按钮点击归属：韩文/英文切换交还原模组处理，避免重复保存；样式缺失时不再吞掉点击
- 修复乱码还原逻辑：改用 C1 控制字符作为乱码信号 + 严格 UTF-8 解码，不再误伤正常文本
- 修复 "Turn off <效果名>" 等小写动态文本的翻译残留（新增大小写不敏感词典回退）

### 安装
UMM 中 Install Mod 选择 ZIP，或解压到 `A Dance of Fire and Ice/UMMMods/StutterFixZh/`。
