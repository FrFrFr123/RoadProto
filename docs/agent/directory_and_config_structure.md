# Agent 目录、配置与日志分区

## 文档目录

Agent 相关文档按以下位置维护：

```text
docs/
  agent/
    README.md
    mvp_architecture.md
    backend_service_contract.md
    wpf_agent_console.md
    directory_and_config_structure.md
    skill_system.md
    skills/
      subgrade_template_skill.md
    agents/
      subgrade_template_create_agent.md  # 早期历史文档，当前已收敛为 subgrade_template Skill 下的创建 Intent
    intents/
      subgrade_template_create.md
      subgrade_template_modify.md
      subgrade_template_delete.md
      subgrade_template_query.md
  business/
    agent/
      Agent控制台_MVP.md
      路基模板创建Agent_MVP验证.md
      路基模板Skill_增删改查_MVP.md
  modules/
    agent.md
  reuse/
    engineering_agent_mvp.md
```

`docs/agent/` 讲 Agent 底座、十二层流程、Skill / Intent 规范，`docs/business/agent/` 讲具体用户功能和验证场景，`docs/modules/agent.md` 只做模块索引和边界。

## 仓库分区

RoadProto 本体仓库：

```text
F:\0_GPT_道路设计原型功能项目
```

独立 Agent 后端仓库：

```text
F:\0_GPT_RoadProtoAgentBackend
```

RoadProto 仓库不新增 `services/agent_backend/` 后端源码目录。后端实现、测试、发布和服务部署都在独立后端仓库中管理。RoadProto 仓库只保留接口契约、WPF 面板、本地 HTTP 客户端和 CAD Adapter。

## RoadProto 规划源码目录

MVP 后续实现时，RoadProto 仓库建议新增以下源码区域：

```text
src/
  domain/
    agent/
      AgentSchemas.*
      AgentTrace.*
      AgentRisk.*
      AgentToolContract.*
  application/
    agent/
      AgentClientService.*
      AgentBackendClient.*
      AgentBackendProcessSupervisor.*
      AgentLocalToolRegistry.*
      AgentExecutionControl.*
      subgrade/
        SubgradeTemplateAgentMapper.*
  cad_adapter/
    objectarx/
      agent/
        ObjectArxAgentContextAdapter.*
        ObjectArxAgentToolAdapter.*
  modules/
    agent/
      AgentModule.*
  ui/
    wpf/
      RoadProto.Terrain.UI/
        AgentConsolePalette.xaml
        AgentConsolePalette.xaml.cs
        ViewModels/
          AgentConsoleViewModel.cs
          AgentSettingsViewModel.cs
          AgentTraceViewModel.cs
        Bridge/
          AgentConsoleDtos.cs
          AgentConsoleFile.cs
        AutoCad/
          AgentConsoleCommands.cs
```

RoadProto 侧的 `AgentBackendClient` 只负责 HTTP 通信和错误转换；`AgentBackendProcessSupervisor` 只负责健康检查、启动后端进程和状态回报，不承载 Agent 编排逻辑。

## 独立后端仓库规划目录

独立后端仓库建议采用以下结构：

```text
F:\0_GPT_RoadProtoAgentBackend\
  README.md
  RoadProtoAgentBackend.sln
  src/
    RoadProtoAgentBackend.Api/
    RoadProtoAgentBackend.Application/
    RoadProtoAgentBackend.Domain/
    RoadProtoAgentBackend.Infrastructure/
  config/
    agents/
    skills/
    models/
    prompts/
    schemas/
    rules/
    tools/
    approval/
    risk/
  tests/
    RoadProtoAgentBackend.Tests/
  artifacts/
    publish/
      RoadProtoAgentBackend.exe
```

后端配置决定“Agent 启用哪些 Skill、Skill 允许哪些 Intent 和 Tool”，RoadProto 本地配置决定“当前 RoadProto 插件允许后端调用什么”。

## 配置分区

后端用户配置默认保存到：

```text
%APPDATA%\RoadProtoAgent\settings.json
```

其中 API Key 必须使用 Windows DPAPI 加密，WPF 和日志只能看到脱敏摘要。

后端配置建议包含：

```text
agent_registry.json
main_agent_manifest.json
business_agent_manifest/*.json
skill_registry.json
skill_manifest/*.json
model_provider_registry.json
api_channel_registry.json
model_policy.json
router_config.json
schema_registry.json
rule_registry.json
tool_registry.json
approval_policy.json
risk_policy.json
prompt_manifest.json
evaluation_cases/*.json
```

RoadProto 本地配置建议包含：

```text
local_agent_client.json
backend_process.json
local_tool_adapter_manifest.json
roadproto_tool_scope.json
local_trace_policy.json
```

`backend_process.json` 至少记录：

- 后端监听地址：`http://127.0.0.1:17861`
- 发布版 exe：`F:\0_GPT_RoadProtoAgentBackend\artifacts\publish\RoadProtoAgentBackend.exe`
- 开发期 csproj：`F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Api\RoadProtoAgentBackend.Api.csproj`
- 启动超时。
- 健康检查重试间隔。

## 模型 Provider 分区

MVP 支持 Provider：

```text
deepseek
qwen
glm
gpt
```

每个 Provider 至少记录：

- 显示名。
- API Base URL。
- API Key 加密值。
- 默认模型名。
- 启用状态。
- 连接测试最近结果。

## Skill 与业务 Agent 分区

每个 Skill 至少有独立分区：

```text
skill_manifest
intent_manifest
param_schema
target_schema
rules
tool_scope
approval_policy
risk_policy
prompts
evaluation_cases
business_doc
```

Skill 不能把规则只写在 Prompt 中。Prompt 负责引导模型输出，Schema 负责结构，规则引擎负责默认值、推导和校验。

业务 Agent 只负责组织一组 Skill，不能绕过 Skill 直接绑定 Tool。Intent 必须挂在 Skill 下，Tool 必须在 Skill 白名单内。

## 工具分区

每个工具必须声明：

- 工具名。
- 工具说明。
- 输入 Schema。
- 输出 Schema。
- 侧作用。
- 风险等级。
- 是否支持 DryRun。
- 是否需要审批。
- 是否支持回滚。
- 所属 RoadProto 模块。
- 本地 Adapter 名称。
- 错误码。
- 版本。
- 状态。
- 所属 Agent。
- 所属 Skill。
- 允许调用它的 Intent。

写入类工具必须同时在后端 Tool Registry 和 RoadProto 本地 Tool Adapter 白名单中注册。

## Trace 分区

Trace 建议分为：

```text
workflow_trace
agent_route_trace
skill_route_trace
llm_trace
schema_trace
rule_trace
tool_trace
dryrun_trace
approval_trace
execution_trace
adapter_trace
error_trace
evaluation_trace
```

后端保留全链路 Trace，RoadProto 本地保留与 AutoCAD 上下文、工具执行和错误相关的本地 Trace 镜像。

每次运行必须带：

```text
TraceId
SessionId
TaskId
AgentId
SkillId
IntentId
```

## 日志分区

后端日志默认目录：

```text
F:\0_GPT_RoadProtoAgentRuntime\logs\backend\
```

RoadProto 本地日志默认目录：

```text
F:\0_GPT_RoadProtoAgentRuntime\logs\roadproto\
```

默认保留最近 14 天，或总量最多 1GB。超过限制时按最旧文件优先清理。

日志至少覆盖：

- 面板启动。
- 后端健康检查。
- 后端自动启动。
- 模型配置和连接测试摘要。
- 用户输入摘要。
- 状态机流转。
- Schema 校验。
- 规则命中。
- 工具计划。
- DryRun。
- 审批。
- Bridge / Adapter 调用。
- CAD 执行结果。
- 异常、重试和回滚。

日志不得包含明文 API Key。

## 测试分区

MVP 测试分为：

- 后端单元测试：模型网关 fake provider、Schema 校验、规则、状态机、Tool Registry、DPAPI 配置保存、日志清理。
- RoadProto 核心测试：不依赖 AutoCAD 的 Agent Schema、状态映射、工具契约和路基模板参数映射。
- 托管 Bridge 测试：WPF Agent Console DTO、后端响应 DTO、模型设置 DTO 和请求/响应文件。
- AutoCAD 手工验证：Ribbon 打开可停靠 Agent Console、自动启动后端、配置模型、DryRun、审批、点取插入点、生成实体、Trace 查看。
