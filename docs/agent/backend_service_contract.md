# 独立 Agent 后端服务契约

## 定位

独立 Agent 后端服务是可控工程 Agent 的主控服务。它不依赖 AutoCAD，不直接写 DWG，不持有 ObjectARX 对象，只通过结构化接口和 RoadProto 本地 `AGENT` 薄模块协作。

后端仓库固定规划为：

```text
F:\0_GPT_RoadProtoAgentBackend
```

后端技术栈固定为：

```text
.NET 8 / ASP.NET Core
```

MVP 后端服务以本机 companion process 运行。后续可扩展为 Windows Service、企业内网部署或私有化模型部署，但不能改变“后端不直接写 DWG”的边界。

## 默认地址和发布路径

默认监听地址：

```text
http://127.0.0.1:17861
```

健康检查：

```text
GET http://127.0.0.1:17861/health
```

发布版默认 exe：

```text
F:\0_GPT_RoadProtoAgentBackend\artifacts\publish\RoadProtoAgentBackend.exe
```

开发期项目路径：

```text
F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Api\RoadProtoAgentBackend.Api.csproj
```

RoadProto WPF 面板打开时，如果 `/health` 不通，由 RoadProto 本地 `AGENT` 模块按配置启动后端进程并轮询健康检查。

## 后端职责

后端服务负责：

- 管理总体 Agent 和业务 Agent 配置。
- 管理 DeepSeek、阿里千问、GLM、GPT 的模型清单、API 通道、模型策略和 Credential 引用。
- 接收 WPF 提交的模型配置，使用 Windows DPAPI 做本机用户级加密保存。
- 调用模型网关并统一返回结构。
- 维护 Orchestrator 状态机和 `TaskId`。
- 构造和裁剪 Context Package。
- 校验 LLM 输出是否符合 Schema。
- 运行默认值、推导、校验、风险和审批规则。
- 管理 Tool Registry、工具契约和工具权限。
- 生成工具调用计划和 DryRun 请求。
- 记录全链路 Trace 和结构化日志。
- 把失败样例转换为评测样例。

后端服务不负责：

- 不直接调用 AutoCAD API。
- 不直接打开、修改或保存 DWG。
- 不保存明文模型 API Key 到 WPF、Trace 或日志。
- 不信任模型输出绕过 Schema、规则、工具、DryRun 或审批。
- 不绕过 RoadProto 本地 Adapter 执行写入动作。

## RoadProto 本地职责

RoadProto 本地 `AGENT` 模块负责：

- 打开 WPF 可停靠 Agent Console。
- 检查后端 `/health`，必要时拉起本地后端进程。
- 提供当前 DWG、选中对象、RoadProto 版本和可用工具摘要。
- 维护 HTTP 客户端、超时、重试和错误转换。
- 接收后端生成的工具调用计划。
- 调用本地 Tool Adapter 执行 read、dry_run、execute、rollback。
- 将本地执行结果、受影响对象和错误码回传后端。
- 维护 AutoCAD 侧本地 Trace 镜像和流转日志。

## 最小接口

MVP 后端服务使用 HTTP JSON。后续如增加 gRPC 或本机 IPC，也必须保留同等语义的接口契约。

| 接口 | 方向 | 用途 |
| --- | --- | --- |
| `GET /health` | RoadProto -> 后端 | 检查后端服务是否可用 |
| `GET /v1/agents` | RoadProto -> 后端 | 获取总体 Agent、业务 Agent、模型/API 通道摘要 |
| `GET /v1/models/providers` | RoadProto -> 后端 | 获取 DeepSeek、阿里千问、GLM、GPT 等 Provider 配置要求 |
| `GET /v1/settings/models` | RoadProto -> 后端 | 获取脱敏后的模型配置 |
| `PUT /v1/settings/models` | RoadProto -> 后端 | 保存模型配置和 API Key，由后端加密 |
| `POST /v1/settings/models/test` | RoadProto -> 后端 | 测试指定模型通道连接 |
| `POST /v1/runs` | RoadProto -> 后端 | 创建一次 Agent 运行，返回 `TraceId` / `SessionId` / `TaskId` 和初始状态 |
| `POST /v1/runs/{task_id}/context` | RoadProto -> 后端 | 提交当前 DWG 和选中对象摘要，推进上下文节点 |
| `POST /v1/runs/{task_id}/user-input` | RoadProto -> 后端 | 提交用户自然语言或追问补充 |
| `POST /v1/runs/{task_id}/tool-result` | RoadProto -> 后端 | 回传本地工具执行结果 |
| `POST /v1/runs/{task_id}/approval` | RoadProto -> 后端 | 回传用户审批、取消或修改参数动作 |
| `GET /v1/runs/{task_id}` | RoadProto -> 后端 | 获取当前状态、参数、规则、DryRun、执行和 Trace 摘要 |
| `GET /v1/runs/{task_id}/trace` | RoadProto -> 后端 | 获取可展示 Trace |
| `GET /v1/runs/{task_id}/logs` | RoadProto -> 后端 | 获取本次任务脱敏流转日志摘要 |

## 健康检查响应

`GET /health` 正常时返回：

```json
{
  "status": "ok",
  "service": "RoadProtoAgentBackend",
  "version": "0.1.0",
  "started_at": "2026-06-24T00:00:00+08:00"
}
```

RoadProto 侧只依赖 `status`、`service` 和 `version`，其他字段可扩展。

## 模型配置结构

### ModelProviderSettings

```json
{
  "provider": "deepseek",
  "display_name": "DeepSeek",
  "api_base_url": "https://api.deepseek.com",
  "api_key": "user-input-secret",
  "default_model": "deepseek-chat",
  "enabled": true
}
```

支持的 `provider`：

```text
deepseek
qwen
glm
gpt
```

后端保存配置时必须：

- API Key 使用 Windows DPAPI 加密。
- 脱敏返回，例如 `sk-****abcd`。
- 日志中不得打印 `api_key` 原文。
- 连接测试只返回成功/失败、耗时、错误摘要，不返回密钥。

配置文件默认位置：

```text
%APPDATA%\RoadProtoAgent\settings.json
```

## 核心消息结构

### RunCreateRequest

```json
{
  "session_id": "local-session-id",
  "main_agent_id": "roadproto_engineering_agent",
  "user_input": "创建一个高速公路路基模板",
  "roadproto_version": "v0.1.x",
  "document_summary": {
    "document_name": "example.dwg",
    "is_saved": false
  },
  "selected_model": {
    "provider": "deepseek",
    "model_id": "deepseek-chat",
    "api_channel_id": "local_deepseek"
  }
}
```

### RunCreateResponse

```json
{
  "trace_id": "trace-id",
  "session_id": "local-session-id",
  "task_id": "task-id",
  "state": "InputReceived"
}
```

### ToolCallRequest

```json
{
  "trace_id": "trace-id",
  "task_id": "task-id",
  "tool_call_id": "tool-call-id",
  "tool_name": "preview_subgrade_template",
  "operation_mode": "dry_run",
  "input_schema": "PreviewSubgradeTemplateInputSchema",
  "input": {},
  "requires_approval": false,
  "risk_level": "low"
}
```

### ToolResult

```json
{
  "trace_id": "trace-id",
  "task_id": "task-id",
  "tool_call_id": "tool-call-id",
  "status": "succeeded",
  "output_schema": "PreviewSubgradeTemplateOutputSchema",
  "output": {},
  "affected_objects": [],
  "rollback_token": "",
  "error": null
}
```

### ApprovalDecision

```json
{
  "trace_id": "trace-id",
  "task_id": "task-id",
  "decision": "approved",
  "approved_by": "local_user",
  "approved_at": "2026-06-24T00:00:00+08:00",
  "parameter_overrides": {}
}
```

## 流转日志

每个任务必须至少记录以下流转事件：

- 面板打开。
- 后端健康检查。
- 后端自动启动。
- 当前模型 Provider、模型名和 API 通道。
- 用户输入摘要。
- 后端状态机节点变化。
- LLM 调用 provider、model、耗时和 token 用量。
- Schema 校验结果。
- 规则命中结果。
- 工具计划。
- DryRun 请求和结果。
- 用户确认、取消或修改参数。
- RoadProto 本地 Adapter 调用名称、入参摘要和结果。
- CAD 执行结果。
- 异常、重试和回滚结果。

不得记录：

- 明文 API Key。
- 完整敏感配置。
- 未脱敏的工程私密路径。
- 大段完整 DWG 原始数据。

## 日志保存策略

后端日志：

```text
F:\0_GPT_RoadProtoAgentRuntime\logs\backend\
```

RoadProto 本地日志：

```text
F:\0_GPT_RoadProtoAgentRuntime\logs\roadproto\
```

默认保留最近 14 天，或总量最多 1GB。超过限制时按最旧文件优先清理。WPF 面板提供“打开日志目录”和“复制 TraceId”入口。

## 安全规则

- API Key 只能在后端 Credential Store 或运行期环境变量中读取。
- WPF 只展示模型和 API 通道代号，不展示密钥原文。
- Trace 中不得包含明文 API Key、完整 DWG 原始数据或未脱敏敏感路径。
- 后端只能请求已注册工具。
- RoadProto 本地 Adapter 必须二次校验工具是否在本地白名单中。
- 写入类工具在 `approval=approved` 前不得执行。
- 后端请求 `execute` 时必须携带对应 DryRun 结果标识，RoadProto 本地模块必须校验其仍然有效。
- 任意执行请求都必须带 `TraceId` / `TaskId`，否则 RoadProto 本地 Adapter 拒绝执行。

## 错误码

MVP 至少统一以下错误类型：

| 错误码 | 含义 |
| --- | --- |
| `BACKEND_UNAVAILABLE` | 后端服务不可用 |
| `BACKEND_START_FAILED` | 后端自动启动失败 |
| `MODEL_PROVIDER_NOT_CONFIGURED` | 模型 Provider 未配置 |
| `MODEL_CONNECTION_FAILED` | 模型连接测试失败 |
| `MODEL_CALL_FAILED` | 模型调用失败 |
| `SCHEMA_INVALID` | Schema 校验失败 |
| `RULE_BLOCKED` | 规则阻断 |
| `TOOL_NOT_ALLOWED` | 工具未授权 |
| `DRYRUN_FAILED` | DryRun 失败 |
| `APPROVAL_REQUIRED` | 需要审批 |
| `APPROVAL_CANCELLED` | 用户取消 |
| `LOCAL_ADAPTER_FAILED` | RoadProto 本地 Adapter 执行失败 |
| `EXECUTION_PARTIAL` | 部分成功，需人工检查 |
| `ROLLBACK_FAILED` | 回滚失败 |

## 评测与 Trace

每次运行后端必须记录：

- `TraceId`
- `SessionId`
- `TaskId`
- Agent 版本
- 模型 Provider、模型名和 API 通道
- Prompt / Schema / Rule / Tool 版本
- Context Package 摘要
- LLM 原始输出和结构化解析结果
- Schema 校验结果
- 规则命中结果
- 工具计划
- DryRun 结果
- 用户审批记录
- 本地工具执行结果
- 失败样例转换结果
