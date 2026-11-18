# blazor-cncnet-client-plugin-cw

CW 的客户端插件仓库，独立于 `blazor-cncnet-client` 主仓库。

## 目录

```
cw-ui/                     CW UI 插件（Razor 类库）
├── CwUi.csproj
├── CwUiPluginEntry.cs     插件入口（ICncNetPlugin）
├── plugin.ini             插件自描述（Id/ApiVersion/EntryType/Style）
├── plugin.css             插件样式（随输出复制）
├── _Imports.razor
└── Components/
    ├── CwHome.razor       独立页面 /plugin/cw-ui/home
    └── CwSettingsAside.razor  设置页侧栏卡片（追加型槽位）
```

## 构建

前置：客户端仓库已产出契约包
`blazor-cncnet-client/dist/nuget/CnCNetClient.Plugin.Abstractions.1.0.0.nupkg`
（`tools/publish.ps1 -PackPluginSdk`），且 `NuGet.config` 中本地源路径正确。

```powershell
dotnet build cw-ui\CwUi.csproj -c Release
```

产物在 `cw-ui\bin\Release\net10.0\`：`CwUi.dll`、`plugin.ini`、`plugin.css`。

## 部署

1. 把上述文件复制到游戏根目录 `Plugins\cw-ui\`（契约 DLL 与宿主程序集**不要**放进去）；
2. 在游戏根 `Resources\ClientDefinitions.ini` 追加：

```ini
[Plugins]
cw-ui=Plugins/cw-ui
```

3. 重启客户端，日志中应出现 `插件已加载：cw-ui`。

## 说明

- 插件与宿主共享默认 `AssemblyLoadContext`，可直接 `@inject` 宿主服务；
- `ApiVersion` 必须等于宿主 `PluginApi.CurrentVersion`，契约升级后需同步修改并重新编译；
- 插件私有依赖才放进插件目录，第三方库同名不同版本会相互覆盖。
