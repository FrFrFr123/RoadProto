# 路基模板增删改查意图 Implementation Plan

> 本计划已被 `docs/superpowers/plans/2026-06-24-subgrade-template-skill-crud.md` 替代，仅保留为历史参考。后续实现以新的 Skill 优先计划为准。

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让路基模板 Skill 及其创建、修改、删除、查询四个 Intent 从规则文件和大模型抽参开始，经过后端规则校验、WPF 确认或展示，再由 RoadProto 本地受控工具执行。

**Architecture:** 后端仓库 `F:\0_GPT_RoadProtoAgentBackend` 负责 Agent / Skill / Intent 规则、模型网关、参数抽取、规则校验、状态机和 Tool 计划。RoadProto 主仓库 `F:\0_GPT_道路设计原型功能项目` 只负责 WPF 交互、HTTP 通信、本地 Tool Adapter、ObjectARX 写入和只读查询。模型只在候选 Agent / Skill 范围内做意图识别、参数抽取、追问建议和解释，不补默认值、不直接调用工具。

**Tech Stack:** .NET 8 / ASP.NET Core, xUnit, System.Text.Json, YamlDotNet, WPF .NET Framework 4.8, C++17, AutoCAD 2021 / ObjectARX 2021.

---

## 当前状态说明

本计划在 2026-06-24 讨论后暂停执行。暂停原因：原计划从 Intent 规则直接进入 Tool 计划，缺少明确的 Skill 层。

新的执行计划已经完成重排，使流程符合以下顺序：

```text
Agent -> Skill -> Intent -> Schema -> Rule Engine -> Tool Registry -> EICAD Adapter -> Execution Control -> Trace
```

其中路基模板本轮的能力边界为：

```text
Agent: roadproto_engineering_agent
Skill: subgrade_template
Intent:
  subgrade_template.create
  subgrade_template.modify
  subgrade_template.delete
  subgrade_template.query
```

在计划重排前，不应继续执行后续代码任务。

## 0. 执行前准备

**参考文档：**

- `F:\0_GPT_道路设计原型功能项目\AGENTS.md`
- `F:\0_GPT_道路设计原型功能项目\README.md`
- `F:\0_GPT_道路设计原型功能项目\docs\dev\ai_development_rules.md`
- `F:\0_GPT_道路设计原型功能项目\docs\coding_rules.md`
- `F:\0_GPT_道路设计原型功能项目\docs\superpowers\specs\2026-06-24-agent-intent-rules-and-model-design.md`
- `F:\0_GPT_道路设计原型功能项目\docs\agent\skill_system.md`
- `F:\0_GPT_道路设计原型功能项目\docs\agent\skills\subgrade_template_skill.md`
- `F:\0_GPT_道路设计原型功能项目\docs\agent\intents\subgrade_template_create.md`
- `F:\0_GPT_道路设计原型功能项目\docs\agent\intent_rule_template.md`
- `F:\0_GPT_RoadProtoAgentBackend\README.md`

**执行约定：**

- 执行代码任务前，先用 `superpowers:using-git-worktrees` 创建隔离工作区。
- PowerShell 每次读取中文前先执行 UTF-8 初始化：

```powershell
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
```

- 后端代码修改发生在 `F:\0_GPT_RoadProtoAgentBackend`。
- RoadProto 代码和文档修改发生在 `F:\0_GPT_道路设计原型功能项目` 或其 worktree；如果在 worktree 执行，默认保持 worktree 目录和对应分支隔离，只有用户明确确认合入或需要主项目目录可见副本时，才按 `AGENTS.md` 执行合入或同步。
- 每个任务完成后运行该任务列出的测试命令。只有用户明确要求提交时，才执行计划中的提交检查点命令。

---

## 1. 文件结构

### 后端仓库新增和修改

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
        IntentRuleValidation.cs
      Models\
        ModelExtractionResult.cs
      Tools\
        AgentPlan.cs
        SubgradeTemplateToolArguments.cs
    RoadProtoAgentBackend.Application\
      Skills\
        ISkillRepository.cs
        SkillRegistryService.cs
      Agents\
        SubgradeTemplateAgent.cs
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
      FileIntentRuleRepositoryTests.cs
      ModelPromptBuilderTests.cs
      ModelExtractionParserTests.cs
      IntentRuleServiceTests.cs
      SubgradeTemplateAgentTests.cs
      AgentRunServiceTests.cs
      FakeModelGateway.cs
```

### RoadProto 主仓库新增和修改

```text
F:\0_GPT_道路设计原型功能项目\
  docs\
    agent\
      README.md
      skill_system.md
      skills\
        subgrade_template_skill.md
      intents\
        subgrade_template_modify.md
        subgrade_template_delete.md
        subgrade_template_query.md
    modules\
      agent.md
    business\
      agent\
        路基模板Agent_增删改查_MVP.md
  src\
    modules\
      agent\
        AgentModule.cpp
    cad_adapter\
      objectarx\
        agent\
          ObjectArxAgentConsoleCommand.h
          ObjectArxAgentConsoleCommand.cpp
          ObjectArxAgentSubgradeTemplateToolCommand.h
          ObjectArxAgentSubgradeTemplateToolCommand.cpp
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
          RoadProto.Terrain.UI.csproj
  tests\
    core_tests.cpp
    RoadProtoManagedBridgeTests\
      Program.cs
```

---

### Task 1: 后端领域模型改为通用意图和通用计划

**Files:**

- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Domain\IntentRules\IntentRule.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Domain\IntentRules\IntentRuleParameter.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Domain\IntentRules\IntentRuleTarget.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Domain\IntentRules\IntentRuleToolBinding.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Domain\IntentRules\IntentRuleValidation.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Domain\Models\ModelExtractionResult.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Domain\Tools\AgentPlan.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Domain\Tools\SubgradeTemplateToolArguments.cs`
- Modify: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Domain\Runs\AgentRun.cs`
- Modify: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Domain\Runs\AgentRunState.cs`
- Test: `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\IntentRuleModelTests.cs`

- [ ] **Step 1: 写领域模型失败测试**

Create `tests\RoadProtoAgentBackend.Tests\IntentRuleModelTests.cs`:

```csharp
using RoadProtoAgentBackend.Domain.IntentRules;
using RoadProtoAgentBackend.Domain.Models;
using RoadProtoAgentBackend.Domain.Runs;
using RoadProtoAgentBackend.Domain.Tools;

namespace RoadProtoAgentBackend.Tests;

public sealed class IntentRuleModelTests
{
    [Fact]
    public void AgentPlan_supports_all_subgrade_template_tool_names()
    {
        var plan = new AgentPlan(
            "subgrade_template.modify",
            "SubgradeTemplate.Modify",
            "修改当前路基模板。",
            "medium",
            true,
            new Dictionary<string, object?> { ["targetHandle"] = "ABCD" },
            new[] { "目标模板: ABCD" },
            null);

        Assert.Equal("subgrade_template.modify", plan.IntentId);
        Assert.Equal("SubgradeTemplate.Modify", plan.ToolName);
        Assert.True(plan.RequiresApproval);
        Assert.Equal("ABCD", plan.Arguments["targetHandle"]);
    }

    [Fact]
    public void Extraction_result_carries_target_and_parameter_sources()
    {
        var extraction = new ModelExtractionResult(
            "subgrade_template.delete",
            0.91,
            "删除这个路基模板",
            new IntentTarget("currentSelection", null, null, 0.82),
            new Dictionary<string, ModelParameterValue>(),
            null,
            Array.Empty<string>());

        Assert.Equal("currentSelection", extraction.Target?.Mode);
        Assert.True(extraction.Confidence > 0.9);
    }

    [Fact]
    public void AgentRun_can_wait_for_missing_user_input()
    {
        Assert.Contains(AgentRunState.AwaitingUserInput, Enum.GetValues<AgentRunState>());
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run:

```powershell
dotnet test F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\RoadProtoAgentBackend.Tests.csproj --filter IntentRuleModelTests
```

Expected: FAIL，提示 `AgentPlan`、`ModelExtractionResult` 或 `AgentRunState.AwaitingUserInput` 不存在。

- [ ] **Step 3: 新增领域模型**

Create `src\RoadProtoAgentBackend.Domain\IntentRules\IntentRule.cs`:

```csharp
namespace RoadProtoAgentBackend.Domain.IntentRules;

public sealed record IntentRule(
    string Id,
    string Version,
    string DisplayName,
    string AgentId,
    string BusinessModule,
    string HumanDoc,
    IReadOnlyList<string> PositiveExamples,
    IReadOnlyList<string> NegativeExamples,
    IReadOnlyDictionary<string, IntentRuleParameter> RequiredParameters,
    IReadOnlyDictionary<string, IntentRuleParameter> OptionalParameters,
    IReadOnlyList<IntentFollowUpRule> FollowUpRules,
    IntentRuleToolBinding ToolBinding,
    IntentRuleTargeting Targeting);
```

Create `src\RoadProtoAgentBackend.Domain\IntentRules\IntentRuleParameter.cs`:

```csharp
namespace RoadProtoAgentBackend.Domain.IntentRules;

public sealed record IntentRuleParameter(
    string Type,
    IReadOnlyList<string> Values,
    string? Unit,
    double? MinExclusive,
    IReadOnlyList<double> Allowed,
    string? Default,
    string? Missing);

public sealed record IntentFollowUpRule(string When, string Message);
```

Create `src\RoadProtoAgentBackend.Domain\IntentRules\IntentRuleTarget.cs`:

```csharp
namespace RoadProtoAgentBackend.Domain.IntentRules;

public sealed record IntentRuleTargeting(
    bool Required,
    IReadOnlyList<string> Modes,
    bool AllowEmptyForQuery);
```

Create `src\RoadProtoAgentBackend.Domain\IntentRules\IntentRuleToolBinding.cs`:

```csharp
namespace RoadProtoAgentBackend.Domain.IntentRules;

public sealed record IntentRuleToolBinding(
    string ToolName,
    string RiskLevel,
    bool RequiresDryRun,
    bool RequiresApproval,
    string RoadProtoAdapter);
```

Create `src\RoadProtoAgentBackend.Domain\IntentRules\IntentRuleValidation.cs`:

```csharp
namespace RoadProtoAgentBackend.Domain.IntentRules;

public sealed record IntentRuleValidation(bool Succeeded, string Code, string Message)
{
    public static IntentRuleValidation Ok() => new(true, "OK", "校验通过。");

    public static IntentRuleValidation Fail(string code, string message) => new(false, code, message);
}
```

Create `src\RoadProtoAgentBackend.Domain\Models\ModelExtractionResult.cs`:

```csharp
namespace RoadProtoAgentBackend.Domain.Models;

public sealed record ModelExtractionResult(
    string IntentId,
    double Confidence,
    string MatchedExpression,
    IntentTarget? Target,
    IReadOnlyDictionary<string, ModelParameterValue> Parameters,
    string? RejectedIntent,
    IReadOnlyList<string> Notes);

public sealed record IntentTarget(string Mode, string? Handle, string? Name, double Confidence);

public sealed record ModelParameterValue(object? Value, string? Unit, string SourceText, double Confidence);
```

Create `src\RoadProtoAgentBackend.Domain\Tools\AgentPlan.cs`:

```csharp
namespace RoadProtoAgentBackend.Domain.Tools;

public sealed record AgentPlan(
    string IntentId,
    string ToolName,
    string Summary,
    string RiskLevel,
    bool RequiresApproval,
    IReadOnlyDictionary<string, object?> Arguments,
    IReadOnlyList<string> ConfirmationItems,
    string? FollowUpMessage);
```

Create `src\RoadProtoAgentBackend.Domain\Tools\SubgradeTemplateToolArguments.cs`:

```csharp
namespace RoadProtoAgentBackend.Domain.Tools;

public sealed record SubgradeTemplateToolArguments(
    string? TargetHandle,
    string? TargetName,
    string TemplateName,
    string? RoadGrade,
    double LaneWidth,
    double HardShoulderWidth,
    double EarthShoulderWidth,
    double SlopeRatio,
    string Unit);
```

Modify `src\RoadProtoAgentBackend.Domain\Runs\AgentRun.cs` so `Plan` uses the generic type and can be missing during early failures:

```csharp
using RoadProtoAgentBackend.Domain.Tools;

namespace RoadProtoAgentBackend.Domain.Runs;

public sealed record AgentRun(
    string TraceId,
    string SessionId,
    string TaskId,
    string UserMessage,
    AgentRunState State,
    AgentPlan? Plan,
    AgentToolCall? DispatchedToolCall,
    AgentToolResult? ToolResult,
    IReadOnlyList<AgentRunEvent> Events);
```

Modify `src\RoadProtoAgentBackend.Domain\Runs\AgentRunState.cs`:

```csharp
namespace RoadProtoAgentBackend.Domain.Runs;

public enum AgentRunState
{
    Created,
    InputReceived,
    Planning,
    IntentRecognized,
    AwaitingUserInput,
    ParametersValidated,
    AwaitingUserConfirmation,
    DispatchingTool,
    Succeeded,
    Failed,
    Cancelled
}
```

- [ ] **Step 4: 运行测试确认通过**

Run:

```powershell
dotnet test F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\RoadProtoAgentBackend.Tests.csproj --filter IntentRuleModelTests
```

Expected: PASS.

- [ ] **Step 5: 检查点**

Run:

```powershell
dotnet test F:\0_GPT_RoadProtoAgentBackend\RoadProtoAgentBackend.sln
```

Expected: 现有测试如果因 `AgentRun.Plan` 类型变化失败，记录失败测试名称，Task 6 会统一迁移状态机测试。

提交检查点命令：

```powershell
git -C F:\0_GPT_RoadProtoAgentBackend add src tests
git -C F:\0_GPT_RoadProtoAgentBackend commit -m "feat: add generic intent and plan domain models"
```

---

### Task 2: 后端新增四个 YAML 规则文件和文件规则仓库

**Files:**

- Create: `F:\0_GPT_RoadProtoAgentBackend\rules\intents\subgrade_template.create.yaml`
- Create: `F:\0_GPT_RoadProtoAgentBackend\rules\intents\subgrade_template.modify.yaml`
- Create: `F:\0_GPT_RoadProtoAgentBackend\rules\intents\subgrade_template.delete.yaml`
- Create: `F:\0_GPT_RoadProtoAgentBackend\rules\intents\subgrade_template.query.yaml`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\IntentRules\IIntentRuleRepository.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Infrastructure\IntentRules\FileIntentRuleRepository.cs`
- Modify: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Infrastructure\RoadProtoAgentBackend.Infrastructure.csproj`
- Modify: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Api\Program.cs`
- Test: `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\FileIntentRuleRepositoryTests.cs`

- [ ] **Step 1: 写规则仓库失败测试**

Create `tests\RoadProtoAgentBackend.Tests\FileIntentRuleRepositoryTests.cs`:

```csharp
using RoadProtoAgentBackend.Infrastructure.IntentRules;

namespace RoadProtoAgentBackend.Tests;

public sealed class FileIntentRuleRepositoryTests
{
    [Fact]
    public async Task LoadAllAsync_loads_all_subgrade_template_rules()
    {
        var repository = new FileIntentRuleRepository(
            @"F:\0_GPT_RoadProtoAgentBackend\rules\intents");

        var rules = await repository.LoadAllAsync();

        Assert.Contains(rules, rule => rule.Id == "subgrade_template.create");
        Assert.Contains(rules, rule => rule.Id == "subgrade_template.modify");
        Assert.Contains(rules, rule => rule.Id == "subgrade_template.delete");
        Assert.Contains(rules, rule => rule.Id == "subgrade_template.query");
        Assert.All(rules, rule => Assert.StartsWith("SubgradeTemplate.", rule.ToolBinding.ToolName, StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetByIdAsync_returns_delete_rule_with_high_risk()
    {
        var repository = new FileIntentRuleRepository(
            @"F:\0_GPT_RoadProtoAgentBackend\rules\intents");

        var rule = await repository.GetByIdAsync("subgrade_template.delete");

        Assert.NotNull(rule);
        Assert.Equal("high", rule!.ToolBinding.RiskLevel);
        Assert.True(rule.ToolBinding.RequiresApproval);
        Assert.True(rule.Targeting.Required);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run:

```powershell
dotnet test F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\RoadProtoAgentBackend.Tests.csproj --filter FileIntentRuleRepositoryTests
```

Expected: FAIL，提示 `FileIntentRuleRepository` 不存在。

- [ ] **Step 3: 添加 YAML 解析依赖**

Run:

```powershell
dotnet add F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Infrastructure\RoadProtoAgentBackend.Infrastructure.csproj package YamlDotNet
```

Expected: `RoadProtoAgentBackend.Infrastructure.csproj` 出现 `PackageReference Include="YamlDotNet"`。

- [ ] **Step 4: 创建四个规则文件**

Create `rules\intents\subgrade_template.create.yaml`:

```yaml
id: subgrade_template.create
version: 0.1.0
displayName: 创建路基模板
agentId: subgrade_template_agent
businessModule: CROSS_SECTION
humanDoc: docs/agent/intents/subgrade_template_create.md
positiveExamples:
  - 帮我创建高速公路路基模板。
  - 新建一个城市快速路路基模板，行车道宽 3.75 米。
negativeExamples:
  - 修改当前模板的行车道宽度。
  - 删除路基模板。
  - 路基模板怎么设置？
requiredParameters:
  roadGrade:
    type: enum
    values: [Expressway, FirstClass, SecondClass, ThirdClass, FourthClass, UrbanExpressway, UrbanArterial, UrbanSubArterial, UrbanBranch]
    missing: ask
optionalParameters:
  templateName:
    type: string
    default: 默认路基模板
  displayScale:
    type: number
    allowed: [1, 10, 20, 50, 100]
    default: 10
  laneWidth:
    type: number
    unit: m
    minExclusive: 0
  laneWidthDelta:
    type: number
    unit: m
  hardShoulderWidth:
    type: number
    unit: m
    minExclusive: 0
  earthShoulderWidth:
    type: number
    unit: m
    minExclusive: 0
followUpRules:
  - when: missing_required.roadGrade
    message: 请问道路等级是什么？
  - when: ambiguous.totalWidth
    message: 你希望把总宽变化分配到哪些部件？
toolBinding:
  toolName: SubgradeTemplate.Create
  riskLevel: medium
  requiresDryRun: true
  requiresApproval: true
  roadProtoAdapter: RD_SECTION_SUBGRADE_TEMPLATE_APPLY_DIALOG_FILE
targeting:
  required: false
  modes: []
  allowEmptyForQuery: false
```

Create `rules\intents\subgrade_template.modify.yaml`:

```yaml
id: subgrade_template.modify
version: 0.1.0
displayName: 修改路基模板
agentId: subgrade_template_agent
businessModule: CROSS_SECTION
humanDoc: docs/agent/intents/subgrade_template_modify.md
positiveExamples:
  - 修改当前模板的行车道宽度。
  - 把这个路基模板的硬路肩改为 3 米。
negativeExamples:
  - 创建高速公路路基模板。
  - 删除这个路基模板。
  - 路基模板怎么设置？
requiredParameters: {}
optionalParameters:
  templateName:
    type: string
  roadGrade:
    type: enum
    values: [Expressway, FirstClass, SecondClass, ThirdClass, FourthClass, UrbanExpressway, UrbanArterial, UrbanSubArterial, UrbanBranch]
  displayScale:
    type: number
    allowed: [1, 10, 20, 50, 100]
  laneWidth:
    type: number
    unit: m
    minExclusive: 0
  laneWidthDelta:
    type: number
    unit: m
  hardShoulderWidth:
    type: number
    unit: m
    minExclusive: 0
  earthShoulderWidth:
    type: number
    unit: m
    minExclusive: 0
followUpRules:
  - when: missing_target
    message: 请在 CAD 中选择要修改的路基模板，或告诉我模板 handle / 名称。
toolBinding:
  toolName: SubgradeTemplate.Modify
  riskLevel: medium
  requiresDryRun: true
  requiresApproval: true
  roadProtoAdapter: RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE
targeting:
  required: true
  modes: [currentSelection, handle, name]
  allowEmptyForQuery: false
```

Create `rules\intents\subgrade_template.delete.yaml`:

```yaml
id: subgrade_template.delete
version: 0.1.0
displayName: 删除路基模板
agentId: subgrade_template_agent
businessModule: CROSS_SECTION
humanDoc: docs/agent/intents/subgrade_template_delete.md
positiveExamples:
  - 删除这个路基模板。
  - 删除 handle 为 ABCD 的路基模板。
negativeExamples:
  - 创建路基模板。
  - 修改当前模板的行车道宽度。
  - 路基模板怎么设置？
requiredParameters: {}
optionalParameters: {}
followUpRules:
  - when: missing_target
    message: 请在 CAD 中选择要删除的路基模板，或告诉我模板 handle / 名称。
  - when: ambiguous_target
    message: 找到多个匹配的路基模板，请指定要删除哪一个。
toolBinding:
  toolName: SubgradeTemplate.Delete
  riskLevel: high
  requiresDryRun: true
  requiresApproval: true
  roadProtoAdapter: RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE
targeting:
  required: true
  modes: [currentSelection, handle, name]
  allowEmptyForQuery: false
```

Create `rules\intents\subgrade_template.query.yaml`:

```yaml
id: subgrade_template.query
version: 0.1.0
displayName: 查询路基模板
agentId: subgrade_template_agent
businessModule: CROSS_SECTION
humanDoc: docs/agent/intents/subgrade_template_query.md
positiveExamples:
  - 当前图里有哪些路基模板？
  - 查询这个路基模板的参数。
negativeExamples:
  - 创建路基模板。
  - 删除这个路基模板。
requiredParameters: {}
optionalParameters: {}
followUpRules:
  - when: ambiguous_target
    message: 找到多个匹配的路基模板，请指定要查询哪一个。
toolBinding:
  toolName: SubgradeTemplate.Query
  riskLevel: low
  requiresDryRun: false
  requiresApproval: false
  roadProtoAdapter: RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE
targeting:
  required: false
  modes: [all, currentSelection, handle, name]
  allowEmptyForQuery: true
```

- [ ] **Step 5: 新增仓库接口和实现**

Create `src\RoadProtoAgentBackend.Application\IntentRules\IIntentRuleRepository.cs`:

```csharp
using RoadProtoAgentBackend.Domain.IntentRules;

namespace RoadProtoAgentBackend.Application.IntentRules;

public interface IIntentRuleRepository
{
    Task<IReadOnlyList<IntentRule>> LoadAllAsync(CancellationToken cancellationToken = default);

    Task<IntentRule?> GetByIdAsync(string intentId, CancellationToken cancellationToken = default);
}
```

Create `src\RoadProtoAgentBackend.Infrastructure\IntentRules\FileIntentRuleRepository.cs`:

```csharp
using RoadProtoAgentBackend.Application.IntentRules;
using RoadProtoAgentBackend.Domain.IntentRules;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace RoadProtoAgentBackend.Infrastructure.IntentRules;

public sealed class FileIntentRuleRepository : IIntentRuleRepository
{
    private readonly string _directory;
    private readonly IDeserializer _deserializer;

    public FileIntentRuleRepository(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.GetFullPath(directory);
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public async Task<IReadOnlyList<IntentRule>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_directory))
        {
            return Array.Empty<IntentRule>();
        }

        var rules = new List<IntentRule>();
        foreach (var path in Directory.GetFiles(_directory, "*.yaml").OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var yaml = await File.ReadAllTextAsync(path, cancellationToken);
            var dto = _deserializer.Deserialize<IntentRuleDto>(yaml);
            rules.Add(dto.ToRule());
        }

        return rules;
    }

    public async Task<IntentRule?> GetByIdAsync(string intentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        var rules = await LoadAllAsync(cancellationToken);
        return rules.FirstOrDefault(rule => rule.Id.Equals(intentId, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class IntentRuleDto
    {
        public string Id { get; set; } = "";
        public string Version { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string AgentId { get; set; } = "";
        public string BusinessModule { get; set; } = "";
        public string HumanDoc { get; set; } = "";
        public List<string> PositiveExamples { get; set; } = new();
        public List<string> NegativeExamples { get; set; } = new();
        public Dictionary<string, IntentRuleParameter> RequiredParameters { get; set; } = new();
        public Dictionary<string, IntentRuleParameter> OptionalParameters { get; set; } = new();
        public List<IntentFollowUpRule> FollowUpRules { get; set; } = new();
        public IntentRuleToolBinding ToolBinding { get; set; } = new("", "", false, false, "");
        public IntentRuleTargeting Targeting { get; set; } = new(false, Array.Empty<string>(), false);

        public IntentRule ToRule() => new(
            Id,
            Version,
            DisplayName,
            AgentId,
            BusinessModule,
            HumanDoc,
            PositiveExamples,
            NegativeExamples,
            RequiredParameters,
            OptionalParameters,
            FollowUpRules,
            ToolBinding,
            Targeting);
    }
}
```

Modify `src\RoadProtoAgentBackend.Api\Program.cs` registration:

```csharp
using RoadProtoAgentBackend.Application.IntentRules;
using RoadProtoAgentBackend.Infrastructure.IntentRules;
```

Add:

```csharp
var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var intentRuleDirectory = builder.Configuration["RoadProtoAgent:IntentRuleDirectory"]
    ?? Path.Combine(repositoryRoot, "rules", "intents");
builder.Services.AddSingleton<IIntentRuleRepository>(_ => new FileIntentRuleRepository(intentRuleDirectory));
```

Use configuration override in tests and publish scripts when needed. During development from `dotnet run`, the computed path resolves to `F:\0_GPT_RoadProtoAgentBackend\rules\intents`.

- [ ] **Step 6: 运行规则仓库测试**

Run:

```powershell
dotnet test F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\RoadProtoAgentBackend.Tests.csproj --filter FileIntentRuleRepositoryTests
```

Expected: PASS.

- [ ] **Step 7: 检查点**

Run:

```powershell
dotnet test F:\0_GPT_RoadProtoAgentBackend\RoadProtoAgentBackend.sln
```

Expected: 当前新增测试通过；因状态机迁移导致的旧测试失败保留到 Task 6 修复。

提交检查点命令：

```powershell
git -C F:\0_GPT_RoadProtoAgentBackend add rules src tests
git -C F:\0_GPT_RoadProtoAgentBackend commit -m "feat: add executable intent rule repository"
```

---

### Task 3: 后端模型网关从请求构造升级为真实可测调用

**Files:**

- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Models\IModelGateway.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Models\ModelGatewayRequest.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Models\ModelGatewayResponse.cs`
- Modify: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Infrastructure\Models\OpenAiCompatibleModelGateway.cs`
- Modify: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Api\Program.cs`
- Test: `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\OpenAiCompatibleModelGatewayTests.cs`
- Test: `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\FakeHttpMessageHandler.cs`

- [ ] **Step 1: 写真实调用失败测试**

Append to `OpenAiCompatibleModelGatewayTests.cs`:

```csharp
[Fact]
public async Task CompleteAsync_returns_assistant_content_from_openai_compatible_response()
{
    using var http = new HttpClient(new FakeHttpMessageHandler("""{"choices":[{"message":{"content":"{\"intentId\":\"subgrade_template.query\"}"}}]}"""))
    {
        BaseAddress = new Uri("https://example.test")
    };
    var gateway = new OpenAiCompatibleModelGateway(http);
    var settings = new ModelProviderSettings(
        ModelProviderKind.GPT,
        "https://example.test/v1",
        "gpt-test",
        "protected",
        true,
        DateTimeOffset.UtcNow);

    var response = await gateway.CompleteAsync(new ModelGatewayRequest(settings, "plain-secret", "请输出 JSON。"));

    Assert.Contains("subgrade_template.query", response.Content, StringComparison.Ordinal);
    Assert.Equal("gpt-test", response.Model);
}
```

Create `tests\RoadProtoAgentBackend.Tests\FakeHttpMessageHandler.cs`:

```csharp
using System.Net;
using System.Text;

namespace RoadProtoAgentBackend.Tests;

public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly string _body;

    public FakeHttpMessageHandler(string body)
    {
        _body = body;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_body, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Run:

```powershell
dotnet test F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\RoadProtoAgentBackend.Tests.csproj --filter OpenAiCompatibleModelGatewayTests
```

Expected: FAIL，提示 `CompleteAsync` 或 `ModelGatewayRequest` 不存在。

- [ ] **Step 3: 新增模型网关接口和请求响应**

Create `src\RoadProtoAgentBackend.Application\Models\IModelGateway.cs`:

```csharp
namespace RoadProtoAgentBackend.Application.Models;

public interface IModelGateway
{
    Task<ModelGatewayResponse> CompleteAsync(ModelGatewayRequest request, CancellationToken cancellationToken = default);
}
```

Create `src\RoadProtoAgentBackend.Application\Models\ModelGatewayRequest.cs`:

```csharp
using RoadProtoAgentBackend.Domain.Settings;

namespace RoadProtoAgentBackend.Application.Models;

public sealed record ModelGatewayRequest(ModelProviderSettings Settings, string ApiKeyPlainText, string Prompt);
```

Create `src\RoadProtoAgentBackend.Application\Models\ModelGatewayResponse.cs`:

```csharp
namespace RoadProtoAgentBackend.Application.Models;

public sealed record ModelGatewayResponse(string Provider, string Model, string Content, int? PromptTokens, int? CompletionTokens);
```

- [ ] **Step 4: 实现 OpenAI-compatible 调用**

Replace `OpenAiCompatibleModelGateway.cs` with:

```csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using RoadProtoAgentBackend.Application.Models;
using RoadProtoAgentBackend.Domain.Settings;

namespace RoadProtoAgentBackend.Infrastructure.Models;

public sealed class OpenAiCompatibleModelGateway : IModelGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly HttpClient _httpClient;

    public OpenAiCompatibleModelGateway(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public static HttpRequestMessage CreateChatCompletionsRequest(
        ModelProviderSettings settings,
        string apiKeyPlainText,
        string prompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.BaseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.Model);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKeyPlainText);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        var baseUrl = settings.BaseUrl.EndsWith("/", StringComparison.Ordinal)
            ? settings.BaseUrl
            : settings.BaseUrl + "/";
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(baseUrl), "chat/completions"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKeyPlainText);
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            model = settings.Model,
            messages = new[]
            {
                new { role = "system", content = "你是 RoadProto 可控工程 Agent。必须只输出一个 JSON 对象，不输出 Markdown。" },
                new { role = "user", content = prompt }
            },
            temperature = 0.1
        }, JsonOptions), Encoding.UTF8, "application/json");
        return request;
    }

    public async Task<ModelGatewayResponse> CompleteAsync(
        ModelGatewayRequest request,
        CancellationToken cancellationToken = default)
    {
        using var httpRequest = CreateChatCompletionsRequest(request.Settings, request.ApiKeyPlainText, request.Prompt);
        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        int? promptTokens = null;
        int? completionTokens = null;
        if (root.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("prompt_tokens", out var prompt)) promptTokens = prompt.GetInt32();
            if (usage.TryGetProperty("completion_tokens", out var completion)) completionTokens = completion.GetInt32();
        }

        return new ModelGatewayResponse(
            request.Settings.Provider.ToString(),
            request.Settings.Model,
            content,
            promptTokens,
            completionTokens);
    }
}
```

Modify `Program.cs` registration:

```csharp
using RoadProtoAgentBackend.Application.Models;
using RoadProtoAgentBackend.Infrastructure.Models;
```

Add:

```csharp
builder.Services.AddHttpClient<IModelGateway, OpenAiCompatibleModelGateway>();
```

- [ ] **Step 5: 运行模型网关测试**

Run:

```powershell
dotnet test F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\RoadProtoAgentBackend.Tests.csproj --filter OpenAiCompatibleModelGatewayTests
```

Expected: PASS.

- [ ] **Step 6: 检查点**

Run:

```powershell
dotnet test F:\0_GPT_RoadProtoAgentBackend\RoadProtoAgentBackend.sln
```

Expected: 模型网关相关测试通过；旧状态机测试允许继续在 Task 6 修复。

提交检查点命令：

```powershell
git -C F:\0_GPT_RoadProtoAgentBackend add src tests
git -C F:\0_GPT_RoadProtoAgentBackend commit -m "feat: call openai compatible model gateway"
```

---

### Task 4: 后端 Prompt Builder 和模型 JSON 解析器

**Files:**

- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Models\ModelPromptBuilder.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Models\ModelExtractionParser.cs`
- Test: `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\ModelPromptBuilderTests.cs`
- Test: `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\ModelExtractionParserTests.cs`

- [ ] **Step 1: 写 Prompt Builder 失败测试**

Create `tests\RoadProtoAgentBackend.Tests\ModelPromptBuilderTests.cs`:

```csharp
using RoadProtoAgentBackend.Application.Models;
using RoadProtoAgentBackend.Domain.IntentRules;

namespace RoadProtoAgentBackend.Tests;

public sealed class ModelPromptBuilderTests
{
    [Fact]
    public void Build_includes_positive_negative_examples_and_json_contract()
    {
        var rule = new IntentRule(
            "subgrade_template.create",
            "0.1.0",
            "创建路基模板",
            "subgrade_template_agent",
            "CROSS_SECTION",
            "docs/agent/intents/subgrade_template_create.md",
            new[] { "帮我创建高速公路路基模板。" },
            new[] { "修改当前模板的行车道宽度。" },
            new Dictionary<string, IntentRuleParameter>(),
            new Dictionary<string, IntentRuleParameter>(),
            Array.Empty<IntentFollowUpRule>(),
            new IntentRuleToolBinding("SubgradeTemplate.Create", "medium", true, true, "RD_SECTION_SUBGRADE_TEMPLATE_APPLY_DIALOG_FILE"),
            new IntentRuleTargeting(false, Array.Empty<string>(), false));

        var prompt = ModelPromptBuilder.Build("创建高速公路路基模板", new[] { rule });

        Assert.Contains("只输出 JSON", prompt, StringComparison.Ordinal);
        Assert.Contains("subgrade_template.create", prompt, StringComparison.Ordinal);
        Assert.Contains("帮我创建高速公路路基模板", prompt, StringComparison.Ordinal);
        Assert.Contains("修改当前模板的行车道宽度", prompt, StringComparison.Ordinal);
        Assert.Contains("不要补默认值", prompt, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 2: 写解析器失败测试**

Create `tests\RoadProtoAgentBackend.Tests\ModelExtractionParserTests.cs`:

```csharp
using RoadProtoAgentBackend.Application.Models;

namespace RoadProtoAgentBackend.Tests;

public sealed class ModelExtractionParserTests
{
    [Fact]
    public void Parse_accepts_json_with_target_and_parameters()
    {
        const string json = """
        {
          "intentId": "subgrade_template.modify",
          "confidence": 0.88,
          "matchedExpression": "把当前路基模板的行车道加宽 1 米",
          "target": { "mode": "currentSelection", "handle": null, "name": null, "confidence": 0.8 },
          "parameters": {
            "laneWidthDelta": { "value": 1.0, "unit": "m", "sourceText": "加宽 1 米", "confidence": 0.9 }
          },
          "rejectedIntent": null,
          "notes": []
        }
        """;

        var result = ModelExtractionParser.Parse(json);

        Assert.Equal("subgrade_template.modify", result.IntentId);
        Assert.Equal("currentSelection", result.Target?.Mode);
        Assert.True(result.Parameters.ContainsKey("laneWidthDelta"));
    }

    [Fact]
    public void Parse_rejects_non_json_text()
    {
        var error = Assert.Throws<InvalidOperationException>(() => ModelExtractionParser.Parse("我将创建模板。"));

        Assert.Contains("MODEL_OUTPUT_INVALID", error.Message, StringComparison.Ordinal);
    }
}
```

- [ ] **Step 3: 运行测试确认失败**

Run:

```powershell
dotnet test F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\RoadProtoAgentBackend.Tests.csproj --filter "ModelPromptBuilderTests|ModelExtractionParserTests"
```

Expected: FAIL，提示 `ModelPromptBuilder` 和 `ModelExtractionParser` 不存在。

- [ ] **Step 4: 实现 Prompt Builder**

Create `src\RoadProtoAgentBackend.Application\Models\ModelPromptBuilder.cs`:

```csharp
using System.Text;
using RoadProtoAgentBackend.Domain.IntentRules;

namespace RoadProtoAgentBackend.Application.Models;

public static class ModelPromptBuilder
{
    public static string Build(string userMessage, IReadOnlyList<IntentRule> rules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);
        var builder = new StringBuilder();
        builder.AppendLine("你是 RoadProto 可控工程 Agent 的意图识别和参数抽取器。");
        builder.AppendLine("只输出 JSON 对象，不输出 Markdown，不输出解释性正文。");
        builder.AppendLine("你只能识别下列意图；不要补默认值，不要推导业务规则，不要调用工具。");
        builder.AppendLine("输出字段: intentId, confidence, matchedExpression, target, parameters, rejectedIntent, notes。");
        builder.AppendLine("target 字段包含 mode, handle, name, confidence。");
        builder.AppendLine("parameters 中每个字段包含 value, unit, sourceText, confidence。");
        builder.AppendLine();
        foreach (var rule in rules)
        {
            builder.AppendLine($"意图: {rule.Id}");
            builder.AppendLine($"名称: {rule.DisplayName}");
            builder.AppendLine("正例:");
            foreach (var example in rule.PositiveExamples)
            {
                builder.AppendLine($"- {example}");
            }

            builder.AppendLine("反例:");
            foreach (var example in rule.NegativeExamples)
            {
                builder.AppendLine($"- {example}");
            }
            builder.AppendLine();
        }

        builder.AppendLine("用户输入:");
        builder.AppendLine(userMessage);
        return builder.ToString();
    }
}
```

- [ ] **Step 5: 实现解析器**

Create `src\RoadProtoAgentBackend.Application\Models\ModelExtractionParser.cs`:

```csharp
using System.Text.Json;
using RoadProtoAgentBackend.Domain.Models;

namespace RoadProtoAgentBackend.Application.Models;

public static class ModelExtractionParser
{
    public static ModelExtractionResult Parse(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            var parameters = new Dictionary<string, ModelParameterValue>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("parameters", out var parameterRoot) && parameterRoot.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in parameterRoot.EnumerateObject())
                {
                    var valueRoot = property.Value;
                    object? value = null;
                    if (valueRoot.TryGetProperty("value", out var valueElement))
                    {
                        value = ReadValue(valueElement);
                    }

                    parameters[property.Name] = new ModelParameterValue(
                        value,
                        valueRoot.TryGetProperty("unit", out var unit) ? unit.GetString() : null,
                        valueRoot.TryGetProperty("sourceText", out var source) ? source.GetString() ?? "" : "",
                        valueRoot.TryGetProperty("confidence", out var confidence) ? confidence.GetDouble() : 0.0);
                }
            }

            IntentTarget? target = null;
            if (root.TryGetProperty("target", out var targetRoot) && targetRoot.ValueKind == JsonValueKind.Object)
            {
                target = new IntentTarget(
                    targetRoot.TryGetProperty("mode", out var mode) ? mode.GetString() ?? "" : "",
                    targetRoot.TryGetProperty("handle", out var handle) ? handle.GetString() : null,
                    targetRoot.TryGetProperty("name", out var name) ? name.GetString() : null,
                    targetRoot.TryGetProperty("confidence", out var targetConfidence) ? targetConfidence.GetDouble() : 0.0);
            }

            var notes = new List<string>();
            if (root.TryGetProperty("notes", out var notesRoot) && notesRoot.ValueKind == JsonValueKind.Array)
            {
                notes.AddRange(notesRoot.EnumerateArray().Select(item => item.GetString() ?? ""));
            }

            return new ModelExtractionResult(
                root.GetProperty("intentId").GetString() ?? "",
                root.TryGetProperty("confidence", out var confidenceRoot) ? confidenceRoot.GetDouble() : 0.0,
                root.TryGetProperty("matchedExpression", out var matched) ? matched.GetString() ?? "" : "",
                target,
                parameters,
                root.TryGetProperty("rejectedIntent", out var rejected) ? rejected.GetString() : null,
                notes);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("MODEL_OUTPUT_INVALID: 模型没有返回合法 JSON。", ex);
        }
        catch (KeyNotFoundException ex)
        {
            throw new InvalidOperationException("MODEL_OUTPUT_INVALID: 模型 JSON 缺少必要字段。", ex);
        }
    }

    private static object? ReadValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }
}
```

- [ ] **Step 6: 运行测试确认通过**

Run:

```powershell
dotnet test F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\RoadProtoAgentBackend.Tests.csproj --filter "ModelPromptBuilderTests|ModelExtractionParserTests"
```

Expected: PASS.

- [ ] **Step 7: 检查点**

Run:

```powershell
dotnet test F:\0_GPT_RoadProtoAgentBackend\RoadProtoAgentBackend.sln
```

Expected: 当前新增测试通过；旧状态机测试允许继续在 Task 6 修复。

提交检查点命令：

```powershell
git -C F:\0_GPT_RoadProtoAgentBackend add src tests
git -C F:\0_GPT_RoadProtoAgentBackend commit -m "feat: build prompts and parse model extraction"
```

---

### Task 5: 后端规则服务生成增删改查计划

**Files:**

- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\IntentRules\IntentResolutionResult.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\IntentRules\IntentRuleService.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Agents\SubgradeTemplateAgent.cs`
- Delete after migration: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Agents\SubgradeTemplateCreateAgent.cs`
- Test: `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\IntentRuleServiceTests.cs`
- Test: `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\SubgradeTemplateAgentTests.cs`

- [ ] **Step 1: 写规则服务失败测试**

Create `tests\RoadProtoAgentBackend.Tests\IntentRuleServiceTests.cs`:

```csharp
using RoadProtoAgentBackend.Application.IntentRules;
using RoadProtoAgentBackend.Domain.IntentRules;
using RoadProtoAgentBackend.Domain.Models;

namespace RoadProtoAgentBackend.Tests;

public sealed class IntentRuleServiceTests
{
    [Fact]
    public void Resolve_create_without_road_grade_returns_follow_up()
    {
        var service = new IntentRuleService(new[] { Rule("subgrade_template.create", "SubgradeTemplate.Create", false, true, "medium") });
        var extraction = new ModelExtractionResult(
            "subgrade_template.create",
            0.9,
            "创建路基模板",
            null,
            new Dictionary<string, ModelParameterValue>(),
            null,
            Array.Empty<string>());

        var result = service.Resolve(extraction);

        Assert.Equal("AwaitingUserInput", result.StateHint);
        Assert.Equal("请问道路等级是什么？", result.FollowUpMessage);
        Assert.Null(result.Plan);
    }

    [Fact]
    public void Resolve_delete_without_target_returns_follow_up()
    {
        var service = new IntentRuleService(new[] { Rule("subgrade_template.delete", "SubgradeTemplate.Delete", true, true, "high") });
        var extraction = new ModelExtractionResult(
            "subgrade_template.delete",
            0.9,
            "删除这个路基模板",
            null,
            new Dictionary<string, ModelParameterValue>(),
            null,
            Array.Empty<string>());

        var result = service.Resolve(extraction);

        Assert.Equal("请在 CAD 中选择要删除的路基模板，或告诉我模板 handle / 名称。", result.FollowUpMessage);
    }

    [Fact]
    public void Resolve_query_all_returns_low_risk_plan_without_approval()
    {
        var service = new IntentRuleService(new[] { Rule("subgrade_template.query", "SubgradeTemplate.Query", false, false, "low", allowEmptyForQuery: true) });
        var extraction = new ModelExtractionResult(
            "subgrade_template.query",
            0.92,
            "当前图里有哪些路基模板",
            null,
            new Dictionary<string, ModelParameterValue>(),
            null,
            Array.Empty<string>());

        var result = service.Resolve(extraction);

        Assert.NotNull(result.Plan);
        Assert.False(result.Plan!.RequiresApproval);
        Assert.Equal("SubgradeTemplate.Query", result.Plan.ToolName);
        Assert.Equal("all", result.Plan.Arguments["targetMode"]);
    }

    private static IntentRule Rule(
        string id,
        string tool,
        bool targetRequired,
        bool approval,
        string risk,
        bool allowEmptyForQuery = false)
        => new(
            id,
            "0.1.0",
            id,
            "subgrade_template_agent",
            "CROSS_SECTION",
            "",
            Array.Empty<string>(),
            Array.Empty<string>(),
            id.EndsWith(".create", StringComparison.Ordinal)
                ? new Dictionary<string, IntentRuleParameter>
                {
                    ["roadGrade"] = new("enum", new[] { "Expressway" }, null, null, Array.Empty<double>(), null, "ask")
                }
                : new Dictionary<string, IntentRuleParameter>(),
            new Dictionary<string, IntentRuleParameter>(),
            new[] { new IntentFollowUpRule("missing_required.roadGrade", "请问道路等级是什么？") },
            new IntentRuleToolBinding(tool, risk, approval, approval, "adapter"),
            new IntentRuleTargeting(targetRequired, new[] { "all", "currentSelection", "handle", "name" }, allowEmptyForQuery));
}
```

- [ ] **Step 2: 写 Agent 失败测试**

Create `tests\RoadProtoAgentBackend.Tests\SubgradeTemplateAgentTests.cs`:

```csharp
using RoadProtoAgentBackend.Application.Agents;
using RoadProtoAgentBackend.Application.IntentRules;
using RoadProtoAgentBackend.Domain.IntentRules;
using RoadProtoAgentBackend.Domain.Models;

namespace RoadProtoAgentBackend.Tests;

public sealed class SubgradeTemplateAgentTests
{
    [Fact]
    public void CreatePlan_builds_modify_plan_with_target_handle()
    {
        var agent = new SubgradeTemplateAgent(new IntentRuleService(new[] { ModifyRule() }));
        var extraction = new ModelExtractionResult(
            "subgrade_template.modify",
            0.91,
            "把 handle 为 ABCD 的路基模板行车道改成 4 米",
            new IntentTarget("handle", "ABCD", null, 0.96),
            new Dictionary<string, ModelParameterValue>
            {
                ["laneWidth"] = new(4.0, "m", "行车道改成 4 米", 0.94)
            },
            null,
            Array.Empty<string>());

        var result = agent.CreatePlan(extraction);

        Assert.NotNull(result.Plan);
        Assert.Equal("SubgradeTemplate.Modify", result.Plan!.ToolName);
        Assert.Equal("ABCD", result.Plan.Arguments["targetHandle"]);
        Assert.Equal(4.0, result.Plan.Arguments["laneWidth"]);
    }

    private static IntentRule ModifyRule()
        => new(
            "subgrade_template.modify",
            "0.1.0",
            "修改路基模板",
            "subgrade_template_agent",
            "CROSS_SECTION",
            "",
            Array.Empty<string>(),
            Array.Empty<string>(),
            new Dictionary<string, IntentRuleParameter>(),
            new Dictionary<string, IntentRuleParameter>
            {
                ["laneWidth"] = new("number", Array.Empty<string>(), "m", 0, Array.Empty<double>(), null, null)
            },
            Array.Empty<IntentFollowUpRule>(),
            new IntentRuleToolBinding("SubgradeTemplate.Modify", "medium", true, true, "RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE"),
            new IntentRuleTargeting(true, new[] { "handle", "name", "currentSelection" }, false));
}
```

- [ ] **Step 3: 运行测试确认失败**

Run:

```powershell
dotnet test F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\RoadProtoAgentBackend.Tests.csproj --filter "IntentRuleServiceTests|SubgradeTemplateAgentTests"
```

Expected: FAIL，提示 `IntentRuleService` 和 `SubgradeTemplateAgent` 不存在。

- [ ] **Step 4: 实现规则解析结果**

Create `src\RoadProtoAgentBackend.Application\IntentRules\IntentResolutionResult.cs`:

```csharp
using RoadProtoAgentBackend.Domain.Tools;

namespace RoadProtoAgentBackend.Application.IntentRules;

public sealed record IntentResolutionResult(string StateHint, AgentPlan? Plan, string? FollowUpMessage, string? ErrorCode, string? ErrorMessage)
{
    public static IntentResolutionResult FollowUp(string message)
        => new("AwaitingUserInput", null, message, null, null);

    public static IntentResolutionResult Planned(AgentPlan plan)
        => new(plan.RequiresApproval ? "AwaitingUserConfirmation" : "ParametersValidated", plan, null, null, null);

    public static IntentResolutionResult Failed(string code, string message)
        => new("Failed", null, null, code, message);
}
```

- [ ] **Step 5: 实现规则服务**

Create `src\RoadProtoAgentBackend.Application\IntentRules\IntentRuleService.cs`:

```csharp
using RoadProtoAgentBackend.Domain.IntentRules;
using RoadProtoAgentBackend.Domain.Models;
using RoadProtoAgentBackend.Domain.Tools;

namespace RoadProtoAgentBackend.Application.IntentRules;

public sealed class IntentRuleService
{
    private const double MinimumConfidence = 0.65;
    private readonly IReadOnlyDictionary<string, IntentRule> _rules;

    public IntentRuleService(IReadOnlyList<IntentRule> rules)
    {
        _rules = rules.ToDictionary(rule => rule.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IntentResolutionResult Resolve(ModelExtractionResult extraction)
    {
        if (!_rules.TryGetValue(extraction.IntentId, out var rule))
        {
            return IntentResolutionResult.Failed("INTENT_NOT_SUPPORTED", $"不支持的意图: {extraction.IntentId}");
        }

        if (extraction.Confidence < MinimumConfidence)
        {
            return IntentResolutionResult.Failed("INTENT_LOW_CONFIDENCE", "意图置信度不足，需要用户澄清。");
        }

        if (rule.RequiredParameters.ContainsKey("roadGrade") && !extraction.Parameters.ContainsKey("roadGrade"))
        {
            var message = rule.FollowUpRules.FirstOrDefault(item => item.When == "missing_required.roadGrade")?.Message
                ?? "请问道路等级是什么？";
            return IntentResolutionResult.FollowUp(message);
        }

        if (rule.Targeting.Required && !HasTarget(extraction.Target))
        {
            var action = extraction.IntentId.EndsWith(".delete", StringComparison.OrdinalIgnoreCase) ? "删除" : "修改";
            var message = rule.FollowUpRules.FirstOrDefault(item => item.When == "missing_target")?.Message
                ?? $"请在 CAD 中选择要{action}的路基模板，或告诉我模板 handle / 名称。";
            return IntentResolutionResult.FollowUp(message);
        }

        var arguments = BuildArguments(rule, extraction);
        var plan = new AgentPlan(
            rule.Id,
            rule.ToolBinding.ToolName,
            BuildSummary(rule, extraction, arguments),
            rule.ToolBinding.RiskLevel,
            rule.ToolBinding.RequiresApproval,
            arguments,
            BuildConfirmationItems(rule, arguments),
            null);

        return IntentResolutionResult.Planned(plan);
    }

    private static bool HasTarget(IntentTarget? target)
    {
        return target != null
            && (!string.IsNullOrWhiteSpace(target.Handle)
                || !string.IsNullOrWhiteSpace(target.Name)
                || target.Mode.Equals("currentSelection", StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, object?> BuildArguments(IntentRule rule, ModelExtractionResult extraction)
    {
        var arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["targetMode"] = extraction.Target?.Mode ?? (rule.Targeting.AllowEmptyForQuery ? "all" : ""),
            ["targetHandle"] = extraction.Target?.Handle,
            ["targetName"] = extraction.Target?.Name,
            ["templateName"] = GetString(extraction, "templateName") ?? "默认路基模板",
            ["roadGrade"] = GetString(extraction, "roadGrade"),
            ["laneWidth"] = GetDouble(extraction, "laneWidth", 3.75),
            ["laneWidthDelta"] = GetDoubleOrNull(extraction, "laneWidthDelta"),
            ["hardShoulderWidth"] = GetDouble(extraction, "hardShoulderWidth", 2.5),
            ["earthShoulderWidth"] = GetDouble(extraction, "earthShoulderWidth", 0.75),
            ["slopeRatio"] = 1.5,
            ["unit"] = "m"
        };
        return arguments;
    }

    private static string BuildSummary(IntentRule rule, ModelExtractionResult extraction, IReadOnlyDictionary<string, object?> arguments)
    {
        return rule.Id switch
        {
            "subgrade_template.create" => $"准备创建路基模板，道路等级: {arguments["roadGrade"]}。",
            "subgrade_template.modify" => $"准备修改路基模板，目标: {arguments["targetHandle"] ?? arguments["targetName"] ?? arguments["targetMode"]}。",
            "subgrade_template.delete" => $"准备删除路基模板，目标: {arguments["targetHandle"] ?? arguments["targetName"] ?? arguments["targetMode"]}。",
            "subgrade_template.query" => "准备查询路基模板。",
            _ => extraction.MatchedExpression
        };
    }

    private static IReadOnlyList<string> BuildConfirmationItems(IntentRule rule, IReadOnlyDictionary<string, object?> arguments)
    {
        var items = new List<string>
        {
            $"Tool: {rule.ToolBinding.ToolName}",
            $"风险等级: {rule.ToolBinding.RiskLevel}"
        };
        if (arguments.TryGetValue("targetHandle", out var handle) && handle is not null)
        {
            items.Add($"目标 handle: {handle}");
        }
        if (rule.Id == "subgrade_template.delete")
        {
            items.Add("删除操作不可直接由后端写 DWG，必须由 RoadProto 本地 Adapter 执行。");
        }
        return items;
    }

    private static string? GetString(ModelExtractionResult extraction, string key)
    {
        return extraction.Parameters.TryGetValue(key, out var value) ? Convert.ToString(value.Value) : null;
    }

    private static double GetDouble(ModelExtractionResult extraction, string key, double fallback)
    {
        return GetDoubleOrNull(extraction, key) ?? fallback;
    }

    private static double? GetDoubleOrNull(ModelExtractionResult extraction, string key)
    {
        if (!extraction.Parameters.TryGetValue(key, out var value) || value.Value is null)
        {
            return null;
        }
        var text = Convert.ToString(value.Value, System.Globalization.CultureInfo.InvariantCulture);
        return double.TryParse(
            text,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;
    }
}
```

- [ ] **Step 6: 实现业务 Agent**

Create `src\RoadProtoAgentBackend.Application\Agents\SubgradeTemplateAgent.cs`:

```csharp
using RoadProtoAgentBackend.Application.IntentRules;
using RoadProtoAgentBackend.Domain.Models;

namespace RoadProtoAgentBackend.Application.Agents;

public sealed class SubgradeTemplateAgent
{
    private readonly IntentRuleService _intentRuleService;

    public SubgradeTemplateAgent(IntentRuleService intentRuleService)
    {
        _intentRuleService = intentRuleService;
    }

    public IntentResolutionResult CreatePlan(ModelExtractionResult extraction)
    {
        return _intentRuleService.Resolve(extraction);
    }
}
```

Keep `SubgradeTemplateCreateAgent.cs` until Task 6 updates dependency injection and tests. Delete it after Task 6 passes.

- [ ] **Step 7: 运行规则服务测试**

Run:

```powershell
dotnet test F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\RoadProtoAgentBackend.Tests.csproj --filter "IntentRuleServiceTests|SubgradeTemplateAgentTests"
```

Expected: PASS.

- [ ] **Step 8: 检查点**

Run:

```powershell
dotnet test F:\0_GPT_RoadProtoAgentBackend\RoadProtoAgentBackend.sln
```

Expected: 新增规则服务测试通过；旧 `AgentRunServiceTests` 仍可能失败，Task 6 修复。

提交检查点命令：

```powershell
git -C F:\0_GPT_RoadProtoAgentBackend add src tests
git -C F:\0_GPT_RoadProtoAgentBackend commit -m "feat: resolve subgrade template intent plans"
```

---

### Task 6: 后端状态机接入规则、模型和用户补充输入

**Files:**

- Modify: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Runs\AgentRunService.cs`
- Modify: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Api\Endpoints\AgentRunEndpoints.cs`
- Modify: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Api\Program.cs`
- Delete: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Agents\SubgradeTemplateCreateAgent.cs`
- Test: `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\AgentRunServiceTests.cs`
- Test: `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\FakeModelGateway.cs`

- [ ] **Step 1: 新增假模型网关**

Create `tests\RoadProtoAgentBackend.Tests\FakeModelGateway.cs`:

```csharp
using RoadProtoAgentBackend.Application.Models;

namespace RoadProtoAgentBackend.Tests;

public sealed class FakeModelGateway : IModelGateway
{
    private readonly Queue<string> _responses = new();

    public List<string> Prompts { get; } = new();

    public void Enqueue(string response)
    {
        _responses.Enqueue(response);
    }

    public Task<ModelGatewayResponse> CompleteAsync(ModelGatewayRequest request, CancellationToken cancellationToken = default)
    {
        Prompts.Add(request.Prompt);
        return Task.FromResult(new ModelGatewayResponse(
            request.Settings.Provider.ToString(),
            request.Settings.Model,
            _responses.Dequeue(),
            null,
            null));
    }
}
```

- [ ] **Step 2: 替换 AgentRunService 测试**

Update `AgentRunServiceTests.cs` so it builds service with rules, fake model gateway, settings and secret protector. Include these test cases:

```csharp
[Fact]
public async Task StartRunAsync_routes_modify_to_modify_plan()
{
    var gateway = new FakeModelGateway();
    gateway.Enqueue("""
    {
      "intentId": "subgrade_template.modify",
      "confidence": 0.91,
      "matchedExpression": "修改当前模板的行车道宽度",
      "target": { "mode": "currentSelection", "handle": null, "name": null, "confidence": 0.8 },
      "parameters": { "laneWidth": { "value": 4.0, "unit": "m", "sourceText": "行车道宽度", "confidence": 0.9 } },
      "rejectedIntent": null,
      "notes": []
    }
    """);
    var service = TestAgentRunServiceFactory.Create(gateway);

    var run = await service.StartRunAsync(new StartAgentRunRequest(null, "修改当前模板的行车道宽度为 4 米"));

    Assert.Equal(AgentRunState.AwaitingUserConfirmation, run.State);
    Assert.Equal("SubgradeTemplate.Modify", run.Plan?.ToolName);
}

[Fact]
public async Task StartRunAsync_returns_follow_up_when_create_misses_road_grade()
{
    var gateway = new FakeModelGateway();
    gateway.Enqueue("""
    {
      "intentId": "subgrade_template.create",
      "confidence": 0.91,
      "matchedExpression": "创建路基模板",
      "target": null,
      "parameters": {},
      "rejectedIntent": null,
      "notes": []
    }
    """);
    var service = TestAgentRunServiceFactory.Create(gateway);

    var run = await service.StartRunAsync(new StartAgentRunRequest(null, "创建路基模板"));

    Assert.Equal(AgentRunState.AwaitingUserInput, run.State);
    Assert.Equal("请问道路等级是什么？", run.Plan?.FollowUpMessage);
}
```

Add a `TestAgentRunServiceFactory` in the same test file or a separate test helper. It must use `ModelProviderSettings` with protected key and an `ISecretProtector` that returns `"plain-secret"`.

- [ ] **Step 3: 运行状态机测试确认失败**

Run:

```powershell
dotnet test F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\RoadProtoAgentBackend.Tests.csproj --filter AgentRunServiceTests
```

Expected: FAIL，提示构造函数、状态或计划生成逻辑不匹配。

- [ ] **Step 4: 改造 AgentRunService**

Update constructor dependencies:

```csharp
private readonly InMemoryAgentRunRepository _repository;
private readonly IIntentRuleRepository _ruleRepository;
private readonly IModelGateway _modelGateway;
private readonly ISettingsStore _settingsStore;
private readonly ISecretProtector _secretProtector;
private readonly IAgentFlowLogger _logger;
```

In `StartRunAsync`:

1. 创建 ids 和 `RunCreated` 事件。
2. 加载启用的第一个模型 Provider。
3. 加载全部意图规则。
4. 用 `ModelPromptBuilder.Build(request.Message, rules)` 构建 prompt。
5. 解密 API Key 并调用 `_modelGateway.CompleteAsync(...)`。
6. 用 `ModelExtractionParser.Parse(response.Content)` 解析。
7. 用 `SubgradeTemplateAgent(new IntentRuleService(rules)).CreatePlan(extraction)` 生成计划。
8. 根据 `IntentResolutionResult.StateHint` 设置 `AgentRunState`。
9. 记录 `ModelCalled`、`IntentRecognized`、`ParametersValidated`、`AwaitingUserInput` 或 `AwaitingUserConfirmation`。

Use this helper for provider selection:

```csharp
private static ModelProviderSettings SelectProvider(AgentSettingsSnapshot snapshot)
{
    var provider = snapshot.Providers.Values.FirstOrDefault(item => item.IsEnabled && item.HasApiKey);
    return provider ?? throw new InvalidOperationException("MODEL_PROVIDER_NOT_CONFIGURED: 没有启用且已配置 API Key 的模型 Provider。");
}
```

Use this state mapper:

```csharp
private static AgentRunState StateFromHint(string hint)
{
    return hint switch
    {
        "AwaitingUserInput" => AgentRunState.AwaitingUserInput,
        "ParametersValidated" => AgentRunState.ParametersValidated,
        "AwaitingUserConfirmation" => AgentRunState.AwaitingUserConfirmation,
        "Failed" => AgentRunState.Failed,
        _ => AgentRunState.Planning
    };
}
```

Update `ConfirmAsync` so it throws unless state is `AwaitingUserConfirmation`; query plans with `RequiresApproval=false` should dispatch immediately in `StartRunAsync` by setting `DispatchedToolCall` and state `DispatchingTool`.

- [ ] **Step 5: 增加用户补充输入接口**

Add request record in `StartAgentRunRequest.cs` or a new file:

```csharp
namespace RoadProtoAgentBackend.Application.Runs;

public sealed record ContinueAgentRunRequest(string Message);
```

Add method to `AgentRunService`:

```csharp
public async Task<AgentRun> ContinueAsync(string taskId, ContinueAgentRunRequest request, CancellationToken cancellationToken = default)
{
    var run = _repository.Get(taskId);
    if (run.State != AgentRunState.AwaitingUserInput)
    {
        throw new InvalidOperationException($"Agent run is not waiting for user input. Current state: {run.State}");
    }

    var combined = run.UserMessage + "\n用户补充: " + request.Message;
    var restarted = await StartRunAsync(new StartAgentRunRequest(run.SessionId, combined), cancellationToken);
    return restarted with { TaskId = run.TaskId, TraceId = run.TraceId };
}
```

Add endpoint in `AgentRunEndpoints.cs`:

```csharp
endpoints.MapPost("/api/agent/runs/{taskId}/user-input", async (
    string taskId,
    ContinueAgentRunRequest request,
    AgentRunService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.ContinueAsync(taskId, request, cancellationToken)));
```

- [ ] **Step 6: 注册新服务**

In `Program.cs`, replace old registration:

```csharp
builder.Services.AddSingleton<SubgradeTemplateCreateAgent>();
```

with no direct singleton for `SubgradeTemplateAgent`, because it is created per resolution with loaded rules. Keep:

```csharp
builder.Services.AddSingleton<AgentRunService>();
```

Ensure `IIntentRuleRepository`, `IModelGateway`, `ISettingsStore`, `ISecretProtector`, and `IAgentFlowLogger` are registered before `AgentRunService`.

Delete `SubgradeTemplateCreateAgent.cs` after all tests compile.

- [ ] **Step 7: 运行状态机测试**

Run:

```powershell
dotnet test F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\RoadProtoAgentBackend.Tests.csproj --filter AgentRunServiceTests
```

Expected: PASS.

- [ ] **Step 8: 运行后端全量测试**

Run:

```powershell
dotnet test F:\0_GPT_RoadProtoAgentBackend\RoadProtoAgentBackend.sln
```

Expected: PASS.

提交检查点命令：

```powershell
git -C F:\0_GPT_RoadProtoAgentBackend add src tests
git -C F:\0_GPT_RoadProtoAgentBackend commit -m "feat: route agent runs through model and intent rules"
```

---

### Task 7: RoadProto WPF 支持追问、查询和四类 Tool DTO

**Files:**

- Modify: `F:\0_GPT_道路设计原型功能项目\src\ui\wpf\RoadProto.Terrain.UI\Agent\Models\AgentDtos.cs`
- Modify: `F:\0_GPT_道路设计原型功能项目\src\ui\wpf\RoadProto.Terrain.UI\Agent\Backend\AgentBackendClient.cs`
- Modify: `F:\0_GPT_道路设计原型功能项目\src\ui\wpf\RoadProto.Terrain.UI\Agent\AgentConsoleViewModel.cs`
- Modify: `F:\0_GPT_道路设计原型功能项目\src\ui\wpf\RoadProto.Terrain.UI\Agent\AgentConsolePalette.xaml`
- Test: `F:\0_GPT_道路设计原型功能项目\tests\RoadProtoManagedBridgeTests\Program.cs`

- [ ] **Step 1: 写托管源码契约测试**

Append to `tests\RoadProtoManagedBridgeTests\Program.cs`:

```csharp
static void AgentConsoleSupportsSubgradeTemplateCrudIntentFlow()
{
    var root = FindRepoRoot();
    var dtos = File.ReadAllText(Path.Combine(root, "src", "ui", "wpf", "RoadProto.Terrain.UI", "Agent", "Models", "AgentDtos.cs"), Encoding.UTF8);
    var client = File.ReadAllText(Path.Combine(root, "src", "ui", "wpf", "RoadProto.Terrain.UI", "Agent", "Backend", "AgentBackendClient.cs"), Encoding.UTF8);
    var viewModel = File.ReadAllText(Path.Combine(root, "src", "ui", "wpf", "RoadProto.Terrain.UI", "Agent", "AgentConsoleViewModel.cs"), Encoding.UTF8);

    Check(dtos.Contains("FollowUpMessage"), "agent plan dto should expose follow-up messages");
    Check(dtos.Contains("RiskLevel"), "agent plan dto should expose risk level");
    Check(dtos.Contains("TargetHandle"), "subgrade CRUD arguments should expose target handle");
    Check(client.Contains("user-input"), "agent backend client should post follow-up user input");
    Check(viewModel.Contains("AwaitingUserInput"), "agent console should display follow-up state");
    Check(viewModel.Contains("SubgradeTemplate.Query"), "agent console should recognize read-only query tools");
}
```

Call it near the other Agent tests:

```csharp
AgentConsoleSupportsSubgradeTemplateCrudIntentFlow();
```

- [ ] **Step 2: 运行托管测试确认失败**

Run:

```powershell
dotnet build F:\0_GPT_道路设计原型功能项目\tests\RoadProtoManagedBridgeTests\RoadProtoManagedBridgeTests.csproj -c Debug /p:UseAppHost=false
F:\0_GPT_道路设计原型功能项目\tests\RoadProtoManagedBridgeTests\bin\Debug\net8.0\RoadProtoManagedBridgeTests.dll
```

If the DLL cannot execute directly, use:

```powershell
dotnet F:\0_GPT_道路设计原型功能项目\tests\RoadProtoManagedBridgeTests\bin\Debug\net8.0\RoadProtoManagedBridgeTests.dll
```

Expected: FAIL，提示 DTO 或 client 缺少字段。

- [ ] **Step 3: 扩展 DTO**

Modify `AgentPlanDto`:

```csharp
[DataMember(Name = "riskLevel")]
public string RiskLevel { get; set; } = string.Empty;

[DataMember(Name = "requiresApproval")]
public bool RequiresApproval { get; set; }

[DataMember(Name = "followUpMessage")]
public string? FollowUpMessage { get; set; }
```

Add request DTO:

```csharp
[DataContract]
public sealed class ContinueAgentRunRequestDto
{
    [DataMember(Name = "message")]
    public string Message { get; set; } = string.Empty;
}
```

Replace `SubgradeTemplateCreateArgumentsDto` with a broader DTO while preserving create fields:

```csharp
[DataContract]
public sealed class SubgradeTemplateToolArgumentsDto
{
    [DataMember(Name = "targetMode")]
    public string TargetMode { get; set; } = string.Empty;

    [DataMember(Name = "targetHandle")]
    public string? TargetHandle { get; set; }

    [DataMember(Name = "targetName")]
    public string? TargetName { get; set; }

    [DataMember(Name = "templateName")]
    public string TemplateName { get; set; } = "默认路基模板";

    [DataMember(Name = "roadGrade")]
    public string? RoadGrade { get; set; }

    [DataMember(Name = "laneWidth")]
    public double LaneWidth { get; set; } = 3.75;

    [DataMember(Name = "laneWidthDelta")]
    public double? LaneWidthDelta { get; set; }

    [DataMember(Name = "hardShoulderWidth")]
    public double HardShoulderWidth { get; set; } = 2.5;

    [DataMember(Name = "earthShoulderWidth")]
    public double EarthShoulderWidth { get; set; } = 0.75;

    [DataMember(Name = "slopeRatio")]
    public double SlopeRatio { get; set; } = 1.5;

    [DataMember(Name = "unit")]
    public string Unit { get; set; } = "m";
}
```

Keep `SubgradeTemplateCreateArgumentsDto` as a derived compatibility class only if existing tests still reference it:

```csharp
[DataContract]
public sealed class SubgradeTemplateCreateArgumentsDto : SubgradeTemplateToolArgumentsDto
{
}
```

- [ ] **Step 4: 扩展后端客户端**

Add to `AgentBackendClient.cs`:

```csharp
public async Task<AgentRunDto> ContinueRunAsync(
    string taskId,
    string message,
    CancellationToken cancellationToken = default)
{
    var request = new ContinueAgentRunRequestDto { Message = message };
    using var response = await _httpClient
        .PostAsync($"api/agent/runs/{taskId}/user-input", CreateJsonContent(request), cancellationToken)
        .ConfigureAwait(false);
    response.EnsureSuccessStatusCode();
    return await ReadJsonAsync<AgentRunDto>(response).ConfigureAwait(false);
}
```

- [ ] **Step 5: 更新 ViewModel 流转**

In `SendAsync`, after receiving run:

```csharp
if (run.State == "AwaitingUserInput")
{
    Messages.Add("Agent: " + (run.Plan?.FollowUpMessage ?? "需要补充信息。"));
    CanConfirm = false;
    return;
}

if (run.DispatchedToolCall != null && string.Equals(run.DispatchedToolCall.ToolName, "SubgradeTemplate.Query", StringComparison.OrdinalIgnoreCase))
{
    await DispatchReadOnlyToolAsync(run).ConfigureAwait(true);
    return;
}
```

Add helper:

```csharp
private async Task DispatchReadOnlyToolAsync(AgentRunDto run)
{
    _currentRun = run;
    var toolResult = await _toolBridge.DispatchAsync(run).ConfigureAwait(true);
    Log("LocalToolValidationCompleted", toolResult.Message);
    var completed = await _client.PostToolResultAsync(run.TaskId, toolResult).ConfigureAwait(true);
    _currentRun = completed;
    Messages.Add("Agent: " + toolResult.Message);
    Log(toolResult.Succeeded ? "ToolResultPosted" : "RunFailed", completed.State);
}
```

In `SendAsync`, when `_currentRun?.State == "AwaitingUserInput"`, call `ContinueRunAsync` instead of `StartRunAsync`:

```csharp
var run = _currentRun?.State == "AwaitingUserInput"
    ? await _client.ContinueRunAsync(_currentRun.TaskId, message).ConfigureAwait(true)
    : await _client.StartRunAsync(null, message).ConfigureAwait(true);
```

- [ ] **Step 6: 更新 XAML 展示风险**

Add a small risk line under TraceId:

```xml
<TextBlock Text="{Binding CurrentRiskText}" Foreground="#B45309" FontSize="12" />
```

Add property in ViewModel:

```csharp
private string _currentRiskText = "";

public string CurrentRiskText
{
    get => _currentRiskText;
    private set => SetField(ref _currentRiskText, value);
}
```

Set it after each run:

```csharp
CurrentRiskText = string.IsNullOrWhiteSpace(run.Plan?.RiskLevel) ? "" : "风险等级: " + run.Plan.RiskLevel;
```

- [ ] **Step 7: 运行托管测试**

Run:

```powershell
dotnet build F:\0_GPT_道路设计原型功能项目\tests\RoadProtoManagedBridgeTests\RoadProtoManagedBridgeTests.csproj -c Debug /p:UseAppHost=false
dotnet F:\0_GPT_道路设计原型功能项目\tests\RoadProtoManagedBridgeTests\bin\Debug\net8.0\RoadProtoManagedBridgeTests.dll
```

Expected: PASS.

提交检查点命令：

```powershell
git -C F:\0_GPT_道路设计原型功能项目 add src/ui/wpf tests/RoadProtoManagedBridgeTests
git -C F:\0_GPT_道路设计原型功能项目 commit -m "feat: show agent follow-up and subgrade CRUD plans"
```

---

### Task 8: RoadProto 本地 Tool Bridge 支持创建、修改、删除、查询

**Files:**

- Modify: `F:\0_GPT_道路设计原型功能项目\src\ui\wpf\RoadProto.Terrain.UI\Agent\Bridge\AgentLocalToolBridge.cs`
- Create: `F:\0_GPT_道路设计原型功能项目\src\cad_adapter\objectarx\agent\ObjectArxAgentSubgradeTemplateToolCommand.h`
- Create: `F:\0_GPT_道路设计原型功能项目\src\cad_adapter\objectarx\agent\ObjectArxAgentSubgradeTemplateToolCommand.cpp`
- Modify: `F:\0_GPT_道路设计原型功能项目\src\cad_adapter\objectarx\agent\ObjectArxAgentConsoleCommand.h`
- Modify: `F:\0_GPT_道路设计原型功能项目\src\modules\agent\AgentModule.cpp`
- Modify: `F:\0_GPT_道路设计原型功能项目\src\app\RoadProtoArx.vcxproj`
- Test: `F:\0_GPT_道路设计原型功能项目\tests\core_tests.cpp`
- Test: `F:\0_GPT_道路设计原型功能项目\tests\RoadProtoManagedBridgeTests\Program.cs`

- [ ] **Step 1: 写 C++ 命令注册失败测试**

Append to `tests\core_tests.cpp`:

```cpp
void agentModuleRegistersSubgradeTemplateToolFileCommand()
{
    CommandRegistry commandRegistry;
    RibbonModel ribbon;
    auto module = roadproto::modules::agent::createAgentModule();
    module.registerCommands(commandRegistry);
    const auto& commands = commandRegistry.commands();
    CHECK(commands.contains(L"RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE"));
    const auto command = commands.find(L"RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE");
    CHECK(command != commands.end());
    CHECK(command->second.moduleCode == L"AGENT");
    CHECK(command->second.businessDocPath == L"docs/business/agent/路基模板Agent_增删改查_MVP.md");
}
```

Call it in `main()` near other Agent module tests.

- [ ] **Step 2: 写托管 Bridge 失败测试**

Append to `tests\RoadProtoManagedBridgeTests\Program.cs`:

```csharp
static void AgentLocalToolBridgeSupportsSubgradeTemplateCrudTools()
{
    var root = FindRepoRoot();
    var bridge = File.ReadAllText(Path.Combine(root, "src", "ui", "wpf", "RoadProto.Terrain.UI", "Agent", "Bridge", "AgentLocalToolBridge.cs"), Encoding.UTF8);

    Check(bridge.Contains("SubgradeTemplate.Create"), "local bridge should support create tool");
    Check(bridge.Contains("SubgradeTemplate.Modify"), "local bridge should support modify tool");
    Check(bridge.Contains("SubgradeTemplate.Delete"), "local bridge should support delete tool");
    Check(bridge.Contains("SubgradeTemplate.Query"), "local bridge should support query tool");
    Check(bridge.Contains("RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE"), "modify/delete/query should use native agent tool command");
}
```

Call it near other Agent tests.

- [ ] **Step 3: 运行测试确认失败**

Run:

```powershell
& "D:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe" F:\0_GPT_道路设计原型功能项目\tests\RoadProtoCoreTests.vcxproj /p:Configuration=Debug /p:Platform=x64
F:\0_GPT_道路设计原型功能项目\artifacts\x64\Debug\RoadProtoCoreTests.exe
dotnet build F:\0_GPT_道路设计原型功能项目\tests\RoadProtoManagedBridgeTests\RoadProtoManagedBridgeTests.csproj -c Debug /p:UseAppHost=false
dotnet F:\0_GPT_道路设计原型功能项目\tests\RoadProtoManagedBridgeTests\bin\Debug\net8.0\RoadProtoManagedBridgeTests.dll
```

Expected: FAIL，提示命令或字符串不存在。

- [ ] **Step 4: 新增本地工具请求文件格式**

In `AgentLocalToolBridge.cs`, add request writer for native agent command:

```csharp
private static void WriteAgentToolRequest(string path, string operation, SubgradeTemplateToolArgumentsDto arguments, string resultPath)
{
    var lines = new List<string>
    {
        "operation=" + operation,
        "resultPath=" + Escape(resultPath),
        "targetMode=" + Escape(arguments.TargetMode),
        "targetHandle=" + Escape(arguments.TargetHandle ?? ""),
        "targetName=" + Escape(arguments.TargetName ?? ""),
        "templateName=" + Escape(arguments.TemplateName),
        "roadGrade=" + Escape(arguments.RoadGrade ?? ""),
        "laneWidth=" + arguments.LaneWidth.ToString(CultureInfo.InvariantCulture),
        "laneWidthDelta=" + (arguments.LaneWidthDelta?.ToString(CultureInfo.InvariantCulture) ?? ""),
        "hardShoulderWidth=" + arguments.HardShoulderWidth.ToString(CultureInfo.InvariantCulture),
        "earthShoulderWidth=" + arguments.EarthShoulderWidth.ToString(CultureInfo.InvariantCulture),
        "slopeRatio=" + arguments.SlopeRatio.ToString(CultureInfo.InvariantCulture),
        "unit=" + Escape(arguments.Unit)
    };
    File.WriteAllLines(path, lines, Encoding.UTF8);
}
```

Add:

```csharp
private static string Escape(string value)
{
    return value.Replace("%", "%25").Replace("\r", "%0D").Replace("\n", "%0A");
}
```

Route tool names:

```csharp
if (string.Equals(toolName, "SubgradeTemplate.Create", StringComparison.OrdinalIgnoreCase))
{
    return await DispatchCreateAsync(run, cancellationToken).ConfigureAwait(true);
}

if (string.Equals(toolName, "SubgradeTemplate.Modify", StringComparison.OrdinalIgnoreCase)
    || string.Equals(toolName, "SubgradeTemplate.Delete", StringComparison.OrdinalIgnoreCase)
    || string.Equals(toolName, "SubgradeTemplate.Query", StringComparison.OrdinalIgnoreCase))
{
    return await DispatchNativeAgentToolAsync(run, cancellationToken).ConfigureAwait(true);
}
```

`DispatchNativeAgentToolAsync` writes request file and sends:

```csharp
document.SendStringToExecute($"RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE \"{commandPath}\"\n", true, false, true);
```

- [ ] **Step 5: 新增 ObjectARX Agent Tool 命令头文件**

Create `src\cad_adapter\objectarx\agent\ObjectArxAgentSubgradeTemplateToolCommand.h`:

```cpp
#pragma once

#include "core/command/CommandRegistry.h"

namespace roadproto::cad_adapter::objectarx::agent {

core::CommandProcedure agentSubgradeTemplateToolFileCommandProcedure();

}
```

- [ ] **Step 6: 新增 ObjectARX Agent Tool 命令实现**

Create `src\cad_adapter\objectarx\agent\ObjectArxAgentSubgradeTemplateToolCommand.cpp` with these responsibilities:

```cpp
#include "cad_adapter/objectarx/agent/ObjectArxAgentSubgradeTemplateToolCommand.h"

#ifndef ROADPROTO_TEST_BUILD
#include "app/startup/ApplicationContext.h"
#include "cad_adapter/objectarx/cross_section/DnSubgradeTemplateEntity.h"

#include "aced.h"
#include "dbapserv.h"

#include <Windows.h>
#include <filesystem>
#include <fstream>
#include <map>
#include <sstream>
#include <string>
#endif

namespace roadproto::cad_adapter::objectarx::agent {
namespace {

#ifndef ROADPROTO_TEST_BUILD

std::map<std::wstring, std::wstring> readRequest(const std::wstring& path);
void writeResult(const std::wstring& path, bool succeeded, const std::wstring& entityId, const std::wstring& templateName, const std::wstring& message);
bool resolveSubgradeByHandle(const std::wstring& handle, AcDbObjectId& objectId);
bool querySubgradeTemplates(const std::map<std::wstring, std::wstring>& request);
bool modifySubgradeTemplate(const std::map<std::wstring, std::wstring>& request);
bool deleteSubgradeTemplate(const std::map<std::wstring, std::wstring>& request);

void runAgentSubgradeTemplateToolFileCommand()
{
    auto& editor = app::ApplicationContext::instance().editor();
    ACHAR pathBuffer[1024] = {};
    if (acedGetString(Adesk::kTrue, L"\nRoadProto Agent subgrade template tool file: ", pathBuffer) != RTNORM) {
        return;
    }

    const std::wstring path = pathBuffer;
    const auto request = readRequest(path);
    const auto operation = request.contains(L"operation") ? request.at(L"operation") : L"";
    if (operation == L"query") {
        querySubgradeTemplates(request);
    } else if (operation == L"modify") {
        modifySubgradeTemplate(request);
    } else if (operation == L"delete") {
        deleteSubgradeTemplate(request);
    } else {
        const auto resultPath = request.contains(L"resultPath") ? request.at(L"resultPath") : L"";
        writeResult(resultPath, false, L"", L"", L"不支持的路基模板 Agent 操作。");
        editor.writeWarning(L"不支持的路基模板 Agent 操作。");
    }
}

#else

void runAgentSubgradeTemplateToolFileCommand()
{
}

#endif

} // namespace

core::CommandProcedure agentSubgradeTemplateToolFileCommandProcedure()
{
    return &runAgentSubgradeTemplateToolFileCommand;
}

} // namespace roadproto::cad_adapter::objectarx::agent
```

Fill the helper bodies in the same file. The minimum required behavior:

- `query`: enumerate model space `DnSubgradeTemplateEntity` objects, write summary to `message`.
- `modify`: resolve `targetHandle`, open entity for write, update MVP fields on travel lane / hard shoulder / earth shoulder widths, write result.
- `delete`: resolve `targetHandle`, erase entity, write result.
- If target is missing for modify/delete, write `succeeded=0` with message `缺少目标路基模板 handle。`.

- [ ] **Step 7: 注册命令**

Modify `AgentModule.cpp`:

```cpp
#include "cad_adapter/objectarx/agent/ObjectArxAgentSubgradeTemplateToolCommand.h"
```

Register:

```cpp
commandRegistry.registerCommand(core::CommandDefinition{
    L"RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE",
    L"Agent 路基模板工具文件",
    L"AGENT",
    L"Internal Agent command that executes controlled subgrade template query, modify, or delete tool files.",
    cad_adapter::objectarx::agent::agentSubgradeTemplateToolFileCommandProcedure(),
    true,
    false,
    L"docs/business/agent/路基模板Agent_增删改查_MVP.md",
    false});
```

Add the new `.cpp` to `src\app\RoadProtoArx.vcxproj`:

```xml
<ClCompile Include="..\cad_adapter\objectarx\agent\ObjectArxAgentSubgradeTemplateToolCommand.cpp" />
```

Add the new `.h` if the project lists headers explicitly:

```xml
<ClInclude Include="..\cad_adapter\objectarx\agent\ObjectArxAgentSubgradeTemplateToolCommand.h" />
```

- [ ] **Step 8: 运行 RoadProto 测试**

Run:

```powershell
& "D:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe" F:\0_GPT_道路设计原型功能项目\tests\RoadProtoCoreTests.vcxproj /p:Configuration=Debug /p:Platform=x64
F:\0_GPT_道路设计原型功能项目\artifacts\x64\Debug\RoadProtoCoreTests.exe
dotnet build F:\0_GPT_道路设计原型功能项目\tests\RoadProtoManagedBridgeTests\RoadProtoManagedBridgeTests.csproj -c Debug /p:UseAppHost=false
dotnet F:\0_GPT_道路设计原型功能项目\tests\RoadProtoManagedBridgeTests\bin\Debug\net8.0\RoadProtoManagedBridgeTests.dll
```

Expected: PASS.

提交检查点命令：

```powershell
git -C F:\0_GPT_道路设计原型功能项目 add src tests
git -C F:\0_GPT_道路设计原型功能项目 commit -m "feat: add local subgrade template CRUD agent tool"
```

---

### Task 9: RoadProto 文档补齐路基模板增删改查意图

**Files:**

- Create: `F:\0_GPT_道路设计原型功能项目\docs\agent\intents\subgrade_template_modify.md`
- Create: `F:\0_GPT_道路设计原型功能项目\docs\agent\intents\subgrade_template_delete.md`
- Create: `F:\0_GPT_道路设计原型功能项目\docs\agent\intents\subgrade_template_query.md`
- Create: `F:\0_GPT_道路设计原型功能项目\docs\business\agent\路基模板Agent_增删改查_MVP.md`
- Modify: `F:\0_GPT_道路设计原型功能项目\docs\agent\README.md`
- Modify: `F:\0_GPT_道路设计原型功能项目\docs\modules\agent.md`

- [ ] **Step 1: 新增三份意图文档**

Use `docs\agent\intent_rule_template.md` as the structure. Each file must include sections 1 through 12.

For `subgrade_template_modify.md`, set:

```text
意图 ID: subgrade_template.modify
本意图负责: 修改已有路基模板的 MVP 参数
本意图不负责: 创建、删除、戴帽、出图
必要参数: target / 用户确认
Tool: SubgradeTemplate.Modify
风险: 中
```

For `subgrade_template_delete.md`, set:

```text
意图 ID: subgrade_template.delete
本意图负责: 删除唯一确定的既有路基模板
本意图不负责: 级联删除道路模型引用
必要参数: target / 用户确认
Tool: SubgradeTemplate.Delete
风险: 高
```

For `subgrade_template_query.md`, set:

```text
意图 ID: subgrade_template.query
本意图负责: 查询全部、当前选中或指定路基模板摘要
本意图不负责: 修改、删除、创建
必要参数: 无；target 可选
Tool: SubgradeTemplate.Query
风险: 低
```

- [ ] **Step 2: 新增业务文档**

Create `docs\business\agent\路基模板Agent_增删改查_MVP.md` with:

```markdown
# 路基模板 Agent 增删改查 MVP

## 功能范围

本功能属于 `AGENT` 模块的受控工程 Agent 验证能力，覆盖路基模板创建、修改、删除、查询四类自然语言意图。

## 边界

- 后端负责规则、模型抽参、状态机和 Tool 计划。
- RoadProto 本地负责 WPF 确认、本地 Tool Adapter 和 ObjectARX 执行。
- 后端不直接写 DWG。
- WPF 不直接操作 ObjectARX 实体。

## 意图清单

| 意图 | Tool | 风险 | 是否审批 |
| --- | --- | --- | --- |
| `subgrade_template.create` | `SubgradeTemplate.Create` | 中 | 是 |
| `subgrade_template.modify` | `SubgradeTemplate.Modify` | 中 | 是 |
| `subgrade_template.delete` | `SubgradeTemplate.Delete` | 高 | 是 |
| `subgrade_template.query` | `SubgradeTemplate.Query` | 低 | 否 |

## 验收

- 反例不会误触发相邻意图。
- 修改和删除必须定位唯一模板。
- 删除必须高风险确认。
- 查询不写 CAD。
- 全流程记录 Trace。
```

- [ ] **Step 3: 更新索引**

Update `docs\agent\README.md` 文档索引，新增：

```markdown
| `intents/subgrade_template_modify.md` | 路基模板修改意图规则 |
| `intents/subgrade_template_delete.md` | 路基模板删除意图规则 |
| `intents/subgrade_template_query.md` | 路基模板查询意图规则 |
```

Update `docs\modules\agent.md` 文档索引，新增：

```markdown
| `docs/business/agent/路基模板Agent_增删改查_MVP.md` | 路基模板增删改查 Agent 验证业务文档 |
```

- [ ] **Step 4: 文档检查**

Run:

```powershell
$files = @(
  'F:\0_GPT_道路设计原型功能项目\docs\agent\intents\subgrade_template_modify.md',
  'F:\0_GPT_道路设计原型功能项目\docs\agent\intents\subgrade_template_delete.md',
  'F:\0_GPT_道路设计原型功能项目\docs\agent\intents\subgrade_template_query.md',
  'F:\0_GPT_道路设计原型功能项目\docs\business\agent\路基模板Agent_增删改查_MVP.md'
)
foreach ($file in $files) {
  $content = Get-Content -LiteralPath $file -Encoding UTF8 -Raw
  if ($content -notmatch '## 1\. 意图基本信息|# 路基模板 Agent 增删改查 MVP') {
    Write-Error "文档缺少预期标题: $file"
  }
}
```

Expected: command prints no errors.

提交检查点命令：

```powershell
git -C F:\0_GPT_道路设计原型功能项目 add docs
git -C F:\0_GPT_道路设计原型功能项目 commit -m "docs: define subgrade template CRUD agent intents"
```

---

### Task 10: 全量验证、发布后端并确认 worktree 收口

**Files:**

- Modify as needed: `F:\0_GPT_道路设计原型功能项目\README.md`
- Modify as needed: `F:\0_GPT_RoadProtoAgentBackend\README.md`

- [ ] **Step 1: 后端全量测试**

Run:

```powershell
dotnet test F:\0_GPT_RoadProtoAgentBackend\RoadProtoAgentBackend.sln
```

Expected: 全部测试通过。

- [ ] **Step 2: RoadProto 核心测试**

Run:

```powershell
& "D:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe" F:\0_GPT_道路设计原型功能项目\tests\RoadProtoCoreTests.vcxproj /p:Configuration=Debug /p:Platform=x64
F:\0_GPT_道路设计原型功能项目\artifacts\x64\Debug\RoadProtoCoreTests.exe
```

Expected: build 成功，测试输出全部通过。

- [ ] **Step 3: RoadProto 托管测试**

Run:

```powershell
dotnet build F:\0_GPT_道路设计原型功能项目\tests\RoadProtoManagedBridgeTests\RoadProtoManagedBridgeTests.csproj -c Debug /p:UseAppHost=false
dotnet F:\0_GPT_道路设计原型功能项目\tests\RoadProtoManagedBridgeTests\bin\Debug\net8.0\RoadProtoManagedBridgeTests.dll
```

Expected: PASS.

- [ ] **Step 4: RoadProto Debug 构建**

Run:

```powershell
& "D:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe" F:\0_GPT_道路设计原型功能项目\RoadProto.sln /p:Configuration=Debug /p:Platform=x64
```

Expected: 0 errors。若 AutoCAD/ObjectARX 路径缺失，记录缺失路径并先完成后端验证。

- [ ] **Step 5: 发布后端**

Run:

```powershell
dotnet publish F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Api\RoadProtoAgentBackend.Api.csproj -c Release -r win-x64 --self-contained false -o F:\0_GPT_RoadProtoAgentBackend\artifacts\publish
```

Expected:

```text
F:\0_GPT_RoadProtoAgentBackend\artifacts\publish\RoadProtoAgentBackend.exe
```

exists.

- [ ] **Step 6: 后端健康检查**

Run:

```powershell
$process = Start-Process -FilePath 'F:\0_GPT_RoadProtoAgentBackend\artifacts\publish\RoadProtoAgentBackend.exe' -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 2
Invoke-RestMethod -Uri 'http://127.0.0.1:17861/health'
Stop-Process -Id $process.Id
```

Expected: health response contains `status` equal to `ok`.

- [ ] **Step 7: 手工联调脚本**

After API Key is configured in WPF or `%APPDATA%\RoadProtoAgent\settings.json`, test these messages in Agent Console:

```text
帮我创建高速公路路基模板。
修改当前模板的行车道宽度为 4 米。
当前图里有哪些路基模板？
删除这个路基模板。
```

Expected:

- 创建进入确认，确认后创建实体。
- 修改缺目标时要求选择或指定 handle；目标明确后进入确认。
- 查询直接返回摘要，不要求审批。
- 删除进入高风险确认。

- [ ] **Step 8: worktree 收口确认**

如果本计划在 worktree 中执行，先确认当前状态，不自动把正式文件同步回主项目目录。只有用户明确确认合入或需要主项目目录可见副本时，才按 `AGENTS.md` 执行 Git 合入、快进、挑拣提交或指定范围同步。

```powershell
git -C <worktree> status --short
git -C <worktree> branch --show-current
git -C F:\0_GPT_道路设计原型功能项目 status --short
```

Expected:

- worktree 分支包含本任务修改。
- 主项目目录未被自动覆盖。
- 是否合入或同步由用户明确确认。

提交检查点命令：

```powershell
git -C F:\0_GPT_RoadProtoAgentBackend status --short
git -C F:\0_GPT_道路设计原型功能项目 status --short
```

---

## Self-Review

**Spec coverage:**

- 四个意图规则文件：Task 2。
- 模型真实调用：Task 3。
- Prompt 和 JSON 解析：Task 4。
- 规则校验、追问、确认计划：Task 5。
- 状态机、用户补充输入、Tool dispatch：Task 6。
- WPF 展示追问、风险、查询结果：Task 7。
- RoadProto 本地创建、修改、删除、查询 Tool：Task 8。
- 文档同步：Task 9。
- 测试、发布、健康检查和 worktree 收口确认：Task 10。

**Incomplete-marker scan:** 每个实现任务都有明确文件、步骤和验证命令，没有留下未完成标记。

**Type consistency:**

- 后端统一使用 `AgentPlan` 替代 `SubgradeTemplatePlan`。
- 模型输出统一使用 `ModelExtractionResult`。
- WPF 使用 `SubgradeTemplateToolArgumentsDto`，兼容旧 `SubgradeTemplateCreateArgumentsDto`。
- Tool 名称统一为 `SubgradeTemplate.Create`、`SubgradeTemplate.Modify`、`SubgradeTemplate.Delete`、`SubgradeTemplate.Query`。
