![FireAxe Logo](FireAxe.GUI/Assets/AppLogo.ico)
# FireAxe
**[English](README.md)** | **简体中文**

[下载](https://github.com/ktxiaok/FireAxe/releases)

FireAxe（旧称L4D2AddonAssistant）是一个开源的用于管理求生之路2模组的GUI工具。它有以下这些特性：
- 通过组的概念层级式地管理模组
- 支持下载创意工坊物品和合集
- 模组启用状态管理
- 模组标签管理
- VPK冲突检测系统
- 模组优先级管理
- 模组依赖管理
- 模组问题检测系统
- 支持模组的导入和导出
- 本地VPK文件管理
- VPK信息显示
- 创意工坊物品信息显示
- 支持模组的搜索和过滤
- ...

这个项目目前处于早期开发阶段，更多的功能将会在未来加入。

![主窗口](Images/MainWindow.png)
![主窗口和下载列表窗口](Images/MainWindowAndDownloadListWindow.png)
![VPK冲突列表窗口](Images/VpkConflictListWindow.png)
## 入门
- 在开始之前，你需要准备一个存储模组的文件夹。（注意：这个文件夹「不是」“left4dead2/addons”或者“left4dead2/addons/workshop”！)
- 打开FireAxe并点击菜单项“文件 -> 打开目录”来打开这个文件夹。
- 点击菜单项“文件 -> 设置”来设置你的游戏路径。
- 如果你有本地的VPK文件，把它们放在文件夹中。然后点击菜单项“文件 -> 导入”来导入它们。
- 如果你想要创意工坊的模组，右键打开上下文菜单然后点击“新建->创意工坊模组”。然后填写项目ID。FireAxe会自动下载你需要的文件。
- 你可以创建一些组来管理模组。
- 启用你想启用的模组然后点击菜单项“操作 -> 推送”将启用状态推送到游戏。
- 你可以点击检查按钮来保持状态更新。