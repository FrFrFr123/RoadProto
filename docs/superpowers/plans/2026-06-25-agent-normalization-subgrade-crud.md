# Agent 归一化与路基模板修改删除 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建立通用自然语言归一化能力，并让路基模板 Agent 支持目标选择、部件级修改、部件删除、部件新增和整模板删除。

**Architecture:** 后端负责模型抽取后的确定性归一化、目标解析、规则追问和 Tool 参数生成；WPF 负责展示分组日志、序列化 Tool 请求文件和接收本地结果；RoadProto C++ ObjectARX Adapter 负责点选、按名称/handle 查找实体、读取当前模板、应用部件操作、校验并写回或删除。归一化以 `rawValue -> canonicalValue -> provenance` 形式进入 Trace，不让本地 Adapter 猜自然语言。

**Tech Stack:** .NET 8 / ASP.NET Core 后端、xUnit、.NET Framework 4.8 WPF、C++17、ObjectARX 2021、PowerShell UTF-8。

---

## 工作目录

- RoadProto worktree：`F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-optimization`
- Agent 后端：`F:\0_GPT_RoadProtoAgentBackend`

执行命令前先设置 PowerShell UTF-8：

```powershell
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
```

## 文件结构

### 后端新增文件

- `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Normalization\NormalizationContext.cs`：归一化上下文，包含 Intent、参数名、追问状态和原始用户输入。
- `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Normalization\NormalizationResult.cs`：归一化结果，保存 raw、canonical、confidence、provenance 和中文解释。
- `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Normalization\RoadProtoNormalizationService.cs`：道路等级、侧别、部件类型、目标引用、颜色和位置的确定性归一化。
- `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Runs\AgentSessionContext.cs`：会话级最近对象上下文。
- `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Runs\InMemoryAgentSessionContextRepository.cs`：按 sessionId 保存最近创建、修改和操作过的路基模板。
- `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\RoadProtoNormalizationServiceTests.cs`：归一化单元测试。
- `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\SubgradeTemplateComponentOperationTests.cs`：部件操作参数生成测试。

### 后端修改文件

- `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\IntentRules\IntentRuleService.cs`：接入归一化、目标解析、侧别追问和部件操作计划。
- `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Domain\Tools\SubgradeTemplateToolArguments.cs`：扩展 `TargetMode`、`TargetRef` 和 `ComponentOperations`。
- `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Runs\AgentRunService.cs`：维护最近对象上下文，输出归一化 Trace。
- `F:\0_GPT_RoadProtoAgentBackend\rules\intents\subgrade_template.modify.yaml`：增加目标引用、侧别、部件选择和部件操作字段。
- `F:\0_GPT_RoadProtoAgentBackend\rules\intents\subgrade_template.delete.yaml`：增加目标引用和点选目标字段。
- `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\IntentRuleServiceTests.cs`：覆盖归一化、追问和部件操作。
- `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\AgentRunServiceTests.cs`：覆盖最近对象上下文、Tool 结果更新和 Trace。
- `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\ModelPromptBuilderTests.cs`：确保模型 Prompt 暴露新字段。

### RoadProto WPF 修改文件

- `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-optimization\src\ui\wpf\RoadProto.Terrain.UI\Agent\Models\AgentDtos.cs`：增加 Tool 参数 DTO。
- `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-optimization\src\ui\wpf\RoadProto.Terrain.UI\Agent\Bridge\AgentLocalToolBridge.cs`：序列化目标模式和部件操作到请求文件。
- `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-optimization\src\ui\wpf\RoadProto.Terrain.UI\Agent\AgentConsoleViewModel.cs`：每轮用户输入前插入两行空行，聊天行补 `---`。
- `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-optimization\src\ui\wpf\RoadProto.Terrain.UI\Agent\AgentLogFormatter.cs`：补充归一化事件显示。
- `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-optimization\tests\RoadProtoManagedBridgeTests\Program.cs`：增加 WPF DTO 和日志格式测试。

### RoadProto C++ 修改文件

- `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-optimization\src\cad_adapter\objectarx\agent\ObjectArxAgentSubgradeTemplateToolCommand.cpp`：从当前空实现命令升级为查询、修改、删除执行器。
- `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-optimization\src\domain\cross_section\SubgradeTemplateModel.h`：如需要，声明纯领域部件操作结构。
- `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-optimization\src\domain\cross_section\SubgradeTemplateModel.cpp`：如需要，提供不依赖 ObjectARX 的部件操作应用函数。
- `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-optimization\tests\core_tests.cpp`：测试部件操作应用逻辑。

### 文档修改文件

- `docs\agent\skills\subgrade_template_skill.md`
- `docs\agent\intents\subgrade_template_modify.md`
- `docs\agent\intents\subgrade_template_delete.md`
- `docs\business\agent\路基模板Skill_增删改查_MVP.md`
- `docs\business\agent\Agent控制台_MVP.md`
- `docs\agent_builder\skill_intent_tool_authoring.md`
- `docs\agent_builder\roadproto_practice_log.md`
- `docs\reuse\capability_catalog.md`
- `docs\dev\version_log.md`

---

### Task 1: 后端通用归一化服务

**Files:**
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Normalization\NormalizationContext.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Normalization\NormalizationResult.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Normalization\RoadProtoNormalizationService.cs`
- Test: `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\RoadProtoNormalizationServiceTests.cs`

- [ ] **Step 1: Write failing normalization tests**

Create `RoadProtoNormalizationServiceTests.cs`:

```csharp
using RoadProtoAgentBackend.Application.Normalization;

namespace RoadProtoAgentBackend.Tests;

public sealed class RoadProtoNormalizationServiceTests
{
    private readonly RoadProtoNormalizationService _service = new();

    [Theory]
    [InlineData("二级", "SecondClass")]
    [InlineData("二级路", "SecondClass")]
    [InlineData("二级道路", "SecondClass")]
    [InlineData("二级公路", "SecondClass")]
    [InlineData("2级", "SecondClass")]
    [InlineData("2 级", "SecondClass")]
    [InlineData("先生成一个二级的吧", "SecondClass")]
    public void NormalizeRoadGrade_accepts_common_natural_expressions(string raw, string expected)
    {
        var result = _service.NormalizeRoadGrade(
            raw,
            new NormalizationContext(
                IntentId: "subgrade_template.create",
                ParameterName: "roadGrade",
                UserMessage: raw,
                IsAnsweringFollowUp: true,
                FollowUpSlot: "roadGrade"));

        Assert.True(result.Succeeded);
        Assert.Equal(expected, result.CanonicalValue);
        Assert.Equal(raw, result.RawValue);
        Assert.Contains("道路等级", result.Explanation, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("创建 2 个模板")]
    [InlineData("行车道宽度 2 米")]
    [InlineData("新增 2 个部件")]
    public void NormalizeRoadGrade_does_not_treat_plain_quantity_as_road_grade(string raw)
    {
        var result = _service.NormalizeRoadGrade(
            raw,
            new NormalizationContext(
                IntentId: "subgrade_template.create",
                ParameterName: "roadGrade",
                UserMessage: raw,
                IsAnsweringFollowUp: false,
                FollowUpSlot: null));

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData("左侧", "Left")]
    [InlineData("左边", "Left")]
    [InlineData("右幅", "Right")]
    [InlineData("两侧", "Both")]
    [InlineData("两边", "Both")]
    public void NormalizeSideScope_maps_chinese_side_words(string raw, string expected)
    {
        var result = _service.NormalizeSideScope(raw);
        Assert.True(result.Succeeded);
        Assert.Equal(expected, result.CanonicalValue);
    }

    [Theory]
    [InlineData("行车道", "TravelLane")]
    [InlineData("车道", "TravelLane")]
    [InlineData("硬路肩", "HardShoulder")]
    [InlineData("土路肩", "EarthShoulder")]
    [InlineData("人行道", "Sidewalk")]
    [InlineData("慢车道", "BikeLane")]
    [InlineData("非机动车道", "BikeLane")]
    public void NormalizeComponentType_maps_common_component_names(string raw, string expected)
    {
        var result = _service.NormalizeComponentType(raw);
        Assert.True(result.Succeeded);
        Assert.Equal(expected, result.CanonicalValue);
    }
}
```

- [ ] **Step 2: Run the new tests and verify failure**

Run:

```powershell
dotnet test RoadProtoAgentBackend.sln --filter RoadProtoNormalizationServiceTests --no-restore
```

Expected: compilation fails because `RoadProtoAgentBackend.Application.Normalization` types do not exist.

- [ ] **Step 3: Add normalization result and context records**

Create `NormalizationContext.cs`:

```csharp
namespace RoadProtoAgentBackend.Application.Normalization;

public sealed record NormalizationContext(
    string? IntentId,
    string ParameterName,
    string UserMessage,
    bool IsAnsweringFollowUp,
    string? FollowUpSlot);
```

Create `NormalizationResult.cs`:

```csharp
namespace RoadProtoAgentBackend.Application.Normalization;

public sealed record NormalizationResult(
    bool Succeeded,
    string RawValue,
    string? CanonicalValue,
    double Confidence,
    string Provenance,
    string Explanation)
{
    public static NormalizationResult Success(
        string rawValue,
        string canonicalValue,
        double confidence,
        string provenance,
        string explanation)
        => new(true, rawValue, canonicalValue, confidence, provenance, explanation);

    public static NormalizationResult Failed(string rawValue, string explanation)
        => new(false, rawValue, null, 0.0, "unmatched", explanation);
}
```

- [ ] **Step 4: Add deterministic normalization service**

Create `RoadProtoNormalizationService.cs`:

```csharp
using System.Text.RegularExpressions;

namespace RoadProtoAgentBackend.Application.Normalization;

public sealed class RoadProtoNormalizationService
{
    private static readonly Regex SecondClassPattern =
        new(@"(^|[^\d])(?:二|2)\s*级(?:路|道路|公路)?(?=$|[^个米\d])", RegexOptions.Compiled);

    public NormalizationResult NormalizeRoadGrade(string? value, NormalizationContext context)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return NormalizationResult.Failed(string.Empty, "未提供道路等级原始表达。");
        }

        var raw = value.Trim();
        var compact = Regex.Replace(raw, @"\s+", string.Empty);

        var exact = compact switch
        {
            "Expressway" or "高速" or "高速公路" => "Expressway",
            "FirstClass" or "一级" or "一级路" or "一级道路" or "一级公路" or "1级" or "1级路" or "1级道路" or "1级公路" => "FirstClass",
            "SecondClass" or "二级" or "二级路" or "二级道路" or "二级公路" or "2级" or "2级路" or "2级道路" or "2级公路" => "SecondClass",
            "ThirdClass" or "三级" or "三级路" or "三级道路" or "三级公路" or "3级" or "3级路" or "3级道路" or "3级公路" => "ThirdClass",
            "FourthClass" or "四级" or "四级路" or "四级道路" or "四级公路" or "4级" or "4级路" or "4级道路" or "4级公路" => "FourthClass",
            "UrbanExpressway" or "城市快速路" or "快速路" => "UrbanExpressway",
            "UrbanArterial" or "城市主干道" or "城市主干路" or "主干道" or "主干路" => "UrbanArterial",
            "UrbanSubArterial" or "城市次干道" or "城市次干路" or "次干道" or "次干路" => "UrbanSubArterial",
            "UrbanBranch" or "城市支路" or "支路" => "UrbanBranch",
            _ => null
        };

        if (exact is not null)
        {
            return NormalizationResult.Success(raw, exact, 1.0, "alias", $"道路等级归一化为 {ExplainRoadGrade(exact)}。");
        }

        if (context.IsAnsweringFollowUp && string.Equals(context.FollowUpSlot, "roadGrade", StringComparison.Ordinal))
        {
            if (SecondClassPattern.IsMatch(raw))
            {
                return NormalizationResult.Success(raw, "SecondClass", 0.9, "context-regex", "用户正在回答道路等级追问，识别为二级公路。");
            }
        }

        return NormalizationResult.Failed(raw, "未能安全识别道路等级。");
    }

    public NormalizationResult NormalizeSideScope(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        var canonical = raw switch
        {
            "Left" or "左" or "左侧" or "左边" or "左幅" => "Left",
            "Right" or "右" or "右侧" or "右边" or "右幅" => "Right",
            "Both" or "两侧" or "两边" or "左右" or "左右侧" or "双侧" => "Both",
            _ => null
        };

        return canonical is null
            ? NormalizationResult.Failed(raw, "未能安全识别作用侧。")
            : NormalizationResult.Success(raw, canonical, 1.0, "alias", $"作用侧归一化为 {canonical}。");
    }

    public NormalizationResult NormalizeComponentType(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        var canonical = raw switch
        {
            "Median" or "中分带" or "中央分隔带" => "Median",
            "TravelLane" or "行车道" or "车道" => "TravelLane",
            "HardShoulder" or "硬路肩" => "HardShoulder",
            "EarthShoulder" or "土路肩" => "EarthShoulder",
            "SideMedian" or "侧分带" => "SideMedian",
            "Sidewalk" or "人行道" => "Sidewalk",
            "BikeLane" or "慢车道" or "非机动车道" => "BikeLane",
            "CurbStrip" or "路缘带" => "CurbStrip",
            _ => null
        };

        return canonical is null
            ? NormalizationResult.Failed(raw, "未能安全识别部件类型。")
            : NormalizationResult.Success(raw, canonical, 1.0, "alias", $"部件类型归一化为 {canonical}。");
    }

    private static string ExplainRoadGrade(string roadGrade)
    {
        return roadGrade switch
        {
            "Expressway" => "高速公路",
            "FirstClass" => "一级公路",
            "SecondClass" => "二级公路",
            "ThirdClass" => "三级公路",
            "FourthClass" => "四级公路",
            "UrbanExpressway" => "城市快速路",
            "UrbanArterial" => "城市主干路",
            "UrbanSubArterial" => "城市次干路",
            "UrbanBranch" => "城市支路",
            _ => roadGrade
        };
    }
}
```

- [ ] **Step 5: Run normalization tests**

Run:

```powershell
dotnet test RoadProtoAgentBackend.sln --filter RoadProtoNormalizationServiceTests --no-restore
```

Expected: all `RoadProtoNormalizationServiceTests` pass.

---

### Task 2: 接入道路等级归一化与 Trace

**Files:**
- Modify: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\IntentRules\IntentRuleService.cs`
- Modify: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Runs\AgentRunService.cs`
- Test: `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\IntentRuleServiceTests.cs`
- Test: `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\AgentRunServiceTests.cs`

- [ ] **Step 1: Add failing tests for `二级路` and raw/canonical trace**

Append to `IntentRuleServiceTests.cs`:

```csharp
[Theory]
[InlineData("二级路", "SecondClass")]
[InlineData("2级", "SecondClass")]
[InlineData("2 级", "SecondClass")]
public void Resolve_create_normalizes_road_grade_before_tool_arguments(string rawRoadGrade, string expected)
{
    var result = Resolve(CreateExtraction("subgrade_template.create", roadGrade: rawRoadGrade));

    Assert.Equal(IntentResolutionStatus.Planned, result.Status);
    var arguments = Assert.IsType<SubgradeTemplateToolArguments>(result.Plan?.Arguments);
    Assert.Equal(expected, arguments.RoadGrade);
    Assert.NotEmpty(arguments.Components);
}
```

Append to `AgentRunServiceTests.cs`:

```csharp
[Fact]
public async Task StartRun_logs_road_grade_normalization_raw_and_canonical_values()
{
    var logger = new RecordingFlowLogger();
    var service = CreateModelBackedService(logger, """
    {
      "agentId":"roadproto_engineering_agent",
      "skillId":"subgrade_template",
      "intentId":"subgrade_template.create",
      "toolName":"SubgradeTemplate.Create",
      "parameters":{"roadGrade":{"value":"二级路","confidence":0.9,"source":"user"}}
    }
    """);

    await service.StartRunAsync(new StartAgentRunRequest(null, "创建二级路路基模板"));

    Assert.Contains(logger.Events, item =>
        item.Stage == "RulesApplied"
        && item.Message.Contains("RoadGradeRaw=二级路（用户原始道路等级表达）", StringComparison.Ordinal)
        && item.Message.Contains("RoadGrade=SecondClass（二级公路）", StringComparison.Ordinal));
}
```

- [ ] **Step 2: Run targeted tests and verify failure**

Run:

```powershell
dotnet test RoadProtoAgentBackend.sln --filter "IntentRuleServiceTests|AgentRunServiceTests" --no-restore
```

Expected: `二级路` remains raw or normalization trace is missing.

- [ ] **Step 3: Inject `RoadProtoNormalizationService` into `IntentRuleService`**

Modify the top of `IntentRuleService.cs`:

```csharp
using RoadProtoAgentBackend.Application.Normalization;
```

Add field and constructor:

```csharp
private readonly RoadProtoNormalizationService _normalization;

public IntentRuleService()
    : this(new RoadProtoNormalizationService())
{
}

public IntentRuleService(RoadProtoNormalizationService normalization)
{
    _normalization = normalization;
}
```

Replace the `roadGrade` assignment in `BuildPlan`:

```csharp
var roadGradeRaw = GetString(extraction, "roadGrade");
var roadGradeResult = _normalization.NormalizeRoadGrade(
    roadGradeRaw,
    new NormalizationContext(
        extraction.IntentId,
        "roadGrade",
        string.Empty,
        IsAnsweringFollowUp: false,
        FollowUpSlot: null));
var roadGrade = roadGradeResult.Succeeded ? roadGradeResult.CanonicalValue : roadGradeRaw;
```

Remove or stop using the old private `NormalizeRoadGrade` switch.

- [ ] **Step 4: Add normalization diagnostic text to rule output**

In `AgentRunService.BuildResolutionOutput` or the nearest method that formats `RulesApplied`, include a normalized field when `result.Plan?.Arguments` is `SubgradeTemplateToolArguments`.

Add helper:

```csharp
private static string BuildNormalizationOutput(IntentResolutionResult result)
{
    if (result.Plan?.Arguments is not SubgradeTemplateToolArguments arguments)
    {
        return string.Empty;
    }

    var raw = result.Extraction is null
        ? null
        : GetParameterValue(result.Extraction, "roadGrade");

    if (string.IsNullOrWhiteSpace(raw) || string.Equals(raw, arguments.RoadGrade, StringComparison.Ordinal))
    {
        return string.Empty;
    }

    return DiagnosticLine(
        "归一化 " + string.Join(
            "; ",
            Field("RoadGradeRaw", raw, "用户原始道路等级表达"),
            Field("RoadGrade", arguments.RoadGrade, ExplainRoadGrade(arguments.RoadGrade))));
}
```

Add `GetParameterValue`:

```csharp
private static string? GetParameterValue(ModelExtractionResult extraction, string parameterName)
{
    return extraction.Parameters.TryGetValue(parameterName, out var parameter)
        ? parameter.Value?.ToString()
        : null;
}
```

Append this line to the `RulesApplied` diagnostic block when non-empty.

- [ ] **Step 5: Run targeted tests**

Run:

```powershell
dotnet test RoadProtoAgentBackend.sln --filter "IntentRuleServiceTests|AgentRunServiceTests" --no-restore
```

Expected: tests pass.

---

### Task 3: 会话最近对象上下文与目标引用

**Files:**
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Runs\AgentSessionContext.cs`
- Create: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Runs\InMemoryAgentSessionContextRepository.cs`
- Modify: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\Runs\AgentRunService.cs`
- Modify: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\IntentRules\IntentRuleService.cs`
- Modify: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Domain\Tools\SubgradeTemplateToolArguments.cs`
- Test: `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\AgentRunServiceTests.cs`
- Test: `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\IntentRuleServiceTests.cs`

- [ ] **Step 1: Write failing session-context tests**

Append to `AgentRunServiceTests.cs`:

```csharp
[Fact]
public async Task CompleteToolAsync_records_last_created_subgrade_template_for_followup_runs()
{
    var service = CreateModelBackedService(new RecordingFlowLogger(), """
    {
      "agentId":"roadproto_engineering_agent",
      "skillId":"subgrade_template",
      "intentId":"subgrade_template.create",
      "toolName":"SubgradeTemplate.Create",
      "parameters":{"roadGrade":{"value":"二级公路","confidence":0.9,"source":"user"}}
    }
    """, """
    {
      "agentId":"roadproto_engineering_agent",
      "skillId":"subgrade_template",
      "intentId":"subgrade_template.modify",
      "toolName":"SubgradeTemplate.Modify",
      "parameters":{"targetRef":{"value":"刚才创建的","confidence":0.9,"source":"user"}}
    }
    """);

    var createdRun = await service.StartRunAsync(new StartAgentRunRequest("session-a", "创建二级公路路基模板"));
    var confirmed = await service.ConfirmAsync(createdRun.TaskId);
    await service.CompleteToolAsync(confirmed.TaskId, new AgentToolResult(true, "ABC", "默认路基模板", "created"));

    var modifyRun = await service.StartRunAsync(new StartAgentRunRequest("session-a", "修改刚才创建的模板"));

    var arguments = Assert.IsType<SubgradeTemplateToolArguments>(modifyRun.Plan?.Arguments);
    Assert.Equal("ABC", arguments.TargetHandle);
}
```

Append to `IntentRuleServiceTests.cs`:

```csharp
[Fact]
public void Resolve_modify_with_this_template_uses_pick_on_execute_target_mode()
{
    var extraction = CreateExtraction(
        "subgrade_template.modify",
        target: null,
        parameters: new Dictionary<string, ModelExtractionParameter>
        {
            ["targetRef"] = new("这个模板", 0.9, "user")
        });

    var result = Resolve(extraction);

    Assert.Equal(IntentResolutionStatus.FollowUpRequired, result.Status);
    Assert.Contains("点选", result.FollowUpMessage, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet test RoadProtoAgentBackend.sln --filter "CompleteToolAsync_records_last_created|Resolve_modify_with_this_template" --no-restore
```

Expected: tests fail because session context and targetRef are not implemented.

- [ ] **Step 3: Add session context records**

Create `AgentSessionContext.cs`:

```csharp
namespace RoadProtoAgentBackend.Application.Runs;

public sealed record AgentSessionContext(
    string SessionId,
    string? LastCreatedSubgradeTemplateHandle,
    string? LastModifiedSubgradeTemplateHandle,
    string? LastTouchedSubgradeTemplateHandle,
    string? LastTouchedSubgradeTemplateName);
```

Create `InMemoryAgentSessionContextRepository.cs`:

```csharp
namespace RoadProtoAgentBackend.Application.Runs;

public sealed class InMemoryAgentSessionContextRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<string, AgentSessionContext> _items = new(StringComparer.Ordinal);

    public AgentSessionContext GetOrCreate(string sessionId)
    {
        lock (_gate)
        {
            if (_items.TryGetValue(sessionId, out var context))
            {
                return context;
            }

            context = new AgentSessionContext(sessionId, null, null, null, null);
            _items[sessionId] = context;
            return context;
        }
    }

    public void Save(AgentSessionContext context)
    {
        lock (_gate)
        {
            _items[context.SessionId] = context;
        }
    }
}
```

- [ ] **Step 4: Extend tool arguments**

Modify `SubgradeTemplateToolArguments.cs` record signature:

```csharp
public sealed record SubgradeTemplateToolArguments(
    string Operation,
    string? TemplateName,
    string? RoadGrade,
    string? TargetMode,
    string? TargetRef,
    string? TargetHandle,
    string? TargetName,
    double? LaneWidth,
    double? LaneWidthDelta,
    double? HardShoulderWidth,
    double? EarthShoulderWidth,
    double? MedianWidth,
    double? SlopeRatio,
    double? DisplayScale,
    string? Unit,
    string? SideScope,
    IReadOnlyList<SubgradeTemplateComponentOperationArguments> ComponentOperations,
    IReadOnlyList<SubgradeTemplateComponentArguments> Components);

public sealed record SubgradeTemplateComponentOperationArguments(
    string Operation,
    string? SideScope,
    string? ComponentType,
    string? Occurrence,
    string? PositionMode,
    string? AnchorType,
    SubgradeTemplateComponentPatchArguments Patch);

public sealed record SubgradeTemplateComponentPatchArguments(
    string? Type,
    double? Width,
    double? WidthDelta,
    double? FixedSlope,
    string? SlopeMode,
    int? ColorR,
    int? ColorG,
    int? ColorB,
    bool? HasInnerCurb,
    double? InnerCurbWidth,
    double? InnerCurbHeight,
    double? InnerCurbEmbedDepth,
    bool? HasOuterCurb,
    double? OuterCurbWidth,
    double? OuterCurbHeight,
    double? OuterCurbEmbedDepth);
```

Update all existing constructor calls to pass `TargetMode: null`, `TargetRef: null`, `ComponentOperations: Array.Empty<SubgradeTemplateComponentOperationArguments>()`.

- [ ] **Step 5: Wire session context into AgentRunService**

Add field:

```csharp
private readonly InMemoryAgentSessionContextRepository _sessionContexts;
```

Update constructors to create or accept repository:

```csharp
_sessionContexts = new InMemoryAgentSessionContextRepository();
```

When building model-backed run, pass session context into `IntentResolutionContext` or into `IntentRuleService.Resolve` by adding fields:

```csharp
LastCreatedSubgradeTemplateHandle = _sessionContexts.GetOrCreate(sessionId).LastCreatedSubgradeTemplateHandle,
LastModifiedSubgradeTemplateHandle = _sessionContexts.GetOrCreate(sessionId).LastModifiedSubgradeTemplateHandle,
LastTouchedSubgradeTemplateHandle = _sessionContexts.GetOrCreate(sessionId).LastTouchedSubgradeTemplateHandle
```

In `CompleteToolAsync`, update context:

```csharp
if (result.Succeeded && run.Plan?.Arguments is SubgradeTemplateToolArguments arguments)
{
    var context = _sessionContexts.GetOrCreate(run.SessionId);
    var handle = result.EntityId;
    var name = result.TemplateName;
    if (!string.IsNullOrWhiteSpace(handle))
    {
        context = arguments.Operation switch
        {
            "create" => context with
            {
                LastCreatedSubgradeTemplateHandle = handle,
                LastTouchedSubgradeTemplateHandle = handle,
                LastTouchedSubgradeTemplateName = name
            },
            "modify" => context with
            {
                LastModifiedSubgradeTemplateHandle = handle,
                LastTouchedSubgradeTemplateHandle = handle,
                LastTouchedSubgradeTemplateName = name
            },
            "delete" => context with
            {
                LastCreatedSubgradeTemplateHandle = context.LastCreatedSubgradeTemplateHandle == handle ? null : context.LastCreatedSubgradeTemplateHandle,
                LastModifiedSubgradeTemplateHandle = context.LastModifiedSubgradeTemplateHandle == handle ? null : context.LastModifiedSubgradeTemplateHandle,
                LastTouchedSubgradeTemplateHandle = context.LastTouchedSubgradeTemplateHandle == handle ? null : context.LastTouchedSubgradeTemplateHandle,
                LastTouchedSubgradeTemplateName = context.LastTouchedSubgradeTemplateHandle == handle ? null : context.LastTouchedSubgradeTemplateName
            },
            _ => context
        };
        _sessionContexts.Save(context);
    }
}
```

- [ ] **Step 6: Resolve targetRef in IntentRuleService**

Add target fields to `BuildPlan`:

```csharp
var targetRef = GetString(extraction, "targetRef");
var targetMode = targetRef switch
{
    "这个模板" or "这个" or "当前这个" => "PickOnExecute",
    "刚才创建的" or "上一次创建的" => "LastCreated",
    "刚才修改的" or "上一次修改的" => "LastModified",
    _ => null
};
```

Use context handles when target mode is `LastCreated` or `LastModified`. If `PickOnExecute`, set `TargetMode="PickOnExecute"` and allow plan only after approval; if the current design prefers an explicit follow-up before approval, return follow-up message that asks user to confirm point-pick. Use the behavior from the failing test.

- [ ] **Step 7: Run tests**

Run:

```powershell
dotnet test RoadProtoAgentBackend.sln --filter "AgentRunServiceTests|IntentRuleServiceTests" --no-restore
```

Expected: target context and targetRef tests pass.

---

### Task 4: 部件级操作参数生成

**Files:**
- Modify: `F:\0_GPT_RoadProtoAgentBackend\rules\intents\subgrade_template.modify.yaml`
- Modify: `F:\0_GPT_RoadProtoAgentBackend\src\RoadProtoAgentBackend.Application\IntentRules\IntentRuleService.cs`
- Modify: `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\IntentRuleServiceTests.cs`
- Modify: `F:\0_GPT_RoadProtoAgentBackend\tests\RoadProtoAgentBackend.Tests\ModelPromptBuilderTests.cs`

- [ ] **Step 1: Add failing tests for component operations**

Append to `IntentRuleServiceTests.cs`:

```csharp
[Fact]
public void Resolve_modify_builds_width_delta_component_operation_for_both_travel_lanes()
{
    var extraction = CreateExtraction(
        "subgrade_template.modify",
        target: new ModelExtractionTarget("handle", "ABC", null, 1.0),
        parameters: new Dictionary<string, ModelExtractionParameter>
        {
            ["sideScope"] = new("两侧", 0.9, "user"),
            ["componentType"] = new("行车道", 0.9, "user"),
            ["widthDelta"] = new(0.5, 0.9, "user")
        });

    var result = Resolve(extraction);

    Assert.Equal(IntentResolutionStatus.Planned, result.Status);
    var arguments = Assert.IsType<SubgradeTemplateToolArguments>(result.Plan?.Arguments);
    var operation = Assert.Single(arguments.ComponentOperations);
    Assert.Equal("modifyComponent", operation.Operation);
    Assert.Equal("Both", operation.SideScope);
    Assert.Equal("TravelLane", operation.ComponentType);
    Assert.Equal(0.5, operation.Patch.WidthDelta);
}

[Fact]
public void Resolve_modify_without_side_scope_for_component_operation_asks_followup()
{
    var extraction = CreateExtraction(
        "subgrade_template.modify",
        target: new ModelExtractionTarget("handle", "ABC", null, 1.0),
        parameters: new Dictionary<string, ModelExtractionParameter>
        {
            ["componentType"] = new("行车道", 0.9, "user"),
            ["widthDelta"] = new(0.5, 0.9, "user")
        });

    var result = Resolve(extraction);

    Assert.Equal(IntentResolutionStatus.FollowUpRequired, result.Status);
    Assert.Contains("左侧、右侧，还是两侧", result.FollowUpMessage, StringComparison.Ordinal);
}

[Fact]
public void Resolve_modify_builds_add_sidewalk_outside_travel_lane_operation()
{
    var extraction = CreateExtraction(
        "subgrade_template.modify",
        target: new ModelExtractionTarget("handle", "ABC", null, 1.0),
        parameters: new Dictionary<string, ModelExtractionParameter>
        {
            ["componentOperation"] = new("新增", 0.9, "user"),
            ["sideScope"] = new("两侧", 0.9, "user"),
            ["componentType"] = new("人行道", 0.9, "user"),
            ["anchorComponentType"] = new("行车道", 0.9, "user"),
            ["positionMode"] = new("外侧", 0.9, "user"),
            ["width"] = new(3.0, 0.9, "user")
        });

    var result = Resolve(extraction);

    var arguments = Assert.IsType<SubgradeTemplateToolArguments>(result.Plan?.Arguments);
    var operation = Assert.Single(arguments.ComponentOperations);
    Assert.Equal("addComponent", operation.Operation);
    Assert.Equal("Sidewalk", operation.ComponentType);
    Assert.Equal("OutsideOf", operation.PositionMode);
    Assert.Equal("TravelLane", operation.AnchorType);
    Assert.Equal(3.0, operation.Patch.Width);
}
```

- [ ] **Step 2: Run tests and verify failure**

Run:

```powershell
dotnet test RoadProtoAgentBackend.sln --filter "builds_width_delta|without_side_scope|builds_add_sidewalk" --no-restore
```

Expected: tests fail because `ComponentOperations` is empty or fields do not parse.

- [ ] **Step 3: Extend modify YAML optional parameters**

Modify `subgrade_template.modify.yaml` optional parameters by adding:

```yaml
  - name: targetRef
    type: string
    required: false
    description: 用户对目标模板的自然语言引用，例如这个模板、刚才创建的、上一次修改的。
    defaultSource: user
  - name: sideScope
    type: enum
    required: false
    description: 修改作用侧，支持 Left、Right、Both，用户可说左侧、右侧、两侧。
    defaultSource: user
  - name: componentOperation
    type: enum
    required: false
    description: 部件操作，支持 modifyComponent、addComponent、deleteComponent、replaceComponentType，也接受新增、删除、修改、替换等自然语言。
    defaultSource: user
  - name: componentType
    type: enum
    required: false
    description: 被修改或新增的部件类型。
    defaultSource: user
  - name: anchorComponentType
    type: enum
    required: false
    description: 新增部件时的锚点部件类型，例如行车道外侧。
    defaultSource: user
  - name: positionMode
    type: enum
    required: false
    description: 新增部件相对位置，支持 InsideOf、OutsideOf、Before、After、AtEnd。
    defaultSource: user
  - name: width
    type: number
    required: false
    description: 部件宽度绝对值。
    defaultSource: user
  - name: widthDelta
    type: number
    required: false
    description: 部件宽度增量。
    defaultSource: user
  - name: fixedSlope
    type: number
    required: false
    description: 固定横坡覆盖值。
    defaultSource: user
  - name: colorR
    type: number
    required: false
    description: RGB 红色通道。
    defaultSource: user
  - name: colorG
    type: number
    required: false
    description: RGB 绿色通道。
    defaultSource: user
  - name: colorB
    type: number
    required: false
    description: RGB 蓝色通道。
    defaultSource: user
```

- [ ] **Step 4: Build component operations in IntentRuleService**

Add helper:

```csharp
private IReadOnlyList<SubgradeTemplateComponentOperationArguments> BuildComponentOperations(ModelExtractionResult extraction)
{
    var rawComponentType = GetString(extraction, "componentType");
    var rawSideScope = GetString(extraction, "sideScope");
    var rawAnchorType = GetString(extraction, "anchorComponentType");
    var componentType = _normalization.NormalizeComponentType(rawComponentType).CanonicalValue;
    var sideScope = _normalization.NormalizeSideScope(rawSideScope).CanonicalValue;
    var anchorType = _normalization.NormalizeComponentType(rawAnchorType).CanonicalValue;
    var operation = NormalizeComponentOperation(GetString(extraction, "componentOperation"), componentType, GetDouble(extraction, "widthDelta"));
    var positionMode = NormalizePositionMode(GetString(extraction, "positionMode"));

    if (operation is null && componentType is null)
    {
        return Array.Empty<SubgradeTemplateComponentOperationArguments>();
    }

    return new[]
    {
        new SubgradeTemplateComponentOperationArguments(
            operation ?? "modifyComponent",
            sideScope,
            componentType,
            GetString(extraction, "occurrence") ?? "all",
            positionMode,
            anchorType,
            new SubgradeTemplateComponentPatchArguments(
                Type: operation == "addComponent" ? componentType : null,
                Width: GetDouble(extraction, "width"),
                WidthDelta: GetDouble(extraction, "widthDelta"),
                FixedSlope: GetDouble(extraction, "fixedSlope"),
                SlopeMode: GetString(extraction, "slopeMode"),
                ColorR: GetInt(extraction, "colorR"),
                ColorG: GetInt(extraction, "colorG"),
                ColorB: GetInt(extraction, "colorB"),
                HasInnerCurb: GetBool(extraction, "hasInnerCurb"),
                InnerCurbWidth: GetDouble(extraction, "innerCurbWidth"),
                InnerCurbHeight: GetDouble(extraction, "innerCurbHeight"),
                InnerCurbEmbedDepth: GetDouble(extraction, "innerCurbEmbedDepth"),
                HasOuterCurb: GetBool(extraction, "hasOuterCurb"),
                OuterCurbWidth: GetDouble(extraction, "outerCurbWidth"),
                OuterCurbHeight: GetDouble(extraction, "outerCurbHeight"),
                OuterCurbEmbedDepth: GetDouble(extraction, "outerCurbEmbedDepth")))
    };
}
```

Add `NormalizeComponentOperation`:

```csharp
private static string? NormalizeComponentOperation(string? raw, string? componentType, double? widthDelta)
{
    if (!string.IsNullOrWhiteSpace(raw))
    {
        return raw.Trim() switch
        {
            "addComponent" or "新增" or "增加" or "加一个" => "addComponent",
            "deleteComponent" or "删除" or "删掉" => "deleteComponent",
            "replaceComponentType" or "替换" or "换成" => "replaceComponentType",
            "modifyComponent" or "修改" or "调整" or "加宽" or "减窄" => "modifyComponent",
            _ => "modifyComponent"
        };
    }

    return componentType is not null || widthDelta.HasValue ? "modifyComponent" : null;
}
```

Add `NormalizePositionMode`:

```csharp
private static string? NormalizePositionMode(string? raw)
{
    return raw?.Trim() switch
    {
        "InsideOf" or "内侧" => "InsideOf",
        "OutsideOf" or "外侧" => "OutsideOf",
        "Before" or "前" or "之前" => "Before",
        "After" or "后" or "之后" => "After",
        "AtEnd" or "末端" or "最外侧" => "AtEnd",
        _ => null
    };
}
```

Add `GetInt` and `GetBool` helpers using `JsonElement`, numeric and string cases.

- [ ] **Step 5: Add side-scope follow-up before planned result**

Before `Planned`, if modify operation contains component operation and `sideScope` is missing:

```csharp
if (intent.Id == "subgrade_template.modify"
    && BuildComponentOperations(extraction).Count > 0
    && string.IsNullOrWhiteSpace(BuildComponentOperations(extraction)[0].SideScope))
{
    return IntentResolutionResult.FollowUp("请问修改左侧、右侧，还是两侧部件？", extraction);
}
```

Avoid calling `BuildComponentOperations` twice by storing the result in a local variable during final implementation.

- [ ] **Step 6: Run component operation tests**

Run:

```powershell
dotnet test RoadProtoAgentBackend.sln --filter "IntentRuleServiceTests|ModelPromptBuilderTests" --no-restore
```

Expected: tests pass.

---

### Task 5: WPF DTO、请求文件和可见日志分组

**Files:**
- Modify: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-optimization\src\ui\wpf\RoadProto.Terrain.UI\Agent\Models\AgentDtos.cs`
- Modify: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-optimization\src\ui\wpf\RoadProto.Terrain.UI\Agent\Bridge\AgentLocalToolBridge.cs`
- Modify: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-optimization\src\ui\wpf\RoadProto.Terrain.UI\Agent\AgentConsoleViewModel.cs`
- Modify: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-optimization\src\ui\wpf\RoadProto.Terrain.UI\Agent\AgentLogFormatter.cs`
- Test: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-optimization\tests\RoadProtoManagedBridgeTests\Program.cs`

- [ ] **Step 1: Add failing managed bridge tests**

Append test functions to `Program.cs`:

```csharp
static void AgentDtosExposeComponentOperations()
{
    var argsType = typeof(RoadProto.Terrain.UI.Agent.Models.SubgradeTemplateCreateArgumentsDto);
    Check(argsType.GetProperty("TargetMode") != null, "agent arguments should expose target mode");
    Check(argsType.GetProperty("TargetRef") != null, "agent arguments should expose target ref");
    Check(argsType.GetProperty("ComponentOperations") != null, "agent arguments should expose component operations");
}

static void AgentVisibleMessagesUseMarkersAndBlankLines()
{
    var viewModel = new RoadProto.Terrain.UI.Agent.AgentConsoleViewModel();
    viewModel.Messages.Add("我: 创建路基模板");
    viewModel.Messages.Add("Agent: 请问道路等级是什么？");
    var text = viewModel.MessagesText;
    Check(text.Contains("--- 我:", StringComparison.Ordinal), "chat messages should prefix user lines with marker");
    Check(text.Contains("--- Agent:", StringComparison.Ordinal), "chat messages should prefix agent lines with marker");
}
```

Call both functions near the bottom of `Program.cs`.

- [ ] **Step 2: Run managed bridge tests and verify failure**

Run:

```powershell
dotnet run --project tests\RoadProtoManagedBridgeTests\RoadProtoManagedBridgeTests.csproj -c Debug
```

Expected: new tests fail because DTO fields and message formatting are missing.

- [ ] **Step 3: Add DTO classes and DataMembers**

In `AgentDtos.cs`, extend `SubgradeTemplateCreateArgumentsDto`:

```csharp
[DataMember(Name = "TargetMode")]
public string? TargetMode { get; set; }

[DataMember(Name = "TargetRef")]
public string? TargetRef { get; set; }

[DataMember(Name = "ComponentOperations")]
public List<SubgradeTemplateComponentOperationArgumentDto> ComponentOperations { get; set; } = new();
```

Add DTO classes:

```csharp
[DataContract]
public sealed class SubgradeTemplateComponentOperationArgumentDto
{
    [DataMember(Name = "Operation")]
    public string Operation { get; set; } = string.Empty;

    [DataMember(Name = "SideScope")]
    public string? SideScope { get; set; }

    [DataMember(Name = "ComponentType")]
    public string? ComponentType { get; set; }

    [DataMember(Name = "Occurrence")]
    public string? Occurrence { get; set; }

    [DataMember(Name = "PositionMode")]
    public string? PositionMode { get; set; }

    [DataMember(Name = "AnchorType")]
    public string? AnchorType { get; set; }

    [DataMember(Name = "Patch")]
    public SubgradeTemplateComponentPatchArgumentDto Patch { get; set; } = new();
}

[DataContract]
public sealed class SubgradeTemplateComponentPatchArgumentDto
{
    [DataMember(Name = "Type")]
    public string? Type { get; set; }

    [DataMember(Name = "Width")]
    public double? Width { get; set; }

    [DataMember(Name = "WidthDelta")]
    public double? WidthDelta { get; set; }

    [DataMember(Name = "FixedSlope")]
    public double? FixedSlope { get; set; }

    [DataMember(Name = "SlopeMode")]
    public string? SlopeMode { get; set; }

    [DataMember(Name = "ColorR")]
    public int? ColorR { get; set; }

    [DataMember(Name = "ColorG")]
    public int? ColorG { get; set; }

    [DataMember(Name = "ColorB")]
    public int? ColorB { get; set; }
}
```

- [ ] **Step 4: Serialize component operations into request file**

In `AgentLocalToolBridge.WriteSubgradeToolRequest`, add:

```csharp
Write("targetMode", arguments.TargetMode ?? string.Empty),
Write("targetRef", arguments.TargetRef ?? string.Empty),
Write("componentOperationCount", arguments.ComponentOperations.Count.ToString(CultureInfo.InvariantCulture)),
```

After the initial line list is built, append operation rows:

```csharp
for (var index = 0; index < arguments.ComponentOperations.Count; index++)
{
    var operation = arguments.ComponentOperations[index];
    var prefix = $"componentOperation.{index}";
    lines.Add(Write($"{prefix}.operation", operation.Operation));
    lines.Add(Write($"{prefix}.sideScope", operation.SideScope ?? string.Empty));
    lines.Add(Write($"{prefix}.componentType", operation.ComponentType ?? string.Empty));
    lines.Add(Write($"{prefix}.occurrence", operation.Occurrence ?? string.Empty));
    lines.Add(Write($"{prefix}.positionMode", operation.PositionMode ?? string.Empty));
    lines.Add(Write($"{prefix}.anchorType", operation.AnchorType ?? string.Empty));
    lines.Add(Write($"{prefix}.patch.type", operation.Patch?.Type ?? string.Empty));
    lines.Add(Write($"{prefix}.patch.width", Format(operation.Patch?.Width)));
    lines.Add(Write($"{prefix}.patch.widthDelta", Format(operation.Patch?.WidthDelta)));
    lines.Add(Write($"{prefix}.patch.fixedSlope", Format(operation.Patch?.FixedSlope)));
    lines.Add(Write($"{prefix}.patch.slopeMode", operation.Patch?.SlopeMode ?? string.Empty));
    lines.Add(Write($"{prefix}.patch.colorR", Format(operation.Patch?.ColorR)));
    lines.Add(Write($"{prefix}.patch.colorG", Format(operation.Patch?.ColorG)));
    lines.Add(Write($"{prefix}.patch.colorB", Format(operation.Patch?.ColorB)));
}
```

Add `Format(int? value)` overload:

```csharp
private static string Format(int? value)
    => value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
```

- [ ] **Step 5: Format chat messages with markers and blank lines**

In `AgentConsoleViewModel`, add helper:

```csharp
private int _userTurnCount;

private void AddChatMessage(string speaker, string message, bool startsUserTurn = false)
{
    if (startsUserTurn && _userTurnCount > 0)
    {
        Messages.Add(string.Empty);
        Messages.Add(string.Empty);
        LogLines.Add(string.Empty);
        LogLines.Add(string.Empty);
    }

    if (startsUserTurn)
    {
        _userTurnCount++;
    }

    Messages.Add($"--- {speaker}: {message}");
}
```

Replace `Messages.Add("我: " + message)` with:

```csharp
AddChatMessage("我", message, startsUserTurn: true);
```

Replace `Messages.Add("Agent: " + ...)` forms with:

```csharp
AddChatMessage("Agent", ...);
```

Update `MessagesText` to keep blank lines:

```csharp
public string MessagesText => string.Join(Environment.NewLine, Messages);
```

- [ ] **Step 6: Add Normalized event display**

In `AgentLogFormatter.Format`, add:

```csharp
"Normalized" => FormatDiagnosticStage("归一化", normalizedMessage, "后端未返回归一化详情。"),
```

- [ ] **Step 7: Run WPF tests**

Run:

```powershell
dotnet run --project tests\RoadProtoManagedBridgeTests\RoadProtoManagedBridgeTests.csproj -c Debug
```

Expected: all managed bridge tests pass.

---

### Task 6: C++ 领域部件操作应用

**Files:**
- Modify: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-optimization\src\domain\cross_section\SubgradeTemplateModel.h`
- Modify: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-optimization\src\domain\cross_section\SubgradeTemplateModel.cpp`
- Modify: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-optimization\tests\core_tests.cpp`

- [ ] **Step 1: Add failing core tests for component operations**

Append to `core_tests.cpp` near existing subgrade template tests:

```cpp
void testSubgradeTemplateComponentWidthDeltaOperation()
{
    using namespace roadproto::domain::cross_section;
    auto data = SubgradeTemplateDefaults::create(RoadGrade::SecondClass);

    SubgradeComponentOperation operation;
    operation.operation = SubgradeComponentOperationType::ModifyComponent;
    operation.sideScope = SubgradeSideScope::Both;
    operation.componentType = SubgradeComponentType::TravelLane;
    operation.widthDelta = 0.5;

    std::wstring error;
    const auto changed = SubgradeTemplateRules::applyComponentOperation(data, operation, error);

    CHECK(changed == 2);
    CHECK(error.empty());
    const auto left = subgradeComponentsForSide(data, SubgradeSide::Left);
    const auto right = subgradeComponentsForSide(data, SubgradeSide::Right);
    CHECK(std::fabs(left[0].width - 4.25) < 1.0e-9);
    CHECK(std::fabs(right[0].width - 4.25) < 1.0e-9);
}

void testSubgradeTemplateAddSidewalkOutsideTravelLaneOperation()
{
    using namespace roadproto::domain::cross_section;
    auto data = SubgradeTemplateDefaults::create(RoadGrade::SecondClass);

    SubgradeComponentOperation operation;
    operation.operation = SubgradeComponentOperationType::AddComponent;
    operation.sideScope = SubgradeSideScope::Both;
    operation.componentType = SubgradeComponentType::Sidewalk;
    operation.anchorType = SubgradeComponentType::TravelLane;
    operation.positionMode = SubgradeComponentPositionMode::OutsideOf;
    operation.width = 3.0;

    std::wstring error;
    const auto changed = SubgradeTemplateRules::applyComponentOperation(data, operation, error);

    CHECK(changed == 2);
    CHECK(error.empty());
    const auto left = subgradeComponentsForSide(data, SubgradeSide::Left);
    CHECK(left.size() == 4);
    CHECK(left[1].type == SubgradeComponentType::Sidewalk);
    CHECK(std::fabs(left[1].width - 3.0) < 1.0e-9);
}
```

Call both tests in `main`.

- [ ] **Step 2: Run core tests and verify failure**

Run:

```powershell
artifacts\x64\Release\RoadProtoCoreTests.exe
```

Expected: compile or test failure because component operation types do not exist.

- [ ] **Step 3: Add domain operation structs**

In `SubgradeTemplateModel.h`, add:

```cpp
enum class SubgradeSideScope {
    Left,
    Right,
    Both
};

enum class SubgradeComponentOperationType {
    ModifyComponent,
    AddComponent,
    DeleteComponent,
    ReplaceComponentType
};

enum class SubgradeComponentPositionMode {
    InsideOf,
    OutsideOf,
    Before,
    After,
    AtEnd
};

struct SubgradeComponentOperation {
    SubgradeComponentOperationType operation = SubgradeComponentOperationType::ModifyComponent;
    SubgradeSideScope sideScope = SubgradeSideScope::Both;
    SubgradeComponentType componentType = SubgradeComponentType::TravelLane;
    SubgradeComponentType anchorType = SubgradeComponentType::TravelLane;
    SubgradeComponentPositionMode positionMode = SubgradeComponentPositionMode::OutsideOf;
    std::optional<double> width;
    std::optional<double> widthDelta;
    std::optional<double> fixedSlope;
    std::optional<SubgradeComponentType> replacementType;
};
```

Add declaration:

```cpp
static std::size_t applyComponentOperation(
    SubgradeTemplateData& data,
    const SubgradeComponentOperation& operation,
    std::wstring& errorMessage);
```

Ensure `<optional>` is included.

- [ ] **Step 4: Implement pure component operation logic**

In `SubgradeTemplateModel.cpp`, implement:

```cpp
std::size_t SubgradeTemplateRules::applyComponentOperation(
    SubgradeTemplateData& data,
    const SubgradeComponentOperation& operation,
    std::wstring& errorMessage)
{
    errorMessage.clear();
    std::size_t changed = 0;

    auto sideMatches = [&](SubgradeSide side) {
        return operation.sideScope == SubgradeSideScope::Both
            || (operation.sideScope == SubgradeSideScope::Left && side == SubgradeSide::Left)
            || (operation.sideScope == SubgradeSideScope::Right && side == SubgradeSide::Right);
    };

    if (operation.operation == SubgradeComponentOperationType::ModifyComponent) {
        for (auto& component : data.components) {
            if (!sideMatches(component.side) || component.type != operation.componentType) {
                continue;
            }
            if (operation.width.has_value()) {
                component.width = *operation.width;
            }
            if (operation.widthDelta.has_value()) {
                component.width += *operation.widthDelta;
            }
            if (operation.fixedSlope.has_value()) {
                component.fixedSlope = *operation.fixedSlope;
                component.slopeMode = SubgradeSlopeMode::Fixed;
            }
            ++changed;
        }
    } else if (operation.operation == SubgradeComponentOperationType::AddComponent) {
        std::vector<SubgradeTemplateComponent> updated;
        updated.reserve(data.components.size() + 2);
        for (const auto& component : data.components) {
            updated.push_back(component);
            if (!sideMatches(component.side) || component.type != operation.anchorType) {
                continue;
            }
            if (operation.positionMode == SubgradeComponentPositionMode::OutsideOf
                || operation.positionMode == SubgradeComponentPositionMode::After) {
                SubgradeTemplateComponent added;
                added.side = component.side;
                added.type = operation.componentType;
                added.width = operation.width.value_or(0.0);
                added.height = 0.0;
                added.fixedSlope = SubgradeTemplateDefaults::defaultSlopeFor(added.side, added.type);
                added.slopeMode = SubgradeSlopeMode::Fixed;
                added.color = SubgradeTemplateDefaults::defaultColorFor(added.side, added.type);
                updated.push_back(added);
                ++changed;
            }
        }
        data.components = std::move(updated);
    } else if (operation.operation == SubgradeComponentOperationType::DeleteComponent) {
        const auto before = data.components.size();
        data.components.erase(
            std::remove_if(
                data.components.begin(),
                data.components.end(),
                [&](const auto& component) {
                    return sideMatches(component.side) && component.type == operation.componentType;
                }),
            data.components.end());
        changed = before - data.components.size();
    } else if (operation.operation == SubgradeComponentOperationType::ReplaceComponentType) {
        if (!operation.replacementType.has_value()) {
            errorMessage = L"Replacement component type is missing.";
            return 0;
        }
        for (auto& component : data.components) {
            if (!sideMatches(component.side) || component.type != operation.componentType) {
                continue;
            }
            component.type = *operation.replacementType;
            component.color = SubgradeTemplateDefaults::defaultColorFor(component.side, component.type);
            ++changed;
        }
    }

    if (changed == 0) {
        errorMessage = L"No matching subgrade component was found.";
        return 0;
    }

    if (!validate(data, errorMessage)) {
        return 0;
    }

    return changed;
}
```

Add `<algorithm>` if missing.

- [ ] **Step 5: Build and run core tests**

Run:

```powershell
& "D:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe" RoadProto.sln /p:Configuration=Release /p:Platform=x64
artifacts\x64\Release\RoadProtoCoreTests.exe
```

Expected: solution builds and core tests pass.

---

### Task 7: C++ Agent Tool 实体查找、点选、修改与删除

**Files:**
- Modify: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-optimization\src\cad_adapter\objectarx\agent\ObjectArxAgentSubgradeTemplateToolCommand.cpp`
- Test: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-optimization\tests\RoadProtoManagedBridgeTests\Program.cs`

- [ ] **Step 1: Add managed source checks for native tool behavior**

Append to `Program.cs`:

```csharp
static void AgentNativeSubgradeToolNoLongerReturnsPlaceholder()
{
    var source = File.ReadAllText(
        Path.Combine(RepoRoot, "src", "cad_adapter", "objectarx", "agent", "ObjectArxAgentSubgradeTemplateToolCommand.cpp"));
    Check(!source.Contains("实体读写将在下一步接入", StringComparison.Ordinal), "native subgrade agent tool should not return placeholder message");
    Check(source.Contains("promptSubgradeTemplateEntity", StringComparison.Ordinal), "native subgrade agent tool should support picking template entity");
    Check(source.Contains("applyComponentOperation", StringComparison.Ordinal), "native subgrade agent tool should apply component operations");
    Check(source.Contains("erase()", StringComparison.Ordinal), "native subgrade agent tool should erase target entity for delete");
}
```

Call this function near the existing Agent bridge checks.

- [ ] **Step 2: Run managed bridge tests and verify failure**

Run:

```powershell
dotnet run --project tests\RoadProtoManagedBridgeTests\RoadProtoManagedBridgeTests.csproj -c Debug
```

Expected: source check fails because native command still returns placeholder.

- [ ] **Step 3: Add request parsing helpers**

In `ObjectArxAgentSubgradeTemplateToolCommand.cpp`, add includes inside non-test block:

```cpp
#include "app/startup/ApplicationContext.h"
#include "cad_adapter/common/IEditor.h"
#include "cad_adapter/objectarx/cross_section/DnSubgradeTemplateEntity.h"
#include "domain/cross_section/SubgradeTemplateModel.h"
#include "acedads.h"
#include "dbapserv.h"
#include "dbsymtb.h"
```

Add parser helpers:

```cpp
std::optional<double> optionalDouble(const std::map<std::string, std::string>& values, const std::string& key)
{
    const auto value = getValue(values, key);
    if (value.empty()) {
        return std::nullopt;
    }
    char* end = nullptr;
    const auto parsed = std::strtod(value.c_str(), &end);
    return end != value.c_str() ? std::optional<double>(parsed) : std::nullopt;
}

roadproto::domain::cross_section::SubgradeSideScope parseSideScope(const std::string& value)
{
    using roadproto::domain::cross_section::SubgradeSideScope;
    if (value == "Left") return SubgradeSideScope::Left;
    if (value == "Right") return SubgradeSideScope::Right;
    return SubgradeSideScope::Both;
}
```

Add parse functions for `SubgradeComponentType`, `SubgradeComponentOperationType`, and `SubgradeComponentPositionMode` using existing enum values.

- [ ] **Step 4: Add target resolving helpers**

Add:

```cpp
bool promptSubgradeTemplateEntity(AcDbObjectId& entityId)
{
    ads_name selection;
    ads_point point;
    if (acedEntSel(L"\n请选择路基模板实体: ", selection, point) != RTNORM) {
        return false;
    }
    return acdbGetObjectId(entityId, selection) == Acad::eOk && !entityId.isNull();
}

bool isSubgradeTemplateEntity(AcDbObjectId entityId)
{
    DnSubgradeTemplateEntity* entity = nullptr;
    if (acdbOpenObject(entity, entityId, AcDb::kForRead) != Acad::eOk || entity == nullptr) {
        return false;
    }
    const auto ok = entity->isKindOf(DnSubgradeTemplateEntity::desc());
    entity->close();
    return ok;
}
```

Add name search helper that iterates model space and compares `entity->templateData().properties.name` to `targetName`.

- [ ] **Step 5: Implement modify and delete execution**

Replace placeholder result branch with:

```cpp
AcDbObjectId entityId;
if (!targetHandle.empty()) {
    if (!resolveObjectIdFromHandle(utf8ToWide(targetHandle), entityId)) {
        writeResult(resultPath, false, targetHandle, templateName, "未找到目标路基模板 handle。");
        return;
    }
} else if (!targetName.empty()) {
    if (!findSubgradeTemplateByName(utf8ToWide(targetName), entityId)) {
        writeResult(resultPath, false, targetHandle, templateName, "未找到指定名称的路基模板。");
        return;
    }
} else if (getValue(values, "targetMode") == "PickOnExecute") {
    if (!promptSubgradeTemplateEntity(entityId)) {
        writeResult(resultPath, false, targetHandle, templateName, "用户取消点选路基模板。");
        return;
    }
} else {
    writeResult(resultPath, false, targetHandle, templateName, "缺少目标路基模板。");
    return;
}

if (!isSubgradeTemplateEntity(entityId)) {
    writeResult(resultPath, false, targetHandle, templateName, "目标对象不是路基模板实体。");
    return;
}

DnSubgradeTemplateEntity* entity = nullptr;
if (acdbOpenObject(entity, entityId, AcDb::kForWrite) != Acad::eOk || entity == nullptr) {
    writeResult(resultPath, false, targetHandle, templateName, "无法打开路基模板实体。");
    return;
}

const auto handle = wideToUtf8(entityHandleText(entity));
auto data = entity->templateData();

if (operation == "delete") {
    const auto status = entity->erase();
    entity->close();
    writeResult(resultPath, status == Acad::eOk, handle, wideToUtf8(data.properties.name), status == Acad::eOk ? "路基模板实体已删除。" : "删除路基模板实体失败。");
    return;
}

if (operation == "modify") {
    auto componentOperations = parseComponentOperations(values);
    std::size_t changed = 0;
    std::wstring error;
    for (const auto& componentOperation : componentOperations) {
        changed += roadproto::domain::cross_section::SubgradeTemplateRules::applyComponentOperation(data, componentOperation, error);
        if (!error.empty()) {
            entity->close();
            writeResult(resultPath, false, handle, wideToUtf8(data.properties.name), wideToUtf8(error));
            return;
        }
    }
    entity->setTemplateData(data);
    entity->close();
    acedUpdateDisplay();
    writeResult(resultPath, true, handle, wideToUtf8(data.properties.name), "路基模板已修改，变更部件数: " + std::to_string(changed));
    return;
}
```

Add `utf8ToWide`, `entityHandleText`, `findSubgradeTemplateByName`, and `parseComponentOperations` helpers in the same file.

- [ ] **Step 6: Run managed bridge source checks and build**

Run:

```powershell
dotnet run --project tests\RoadProtoManagedBridgeTests\RoadProtoManagedBridgeTests.csproj -c Debug
& "D:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe" RoadProto.sln /p:Configuration=Release /p:Platform=x64
```

Expected: managed bridge tests pass and solution builds.

---

### Task 8: 后端、WPF、本地工具联调测试和文档同步

**Files:**
- Modify: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-optimization\docs\agent\skills\subgrade_template_skill.md`
- Modify: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-optimization\docs\agent\intents\subgrade_template_modify.md`
- Modify: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-optimization\docs\agent\intents\subgrade_template_delete.md`
- Modify: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-optimization\docs\business\agent\路基模板Skill_增删改查_MVP.md`
- Modify: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-optimization\docs\business\agent\Agent控制台_MVP.md`
- Modify: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-optimization\docs\agent_builder\skill_intent_tool_authoring.md`
- Modify: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-optimization\docs\agent_builder\roadproto_practice_log.md`
- Modify: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-optimization\docs\reuse\capability_catalog.md`
- Modify: `F:\0_GPT_道路设计原型功能项目\.worktrees\codex-agent-optimization\docs\dev\version_log.md`

- [ ] **Step 1: Update docs with concrete behavior**

Add the following rules to the relevant docs:

```text
归一化规则：
- 用户原始表达必须先经过规则层归一化，再进入 Tool 参数。
- 可见日志必须输出 Raw 与 Canonical，例如 RoadGradeRaw=二级路; RoadGrade=SecondClass。
- 归一化适用于道路等级、侧别、部件类型、目标引用、操作动词、相对位置和颜色。

修改目标规则：
- 修改和删除必须先定位目标模板。
- “这个模板”在确认执行后进入 AutoCAD 点选。
- “刚才创建的模板”使用会话 lastCreatedSubgradeTemplateHandle。
- “上一次修改的模板”使用会话 lastModifiedSubgradeTemplateHandle。

部件操作规则：
- 修改、删除、新增部件必须明确 Left、Right 或 Both。
- 缺少侧别时追问，不默认两侧。
- 删除模板实体走 subgrade_template.delete；删除模板内部部件走 subgrade_template.modify + deleteComponent。
```

- [ ] **Step 2: Run backend full tests**

Run:

```powershell
dotnet test RoadProtoAgentBackend.sln
```

Expected: all backend tests pass.

- [ ] **Step 3: Run RoadProto managed tests**

Run from worktree:

```powershell
dotnet run --project tests\RoadProtoManagedBridgeTests\RoadProtoManagedBridgeTests.csproj -c Debug
```

Expected: all managed bridge tests pass.

- [ ] **Step 4: Build RoadProto WPF**

Run:

```powershell
dotnet build src\ui\wpf\RoadProto.Terrain.UI\RoadProto.Terrain.UI.csproj -c Release
```

Expected: build succeeds with 0 errors.

- [ ] **Step 5: Build RoadProto solution and run core tests**

Run:

```powershell
& "D:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe" RoadProto.sln /p:Configuration=Release /p:Platform=x64
artifacts\x64\Release\RoadProtoCoreTests.exe
```

Expected: solution builds and all core tests pass.

- [ ] **Step 6: Publish and restart backend**

Run:

```powershell
$publishDir = 'F:\0_GPT_RoadProtoAgentBackend\artifacts\publish'
$exe = Join-Path $publishDir 'RoadProtoAgentBackend.exe'
$existing = Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -eq $exe }
foreach ($p in $existing) { Stop-Process -Id $p.ProcessId -Force }
dotnet publish src\RoadProtoAgentBackend.Api\RoadProtoAgentBackend.Api.csproj -c Release -o $publishDir
$started = Start-Process -FilePath $exe -WorkingDirectory $publishDir -WindowStyle Hidden -PassThru
Start-Sleep -Seconds 2
Invoke-RestMethod -Uri 'http://127.0.0.1:17861/health' -TimeoutSec 10
```

Expected: health response has `status: healthy`.

- [ ] **Step 7: Manual AutoCAD checks after user approval**

Only run these when the user approves operating AutoCAD:

```text
1. 打开 Agent 控制台。
2. 输入：创建二级路路基模板。
3. 确认创建，记录返回 handle。
4. 输入：把刚才创建的模板两侧行车道加宽 0.5 米。
5. 确认修改，检查图面和日志。
6. 输入：在两侧行车道外侧新增 3 米人行道。
7. 确认修改，检查部件列表。
8. 输入：删除这个模板。
9. 确认后点选模板，检查实体删除。
```

Expected: AutoCAD 不崩溃，目标定位、追问、确认、修改和删除行为符合设计。

- [ ] **Step 8: Final clean checks**

Run:

```powershell
git diff --check
```

Expected: no whitespace errors. Windows CRLF warnings are acceptable if they match current repo behavior.

---

## Self-Review

- Spec coverage: 本计划覆盖道路等级与其他字段归一化、目标选择、最近对象上下文、部件级修改/新增/删除、整模板删除、日志 `---` 与空两行显示、本地 Adapter 执行和文档同步。
- Placeholder scan: 本计划没有未定义步骤；每个代码任务给出目标文件、测试命令和期望结果。
- Type consistency: 后端 Tool 参数统一使用 `SubgradeTemplateToolArguments`、`SubgradeTemplateComponentOperationArguments` 和 `SubgradeTemplateComponentPatchArguments`；WPF DTO 使用同名后缀 `Dto`；C++ 领域操作使用 `SubgradeComponentOperation`。
