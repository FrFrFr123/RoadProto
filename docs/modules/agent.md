# Agent 模块

## 模块定位

Agent 模块用于承载 RoadProto 内的可控工程 Agent 本地能力。它是独立模块，不属于横断面、平面、纵断面或出图出表模块。

Agent 模块是薄模块，只负责 WPF Agent Console、后端健康检查和自动启动、HTTP 通信、本地上下文摘要、本地工具适配、DryRun / 执行桥接和本地 Trace 镜像。Agent 编排、任务状态机、模型网关、API Key 加密配置、规则、Tool Registry、Trace 汇总和评测治理放在独立后端仓库：

```text
F:\0_GPT_RoadProtoAgentBackend
```

## 模块编码

```text
AGENT
```

## 命令前缀

```text
RD_AGENT_
```

## 已实现命令

| 命令 | 显示名 | 类型 | 业务文档 |
| --- | --- | --- | --- |
| `RD_AGENT_CONSOLE` | Agent 控制台 | 用户入口 | `docs/business/agent/Agent控制台_MVP.md` |
| `RD_AGENT_HEALTH` | Agent 后端健康检查 | 诊断入口 | `docs/business/agent/Agent控制台_MVP.md` |
| `RD_AGENT_LOGS` | Agent 日志目录 | 诊断入口 | `docs/business/agent/Agent控制台_MVP.md` |
| `RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE` | Agent 路基模板工具 | 本地 Tool Adapter | `docs/business/agent/路基模板Skill_增删改查_MVP.md` |

命令必须通过 `CommandRegistry` 注册，Ribbon 可见按钮必须同步更新 C++ Ribbon 模型和托管 WPF Ribbon。当前托管 Ribbon 的 `Agent 控制台` 按钮只排队发送 `RD_AGENT_CONSOLE`，不得直接创建 WPF 窗口或 Palette；`RD_AGENT_CONSOLE` 会确认 `RoadProto.Terrain.UI.dll` 已加载并转发到托管命令 `RD_AGENT_CONSOLE_UI`。托管命令在 AutoCAD 2021 中使用 `PaletteSet` 创建可停靠面板，默认停靠在右侧，并在 `Visible / Dock / Activate` 后延迟挂载 `AgentConsoleSafePanel`；面板尺寸必须由 `PaletteSet.SizeChanged` 同步到 WPF 宿主和安全面板，不能只依赖 WPF `Stretch`。

## Ribbon 位置

规划位置：

```text
RoadProto / 工程 Agent / Agent 控制台
```

MVP 不新增独立 Web 页面，不使用 WebView。所有交互在 AutoCAD 内 WPF Agent Console 中完成；AutoCAD 2021 当前稳定入口为默认右侧停靠的 `PaletteSet`。

## 代码落点

RoadProto 本体仓库源码位置：

- `src/modules/agent/`
- `src/cad_adapter/objectarx/agent/`
- `src/ui/wpf/RoadProto.Terrain.UI/Agent/`
- `src/ui/wpf/RoadProto.Terrain.UI/Agent/Bridge/AgentLocalToolBridge.cs`
- `src/ui/wpf/RoadProto.Terrain.UI/Agent/Backend/AgentBackendClient.cs`
- `src/ui/wpf/RoadProto.Terrain.UI/Agent/AgentConsolePalette.xaml`
- `src/ui/wpf/RoadProto.Terrain.UI/Agent/AgentConsoleSafePanel.cs`
- `src/ui/wpf/RoadProto.Terrain.UI/Agent/AgentConsoleCommands.cs`

独立后端仓库位置：

```text
F:\0_GPT_RoadProtoAgentBackend
```

RoadProto 侧必须遵守 `docs/agent/backend_service_contract.md`，不得把后端 Orchestrator、模型调用、Credential Store 或 Tool Registry 主控逻辑写入 RoadProto 仓库。

## 后端连接和启动

Agent 模块打开面板时检查：

```text
http://127.0.0.1:17861/health
```

如果后端不可用，Agent 模块按配置启动：

```text
F:\0_GPT_RoadProtoAgentBackend\artifacts\publish\RoadProtoAgentBackend.exe
```

启动失败时，WPF 面板展示后端路径、端口、错误原因、重试按钮和手动选择后端 exe 入口。MVP 不安装 Windows Service，但后端仓库预留后续 Windows Service 部署。

## 文档索引

| 文档 | 用途 |
| --- | --- |
| `docs/agent/README.md` | Agent 文档区入口 |
| `docs/agent/mvp_architecture.md` | MVP 总体架构 |
| `docs/agent/backend_service_contract.md` | 独立后端服务契约 |
| `docs/agent/wpf_agent_console.md` | WPF 可停靠 Agent Console 交互规范 |
| `docs/agent/directory_and_config_structure.md` | 目录、配置与日志分区 |
| `docs/agent/skill_system.md` | Agent / Skill / Intent 全局规范 |
| `docs/agent/skills/subgrade_template_skill.md` | 路基模板 Skill 规则 |
| `docs/agent/intents/subgrade_template_create.md` | 路基模板创建 Intent |
| `docs/agent/intents/subgrade_template_modify.md` | 路基模板修改 Intent |
| `docs/agent/intents/subgrade_template_delete.md` | 路基模板删除 Intent |
| `docs/agent/intents/subgrade_template_query.md` | 路基模板查询 Intent |
| `docs/agent/agents/subgrade_template_create_agent.md` | 早期路基模板创建 Agent 历史验证说明，当前已收敛为 `subgrade_template` Skill 下的创建 Intent |
| `docs/business/agent/Agent控制台_MVP.md` | Agent 控制台业务文档 |
| `docs/business/agent/路基模板创建Agent_MVP验证.md` | 早期路基模板创建 Agent 历史业务文档，当前已收敛为 `subgrade_template` Skill 下的创建 Intent |
| `docs/business/agent/路基模板Skill_增删改查_MVP.md` | 路基模板 Skill 增删改查验证业务文档 |
| `docs/reuse/engineering_agent_mvp.md` | 可复用 Agent 底座说明 |

## 模块边界

- Agent 模块可以路由到其他业务模块的工具能力。
- Agent 模块不能绕过其他模块的 application / domain / cad_adapter 边界。
- Agent 模块不能把业务规则写进 WPF 事件。
- Agent 模块不能让 LLM 直接生成 AutoCAD 脚本并执行。
- Agent 模块不能把外置后端服务当作 DWG 写入者。
- Agent 模块不能保存明文 API Key。
- Agent 模块不能承载主 Orchestrator、模型网关和后端 Tool Registry。

## 模型配置

WPF 面板提供 DeepSeek、阿里千问、GLM、GPT 的配置入口。配置保存由后端完成：

```text
%APPDATA%\RoadProtoAgent\settings.json
```

API Key 使用 Windows DPAPI 加密。WPF 只显示脱敏状态和连接测试结果。

## 日志和 Trace

Agent 模块必须记录 AutoCAD 侧本地日志：

```text
F:\0_GPT_RoadProtoAgentRuntime\logs\roadproto\
```

后端日志位置：

```text
F:\0_GPT_RoadProtoAgentRuntime\logs\backend\
```

默认保留最近 14 天，或总量最多 1GB。WPF 面板必须提供流转日志视图、打开日志目录和复制 `TraceId`。

Agent Console 启动探针固定写入：

```text
F:\0_GPT_RoadProtoAgentRuntime\logs\roadproto\agent_palette_startup_probe.log
```

该探针用于定位 Ribbon 排队、`PaletteSet` 创建、最小 WPF 宿主注册、`Visible / Dock / Activate`、安全面板挂载、Loaded 和 ViewModel 初始化阶段的宿主崩溃问题。

## MVP 验证范围

首个验证场景已收敛为路基模板 Skill：

- 通过 WPF 输入自然语言。
- 后端路由到 `roadproto_engineering_agent` 下的 `subgrade_template` Skill。
- 在 Skill 内识别 `create / modify / delete / query` Intent。
- 提取模板名称、道路等级、目标模板、宽度变化等字段。
- 规则引擎补默认值和校验。
- 本地 Adapter DryRun。
- WPF 展示结构化执行计划。
- 用户审批。
- 复用 `RD_SECTION_SUBGRADE_TEMPLATE_APPLY_DIALOG_FILE` 创建 `DnSubgradeTemplateEntity`。
- 修改、删除、查询通过 `RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE` 进入本地受控 Tool Adapter；该命令已支持按 handle、名称和执行时点选定位路基模板，查询模板摘要，修改实体参数和部件级增删改，删除目标 `DnSubgradeTemplateEntity`。
- WPF 通过结果文件读取本地工具结果，并回传后端 `/api/agent/runs/{taskId}/tool-result`。
- 记录 Trace 和全流程日志。
