# 贡献指南 (Contributing)

感谢你考虑为 **Lumen** 做出贡献！这是一个个人维护的 Windows 桌面覆盖层项目，欢迎Issue、建议与 PR。

## 行为准则

- 讨论与提交请保持友善、就事论事。
- 提交 Issue 前请先搜索是否已有类似问题，避免重复。

## 如何报告问题

请在仓库的 Issues 中提供：

1. **复现步骤**：尽量具体（操作序列 + 期望 / 实际结果）。
2. **环境信息**：Windows 版本、.NET 8 运行时是否安装、Lumen 版本/构建配置。
3. **日志**：程序异常会写入 `%TEMP%/lumen.log`，请附上相关片段（去除敏感信息）。

## 开发环境搭建

要求 **Windows 10/11 + .NET 8 SDK**。

```powershell
git clone https://github.com/pingju555/lumen.git
cd lumen/src/lumen
dotnet build -c Release
.\bin\Release\net8.0-windows10.0.22621.0\lumen.exe
```

> 项目是纯 WPF / C#，无第三方 UI 框架，无需 `npm` / `restore` 外部包。
> 本地运行配置默认在**程序（exe）所在文件夹**（便携：配置随 exe 走）；可在「设置 → 数据存储位置」改到任意文件夹并一键迁移；旧 `%LocalAppData%/Lumen/` 数据首次启动会提示迁移。

## 代码规范

- 语言：C#，命名遵循 .NET 惯例（PascalCase 类型/方法，camelCase 私有字段）。
- 文件编码：UTF-8，行尾 CRLF（Windows 项目，git 会自动处理）。
- 提交信息：中文或英文均可，建议「动词 + 简述」，例如：
  - `fix: 修复切档后触发器未重置的问题`
  - `feat: 新增 Progress 原子环形样式`
  - `docs: 补充公式引擎函数说明`
- 新增原子 / 数据源 / 函数时，请同步更新 `Resources/help_manual.json` 对应的使用手册页。

## 提交 PR

1. Fork 本仓库并基于 `main` 创建特性分支（`feat/xxx` 或 `fix/xxx`）。
2. 确保 `dotnet build -c Release` 通过、无新增警告。
3. 在 PR 描述中说明：改动动机、影响范围、自测情况。
4. 涉及行为变更的，请在 `CONTRIBUTING` 或 `README` 同步文档。

## 许可证

提交即视为同意你的贡献以 **MIT 许可证** 发布（见 [LICENSE](LICENSE)）。
