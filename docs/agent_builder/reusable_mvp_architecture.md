# 可复用 Agent MVP 架构

## 1. 目标

本架构用于在任意工程软件中搭建一个可控 Agent MVP。MVP 的业务范围可以很小，但架构要能继续扩展到多业务、多 Skill、多 Provider、多工具和可评测运行。

## 2. 推荐运行形态

```text
宿主软件内交互面板
  ↓
宿主软件薄模块 / 插件
  ↓ HTTP / IPC
独立 Agent 后端服务
  ↓
模型 Provider / 配置 / 编排 / 规则 / Trace
  ↓ HTTP / IPC
宿主软件 Adapter
  ↓
工程数据读写
```

### 2.1 宿主软件内交互面板

负责：

- 用户自然语言输入。
- 模型和 API 配置入口。
- 追问展示。
- 参数确认。
- DryRun 预览。
- 用户审批。
- Trace 和日志查看。

不负责：

- 直接调用模型。
- 保存明文 API Key。
- 执行业务规则。
- 直接写工程数据。

### 2.2 宿主软件薄模块

负责：

- 面板注册和打开。
- 后端健康检查。
- 后端自动启动。
- HTTP / IPC 客户端。
- 当前选中对象、当前工程状态等上下文摘要。
- 本地 Tool Adapter 调用。
- 本地日志镜像。

### 2.3 独立 Agent 后端

负责：

- Agent Run 状态机。
- Agent / Skill / Intent 路由。
- 模型网关。
- API Key 加密保存。
- Context Package 构建。
- LLM 调用。
- Schema 校验。
- 规则引擎。
- Tool Registry。
- Trace、日志、评测样例沉淀。

后端不得直接写宿主软件工程数据。

### 2.4 宿主软件 Adapter

负责：

- 读取工程对象。
- DryRun。
- 执行已审批工具。
- 保存结果。
- 返回结构化结果。

Adapter 不负责模型推理和意图识别。

## 3. 十二层流程

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
10 软件 Adapter 层 Software Adapter
↓
11 执行控制层 DryRun / 审批 / 执行 / 保存 / 回滚
↓
12 Trace / 评测 / 治理层
```

## 4. MVP 最小状态机

```text
Created
InputReceived
Routed
ContextPrepared
ModelCalled
IntentResolved
SchemaValidated
RulesApplied
AwaitingUserInput
PlanGenerated
DryRunReady
AwaitingUserConfirmation
DispatchingTool
Executing
Succeeded
Failed
Cancelled
```

MVP 可以合并部分内部状态，但 Trace 中应保留阶段事件。

## 5. MVP 最小接口

```text
GET  /health
GET  /api/settings/models
PUT  /api/settings/models/{provider}
POST /api/agent/runs
GET  /api/agent/runs/{taskId}
POST /api/agent/runs/{taskId}/user-input
POST /api/agent/runs/{taskId}/confirm
POST /api/agent/runs/{taskId}/cancel
POST /api/agent/runs/{taskId}/tool-result
GET  /api/agent/runs/{taskId}/trace
```

接口名可随项目调整，但语义不应丢失。

## 6. MVP 最小数据对象

```text
AgentRun
AgentRunEvent
ContextPackage
ModelProviderSettings
ModelExtractionResult
IntentResolutionResult
AgentPlan
ToolCall
ToolResult
TraceRecord
```

其中 `AgentRun` 应包含：

- `traceId`
- `sessionId`
- `taskId`
- `state`
- `followUpMessage`
- `plan`
- `dispatchedToolCall`
- `toolResult`
- `events`

`followUpMessage` 是实践中补出的关键字段。缺少它时，前端容易退化成“请补充必要信息”这种不可定位的兜底文案。

## 7. MVP 最小安全要求

- API Key 不进前端日志。
- API Key 不进 Trace。
- API Key 不进 Git。
- 写入动作必须确认。
- 删除动作默认高风险。
- Tool 必须白名单注册。
- Adapter 只接受结构化 ToolCall。
- 失败时返回结构化错误，不让前端只收到 500。

## 8. MVP 最小日志策略

日志建议分为：

```text
backend/
host/
trace/
eval_samples/
```

推荐保留策略：

```text
保留最近 14 天，或总量最多 1GB
```

日志必须能按 `traceId` 串起前端、后端、Adapter 和执行结果。

## 9. 下一项目复用步骤

1. 复制本目录文档。
2. 替换宿主软件名称和 Adapter 名称。
3. 明确第一个验证 Skill。
4. 写 Skill 文档。
5. 写 1 到 4 个 Intent 文档。
6. 定义 Schema 和 Tool 白名单。
7. 搭建后端状态机和模型网关。
8. 搭建宿主软件面板和 Adapter。
9. 跑通一个 DryRun + 审批 + 执行闭环。
10. 用 Trace 反推规则和评测样例。

