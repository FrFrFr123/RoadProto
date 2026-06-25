# Agent 意图规则可执行化与大模型接入设计

## 1. 背景

RoadProto 当前已经具备可控工程 Agent 的基础闭环：

- WPF 可停靠 Agent Console。
- RoadProto 本地 `AGENT` 薄模块。
- 独立 Agent 后端仓库 `F:\0_GPT_RoadProtoAgentBackend`。
- 后端健康检查、自动启动、模型 Provider 配置、DPAPI 密钥保存、Trace 和日志。
- `SubgradeTemplate.Create` 本地工具确认后写入 `DnSubgradeTemplateEntity` 的最小流转。

当前不足是：后端 `SubgradeTemplateCreateAgent` 仍然用硬编码参数生成计划，模型网关只具备 OpenAI-compatible 请求构造能力，没有真正把 `docs/agent/skills/` 和 `docs/agent/intents/` 下的规则文档变成可执行规则，也没有让大模型在已注册 Agent / Skill 范围内参与路基模板增、删、改、查的意图识别和参数抽取。

本设计的目标是把路基模板 Skill 和其意图族落成可执行 MVP，让“创建、修改、删除、查询路基模板”都从自然语言开始，经 Agent 路由、Skill 识别、Intent 识别、大模型抽参、Schema 校验、规则校验、追问或确认，最终仍由 RoadProto 本地工具执行。

## 2. 范围

本轮覆盖路基模板 Skill 和完整基础意图族：

```text
Skill: subgrade_template

subgrade_template.create
subgrade_template.modify
subgrade_template.delete
subgrade_template.query
```

本轮要实现：

- 在 RoadProto 文档中建立 `subgrade_template` Skill 文档，明确共享上下文、共享规则、Tool 白名单和 Trace 阶段。
- 在后端仓库建立机器可读 Skill 规则文件。
- 在后端仓库建立四个机器可读意图规则文件。
- 建立规则加载、版本标识和基础校验。
- 接入当前启用的模型 Provider，完成真实 LLM 调用。
- 让 LLM 只在候选 Agent / Skill 范围内输出结构化 Intent 识别、目标对象识别、参数抽取、追问建议和解释结果。
- 由后端规则层完成 Agent / Skill / Intent 裁决、默认值补全、参数校验、追问判断和确认计划生成。
- 创建和修改类动作继续沿用或扩展现有 WPF 确认和 RoadProto 本地 Tool 执行链路。
- 删除类动作必须先展示风险确认，再由 RoadProto 本地 Adapter 执行。
- 查询类动作只读执行，不要求用户审批，但必须记录 Trace。
- 增加后端单元测试，覆盖四个意图的规则加载、模型输出解析、默认值补全、追问、确认计划和只读查询。

本轮不做：

- 不新增 Web 前端。
- 不让后端直接写 DWG。
- 不让 LLM 直接调用工具。
- 不实现戴帽、横断面出图、土石方计算、路面结构模板等非路基模板基础意图。
- 不实现完整路基模板所有部件参数的 WPF 编辑重构。
- 不把 RoadProto 主仓库里的 Markdown 当作后端运行时唯一数据源。

## 3. 推荐方案

采用“人读 Markdown + 机器读 YAML”的双轨方案，并把规则拆成 Skill 和 Intent 两层。

RoadProto 主仓库继续保存人读规范，先写 Skill，再按意图拆分：

```text
F:\0_GPT_道路设计原型功能项目\docs\agent\skill_system.md
F:\0_GPT_道路设计原型功能项目\docs\agent\skills\subgrade_template_skill.md
F:\0_GPT_道路设计原型功能项目\docs\agent\intents\subgrade_template_create.md
F:\0_GPT_道路设计原型功能项目\docs\agent\intents\subgrade_template_modify.md
F:\0_GPT_道路设计原型功能项目\docs\agent\intents\subgrade_template_delete.md
F:\0_GPT_道路设计原型功能项目\docs\agent\intents\subgrade_template_query.md
```

后端仓库新增机器可读规则，Skill 文件使用 Skill ID，Intent 文件使用意图 ID：

```text
F:\0_GPT_RoadProtoAgentBackend\rules\skills\subgrade_template.skill.yaml
F:\0_GPT_RoadProtoAgentBackend\rules\intents\subgrade_template.create.yaml
F:\0_GPT_RoadProtoAgentBackend\rules\intents\subgrade_template.modify.yaml
F:\0_GPT_RoadProtoAgentBackend\rules\intents\subgrade_template.delete.yaml
F:\0_GPT_RoadProtoAgentBackend\rules\intents\subgrade_template.query.yaml
```

原因：

- RoadProto 主仓库文档用于业务讨论、人工审阅和长期沉淀。
- 后端运行时不应该跨仓库依赖 RoadProto 文档路径，否则发布版后端会难以独立运行。
- YAML 比 Markdown 更适合稳定解析、测试和版本化。
- Skill YAML 用于控制能力边界和 Tool 白名单，Intent YAML 用于控制具体动作。
- 后续多个 Skill / Intent 可以复用同一套 Rule Loader、Schema、Prompt Builder 和 Validator。

## 4. 架构

后端新增四个能力区：

```text
Domain
  IntentRules
    IntentRule
    IntentParameterRule
    IntentFollowUpRule
    IntentToolBinding
    IntentModelExtraction

Application
  IntentRules
    IIntentRuleRepository
    IntentRuleService
  Models
    IModelGateway
    ModelPromptBuilder
    ModelExtractionParser
  Agents
    SubgradeTemplateAgent

Infrastructure
  IntentRules
    FileIntentRuleRepository
  Models
    OpenAiCompatibleModelGateway

Api
  Endpoints
    AgentRunEndpoints
```

RoadProto 主仓库只更新契约和文档，不在本轮新增 CAD 侧核心逻辑。

## 5. 数据流

用户输入后，后端必须按十二层流程流转：

```text
用户 / 前端
-> 01 Agent Console 提交输入
-> 02 配置中心加载 Agent / Skill / Tool 权限
-> 03 Model Gateway 准备模型通道
-> 04 Orchestrator 创建 TraceId / SessionId / TaskId，并选择候选 Agent / Skill
-> 05 Context Manager 按候选 Skill 裁剪上下文
-> 06 LLM 在候选范围内输出 Agent / Skill / Intent / 参数 / 追问建议
-> 07 Schema 校验结构化输出
-> 08 Rule Engine 裁决 Agent / Skill / Intent，补默认值、校验、追问或阻断
-> 09 Tool Registry 校验 Tool 是否在 Skill 白名单内
-> 10 EICAD Adapter 映射到 RoadProto 本地 Adapter
-> 11 DryRun / 审批 / 执行 / 保存 / 回滚
-> 12 Trace / 日志 / 评测样例沉淀
```

如果模型未配置或调用失败，后端返回可展示错误，不回退到静默硬编码计划。后续可以增加离线规则兜底，但本轮先让问题清楚暴露，方便联调。

## 6. 规则文件格式

Skill 规则文件示例：

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
  - AgentRouted
  - SkillRouted
  - IntentRecognized
  - SchemaValidated
  - RulesApplied
  - ToolPlanned
  - Executed
```

四个 Intent 规则文件使用同一结构。创建意图示例：

```yaml
id: subgrade_template.create
skillId: subgrade_template
version: 0.1.0
displayName: 创建路基模板
agentId: roadproto_engineering_agent
businessModule: CROSS_SECTION
humanDoc: docs/agent/intents/subgrade_template_create.md

positiveExamples:
  - 帮我创建高速公路路基模板。
  - 帮我创建路基模板，行车道比默认参数增加 1 米。其他参数按默认配置。

negativeExamples:
  - 修改当前模板的行车道宽度。
  - 删除路基模板。
  - 路基模板怎么设置？
  - 用当前模板进行戴帽。
  - 创建路面结构模板。

requiredParameters:
  roadGrade:
    type: enum
    values:
      - Expressway
      - FirstClass
      - SecondClass
      - ThirdClass
      - FourthClass
      - UrbanExpressway
      - UrbanArterial
      - UrbanSubArterial
      - UrbanBranch
    missing: ask
  confirmation:
    type: approval
    requiredBefore: execute

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
  requiresDryRun: true
  requiresApproval: true
  roadProtoAdapter: RD_SECTION_SUBGRADE_TEMPLATE_APPLY_DIALOG_FILE
```

修改、删除、查询意图沿用同一结构，但 Tool 绑定和风险等级不同：

| 意图 | Tool | 风险 | 审批 | 目标对象要求 |
| --- | --- | --- | --- | --- |
| `subgrade_template.create` | `SubgradeTemplate.Create` | 中 | 是 | 不需要既有模板 |
| `subgrade_template.modify` | `SubgradeTemplate.Modify` | 中 | 是 | 必须能确定既有模板 |
| `subgrade_template.delete` | `SubgradeTemplate.Delete` | 高 | 是 | 必须能确定既有模板 |
| `subgrade_template.query` | `SubgradeTemplate.Query` | 低 | 否 | 可查询全部、当前选中或指定模板 |

本轮 YAML 先覆盖 MVP 所需字段，不一次性把完整 `SubgradeTemplateData` 的所有部件结构塞进去。完整部件数组、路缘石、变宽表、坡度变化表和结构层绑定作为下一阶段扩展。修改意图可以先支持名称、道路等级、显示比例、行车道宽度、硬路肩宽度、土路肩宽度等 MVP 字段；删除和查询必须先支持 handle、名称、当前选择集三种目标定位方式。

## 7. 模型输出格式

模型必须输出 JSON，不输出自然语言正文。推荐结构：

```json
{
  "intentId": "subgrade_template.modify",
  "confidence": 0.86,
  "matchedExpression": "把当前路基模板的行车道加宽 1 米。",
  "target": {
    "mode": "currentSelection",
    "handle": null,
    "name": null,
    "confidence": 0.75
  },
  "parameters": {
    "roadGrade": {
      "value": null,
      "sourceText": "",
      "confidence": 0.0
    },
    "laneWidthDelta": {
      "value": 1.0,
      "unit": "m",
      "sourceText": "行车道比默认参数增加 1 米",
      "confidence": 0.9
    }
  },
  "rejectedIntent": null,
  "notes": []
}
```

后端解析后必须二次检查：

- `agentId` 必须存在并启用。
- `skillId` 必须存在并启用。
- `intentId` 必须存在于规则库。
- `intentId` 必须属于 `skillId` 声明的意图清单。
- `confidence` 低于阈值时不得执行。
- 写入类意图必须能确定目标对象或进入追问。
- `parameters` 只能包含 Skill / Intent 规则允许字段或可识别的派生字段。
- 创建意图缺 `roadGrade` 时进入追问，不用模型自行猜。
- 修改意图缺目标模板时进入追问或要求用户在 CAD 中选择。
- 删除意图必须明确目标模板，不能根据模糊描述直接删除。
- 查询意图允许目标为空，表示查询当前 DWG 中可用路基模板摘要。
- 命中 negative example 或相邻意图时进入拒绝或澄清。
- Tool 必须在 Skill 的 `toolWhitelist` 中。

## 8. 默认值和校验

默认值只由规则层或 RoadProto 既有领域规则产生。

MVP 阶段创建意图先生成面向当前 Tool DTO 的参数：

```text
SubgradeTemplateCreateArguments
  TemplateName
  LaneWidth
  HardShoulderWidth
  EarthShoulderWidth
  SlopeRatio
  Unit
```

创建映射规则：

- `templateName` 缺失时使用 `默认路基模板`。
- `laneWidth` 缺失时使用当前后端 MVP 默认值，后续再与 RoadProto `SubgradeTemplateDefaults` 对齐。
- `laneWidthDelta` 表示在默认 `laneWidth` 基础上增减。
- `hardShoulderWidth`、`earthShoulderWidth` 同理。
- `roadGrade` 必须抽出或追问，但本轮可以先只进入计划摘要和确认项，不强制写入当前 DTO；后续 Tool DTO 扩展时再写入。
- `displayScale` 先校验和展示，当前 Tool DTO 不写入时必须在确认摘要中说明。

修改映射规则：

- 必须先定位既有路基模板，来源可以是当前选择集、用户提供的 handle、用户提供的模板名称或 WPF 上下文。
- 用户只说“当前模板”时，如果当前选择集中没有唯一 `DnSubgradeTemplateEntity`，必须追问或提示点选。
- 用户只修改一个参数时，只覆盖该参数，其余字段保持实体原值。
- 当前 Tool DTO 不足以承载完整修改时，后端仍要在计划中保留结构化修改项，RoadProto 本地 Adapter 负责读取实体原值、应用差异并打开确认。

删除映射规则：

- 必须定位唯一既有路基模板。
- 删除前必须展示模板名称、handle、是否被道路模型引用、风险等级和不可恢复提示。
- 如果检测到模板被道路模型或配置引用，MVP 阶段默认阻断，后续再设计级联解除引用。

查询映射规则：

- 查询全部模板时返回模板列表摘要。
- 查询当前模板时需要当前选择集有唯一模板。
- 查询指定模板时可按 handle 或名称匹配；名称匹配多个时必须返回候选列表让用户确认。
- 查询不写 CAD，不需要审批。

校验失败返回规则错误，不调用 Tool。

## 9. 状态机调整

当前状态已包含确认和 Tool Dispatch。本轮新增或明确以下状态语义：

```text
InputReceived
IntentRecognized
AwaitingUserInput
ParametersValidated
AwaitingUserConfirmation
DispatchingTool
Succeeded
Failed
Cancelled
```

如果暂不改 `AgentRunState` 枚举，也必须在 `AgentRunEvent.Stage` 和 Trace 中记录上述阶段，避免 WPF 只能看到“直接生成计划”。查询类意图可以从 `ParametersValidated` 直接进入只读 Tool Dispatch 或 `Succeeded`，但 Trace 仍要记录 Tool 名称和结果摘要。

## 10. WPF 交互影响

本轮不重做 WPF 布局，只要求现有 Agent Console 能展示：

- 模型调用失败。
- 低置信度或不匹配。
- 追问文本。
- 目标对象候选列表。
- 需要用户在 CAD 中点选目标模板的提示。
- 参数摘要。
- 默认值和用户覆盖项。
- 确认项。
- 删除风险提示。
- 查询结果摘要。
- Trace 阶段。

如果后端返回 `AwaitingUserInput`，WPF 应继续允许用户输入补充文本，并调用同一个 run 的用户输入接口。若当前接口尚未实现，可在本轮先通过再次创建 run 的方式临时联调，但设计上应保留 `POST /v1/runs/{task_id}/user-input`。

## 11. 错误处理

必须区分以下错误：

- `MODEL_PROVIDER_NOT_CONFIGURED`：没有启用模型 Provider。
- `MODEL_CALL_FAILED`：HTTP 调用失败、超时、鉴权失败或模型返回非 2xx。
- `MODEL_OUTPUT_INVALID`：模型返回非 JSON 或字段不合规。
- `INTENT_NOT_SUPPORTED`：没有匹配到支持意图。
- `INTENT_LOW_CONFIDENCE`：置信度不足，需要用户澄清。
- `PARAMETER_MISSING`：必要参数缺失，需要追问。
- `PARAMETER_INVALID`：参数类型、范围或枚举不合法。
- `RULE_BLOCKED`：规则明确阻断。
- `TARGET_NOT_FOUND`：找不到目标路基模板。
- `TARGET_AMBIGUOUS`：目标路基模板不唯一，需要用户确认。
- `REFERENCE_BLOCKED`：目标模板存在引用，删除或修改被阻断。

所有错误都要写入 Trace，不记录明文 API Key。

## 12. 测试策略

后端新增单元测试：

- `FileIntentRuleRepositoryTests`：能加载四个 `subgrade_template.*.yaml`，必填字段完整。
- `ModelPromptBuilderTests`：Prompt 包含正例、反例、输出 JSON 约束和禁止模型补默认值的规则。
- `ModelExtractionParserTests`：能解析合法 JSON，拒绝非 JSON、未知意图、非法参数字段、非法目标对象字段。
- `IntentRuleServiceTests`：创建缺 `roadGrade` 时返回追问；修改缺目标时要求选择；删除目标不唯一时阻断；查询无目标时生成列表查询计划；非法 `displayScale` 阻断。
- `SubgradeTemplateAgentTests`：从模型抽参结果生成创建、修改、删除、查询四类计划。
- `AgentRunServiceTests`：StartRun 经过模型抽参和规则校验后进入追问或确认状态。

模型网关测试继续使用构造请求和伪造 HTTP Handler，不在单元测试中调用真实外网。

RoadProto 主仓库本轮只更新文档，不需要重新构建 ARX。真正实施代码后，再按影响范围运行后端 `dotnet test` 和 RoadProto 相关测试。

## 13. 实施顺序

推荐拆成六步：

1. 在 RoadProto 文档中补齐 Skill 全局规范和路基模板 Skill / Intent 文档。
2. 在后端新增 Skill 规则、四个 Intent 规则和规则加载模型。
3. 抽象 `IModelGateway`，让 `OpenAiCompatibleModelGateway` 能真实发送请求并返回文本。
4. 新增 Prompt Builder 和模型 JSON 解析。
5. 改造 `SubgradeTemplateCreateAgent` 为 `SubgradeTemplateAgent` 或 `SubgradeTemplateSkillHandler`，并改造 `AgentRunService`，把硬编码计划替换为 Skill / Intent 规则驱动计划。
6. 增加测试、更新后端 README 和 RoadProto 契约文档。

每一步都要保持后端测试可运行，避免一次性把模型、规则、状态机和 WPF 都搅在一起。

## 14. 验收标准

验收时应满足：

- 后端仓库存在四个规则文件：`subgrade_template.create.yaml`、`subgrade_template.modify.yaml`、`subgrade_template.delete.yaml`、`subgrade_template.query.yaml`。
- 后端仓库存在 Skill 规则文件：`subgrade_template.skill.yaml`。
- RoadProto 主仓库存在全局 Skill 规范和路基模板 Skill 文档。
- 用户输入“帮我创建高速公路路基模板”时，后端会调用当前启用模型 Provider。
- 模型输出经过 JSON 解析和 Schema 校验。
- Schema 输出包含 `AgentId`、`SkillId` 和 `IntentId`。
- 缺道路等级时返回追问，而不是默认猜高速。
- 明确创建路基模板时进入创建确认状态。
- “修改当前模板的行车道宽度”会路由到 `subgrade_template.modify`，不会误触发创建 Tool。
- “删除这个路基模板”会进入高风险确认，目标不明确时必须追问或要求点选。
- “当前图里有哪些路基模板”会进入查询意图，不需要用户审批。
- 用户确认前不 dispatch 任何写入类 `SubgradeTemplate.*` Tool。
- 任何 `SubgradeTemplate.*` Tool 调用都必须通过 `subgrade_template` Skill 白名单校验。
- 所有阶段在 Trace / flow log 中可见。
- 后端 `dotnet test F:\0_GPT_RoadProtoAgentBackend\RoadProtoAgentBackend.sln` 通过。

## 15. 风险和约束

- 模型输出不稳定，所以所有模型结果必须经过解析、白名单和规则校验。
- 不同 Provider 的 OpenAI-compatible 行为可能有差异，模型网关要保留 Provider 适配点。
- 当前创建 Tool DTO 比完整路基模板参数窄，修改、删除、查询还需要补本地 Tool Adapter；本轮先把意图族、规则、模型抽参和受控执行边界一起设计完整，再按实施计划逐项落地。
- 规则文件和 Markdown 文档可能漂移，后续需要建立“规则文件引用人读文档路径和版本”的检查机制。
- 真实模型调用依赖用户配置 API Key；自动测试不得依赖外网或真实密钥。

## 16. 用户确认点

进入实施计划前，需要确认本设计的几个关键选择：

- 可执行规则放在后端仓库 `F:\0_GPT_RoadProtoAgentBackend\rules\intents\`。
- RoadProto 主仓库保留人读 Markdown 规范。
- 本轮覆盖 `subgrade_template.create`、`subgrade_template.modify`、`subgrade_template.delete`、`subgrade_template.query` 四个意图。
- 模型只做意图识别和参数抽取，默认值、校验、追问、Tool 决策都由后端规则层完成。
- 当前 Tool DTO 和本地 Adapter 按 MVP 字段先落地，完整路基模板部件结构留到下一阶段。
