# Skill / Intent / Tool 编写方法

## 1. 编写顺序

新增一个业务能力时，按以下顺序写：

```text
业务对象
-> Skill
-> Intent
-> ParamSchema
-> Rule
-> Tool
-> Adapter
-> Trace
-> 评测样例
```

不要先写 Prompt，也不要先接工具。

## 2. Skill 文档

Skill 是业务能力边界。一个 Skill 应回答：

- 它负责哪个业务对象。
- 它支持哪些 Intent。
- 它共享哪些参数。
- 它共享哪些默认值和校验规则。
- 它允许哪些 Tool。
- 它的风险等级和审批策略是什么。
- 它如何记录 Trace。

Skill 不应只描述一个动作。例如“路基模板创建”不适合作为 Skill，“路基模板”才是 Skill。

## 3. Intent 文档

Intent 是可独立识别、校验、确认和执行的动作。

每个 Intent 必须单独成文，至少包含：

- 意图 ID。
- 所属 Agent。
- 所属 Skill。
- 用户可能表达。
- 不应匹配表达。
- 意图边界。
- 必要参数。
- 可选参数。
- 默认值规则。
- 校验规则。
- 追问规则。
- 确认前展示。
- Tool 绑定。
- 执行结果。
- 日志与 Trace。

创建、修改、删除、查询必须拆分，不要长期混在一份文档里。

## 4. 参数所有权

参数来源必须可追踪：

| 来源 | 说明 |
| --- | --- |
| `user_explicit` | 用户明确说出 |
| `context_current` | 当前任务或当前对象上下文 |
| `default_rule` | 默认值规则补全 |
| `derived_rule` | 规则推导 |
| `adapter_read` | 软件 Adapter 读取 |
| `user_confirmed` | 用户确认页修改或确认 |

LLM 只负责抽取 `user_explicit` 和解释文本，不负责生成 `default_rule`。

默认值应优先落到机器可读规则文件中，例如：

```yaml
optionalParameters:
  - name: laneWidth
    type: number
    required: false
    defaultSource: agent-rule
    defaultValue: "3.75"
```

当默认值本身是工程对象结构时，不要把它压扁成几个标量。应在规则文件中表达完整结构，例如：

```yaml
defaultComponentsByRoadGrade:
  Expressway:
    - side: Left
      type: TravelLane
      width: 7.5
      fixedSlope: 0.02
      slopeMode: Fixed
      colorR: 204
      colorG: 102
      colorB: 0
      wideningTable: []
      variableSlopeTable: []
```

标量参数可以作为用户明确覆盖项，例如 `laneWidthDelta`，但默认工程结构应由规则层展开成完整 Tool 参数，再交给 Adapter 执行。

宿主软件 Adapter 只负责执行和校验，不应把缺失参数悄悄补成本地默认值。若规则文件没有下发必需默认值，Adapter 应返回可读失败信息，让问题回到规则文件维护。

枚举参数也应在规则层归一化。用户或模型输出可以是中文表达，例如“高速公路”，但进入 Tool 参数时应变成宿主软件稳定编码，例如 `Expressway`。不要让 Adapter 在执行阶段猜测自然语言枚举。

对象边界也必须由 Skill / Intent 文档和入口路由共同约束。一个 Skill 的负向样例必须写清相邻对象，例如 `道路模型` 不是 `路基模板`，`路面结构模板` 不是 `路基模板`。否则模型或关键词路由容易把相近业务对象误吸收到已有 Skill。

## 5. Tool 绑定规则

Intent 不能直接执行软件命令，必须绑定 Tool。

Tool 绑定需要写清：

- Tool 名称。
- Tool Scope。
- 输入 Schema。
- 输出 Schema。
- 风险等级。
- 是否需要 DryRun。
- 是否需要 Approval。
- 是否支持 Rollback。
- Adapter 能力声明。

## 6. Tool 风险等级

推荐等级：

| 等级 | 示例 | 控制 |
| --- | --- | --- |
| `read` | 查询、解释、列出对象 | 可直接执行 |
| `low` | 生成草稿、不写工程数据 | 可提示后执行 |
| `medium` | 创建对象、修改非关键参数 | DryRun + 审批 |
| `high` | 删除、批量修改、不可逆操作 | 强审批 + 影响范围展示 |
| `blocked` | 越权、危险、不支持 | 阻断 |

## 7. Prompt 与规则的关系

Prompt 应只告诉模型如何识别和抽取，不应把业务强规则写成让模型自由判断。

错误做法：

```text
如果用户没说道路等级，你自己选择一个默认等级。
```

正确做法：

```text
如果用户没说道路等级，将 roadGrade 标记为缺失，并给出 followUp 建议。
```

默认等级是否存在、是否可用，由 Rule Engine 决定。

## 8. 评测样例

每个 Intent 至少沉淀：

- 正向表达。
- 负向表达。
- 缺参表达。
- 歧义表达。
- 低置信度表达。
- 多轮补充表达。
- Tool 阻断表达。

失败线上样例必须回填评测集。

## 9. RoadProto 实践示例

当前实践中：

```text
Skill: subgrade_template
Intent:
  subgrade_template.create
  subgrade_template.modify
  subgrade_template.delete
  subgrade_template.query
Tool:
  SubgradeTemplate.Create
  SubgradeTemplate.Modify
  SubgradeTemplate.Delete
  SubgradeTemplate.Query
```

创建时缺少道路等级，会返回：

```text
请问道路等级是什么？
```

这个追问由规则层生成，通过 run 级 `followUpMessage` 返回前端。

当前 RoadProto 进一步沉淀的规则：

- 路基模板创建的模板名称、显示比例和单位写入 `rules/intents/subgrade_template.create.yaml`，由 `defaultValue` 控制。
- 路基模板创建的道路等级默认部件写入 `defaultComponentsByRoadGrade`，后端规则层展开成完整 `Components` 后再调用 Tool。
- RoadProto 本地 Tool Adapter 不补默认宽度、坡度、颜色、路缘石或结构层引用，只做组件级参数存在性和合法性校验。
- `道路模型` 被路由为独立 `road_model` Skill 候选，未接入时提示不支持，不调用路基模板 Tool。
