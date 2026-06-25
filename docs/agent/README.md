# 可控工程 Agent 文档区

本目录是 RoadProto 可控工程 Agent 的独立文档区。它描述 Agent 底座、独立后端仓库、RoadProto 本地 `AGENT` 薄模块、WPF 可停靠 Agent Console、Agent / Skill / Intent、Schema / Rule / Tool / Adapter / DryRun / Approval / Trace 的边界和扩展方式。

Agent 文档区不替代 `docs/business/`、`docs/modules/` 和 `docs/reuse/`：

- `docs/agent/` 记录 Agent 架构、服务契约、配置分区、日志追踪和扩展规范。
- `docs/agent_builder/` 记录跨项目可复用的 Agent 搭建方法、12 层模块说明、入口路由、Skill / Intent / Tool 模板和 RoadProto 实践结论。
- `docs/business/agent/` 记录具体用户入口、Skill / Intent 验证场景和交互流程。
- `docs/modules/agent.md` 记录 RoadProto 内 `AGENT` 模块职责、命令清单和文档索引。
- `docs/reuse/engineering_agent_mvp.md` 记录可复用 Agent 底座能力。

## 固定仓库位置

RoadProto 本体仓库固定为：

```text
F:\0_GPT_道路设计原型功能项目
```

Agent 后端采用独立仓库，固定位置为：

```text
F:\0_GPT_RoadProtoAgentBackend
```

后端仓库使用 `.NET 8 / ASP.NET Core`。RoadProto 仓库不放后端实现源码，只保留本地 `AGENT` 模块、WPF 交互、HTTP 客户端、CAD Bridge / Adapter 和接口契约文档。当前 MVP 已在该独立仓库实现健康检查、Provider 设置、DPAPI 凭据、日志和 Agent Run 状态机。

## MVP 定位

本项目中的 Agent MVP 指“架构完整的最小闭环”，不是临时 Prompt Demo，也不是只服务路基模板的单点功能。

MVP 必须保留完整链路，当前版本已实现基础闭环：

```text
WPF 可停靠 Agent Console
-> RoadProto 本地 AGENT 薄模块
-> HTTP Client
-> 独立 Agent 后端服务
-> Agent / Skill / Intent 路由
-> Model Gateway
-> Orchestrator 状态机
-> Context Manager
-> LLM 意图识别 / 参数提取 / 追问 / 解释
-> Schema 校验
-> 规则引擎
-> Tool Registry
-> RoadProto 本地 Tool Adapter
-> DryRun
-> 用户审批
-> 正式执行
-> Trace / 日志 / 评测样例沉淀
```

首个验证场景选用“路基模板 Skill 的增删改查意图”，原因是它已有清晰的领域模型、默认值、WPF Bridge、CAD 自定义实体和核心测试基础。路基模板只用于验证 Agent 底座闭环，不代表 Agent 架构隶属于横断面模块。

当前路基模板创建链路在用户确认后复用既有 `RD_SECTION_SUBGRADE_TEMPLATE_APPLY_DIALOG_FILE`，由 C++ ObjectARX Adapter 完成 `DnSubgradeTemplateEntity` 写入，并通过结果文件回传后端 `/tool-result`。修改、删除和查询链路通过 `RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE` 进入本地受控 Tool Adapter。

## 十二层受控流程

RoadProto Agent 总流程固定按以下十二层组织：

```text
用户 / 前端
↓
01 前端交互层 Agent Console
↓
02 Agent 配置中心 / 后端后台配置服务
↓
03 模型网关 Model Gateway
↓
04 工作流编排层 Orchestrator
↓
05 上下文管理层 Context Manager
↓
06 LLM 能力层：意图识别 / 参数提取 / 追问 / 解释
↓
07 Schema 结构化控制层
↓
08 规则引擎层 Rule Engine
↓
09 工具与原子函数层 Tool Registry
↓
10 软件 Adapter 层 EICAD Adapter
↓
11 执行控制层 DryRun / 审批 / 执行 / 保存 / 回滚
↓
12 Trace / 评测 / 治理层
```

`Agent / Skill / Intent` 必须嵌入这十二层：第 04 层启动 Agent 和 Skill 路由，第 06 层让模型在候选范围内识别 Intent 和参数，第 07 层做结构化约束，第 08 层做规则裁决，第 09 层校验 Tool 白名单，第 11 层执行审批和写入控制，第 12 层记录可回放 Trace。

## 核心约束

- Agent 后端服务外置并位于独立仓库 `F:\0_GPT_RoadProtoAgentBackend`。
- 后端负责 Agent 编排、任务状态机、模型网关、API Key 加密保存、Schema 校验、规则引擎、Tool Registry、Trace、日志和评测治理。
- RoadProto 内新增独立 `AGENT` 薄模块，只负责 WPF 入口、HTTP 通信、本地上下文摘要、受控工具适配、DryRun / 执行桥接和本地 Trace 镜像。
- 不新增独立 Web 前端，不嵌入 WebView。用户交互统一放在 AutoCAD 内可停靠 WPF Palette / 面板中。
- WPF 只负责输入、展示、参数修改、审批、模型设置和 Trace 查看，不直接调用模型 API，不直接操作 CAD 实体。
- 外置后端不能直接修改 DWG。所有 CAD 动作必须经过 RoadProto 本地 `AGENT` 模块和 ObjectARX Adapter。
- LLM 只负责在已注册 Agent / Skill 范围内做意图识别、参数提取、追问建议和解释，不直接补默认值、不直接推导强规则、不直接调用工具。
- 默认值必须由后端规则文件或规则引擎补全。当前路基模板创建默认值位于 `F:\0_GPT_RoadProtoAgentBackend\rules\intents\subgrade_template.create.yaml`，RoadProto 本地 Tool Adapter 只负责执行和校验。
- 路基模板创建的默认值必须按完整组件结构下发。`defaultComponentsByRoadGrade` 是规则层字段，RoadProto 本地不得用 `laneWidth`、`hardShoulderWidth`、`earthShoulderWidth`、`medianWidth` 或 `slopeRatio` 等标量重新拼默认模板。
- 工程对象必须先在入口路由层分清楚。`道路模型` 是独立工程对象，不属于 `subgrade_template` Skill；当前 MVP 未接入 `road_model` Skill 时必须明确提示不支持，不得误调用路基模板工具。
- 明确未接入的工程对象应返回终止性 `UnsupportedWorkflow` 结果，不进入 `AwaitingUserInput`，避免后续用户输入被旧任务粘住。
- `AwaitingUserInput` 只代表“等待本次追问的有效补充”，不得把用户后续所有输入无条件粘到旧任务上。若补充内容本身明确是闲聊、咨询、运行时事实问题、未接入对象或新的完整工程指令，后端必须记录 `ContinuationRerouted` 并重新执行入口路由；例如修改模板缺目标时用户输入“你好”，应跳出旧追问进入 `ChatOnly`。
- 新增业务能力时，必须先定义 Skill，再在 Skill 下拆分 Intent；不得只新增孤立意图文档或让 Intent 直接绑定 Tool。
- 写入类动作必须先 DryRun，再由用户在 WPF 中审批，最后才允许执行。
- 全流程必须打印和记录关键流转信息，所有任务都带 `TraceId` / `SessionId` / `TaskId`。WPF 可见日志要消费后端 `AgentRun.events`，展示每个流程阶段及阶段输出；原始 stage / message 保留在文件日志中。
- WPF 聊天区也不得直接展示 `subgrade_template`、`subgrade_template.create`、`medium`、`SubgradeTemplate.Create` 等机器码；这些只保留在 Trace / 文件日志中。
- WPF 打开 Agent Console 时必须读取后端已保存的 Provider 设置并恢复上次模型配置；API Key 只显示配置状态，不回填明文。
- 写入类任务的确认 / 取消按钮应出现在对话区域内，不得长期固定放在发送按钮旁边。
- 日志不得打印明文 API Key，也不得完整泄露敏感配置。

## 模型支持

MVP 支持在 WPF 中配置以下模型 Provider：

- DeepSeek
- 阿里千问
- GLM
- GPT

WPF 提供模型设置页，用户输入 Provider、API Base URL、API Key、默认模型名、启用状态，并可测试连接。保存时由后端接口接收配置，后端写入：

```text
%APPDATA%\RoadProtoAgent\settings.json
```

API Key 使用 Windows DPAPI 做本机用户级加密，不写入 RoadProto 仓库，不进入 Git，不在 Trace 中明文展示。

## 后端启动策略

WPF 可停靠 Agent Console 打开时，RoadProto 本地 `AGENT` 模块先检查：

```text
http://127.0.0.1:17861/health
```

如果连接失败，RoadProto 以本地 companion process 方式自动启动：

```text
F:\0_GPT_RoadProtoAgentBackend\artifacts\publish\RoadProtoAgentBackend.exe
```

启动后轮询 `/health`。失败时 WPF 面板必须展示后端路径、端口、错误原因、重试按钮和手动选择后端 exe 的入口。MVP 不安装 Windows Service，但文档和后端工程应预留后续 Windows Service 部署模式。

## 日志与追踪

MVP 日志默认位置：

```text
F:\0_GPT_RoadProtoAgentRuntime\logs\backend\
F:\0_GPT_RoadProtoAgentRuntime\logs\roadproto\
```

默认保留最近 14 天，或总量最多 1GB。WPF 面板必须提供流转日志视图、打开日志目录和复制 `TraceId` 的入口。

## UI 参考

WPF Agent Console 的交互和视觉密度参考：

```text
E:\Download\google\道路设计软件对话框.zip
```

参考包中的 JS / React 代码只作为交互结构和视觉层级参考：CAD 侧边栏、聊天流、可折叠 thinking、内嵌选项按钮、滑入设置面板、模型/API 设置、深浅色切换。最终实现必须是 WPF 可停靠 Palette，不引入独立 Web 页面。

## 文档索引

| 文档 | 用途 |
| --- | --- |
| `mvp_architecture.md` | Agent MVP 总体架构、仓库边界和 RoadProto 嵌入边界 |
| `backend_service_contract.md` | 独立 Agent 后端服务职责、接口、启动和安全边界 |
| `wpf_agent_console.md` | WPF 可停靠 Agent Console 的页面区域、状态和交互规则 |
| `directory_and_config_structure.md` | 后续源码、配置、日志、评测样例和文档分区 |
| `skill_system.md` | Agent / Skill / Intent 在十二层受控流程中的作用和全局规范 |
| `intent_rule_template.md` | Agent 意图规则文档固定模板 |
| `../agent_builder/README.md` | 跨项目可复用 Agent 搭建能力文档区入口 |
| `../agent_builder/maintenance_policy.md` | Agent 相关修改后的可复用文档同步规则 |
| `skills/subgrade_template_skill.md` | 路基模板 Skill 的意图、共享规则、工具白名单和 Trace 规范 |
| `agents/subgrade_template_create_agent.md` | 早期路基模板创建链路历史验证说明，当前已收敛为 `subgrade_template` Skill 下的创建 Intent |
| `intents/subgrade_template_create.md` | 路基模板创建意图的表达、参数、确认、工具和 Trace 规则 |
| `intents/subgrade_template_modify.md` | 路基模板修改意图规则 |
| `intents/subgrade_template_delete.md` | 路基模板删除意图规则 |
| `intents/subgrade_template_query.md` | 路基模板查询意图规则 |
