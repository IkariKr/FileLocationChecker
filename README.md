# FileLocationChecker (文件定位检查工具)

[![.NET 8.0 WPF](https://img.shields.io/badge/.NET-8.0--WPF-blue.svg)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

一个基于 C# WPF 开发的图形化桌面工具，用于检索并列出【文件夹 A】中的文件在【文件夹 B】中的准确位置。

---

## 🌟 功能特性

- 📁 **文件夹选取**：支持在界面中快速选择源文件夹 A 与目标文件夹 B。
- ⚙️ **灵活配置规则**：
  - `递归检查 A`（默认未勾选）：开启后递归检索文件夹 A 中的所有层级子目录。
  - `检查文件名`（默认已勾选）：匹配 A 与 B 中的文件名是否一致。
  - `检查文件大小`（默认已勾选）：匹配 A 与 B 中的文件字节大小（Length）是否完全一致。
  - `排除 A 路径`（默认已勾选）：当 A 为 B 的子文件夹时，在 B 的扫描结果中自动排除 A 路径及其子目录下的文件自身。
- ⚡ **高效算法**：为文件夹 B 建立高效索引映射，即使面对数万个文件的复杂目录架构，定位比对也在毫秒内完成。
- 📊 **可视化数据表格 (DataGrid)**：
  - 展示匹配状态：`已定位` (匹配 1 处)、`未找到` 或 `找到 N 处匹配`。
  - 支持多列点击排序与检索结果摘要统计。
  - **右键菜单 & 双击**：直接打开资源管理器并高亮定位指定文件，或一键复制文件路径到剪贴板。
- 🔄 **响应式 UI**：基于后台异步 Task + 进度回调，扫描时不卡顿，并支持随时随时手动取消任务。

---

## 🚀 编译与运行

### 前置要求

- Windows 10 / 11 操作系统
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 或更高版本

### 命令行构建与运行

```bash
# 克隆仓库
git clone https://github.com/IkariKr/FileLocationChecker.git
cd FileLocationChecker

# 构建项目
dotnet build

# 运行 UI 程序
dotnet run --project src/FileLocationChecker/FileLocationChecker.csproj

# 执行单元测试
dotnet test
```

---

## 🛠️ 项目结构

```text
FileLocationChecker/
├── FileLocationChecker.sln          # Solution 文件
├── .gitignore                       # Git 忽略配置
├── README.md                        # 说明文档
├── src/
│   └── FileLocationChecker/         # WPF 主程序
│       ├── App.xaml / App.xaml.cs
│       ├── MainWindow.xaml / MainWindow.xaml.cs
│       ├── Models/                  # 数据模型与配置类
│       └── Services/                # 文件搜寻与匹配核心逻辑
└── tests/
    └── FileLocationChecker.Tests/   # xUnit 自动化单元测试
```

---

## 📄 开源协议

本项目基于 [MIT License](LICENSE) 协议开源。
