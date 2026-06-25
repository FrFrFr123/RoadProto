# 模块 07：Schema 结构化控制层 MVP 能力说明

## 1. 当前模块

当前讨论模块：

```text
模块 07：Schema 结构化控制层 / Structured Control
```

Schema 结构化控制层负责定义和校验 Agent 运行过程中所有关键数据对象。

MVP 中，Schema 是连接 LLM、规则引擎、工具调用、DryRun、前端展示和 Trace 的核心数据契约。

一句话：

```text
Schema 负责把模型输出和工程执行之间隔离开。
```

LLM 可以理解用户语言，但必须输出到 Schema；  
规则引擎可以补默认值、做推导，但必须基于 Schema；  
工具可以执行工程动作，但工具入参必须来自 Schema；  
前端可以展示参数和结果，但展示字段也应来自 Schema。

---

## 2. 本模块在 MVP 总体流程中的位置

Schema 结构化控制层位于 LLM 能力层之后、规则引擎层之前。

整体流程如下：

```text
用户输入
↓
Context Manager 生成上下文
↓
LLM 识别意图、提取参数
↓
Schema Validator 校验模型输出
↓
Param Normalizer 标准化字段、单位、枚举
↓
Draft Object Builder 生成业务草稿对象
↓
Rule Engine 基于 Schema 补默认值、推导、校验
↓
Tool Planner 基于 Schema 生成工具计划
↓
DryRun 基于 Schema 预演
↓
Approval 基于 Schema 展示确认内容
↓
Execution 基于 Schema 传入工具参数
↓
Trace 记录 Schema 数据变化
```

本模块主要覆盖：

```text
Schema 定义；
Schema 校验；
字段来源标记；
字段置信度；
字段标准化；
Draft Object 构造；
参数版本管理；
Schema 变更兼容；
Schema 结果写入 Trace。
```

---

## 3. 模块定位

Schema 结构化控制层的定位是：

```text
让 Agent 的所有关键输入、输出、参数、规则结果、工具计划和执行结果都有统一结构。
```

它解决的问题是：

```text
1. 防止模型自由输出不可控文本；
2. 防止参数字段混乱；
3. 防止单位、枚举、格式不一致；
4. 防止工具收到非法入参；
5. 防止规则不知道处理哪些字段；
6. 防止前端无法解释参数来源；
7. 防止 Trace 无法回放；
8. 防止后续新增 Agent 时数据结构不可复用。
```

Schema 结构化控制层不是：

```text
1. 大模型；
2. 业务规则引擎；
3. 工具执行器；
4. 软件 Adapter；
5. 前端页面；
6. 数据库表设计的简单替代品。
```

---

## 4. MVP 必须定义的 Schema 类型

MVP 不需要一次定义全部成熟版 Schema，但必须覆盖主链路。

至少需要：

```text
1. InputSchema
2. IntentSchema
3. ParamSchema
4. DraftObjectSchema
5. ValidationSchema
6. RuleHitSchema
7. RiskSchema
8. ToolPlanSchema
9. DryRunSchema
10. ApprovalSchema
11. ExecutionSchema
12. TraceSchema
```

这些 Schema 不需要一开始非常复杂，但必须有基本结构。

---

## 5. 各类 Schema 的作用

### 5.1 InputSchema

描述前端提交的原始任务输入。

包含：

```text
user_input；
main_agent_id；
project_id；
module_id；
selected_object_ids；
session_id；
user_selected_model；
user_selected_api_channel。
```

作用：

```text
保证前端提交给后端的数据结构统一。
```

---

### 5.2 IntentSchema

描述 LLM 识别出的用户意图。

包含：

```text
intent；
confidence；
candidate_agents；
need_clarification；
clarification_question。
```

作用：

```text
让 AgentRouter 可以基于结构化意图进行业务 Agent 路由。
```

---

### 5.3 ParamSchema

描述业务 Agent 所需参数。

例如路基模板创建 Agent 的 ParamSchema 包含：

```text
road_grade；
design_speed；
subgrade_width；
lane_count；
slope_type；
shoulder_width；
median_width；
template_name。
```

每个字段至少包含：

```text
value；
type；
unit；
required；
source；
confidence；
editable；
validation_status。
```

作用：

```text
约束 LLM 参数提取结果；
约束规则引擎处理字段；
约束前端参数面板展示。
```

---

### 5.4 DraftObjectSchema

描述业务草稿对象。

例如：

```text
路基模板草稿；
横断面戴帽草稿；
结构层统计草稿；
平纵审核草稿。
```

作用：

```text
在正式执行前形成可预览、可修改、可审批的中间对象。
```

注意：

```text
MVP 中应先生成草稿对象，再 DryRun，再审批，再正式执行。
```

---

### 5.5 ValidationSchema

描述 Schema 校验和业务校验结果。

包含：

```text
field；
validation_type；
status；
message；
blocking；
suggestion。
```

作用：

```text
统一表达字段是否合法、是否阻断、是否需要用户修改。
```

---

### 5.6 RuleHitSchema

描述规则命中结果。

包含：

```text
rule_id；
rule_name；
rule_type；
hit；
result；
affected_fields；
before_value；
after_value；
explain；
level。
```

作用：

```text
让规则补默认值、推导、校验结果可展示、可追踪。
```

---

### 5.7 RiskSchema

描述风险判断结果。

包含：

```text
risk_level；
risk_reasons；
affected_objects；
requires_approval；
rollback_supported。
```

作用：

```text
让审批策略和前端风险提示有统一依据。
```

---

### 5.8 ToolPlanSchema

描述工具调用计划。

包含：

```text
tool_name；
tool_version；
input_params；
expected_output；
risk_level；
requires_approval；
supports_dry_run；
execution_order。
```

作用：

```text
让工具调用不依赖模型自由文本，而是依赖结构化计划。
```

---

### 5.9 DryRunSchema

描述预演结果。

包含：

```text
actions；
affected_objects；
params_used；
rule_hits；
warnings；
risk_level；
preview_result；
rollback_supported；
need_approval。
```

作用：

```text
让用户在执行前看到清晰的影响范围。
```

---

### 5.10 ApprovalSchema

描述用户审批记录。

包含：

```text
run_id；
approval_action；
approved_params；
modified_params；
approval_time；
approver；
comment。
```

作用：

```text
保证用户确认行为可追踪。
```

---

### 5.11 ExecutionSchema

描述正式执行结果。

包含：

```text
execution_status；
executed_tools；
created_objects；
modified_objects；
deleted_objects；
saved_files；
errors；
rollback_available；
result_summary。
```

作用：

```text
保证正式执行结果可以被系统校验和解释。
```

---

### 5.12 TraceSchema

描述运行链路日志结构。

包含：

```text
run_id；
workflow_state；
agent_version；
model_version；
schema_version；
rule_version；
tool_version；
input；
output；
error；
timestamp。
```

作用：

```text
保证问题可回放、可定位、可评测。
```

---

## 6. 字段来源标记

MVP 中，每个关键字段都必须记录来源。

来源类型包括：

```text
user_input：用户明确输入；
llm_extracted：LLM 提取；
system_context：系统上下文；
project_config：项目配置；
default_rule：默认值规则；
rule_derived：规则推导；
manual_confirmed：用户确认；
tool_result：工具返回；
imported_data：外部导入。
```

示例：

```json
{
  "design_speed": {
    "value": 80,
    "unit": "km/h",
    "source": "user_input",
    "confidence": 0.96,
    "editable": true
  },
  "slope_type": {
    "value": "project_default_slope",
    "source": "default_rule",
    "confidence": 1.0,
    "editable": true
  }
}
```

关键原则：

```text
用户输入的字段不能被模型覆盖；
规则补的字段必须标明 default_rule；
规则推导的字段必须标明 rule_derived；
用户确认后的字段必须标明 manual_confirmed。
```

---

## 7. 字段标准化

Schema 层需要配合 Normalizer 做字段标准化。

MVP 至少支持：

```text
单位标准化；
枚举标准化；
布尔值标准化；
桩号格式标准化；
数值类型标准化；
字段命名标准化。
```

示例：

```text
80公里/小时 → 80 km/h
24.5米 → 24.5 m
高速 → 高速公路
K1+200 → 1200.0
是 / 对 / true → true
```

注意：

```text
标准化不等于业务推导；
标准化只是把同一含义的数据变成统一格式。
```

---

## 8. Schema 与 LLM 的关系

LLM 输出必须落到 Schema。

流程：

```text
LLM 输出 JSON
↓
Schema Validator 校验
↓
校验通过，进入规则引擎
↓
校验失败，返回错误或进入追问
```

原则：

```text
LLM 不能输出自由格式参数；
LLM 不能绕过 Schema；
LLM 不能自己声明 Schema 通过；
LLM 输出不通过 Schema 时不能继续执行。
```

---

## 9. Schema 与规则引擎的关系

规则引擎基于 Schema 运行。

```text
规则读取 Schema 字段；
规则补默认值；
规则推导字段；
规则校验字段组合；
规则输出 RuleHitSchema；
规则结果回写 DraftObjectSchema。
```

原则：

```text
规则不能处理不存在于 Schema 的字段；
规则输出必须进入 RuleHitSchema；
规则修改字段必须保留 before / after；
规则结果必须可展示、可追踪。
```

---

## 10. Schema 与工具调用的关系

工具输入必须来自 Schema。

```text
ToolPlanSchema 生成工具计划；
ToolInputValidator 校验工具入参；
工具执行后返回 ExecutionSchema；
执行结果回写 TraceSchema。
```

原则：

```text
工具不能接收自由文本参数；
工具不能接收未校验参数；
工具不能执行 Schema 中不存在的动作；
工具返回结果必须结构化。
```

---

## 11. Schema 与前端展示的关系

前端参数面板、规则面板、DryRun 面板都应基于 Schema 渲染。

前端展示：

```text
字段名；
字段值；
单位；
来源；
是否必填；
是否可改；
校验状态；
规则命中；
风险等级；
审批状态。
```

这样用户能看清楚：

```text
哪些是他说的；
哪些是模型提取的；
哪些是规则补的；
哪些是系统推导的；
哪些需要确认。
```

---

## 12. MVP 不做范围

Schema MVP 暂不做：

```text
复杂图形化 Schema 编辑器；
在线 Schema 发布平台；
复杂版本兼容迁移；
跨客户 Schema 继承；
自动生成全部前端表单；
复杂对象关系建模；
完整 DSL 规则绑定。
```

但需要预留：

```text
schema_id；
schema_version；
field_version；
deprecated 标记；
兼容性说明；
字段来源；
字段可覆盖策略；
字段测试样例。
```

---

## 13. MVP 验收标准

本模块完成后，应满足：

```text
1. 每个业务 Agent 有自己的 ParamSchema；
2. LLM 输出必须经过 Schema Validator；
3. Schema 不通过不能进入规则引擎；
4. 工具入参必须来自 Schema；
5. 每个关键字段有来源标记；
6. 每个关键字段有单位、类型、是否必填；
7. 默认值字段能标记为 default_rule；
8. 推导字段能标记为 rule_derived；
9. 用户确认字段能标记为 manual_confirmed；
10. 前端能基于 Schema 展示参数；
11. DryRun 能基于 Schema 展示影响范围；
12. Execution 结果能结构化返回；
13. Trace 能记录 Schema 版本和字段变化；
14. 后续新增业务 Agent 时，可以新增 Schema 而不改主框架。
```

---

## 14. 本模块结论

Schema 结构化控制层是可控工程 Agent 的数据契约层。

它的核心作用是：

```text
把模型理解结果变成系统可校验的数据；
把规则结果变成可解释的数据；
把工具调用变成可控的数据；
把执行结果变成可追踪的数据。
```

正确设计是：

```text
LLM 输出必须落 Schema；
规则处理必须基于 Schema；
工具输入必须来自 Schema；
前端展示必须读取 Schema；
Trace 回放必须记录 Schema。
```

一句话：

```text
Schema 是 LLM 和工程软件之间的安全隔离层。
```
