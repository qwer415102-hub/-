# BEOFN · 批量名称提取工具

Windows 免安装小工具，批量提取文件和子文件夹名称，支持带后缀与不带后缀两种模式，结果可复制或导出为 TXT。

## 下载与使用

下载仓库中的 [名称提取工具-免安装.zip](名称提取工具-免安装.zip)，解压后双击 `名称提取工具.exe`。

1. 添加或拖入一个或多个文件夹。
2. 选择“不带后缀”或“文件名＋后缀”。
3. 选择提取文件、子文件夹或两者。
4. 点击“开始提取”，确认预览后复制结果或导出 TXT。

运行环境：Windows 10/11，系统自带的 .NET Framework 4.x。无需安装 Python，无需联网或管理员权限。

## 功能

- `报告.docx` → 不带后缀时为 `报告`，带后缀时为 `报告.docx`。
- 多重后缀只去除最后一段：`archive.tar.gz` → `archive.tar`。
- 保留完整文件夹名称，如 `实验.v2`；保留 `.gitignore` 等无其他后缀的点文件。
- 可递归扫描、包含隐藏项、保留路径以区分重名。
- UTF-8 BOM 编码 TXT，每行一个名称；预览上限为 5000 项，复制及导出包含全部结果。
- 不递归进入目录联接和符号链接；无法访问的目录会显示跳过提示。
- 两个 BAT 提供与图片需求相同的一键导出方式，详见 [使用说明](使用说明.txt)。

## 构建与验证

在 Windows PowerShell 或 PowerShell 中运行：

```powershell
.\build.ps1
.\test.ps1
```

构建使用 Windows 自带的 .NET Framework C# 编译器，不需要 NuGet 依赖。构建产物在 `名称提取工具` 目录。

`test.ps1` 检查中文及特殊字符、后缀处理、文件夹名称、递归、重名、隐藏项、重叠目录、TXT 编码及窗口模式切换，并在 `.verification` 下生成测试程序和窗口预览。

发布包可重新生成：

```powershell
Compress-Archive -LiteralPath '.\名称提取工具' -DestinationPath '.\名称提取工具-免安装.zip' -Force
```

## 相似开源方案

开发前检索并查看了 [DirLister](https://github.com/SanderSade/DirLister)，它提供多格式目录列表导出。本项目针对简单中文使用流程，以 .NET Framework / Windows Forms 独立实现，未复制 DirLister 源码。
