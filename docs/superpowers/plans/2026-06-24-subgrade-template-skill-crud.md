# 路基模板 Skill 增删改查 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让 `subgrade_template` Skill 及其创建、修改、删除、查询四个 Intent 从自然语言进入十二层受控流程，并最终由 RoadProto 本地 Tool Adapter 执行。

**Architecture:** 后端仓库 `F:\0_GPT_RoadProtoAgentBackend` 负责 Agent / Skill / Intent 规则、模型网关、Schema 解析、规则裁决、状态机和 Tool 白名单。RoadProto 主仓库 `F:\0_GPT_道路设计原型功能项目` 负责 WPF 展示、用户补充输入、审批、本地 Tool Adapter、ObjectARX 写入和只读查询。模型只在已注册 Agent / Skill 范围内做 Intent 识别、参数提取、追问建议和解释，不能补默认值或直接调用工具。

**Tech Stack:** .NET 8 / ASP.NET Core, xUnit, System.Text.Json, YamlDotNet, WPF .NET Framework 4.8, C++17, AutoCAD 2021 / ObjectARX 2021.

---

## 0. 执行前准备

**必读文档：**

- `F:\0_GPT_道路设计原型功能项目\AGENTS.md`
- `F:\0_GPT_道路设计原型功能项目\README.md`
- `F:\0_GPT_道路设计原型功能项目\docs\dev\ai_development_rules.md`
- `F:\0_GPT_道路设计原型功能项目\docs\coding_rules.md`
- `F:\0_GPT_道路设计原型功能项目\docs\agent\skill_system.md`
- `F:\0_GPT_道路设计原型功能项目\docs\agent\skills\subgrade_template_skill.md`
- `F:\0_GPT_道路设计原型功能项目\docs\agent\intents\subgrade_template_create.md`
- `F:\0_GPT_道路设计原型功能项目\docs\agent\intents\subgrade_template_modify.md`
- `F:\0_GPT_道路设计原型功能项目\docs\agent\intents\subgrade_template_delete.md`
- `F:\0_GPT_道路设计原型功能项目\docs\agent\intents\subgrade_template_query.md`
- `F:\0_GPT_RoadProtoAgentBackend\README.md`

**基线验证：**

- [ ] 运行后端测试：

```powershell
dotnet test F:\0_GPT_RoadProtoAgentBackend\RoadProtoAgentBackend.sln
```

Expected: 全部通过。

- [ ] 运行 RoadProto 托管 Bridge 测试：

```powershell
dotnet run --project F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-mvp\tests\RoadProtoManagedBridgeTests\RoadProtoManagedBridgeTests.csproj -c Debug
```

Expected: `All RoadProto managed bridge tests passed.`

- [ ] 运行 RoadProto 核心测试：

```powershell
& "D:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe" F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-mvp\tests\RoadProtoCoreTests.vcxproj /p:Configuration=Debug /p:Platform=x64
F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-mvp\artifacts\x64\Debug\RoadProtoCoreTests.exe
```

Expected: `All RoadProto core tests passed.`

若任一基线失败，先记录失败命令和错误，不进入实现。

---

## 1. 文件结构

### 后端仓库

```text
F:\0_GPT_RoadProtoAgentBackend\
  rules\
    skills\
      subgrade_template.skill.yaml
    intents\
      subgrade_template.create.yaml
      subgrade_template.modify.yaml
      subgrade_template.delete.yaml
      subgrade_template.query.yaml
  src\
    RoadProtoAgentBackend.Domain\
      Skills\
        AgentSkill.cs
      IntentRules\
        IntentRule.cs
        IntentRuleParameter.cs
        IntentRuleTarget.cs
        IntentRuleToolBinding.cs
      Models\
        ModelExtractionResult.cs
      Tools\
        AgentPlan.cs
        SubgradeTemplateToolArguments.cs
      Runs\
        AgentRun.cs
        AgentRunState.cs
    RoadProtoAgentBackend.Application\
      Skills\
        ISkillRepository.cs
        SkillRegistryService.cs
      IntentRules\
        IIntentRuleRepository.cs
        IntentRuleService.cs
        IntentResolutionResult.cs
      Models\
        IModelGateway.cs
        ModelGatewayRequest.cs
        ModelGatewayResponse.cs
        ModelPromptBuilder.cs
        ModelExtractionParser.cs
      Agents\
        SubgradeTemplateAgent.cs
      Runs\
        AgentRunService.cs
    RoadProtoAgentBackend.Infrastructure\
      Skills\
        FileSkillRepository.cs
      IntentRules\
        FileIntentRuleRepository.cs
      Models\
        OpenAiCompatibleModelGateway.cs
    RoadProtoAgentBackend.Api\
      Program.cs
      Endpoints\
        AgentRunEndpoints.cs
  tests\
    RoadProtoAgentBackend.Tests\
      SkillRuleRepositoryTests.cs
      ModelPromptBuilderTests.cs
      ModelExtractionParserTests.cs
      IntentRuleServiceTests.cs
      SubgradeTemplateAgentTests.cs
      AgentRunServiceTests.cs
      FakeModelGateway.cs
```

### RoadProto 仓库

```text
F:\0_GPT_道路设计原型功能项目\
  docs\
    business\
      agent\
        路基模板Skill_增删改查_MVP.md
  src\
    cad_adapter\
      objectarx\
        agent\
          ObjectArxAgentSubgradeTemplateToolCommand.h
          ObjectArxAgentSubgradeTemplateToolCommand.cpp
    modules\
      agent\
        AgentModule.cpp
    ui\
      wpf\
        RoadProto.Terrain.UI\
          Agent\
            AgentConsoleViewModel.cs
            Backend\
              AgentBackendClient.cs
            Bridge\
              AgentLocalToolBridge.cs
            Models\
              AgentDtos.cs
  tests\
    core_tests.cpp
    RoadProtoManagedBridgeTests\
      Program.cs
```

---

## Task 1: 后端领域模型加入 Agent / Skill / Intent

**Files:**

- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Domain\Skills\AgentSkill.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Domain\IntentRules\IntentRule.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Domain\Models\ModelExtractionResult.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Domain\Tools\AgentPlan.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Domain\Tools\SubgradeTemplateToolArguments.cs`
- Modify: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Domain\Runs\AgentRun.cs`
- Modify: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Domain\Runs\AgentRunState.cs`
- Test: `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\SkillRuleModelTests.cs`

- [ ] **Step 1: 写失败测试**

Create `SkillRuleModelTests.cs` with assertions that:

- `AgentSkill` contains `AgentId=roadproto_engineering_agent` and `Id=subgrade_template`.
- `AgentPlan` contains `AgentId`、`SkillId`、`IntentId`、`ToolName`、`RiskLevel`、`RequiresApproval`。
- `ModelExtractionResult` contains `AgentId`、`SkillId`、`IntentId`、`Target`、`Parameters`。
- `AgentRunState` contains `SkillRouted` and `AwaitingUserInput`。

Run:

```powershell
dotnet test F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\RoadProtoAgentBackend.Tests.csproj --filter SkillRuleModelTests
```

Expected: FAIL because types do not exist.

- [ ] **Step 2: 实现最小领域模型**

`AgentSkill` must include:

```text
Id
Version
DisplayName
AgentId
BusinessModule
HumanDoc
Intents
ToolWhitelist
TraceStages
```

`IntentRule` must include:

```text
Id
SkillId
AgentId
Version
DisplayName
BusinessModule
HumanDoc
PositiveExamples
NegativeExamples
RequiredParameters
OptionalParameters
FollowUpRules
ToolBinding
Targeting
```

`AgentPlan` must include:

```text
AgentId
SkillId
IntentId
ToolName
Summary
RiskLevel
RequiresApproval
Arguments
ConfirmationItems
FollowUpMessage
ResultMessage
```

Update `AgentRun.Plan` to `AgentPlan?` and add states:

```text
InputReceived
AgentRouted
SkillRouted
IntentRecognized
AwaitingUserInput
ParametersValidated
AwaitingUserConfirmation
DispatchingTool
Succeeded
Failed
Cancelled
```

- [ ] **Step 3: 运行测试**

```powershell
dotnet test F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\RoadProtoAgentBackend.Tests.csproj --filter SkillRuleModelTests
dotnet test F:\0_GPT_RoadProtoAgentBackend\RoadProtoAgentBackend.sln
```

Expected: `SkillRuleModelTests` PASS。全量测试若因旧 `SubgradeTemplatePlan` 失败，在 Task 5 迁移。

---

## Task 2: 后端新增 Skill / Intent YAML 与文件仓库

**Files:**

- Create: `F:\0_GPT_RoadProtoAgentBackend\rules\skills\subgrade_template.skill.yaml`
- Create: `F:\0_GPT_RoadProtoAgentBackend\rules\intents\subgrade_template.create.yaml`
- Create: `F:\0_GPT_RoadProtoAgentBackend\rules\intents\subgrade_template.modify.yaml`
- Create: `F:\0_GPT_RoadProtoAgentBackend\rules\intents\subgrade_template.delete.yaml`
- Create: `F:\0_GPT_RoadProtoAgentBackend\rules\intents\subgrade_template.query.yaml`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Skills\ISkillRepository.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\IntentRules\IIntentRuleRepository.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Infrastructure\Skills\FileSkillRepository.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Infrastructure\IntentRules\FileIntentRuleRepository.cs`
- Modify: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Infrastructure\RoadProtoAgentBackend.Infrastructure.csproj`
- Test: `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\SkillRuleRepositoryTests.cs`

- [ ] **Step 1: 写失败测试**

Test cases:

- loads `subgrade_template.skill.yaml`;
- loads four `subgrade_template.*.yaml` intent rules;
- every intent has `agentId=roadproto_engineering_agent`;
- every intent has `skillId=subgrade_template`;
- every intent is listed in the Skill `intents`;
- every intent Tool exists in Skill `toolWhitelist`;
- delete intent is high risk and requires approval.

Run:

```powershell
dotnet test F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\RoadProtoAgentBackend.Tests.csproj --filter SkillRuleRepositoryTests
```

Expected: FAIL because repositories and YAML files do not exist.

- [ ] **Step 2: 添加 YAML 依赖**

```powershell
dotnet add F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Infrastructure\RoadProtoAgentBackend.Infrastructure.csproj package YamlDotNet
```

- [ ] **Step 3: 创建 YAML**

`subgrade_template.skill.yaml` must match `docs/agent/skills/subgrade_template_skill.md`:

```yaml
id: subgrade_template
version: 0.1.0
displayName: 路基模板
agentId: roadproto_engineering_agent
businessModule: CROSS_SECTION
humanDoc: docs/agent/skills/subgrade_template_skill.md
intents:
  - subgrade_template.create
  - subgrade_template.modify
  - subgrade_template.delete
  - subgrade_template.query
toolWhitelist:
  - SubgradeTemplate.Create
  - SubgradeTemplate.Modify
  - SubgradeTemplate.Delete
  - SubgradeTemplate.Query
traceStages:
  - InputReceived
  - AgentRouted
  - SkillRouted
  - IntentRecognized
  - SchemaValidated
  - RulesApplied
  - ToolPlanned
  - Executed
```

Each intent YAML must include `agentId` and `skillId`. Tool bindings:

```text
create -> SubgradeTemplate.Create, medium, requiresApproval=true, requiresDryRun=true
modify -> SubgradeTemplate.Modify, medium, requiresApproval=true, requiresDryRun=true
delete -> SubgradeTemplate.Delete, high, requiresApproval=true, requiresDryRun=true
query  -> SubgradeTemplate.Query, low, requiresApproval=false, requiresDryRun=false
```

- [ ] **Step 4: 实现仓库**

`FileSkillRepository` and `FileIntentRuleRepository` load YAML from configured directories and return immutable domain records. Missing directory returns empty list; malformed YAML throws an invalid operation exception with filename in message.

- [ ] **Step 5: 注册 DI 并测试**

Register repositories in `Program.cs` using:

```text
F:\0_GPT_RoadProtoAgentBackend\rules\skills
F:\0_GPT_RoadProtoAgentBackend\rules\intents
```

Run:

```powershell
dotnet test F:\0_GPT_RoadProtoAgentBackend\RoadProtoAgentBackend.sln
```

Expected: PASS, unless old hardcoded run tests are intentionally migrated in Task 5.

---

## Task 3: 模型网关、Prompt Builder 和 Schema 解析

**Files:**

- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Models\IModelGateway.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Models\ModelGatewayRequest.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Models\ModelGatewayResponse.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Models\ModelPromptBuilder.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Models\ModelExtractionParser.cs`
- Modify: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Infrastructure\Models\OpenAiCompatibleModelGateway.cs`
- Test: `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\ModelPromptBuilderTests.cs`
- Test: `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\ModelExtractionParserTests.cs`

- [ ] **Step 1: 写 Prompt 测试**

Prompt must contain:

- twelve-layer control reminder;
- candidate `AgentId=roadproto_engineering_agent`;
- candidate `SkillId=subgrade_template`;
- four allowed Intent IDs;
- allowed parameter names;
- JSON-only output requirement;
- prohibition on default-value guessing and Tool execution.

- [ ] **Step 2: 写 Parser 测试**

Parser must accept JSON with:

```json
{
  "agentId": "roadproto_engineering_agent",
  "skillId": "subgrade_template",
  "intentId": "subgrade_template.modify",
  "confidence": 0.86,
  "matchedExpression": "把当前路基模板行车道加宽 1 米",
  "target": { "mode": "currentSelection", "handle": null, "name": null, "confidence": 0.75 },
  "parameters": {
    "laneWidthDelta": { "value": 1.0, "unit": "m", "sourceText": "加宽 1 米", "confidence": 0.9 }
  },
  "followUp": null,
  "explanation": "用户希望修改当前路基模板。"
}
```

Parser must reject:

- non-JSON;
- missing `agentId`;
- unknown `skillId`;
- `intentId` not under Skill;
- parameter not declared by Skill / Intent rules;
- Tool name in model output.

- [ ] **Step 3: 实现 Gateway**

`OpenAiCompatibleModelGateway` sends OpenAI-compatible `chat/completions` HTTP request using configured provider settings. It must:

- use decrypted API key from settings store;
- include model id and messages;
- return text content, provider, model, elapsed milliseconds;
- never log API key.

Unit tests use fake `HttpMessageHandler`; no real network in tests.

- [ ] **Step 4: 运行测试**

```powershell
dotnet test F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\RoadProtoAgentBackend.Tests.csproj --filter "ModelPromptBuilderTests|ModelExtractionParserTests|OpenAiCompatibleModelGatewayTests"
```

Expected: PASS.

---

## Task 4: Skill Registry、Intent Rule Service 和路基模板 Agent

**Files:**

- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Skills\SkillRegistryService.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\IntentRules\IntentRuleService.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\IntentRules\IntentResolutionResult.cs`
- Create or Replace: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Agents\SubgradeTemplateAgent.cs`
- Modify: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Agents\SubgradeTemplateCreateAgent.cs`
- Test: `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\IntentRuleServiceTests.cs`
- Test: `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\SubgradeTemplateAgentTests.cs`

- [ ] **Step 1: 写服务测试**

Cover:

- create missing `roadGrade` returns follow-up;
- create with `roadGrade=Expressway` returns `SubgradeTemplate.Create` plan;
- modify missing target returns follow-up;
- delete without unique target returns follow-up;
- delete with reference flag returns blocked result;
- query with no target returns `SubgradeTemplate.Query` plan and no approval;
- any plan Tool not in Skill whitelist is blocked.

- [ ] **Step 2: 实现服务**

`SkillRegistryService`:

- loads enabled skills;
- validates intent membership;
- validates Tool whitelist.

`IntentRuleService`:

- validates model extraction;
- resolves missing parameters;
- applies MVP default values;
- builds `AgentPlan`;
- returns follow-up or blocked result before Tool planning when needed.

`SubgradeTemplateAgent`:

- accepts user message and optional continuation context;
- builds prompt;
- calls model gateway;
- parses extraction;
- calls rule service;
- returns plan or follow-up.

Keep `SubgradeTemplateCreateAgent` as a compatibility wrapper only if existing DI/tests still reference it; otherwise replace references with `SubgradeTemplateAgent`.

- [ ] **Step 3: 运行测试**

```powershell
dotnet test F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\RoadProtoAgentBackend.Tests.csproj --filter "IntentRuleServiceTests|SubgradeTemplateAgentTests"
```

Expected: PASS.

---

## Task 5: 后端 Run 状态机接入十二层 Trace 和用户补充输入

**Files:**

- Modify: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Runs\AgentRunService.cs`
- Modify: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Api\Endpoints\AgentRunEndpoints.cs`
- Modify: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Api\Program.cs`
- Test: `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\AgentRunServiceTests.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\FakeModelGateway.cs`

- [ ] **Step 1: 写状态机测试**

Cover:

- `StartRunAsync("帮我创建高速公路路基模板")` records stages `InputReceived`、`AgentRouted`、`SkillRouted`、`IntentRecognized`、`SchemaValidated`、`RulesApplied`。
- missing `roadGrade` returns `AwaitingUserInput` and no Tool call.
- follow-up input on same `taskId` can complete missing parameter.
- write intent enters `AwaitingUserConfirmation`.
- query intent can return succeeded result or dispatch read-only Tool without approval.
- confirm write intent dispatches Tool only after approval.

- [ ] **Step 2: 实现 API**

Add endpoint:

```text
POST /v1/runs/{task_id}/user-input
```

Request body:

```json
{ "message": "高速公路" }
```

It appends user input to the existing run and re-enters Skill / Intent rule flow.

- [ ] **Step 3: Trace 要求**

Every `AgentFlowEvent` properties must include at least:

```text
agentId
skillId
intentId
state
toolName
```

When value is unknown, use empty string; do not omit keys.

- [ ] **Step 4: 运行测试**

```powershell
dotnet test F:\0_GPT_RoadProtoAgentBackend\RoadProtoAgentBackend.sln
```

Expected: PASS.

---

## Task 6: RoadProto WPF DTO 和 Agent Console 支持 Skill / Intent / 追问 / 查询

**Files:**

- Modify: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-mvp\src\ui\wpf\RoadProto.Terrain.UI\Agent\Models\AgentDtos.cs`
- Modify: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-mvp\src\ui\wpf\RoadProto.Terrain.UI\Agent\Backend\AgentBackendClient.cs`
- Modify: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-mvp\src\ui\wpf\RoadProto.Terrain.UI\Agent\AgentConsoleViewModel.cs`
- Test: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-mvp\tests\RoadProtoManagedBridgeTests\Program.cs`

- [ ] **Step 1: 写托管源码契约测试**

Test must check:

- `AgentPlanDto` contains `AgentId`、`SkillId`、`IntentId`、`RiskLevel`、`FollowUpMessage`、`ResultMessage`。
- `AgentBackendClient` contains `PostUserInputAsync`。
- `AgentConsoleViewModel` handles `AwaitingUserInput`。
- UI strings include Skill / Intent display in trace or messages.

- [ ] **Step 2: 实现 DTO 和 Client**

Add DTO fields to mirror backend `AgentPlan`.

Add:

```csharp
Task<AgentRunDto> PostUserInputAsync(string taskId, string message, CancellationToken cancellationToken)
```

calling:

```text
POST /v1/runs/{taskId}/user-input
```

- [ ] **Step 3: 实现 ViewModel**

When state is:

- `AwaitingUserInput`: show follow-up message, keep input enabled, do not enable confirm.
- `AwaitingUserConfirmation`: show confirmation items and enable confirm.
- `Succeeded` with query result: show result message, do not call local Tool.
- `DispatchingTool`: call local Tool bridge.

- [ ] **Step 4: 运行测试**

```powershell
dotnet run --project F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-mvp\tests\RoadProtoManagedBridgeTests\RoadProtoManagedBridgeTests.csproj -c Debug
```

Expected: PASS.

---

## Task 7: RoadProto 本地 Tool Bridge 和 ObjectARX 支持 CRUD

**Files:**

- Modify: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-mvp\src\ui\wpf\RoadProto.Terrain.UI\Agent\Bridge\AgentLocalToolBridge.cs`
- Create: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-mvp\src\cad_adapter\objectarx\agent\ObjectArxAgentSubgradeTemplateToolCommand.h`
- Create: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-mvp\src\cad_adapter\objectarx\agent\ObjectArxAgentSubgradeTemplateToolCommand.cpp`
- Modify: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-mvp\src\modules\agent\AgentModule.cpp`
- Modify: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-mvp\src\app\RoadProtoArx.vcxproj`
- Test: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-mvp\tests\core_tests.cpp`
- Test: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-mvp\tests\RoadProtoManagedBridgeTests\Program.cs`

- [ ] **Step 1: 写失败测试**

Core test checks command registry includes:

```text
RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE
moduleCode: AGENT
businessDocPath: docs/business/agent/路基模板Skill_增删改查_MVP.md
```

Managed test checks bridge supports:

```text
SubgradeTemplate.Create
SubgradeTemplate.Modify
SubgradeTemplate.Delete
SubgradeTemplate.Query
RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE
```

- [ ] **Step 2: WPF Bridge**

Bridge routing:

- `SubgradeTemplate.Create`: keep existing `RD_SECTION_SUBGRADE_TEMPLATE_APPLY_DIALOG_FILE` path.
- `SubgradeTemplate.Modify/Delete/Query`: write native tool request file and call `RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE`.

Request file fields:

```text
operation=create|modify|delete|query
traceId=
taskId=
agentId=
skillId=
intentId=
targetMode=
targetHandle=
targetName=
templateName=
roadGrade=
laneWidth=
laneWidthDelta=
hardShoulderWidth=
earthShoulderWidth=
slopeRatio=
unit=
resultPath=
```

- [ ] **Step 3: Native command**

`ObjectArxAgentSubgradeTemplateToolCommand.cpp` must:

- read request file;
- validate `skillId=subgrade_template`;
- reject unknown operation;
- query model space `DnSubgradeTemplateEntity`;
- modify by handle for MVP width/name fields;
- delete by handle only after explicit request;
- write result file with `succeeded`、`entityId`、`templateName`、`message`。

MVP deletion must not batch delete. If target is missing or ambiguous, return failure.

- [ ] **Step 4: 运行测试**

```powershell
& "D:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe" F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-mvp\tests\RoadProtoCoreTests.vcxproj /p:Configuration=Debug /p:Platform=x64
F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-mvp\artifacts\x64\Debug\RoadProtoCoreTests.exe
dotnet run --project F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-mvp\tests\RoadProtoManagedBridgeTests\RoadProtoManagedBridgeTests.csproj -c Debug
```

Expected: PASS.

---

## Task 8: 文档、业务文档和索引收口

**Files:**

- Create: `F:\0_GPT_道路设计原型功能项目\docs\business\agent\路基模板Skill_增删改查_MVP.md`
- Modify: `F:\0_GPT_道路设计原型功能项目\docs\agent\README.md`
- Modify: `F:\0_GPT_道路设计原型功能项目\docs\modules\agent.md`
- Modify: `F:\0_GPT_道路设计原型功能项目\docs\reuse\engineering_agent_mvp.md`
- Modify as needed: `F:\0_GPT_道路设计原型功能项目\README.md`

- [ ] **Step 1: 新增业务文档**

Business doc must state:

- this is `AGENT` module controlled Skill verification;
- Skill is `subgrade_template`;
- intents are create / modify / delete / query;
- backend does not write DWG;
- WPF does not operate ObjectARX;
- Tool whitelist and approval table.

- [ ] **Step 2: 更新索引**

Agent module docs must link:

```text
docs/agent/skill_system.md
docs/agent/skills/subgrade_template_skill.md
docs/agent/intents/subgrade_template_create.md
docs/agent/intents/subgrade_template_modify.md
docs/agent/intents/subgrade_template_delete.md
docs/agent/intents/subgrade_template_query.md
docs/business/agent/路基模板Skill_增删改查_MVP.md
```

- [ ] **Step 3: 文档自查**

```powershell
rg -n "subgrade_template_create_agent|路基模板创建 Agent" F:\0_GPT_道路设计原型功能项目\docs\agent F:\0_GPT_道路设计原型功能项目\docs\modules\agent.md
```

Expected: only historical note lines remain, and those lines explicitly say the old name has been收敛为 Skill / Intent。

---

## Task 9: 全量验证、发布和 worktree 收口确认

- [ ] **Step 1: 后端全量测试**

```powershell
dotnet test F:\0_GPT_RoadProtoAgentBackend\RoadProtoAgentBackend.sln
```

Expected: PASS.

- [ ] **Step 2: RoadProto 测试**

```powershell
& "D:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe" F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-mvp\tests\RoadProtoCoreTests.vcxproj /p:Configuration=Debug /p:Platform=x64
F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-mvp\artifacts\x64\Debug\RoadProtoCoreTests.exe
dotnet run --project F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-mvp\tests\RoadProtoManagedBridgeTests\RoadProtoManagedBridgeTests.csproj -c Debug
```

Expected: PASS.

- [ ] **Step 3: RoadProto Debug 构建**

```powershell
& "D:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe" F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-mvp\RoadProto.sln /p:Configuration=Debug /p:Platform=x64
```

Expected: 0 errors. If ObjectARX path is missing, record exact missing path.

- [ ] **Step 4: 发布后端**

```powershell
dotnet publish F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Api\RoadProtoAgentBackend.Api.csproj -c Release -r win-x64 --self-contained false -o F:\0_GPT_RoadProtoAgentBackend\artifacts\publish
```

Expected: `F:\0_GPT_RoadProtoAgentBackend\artifacts\publish\RoadProtoAgentBackend.exe` exists.

- [ ] **Step 5: 健康检查**

```powershell
$process = Start-Process -FilePath 'F:\0_GPT_RoadProtoAgentBackend\artifacts\publish\RoadProtoAgentBackend.exe' -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 2
Invoke-RestMethod -Uri 'http://127.0.0.1:17861/health'
Stop-Process -Id $process.Id
```

Expected: `status` is `ok`.

- [ ] **Step 6: worktree 收口确认**

如果本计划在 worktree 中执行，默认保持 worktree 目录和对应分支隔离，不自动同步回主项目目录。先确认当前状态：

```powershell
git -C F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-mvp status --short
git -C F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-mvp branch --show-current
git -C F:\0_GPT_道路设计原型功能项目 status --short
```

只有用户明确确认合入或需要主项目目录可见副本时，才按 `AGENTS.md` 执行 Git 合入、快进、挑拣提交或指定范围同步。若构建了 artifacts，也只有用户明确需要主项目目录直接加载、调试或分发时，才按 `AGENTS.md` 复制 ARX / DLL / PDB。

---

## Self-Review

**Spec coverage:**

- 十二层流程：Task 1、Task 3、Task 5、Task 9。
- Skill 规则和 Tool 白名单：Task 1、Task 2、Task 4。
- 四个 Intent：Task 2、Task 4、Task 7。
- 模型调用和 Schema：Task 3。
- 追问和用户补充输入：Task 4、Task 5、Task 6。
- WPF 完整交互：Task 6。
- RoadProto 本地执行：Task 7。
- 文档和索引：Task 8。
- 验证、发布和同步：Task 9。

**Placeholder scan:** 本计划不含未决占位标记。

**Type consistency:**

- `AgentId=roadproto_engineering_agent`
- `SkillId=subgrade_template`
- `IntentId=subgrade_template.create|modify|delete|query`
- `ToolName=SubgradeTemplate.Create|Modify|Delete|Query`
- RoadProto native command: `RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE`
