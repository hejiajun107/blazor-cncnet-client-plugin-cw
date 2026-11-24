# AGENTS.md

本仓库是 CNCNet Blazor 客户端的 CW 插件（`cw-ui`）。以下规则对本仓库内工作的 AI 助手与协作者生效。

## 硬性规则

1. **修改客户端宿主源码，必须先告知用户并取得同意，禁止擅自修改**
   - 宿主源码位于 `f:\work\PhotinoCLient\blazor-cncnet-client`，**不属于本仓库**。
   - 动手前必须先说明：要改哪些文件、为什么插件/资源 INI 做不到、影响范围；用户明确同意后才能改。
   - 改完必须在变更记录里单独列出（文件路径 + 改动摘要）。

2. **优先用插件 + 资源 INI 实现**
   - UI 外观、布局、扩展入口优先在 `cw-ui` 内用插件槽位 + CSS 实现，不改宿主。
   - 显隐、文案、开关类配置优先走 `Resources\*.ini`（`MainMenu.ini`、`GenericWindow.ini`、`ExtrasWindow.ini`、`ClientDefinitions.ini` 等）。
   - 现有宿主扩展点：`page.menu.list`、`page.mainmenu`、`page.campaign.aside`、`page.settings.aside`、`page.lobby.aside`、`layout.topbar`、`layout.footer`、`menu.entry`。

3. **变更记录**
   - 部署与配置改动统一记录到 mod 目录的 `F:\cw202b\CWBlazorClient\MIGRATION-CHANGES.md`（逐条：文件路径 + 改动摘要）。
   - 该变更记录文件**不进任何仓库**。
