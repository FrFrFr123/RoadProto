# 可控工程 Agent MVP 总体架构

## 目标

可控工程 Agent MVP 的目标是在 RoadProto 中建立一套可扩展、可追踪、可测试、可审批的工程智能体底座。MVP 需要架构完整，但业务能力先控制在一个验证场景内。

本轮选择的验证场景是“路基模板 Skill 的创建意图”。它用于证明自然语言输入可以经过 Agent 路由、Skill 边界、Intent 识别、Schema、规则、工具、DryRun、审批和执行闭环，最终安全调用现有 RoadProto 能力生成工程对象。

## 仓库和运行形态

RoadProto 本体仓库：

```text
F:\0_GPT_道路设计原型功能项目
```

Agent 后端独立仓库：

```text
F:\0_GPT_RoadProtoAgentBackend
```

后端技术栈：

```text
.NET 8 / ASP.NET Core
```

RoadProto 仓库只承载本地 `AGENT` 薄模块、WPF 可停靠面板、HTTP 客户端、CAD Bridge / Adapter 和接口契约文档。Agent 编排、任务状态机、工具注册、模型调用、API Key 加密配置、日志与评测均放在独立后端仓库。

## 总体形态

MVP 采用“独立后端服务 + RoadProto 本地 AGENT 薄模块 + WPF 可停靠 Agent Console”的形态。

```text
用户
  |
  v
AutoCAD 内 WPF 可停靠 Agent Console
  |
  v
RoadProto AGENT 薄模块
  |
  v
HTTP Client
  |
  v
独立 Agent 后端服务 (.NET 8 / ASP.NET Core)
  |
  +--> Model Gateway
  +--> Agent Config Center
  +--> Orchestrator
  +--> Schema Validator
  +--> Rule Engine
  +--> Tool Registry
  +--> Trace / Log / Eval Store
  |
  v
RoadProto 本地 Tool Adapter
  |
  v
ObjectARX / application / domain / cad_adapter
```

## 分层职责

### WPF 可停靠 Agent Console

WPF 是唯一用户交互界面。它以 AutoCAD Palette / 可停靠面板形式常驻，不建设独立 Web 前端，不嵌入 WebView。

WPF 负责：

- 接收自然语言任务。
- 展示后端服务连接、自动启动状态和健康检查结果。
- 展示总体 Agent、本次路由的 Skill 和 Intent。
- 提供 DeepSeek、阿里千问、GLM、GPT 的模型配置和测试连接入口。
- 展示结构化参数、字段来源和置信度。
- 展示规则命中、风险分级和阻断原因。
- 展示 DryRun 预览、影响范围和工具计划。
- 提供用户修改参数、补充信息、审批、取消、重试和查看 Trace 的入口。
- 展示全流程流转日志，并支持打开日志目录和复制 `TraceId`。

WPF 不负责：

- 不直接调用模型 API。
- 不保存明文 API Key。
- 不直接调用 ObjectARX。
- 不直接写入 DWG。
- 不拼接业务规则结论。

### RoadProto 本地 AGENT 薄模块

RoadProto 内新增独立 `AGENT` 模块。该模块不是横断面模块的一部分，也不属于具体道路业务模块。它只负责把独立 Agent 后端与 RoadProto 当前 CAD 环境安全连接起来。

本地 `AGENT` 模块负责：

- 注册 `RD_AGENT_` 前缀命令和 Ribbon 入口。
- 打开 WPF 可停靠 Agent Console。
- 检查后端健康状态，必要时自动拉起本地后端进程。
- 维护后端 HTTP 客户端和超时、重试、错误转换。
- 读取当前 DWG、当前文档、选中对象和可用 RoadProto 工具摘要。
- 把后端要求的工具调用转成 RoadProto 本地 Adapter 调用。
- 执行本地 DryRun、预览和正式写入。
- 对写入动作做本地二次校验，防止绕过审批或工具权限。
- 保存 AutoCAD 侧本地 Trace 镜像和流转日志。

本地 `AGENT` 模块不负责：

- 不持有模型 API Key。
- 不做多模型调度。
- 不做 Orchestrator 主状态机。
- 不做 Agent 配置中心。
- 不让 LLM 直接生成 AutoCAD 脚本并执行。
- 不替代既有业务模块的 domain / application 能力。

### 独立 Agent 后端服务

独立后端服务是 Agent 主控层。MVP 中它以本机 companion process 运行，后续可扩展为 Windows Service、企业内网服务或私有化模型部署。

独立后端负责：

- Agent Manifest、Skill 注册表、业务 Agent 注册表、工具范围和审批策略。
- DeepSeek、阿里千问、GLM、GPT 的模型网关和 API 通道管理。
- API Key 保存、加密、测试连接和脱敏展示。
- Orchestrator 主状态机。
- Context Package 组装和裁剪。
- 入口路由，先区分闲聊、咨询、工作流候选和明确工程指令。
- 在已注册 Agent / Skill 范围内做意图识别、参数提取、追问和结果解释。
- Schema 校验和参数标准化。
- 默认值、推导、业务校验、风险和审批规则。
- Tool Registry 校验和工具计划生成。
- Trace、日志、失败样例和最小评测集沉淀。

独立后端不负责：

- 不直接访问 AutoCAD 原始对象指针。
- 不直接写 DWG。
- 不绕过 RoadProto 本地 Adapter 执行工具。
- 不把模型输出直接当作工程执行结果。

## 后端自动启动

WPF 面板打开时，RoadProto 本地 `AGENT` 模块先访问：

```text
http://127.0.0.1:17861/health
```

如果连接失败，则按配置启动后端发布版：

```text
F:\0_GPT_RoadProtoAgentBackend\artifacts\publish\RoadProtoAgentBackend.exe
```

开发期可以支持从源码工程启动：

```text
F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Api\RoadProtoAgentBackend.Api.csproj
```

启动后轮询 `/health`。如果仍失败，WPF 面板必须显示端口、后端路径、错误原因、重试按钮和手动选择后端 exe 的入口。

MVP 采用本地 companion process，不安装 Windows Service。后端工程预留 Windows Service 封装和部署文档，作为后续产品化部署方式。

## MVP 状态机

MVP 状态机应保留完整节点，但每个节点先实现最小能力。

```text
PanelOpened
BackendHealthChecking
BackendStarting
BackendConnected
InputReceived
EntryRouted
ConfigLoaded
ContextLoaded
AgentRouted
SkillRouted
IntentRecognized
ParamsExtracted
SchemaValidated
ParamsNormalized
RulesApplied
BusinessValidated
RiskClassified
PlanGenerated
DryRunExecuted
PreviewReady
WaitingApproval
Executing
ResultValidated
Explained
Traced
Completed
Failed
Cancelled
```

参数不足时进入：

```text
WaitingUserInput
UserInputCompleted
```

入口路由阶段可能直接结束任务或进入追问：

```text
ChatOnly -> ControlledConversation -> Completed，不调用模型意图识别，不调用 Tool
HelpOnly -> ControlledConsultation -> Completed，不调用模型意图识别，不调用 Tool
WorkflowCandidate -> WaitingUserInput，追问动作或对象
WorkflowCommand -> AgentRouted，继续 Skill / Intent 识别
```

`ChatOnly` 和 `HelpOnly` 不等于固定话术兜底。它们应进入受控对话通道：允许调用已配置的大模型生成自然回答，但系统提示必须限定角色为 RoadProto 道路设计 Agent，并明确禁止声称已修改 CAD、创建模板、删除对象、保存 DWG 或调用 Tool。

受控对话通道必须先经过运行时事实层。当前日期、当前本地时间、当前模型 Provider、当前模型名和 Agent 身份属于后端确定事实，不允许交给模型猜。若用户询问“今天几号”“你是什么模型”“当前模型版本”等运行状态问题，后端应直接依据运行时事实回答，并记录 `RuntimeFactsAnswered`；普通闲聊或咨询再把同一组事实注入 prompt 后调用模型。

执行失败且本地 Adapter 支持恢复时进入：

```text
RollingBack
RolledBack
RollbackFailed
```

所有状态流转必须写入 WPF 流转日志、后端日志和 RoadProto 本地日志。

## 数据流

1. 用户打开 WPF 可停靠 Agent Console。
2. RoadProto 本地 `AGENT` 模块检查 `/health`，必要时自动拉起独立后端。
3. 用户选择或配置模型 Provider、API Base URL、API Key 和默认模型。
4. WPF 把模型配置提交给后端，后端使用 DPAPI 加密保存。
5. 用户输入自然语言任务。
6. RoadProto 本地 `AGENT` 模块生成 `InputSchema`，附带当前 DWG 和选择对象摘要。
7. 独立后端加载 Agent 配置、Skill 注册表、模型策略、工具范围和审批策略。
8. Orchestrator 创建 `TraceId`、`SessionId`、`TaskId` 和状态机实例。
9. Orchestrator 执行入口路由，区分闲聊、咨询、工作流候选和明确工程指令。
10. 若为 `ChatOnly` 或 `HelpOnly`，进入受控对话通道，先检查运行时事实问题；命中日期、时间、模型身份等事实问题时由后端确定性回答，不调用模型。
11. 其余 `ChatOnly` 或 `HelpOnly` 输入调用大模型生成自然回复，但必须注入运行时事实，不进入 Skill / Intent，不调用 Tool。
12. 若为 `WorkflowCandidate`，返回结构化追问，不执行 Tool。
13. 若为 `WorkflowCommand`，确定候选 Agent 和候选 Skill。
14. Context Manager 按候选 Skill 构造 Context Package。
15. LLM 在候选 Skill 内识别 Intent，并提取用户明确表达的参数。
16. Schema Validator 校验 Agent、Skill、Intent、字段、类型、单位、来源和置信度。
17. Rule Engine 对 Agent / Skill / Intent 做最终裁决，补默认值、推导字段、校验业务规则并判断风险。
18. Tool Registry 校验 Tool 是否在 Skill 白名单中，并生成工具计划。
19. RoadProto 本地 Adapter 执行 DryRun，不修改 DWG。
20. WPF 展示 DryRun 预览、影响范围、规则命中、执行计划和审批按钮。
21. 用户确认后，RoadProto 本地 Adapter 正式调用 application / cad_adapter 创建或修改对象。
22. Execution Control 校验执行结果。
23. Trace 写入后端 Trace Store 和本地 Trace 镜像。
24. 失败样例按结构化格式进入评测样例库。

## 与 RoadProto 现有架构的关系

Agent 模块必须遵守 RoadProto 当前分层：

| RoadProto 层 | Agent 落点 |
| --- | --- |
| `modules` | 注册 `AGENT` 模块和 `RD_AGENT_` 命令 |
| `application` | 本地 Agent 客户端流程、HTTP 客户端、工具适配、DryRun 和执行控制 |
| `domain` | Agent Schema、字段来源、风险、Trace 等不依赖 ObjectARX 的结构 |
| `cad_adapter/objectarx` | 当前 DWG 上下文读取、对象选择、预览和写入 |
| `ui/wpf` | 可停靠 Agent Console、审批面板、Trace 查看和模型设置 |
| 独立后端仓库 | 模型网关、主 Orchestrator、配置中心、Credential、评测和治理 |

## MVP 必做

- 新增独立 Agent 文档区和模块说明。
- 明确独立后端仓库 `F:\0_GPT_RoadProtoAgentBackend`。
- 定义 `.NET 8 / ASP.NET Core` 后端服务契约。
- 定义 `AGENT` 薄模块、`RD_AGENT_` 命令前缀和 WPF Ribbon / Palette 入口。
- 定义后端自动启动、健康检查和失败提示流程。
- 定义多模型 Provider 配置、DPAPI 加密和连接测试流程。
- 定义 RoadProto 本地 Agent Adapter 契约。
- 定义 Agent / Skill / Intent 分层和十二层受控流程。
- 定义最小 Schema 集合。
- 定义最小 Tool Registry 和工具六道门。
- 定义 DryRun、Approval、Execution、Rollback、Trace、日志的结构。
- 用路基模板 Skill 的创建意图走通完整链路。

## MVP 不做

- 不做独立 Web 前端。
- 不嵌入 WebView。
- 不做复杂后台可视化配置平台。
- 不安装 Windows Service。
- 不做多 Agent 自治。
- 不做模型自由工具规划。
- 不做大规模 RAG 知识库。
- 不做多客户配置继承。
- 不做复杂权限矩阵。
- 不做完整 DWG 文件级备份恢复。
- 不做跨软件分布式事务。

## 验收标准

- 用户能在 AutoCAD 内可停靠 WPF 面板中发起自然语言任务。
- 面板打开时能自动检查并拉起后端服务。
- `/health` 正常时能显示后端版本和状态。
- 用户能配置 DeepSeek、阿里千问、GLM、GPT 的 API 信息并测试连接。
- API Key 不以明文写入配置文件和日志。
- 系统能显示总体 Agent、路由 Skill、Intent 和路由原因。
- 系统能显示当前 Skill、Intent 和 Tool 白名单校验结果。
- 模型输出必须经过 Schema 校验。
- 默认值和强业务规则必须由规则引擎处理。
- 工具必须注册、授权、校验后才能调用。
- 写入类工具必须 DryRun。
- 写入前必须用户审批。
- 外置后端不能直接写 DWG。
- WPF 不能直接调用 ObjectARX 或模型 API。
- 每次运行都有 `TraceId` / `SessionId` / `TaskId` 和完整流转日志。
- 日志保存到 `F:\0_GPT_RoadProtoAgentRuntime\logs\backend\` 和 `F:\0_GPT_RoadProtoAgentRuntime\logs\roadproto\`，默认保留 14 天或最多 1GB。
- 路基模板 Skill 验证场景能生成可检查的 DryRun 结果，并在用户确认后创建 `DnSubgradeTemplateEntity`。
