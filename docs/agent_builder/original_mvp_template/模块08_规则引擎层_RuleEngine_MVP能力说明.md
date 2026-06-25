# 模块 08：规则引擎层 / Rule Engine MVP 能力说明

## 1. 当前模块

当前讨论模块：

```text
模块 08：规则引擎层 / Rule Engine
```

规则引擎层负责执行工程 Agent 中的确定性业务规则。

MVP 中，规则引擎是保证 Agent 可控性的核心模块之一。

它负责：

```text
默认值补全；
参数标准化后的业务判断；
参数推导；
业务校验；
风险判断；
审批判断；
自动修复；
阻断错误；
规则命中解释；
规则结果写入 Trace。
```

一句话：

```text
LLM 负责理解用户表达，规则引擎负责工程判断。
```

---

## 2. 本模块在 MVP 总体流程中的位置

规则引擎位于 Schema 结构化控制层之后、工具计划和 DryRun 之前。

整体流程如下：

```text
用户输入
↓
LLM 识别意图、提取参数
↓
Schema Validator 校验结构
↓
Param Normalizer 标准化字段
↓
Draft Object Builder 生成业务草稿
↓
Rule Engine 补默认值、推导、校验、风险判断
↓
Business Validator 判断是否可继续
↓
Risk Classifier 输出风险等级
↓
Approval Policy 判断是否需要审批
↓
Tool Planner 生成工具计划
↓
DryRun 预演
↓
用户确认
↓
正式执行
```

规则引擎主要覆盖这些节点：

```text
RulesApplied
BusinessValidated
RiskClassified
PolicyChecked
AutoFixed
Blocked
NeedConfirm
```

---

## 3. 模块定位

规则引擎的定位是：

```text
把工程经验、软件默认、项目配置、客户要求、规范约束，转化为可执行、可测试、可解释的程序规则。
```

它解决的问题是：

```text
1. 默认值不能靠模型猜；
2. 强业务规则不能写死在 Prompt；
3. 参数推导不能由 LLM 自由发挥；
4. 业务校验需要稳定、可测试；
5. 用户输入与规则冲突时需要明确处理；
6. 风险等级需要有依据；
7. 是否审批需要有规则；
8. 规则命中结果需要能展示、能追踪。
```

规则引擎不是：

```text
1. 大模型；
2. Prompt；
3. Skill；
4. RAG 知识库；
5. 工具执行器；
6. 软件 Adapter；
7. 前端页面。
```

---

## 4. MVP 规则分类

MVP 中规则不必一开始覆盖全部成熟场景，但至少要分清类型。

建议包含：

```text
1. 默认值规则 DefaultRule
2. 标准化规则 NormalizeRule
3. 推导规则 DerivationRule
4. 业务校验规则 ValidationRule
5. 自动修复规则 AutoFixRule
6. 风险判断规则 RiskRule
7. 审批规则 ApprovalRule
8. 工具选择规则 ToolRule
```

其中 MVP 最核心的是：

```text
默认值规则；
推导规则；
业务校验规则；
风险判断规则；
审批规则。
```

---

## 5. 默认值规则

### 5.1 作用

默认值规则负责补全用户没有输入，但可以根据项目配置、客户配置、企业规则、软件默认合理补全的字段。

示例：

```text
用户没有输入边坡形式；
系统根据项目配置补默认边坡。
```

### 5.2 默认值来源优先级

MVP 建议固定优先级：

```text
用户明确输入 > 项目配置 > 客户配置 > 企业规则 > 规范依据 > 软件默认 > 模型建议
```

注意：

```text
模型建议只能作为低优先级参考，不能直接作为强制默认值。
```

### 5.3 示例规则

```yaml
rule_id: R_DEFAULT_001
rule_name: 边坡形式默认值
rule_type: default
scope: subgrade_template
condition:
  field: slope_type
  operator: is_null
action:
  set_field: slope_type
  value_from: project_config.default_slope_type
source: project_config
priority: 100
level: auto_fixed
explain: 用户未输入边坡形式，系统按项目默认边坡补全。
version: 1.0.0
```

---

## 6. 推导规则

### 6.1 作用

推导规则负责根据已有字段计算或推导其他字段。

示例：

```text
根据道路等级和设计速度推导默认路幅组成；
根据车道数和车道宽度推导行车道总宽；
根据断面类型推导可用模板类型。
```

### 6.2 推导原则

```text
推导必须有明确规则来源；
推导结果必须标记为 rule_derived；
推导结果必须可解释；
推导结果不能覆盖用户明确输入；
推导结果可进入用户确认。
```

### 6.3 示例规则

```yaml
rule_id: R_DERIVE_001
rule_name: 行车道总宽推导
rule_type: derivation
scope: subgrade_template
condition:
  all:
    - lane_count is not null
    - lane_width is not null
action:
  set_field: carriageway_width
  expression: lane_count * lane_width
source: engineering_rule
priority: 90
level: auto_fixed
explain: 根据车道数和车道宽度推导行车道总宽。
version: 1.0.0
```

---

## 7. 业务校验规则

### 7.1 作用

业务校验规则负责判断参数是否合法、合理、完整。

校验类型包括：

```text
必填校验；
数值范围校验；
枚举合法性校验；
单位校验；
参数组合校验；
项目配置校验；
软件状态校验；
对象状态校验。
```

### 7.2 校验结果分级

```text
blocking_error：阻断错误，不能继续；
warning：警告，可以继续但必须提示；
info：提示信息；
need_confirm：需要用户确认；
need_manual_check：需要人工判断。
```

### 7.3 示例规则

```yaml
rule_id: R_VALIDATE_001
rule_name: 路基宽度必须大于 0
rule_type: validation
scope: subgrade_template
condition:
  field: subgrade_width
  operator: less_or_equal
  value: 0
action:
  result: blocking_error
source: system_rule
priority: 100
explain: 路基宽度必须大于 0，当前参数不合法。
version: 1.0.0
```

---

## 8. 风险判断规则

### 8.1 作用

风险判断规则负责判断当前任务的风险等级。

风险等级建议：

```text
low：只读、问答、解释、查询；
medium：生成草稿、预览、局部修改；
high：批量修改、覆盖、保存、删除、不可完全回滚；
blocked：禁止执行。
```

### 8.2 示例

```yaml
rule_id: R_RISK_001
rule_name: 修改工程对象需要中风险标记
rule_type: risk
scope: execution
condition:
  action_type: modify_engineering_object
action:
  risk_level: medium
  requires_approval: true
source: safety_policy
priority: 100
explain: 当前操作将修改工程对象，执行前需要用户确认。
version: 1.0.0
```

---

## 9. 审批规则

### 9.1 作用

审批规则负责判断是否需要用户确认。

MVP 中建议：

```text
只读问答：不需要审批；
参数提取：不需要审批；
规则补默认值：前端提示，可不单独审批；
生成草稿：需要确认或弱确认；
修改工程对象：必须审批；
保存工程文件：必须审批；
覆盖文件：必须强审批；
删除对象：MVP 不建议支持。
```

### 9.2 示例规则

```yaml
rule_id: R_APPROVAL_001
rule_name: 工程对象修改必须审批
rule_type: approval
scope: execution
condition:
  any:
    - action_type == create_engineering_object
    - action_type == modify_engineering_object
action:
  requires_approval: true
  approval_level: normal
source: approval_policy
priority: 100
explain: 当前操作会创建或修改工程对象，必须用户确认后执行。
version: 1.0.0
```

---

## 10. 自动修复规则

### 10.1 作用

自动修复规则负责对低风险、确定性问题做自动修正。

示例：

```text
单位缺省时补单位；
枚举别名标准化；
桩号格式标准化；
空格、大小写、中文同义词统一。
```

### 10.2 原则

```text
自动修复只能处理确定性问题；
自动修复必须记录 before / after；
自动修复不能覆盖用户明确输入的业务意图；
自动修复结果必须进入 RuleHitSchema。
```

---

## 11. 工具选择规则

### 11.1 作用

工具选择规则负责根据任务和参数判断应生成什么工具调用计划。

示例：

```text
创建路基模板 → preview_subgrade_template → apply_subgrade_template
查询项目信息 → get_current_project_info
只问软件操作方法 → 不调用写入工具
```

注意：

```text
MVP 中可以先用简单映射；
后续再升级为复杂 Tool Planner。
```

---

## 12. 规则结构

MVP 中每条规则建议至少包含：

```text
rule_id；
rule_name；
rule_type；
scope；
condition；
action；
priority；
source；
version；
status；
explain；
test_cases。
```

示例：

```yaml
rule_id: R001
rule_name: 设计速度默认值
rule_type: default
scope: subgrade_template
condition:
  all:
    - road_grade == 高速公路
    - design_speed is null
action:
  set_field: design_speed
  value_from: project_config.default_design_speed
priority: 100
source: project_config
version: 1.0.0
status: enabled
explain: 设计速度未输入，按项目默认设计速度补全。
```

---

## 13. 规则执行顺序

MVP 建议按以下顺序执行：

```text
1. 标准化规则
2. 默认值规则
3. 推导规则
4. 自动修复规则
5. 业务校验规则
6. 风险判断规则
7. 审批规则
8. 工具选择规则
```

原因：

```text
先统一格式；
再补缺省值；
再进行推导；
再修复简单问题；
再做业务校验；
再判断风险和审批；
最后生成工具计划。
```

---

## 14. 规则冲突处理

规则冲突时，建议优先级为：

```text
用户明确输入 > 项目配置 > 客户配置 > 企业规则 > 规范依据 > 软件默认 > 模型建议
```

处理原则：

```text
用户输入违反强制规则时，不直接覆盖，而是阻断或提示冲突；
规则之间冲突时，按优先级和作用范围处理；
来源不清的规则不能作为强制执行依据；
模型建议不能覆盖程序规则。
```

示例：

```text
用户输入路基宽度为 -1；
系统不能自动改成默认宽度；
应阻断并提示用户修改。
```

---

## 15. 规则结果输出

规则执行后，必须输出 RuleHitSchema。

示例：

```json
{
  "rule_id": "R_DEFAULT_001",
  "rule_name": "边坡形式默认值",
  "rule_type": "default",
  "hit": true,
  "level": "auto_fixed",
  "affected_fields": ["slope_type"],
  "before_value": null,
  "after_value": "project_default_slope",
  "source": "project_config",
  "explain": "用户未输入边坡形式，系统按项目默认边坡补全。"
}
```

规则结果必须用于：

```text
前端展示；
DryRun 展示；
审批说明；
结果解释；
Trace 回放；
评测样例。
```

---

## 16. 与 LLM、Schema、Tool 的关系

### 16.1 与 LLM 的关系

```text
LLM 提取用户明确表达；
Rule Engine 补默认值、推导和校验；
LLM 不直接补默认值；
LLM 不直接推导强业务规则。
```

### 16.2 与 Schema 的关系

```text
规则基于 Schema 字段执行；
规则不能处理 Schema 中不存在的字段；
规则输出必须符合 RuleHitSchema；
规则修改字段必须回写 DraftObjectSchema。
```

### 16.3 与 Tool 的关系

```text
规则判断工具是否可选；
Tool Registry 判断工具是否注册；
ToolInputValidator 校验工具入参；
规则不能直接绕过 Tool Registry 调工具。
```

---

## 17. MVP 不做范围

规则引擎 MVP 暂不做：

```text
复杂可视化规则编辑器；
复杂 DSL；
在线规则发布平台；
多客户规则继承；
规则灰度发布；
复杂冲突自动求解；
规则自动学习；
模型自动生成并上线规则。
```

但需要预留：

```text
规则 ID；
规则版本；
规则来源；
规则状态；
规则优先级；
规则测试样例；
规则命中 Trace；
规则负责人；
规则生效范围。
```

---

## 18. MVP 验收标准

本模块完成后，应满足：

```text
1. 默认值由规则引擎补，不由 LLM 直接补；
2. 强业务推导由规则引擎执行，不由 LLM 自由推导；
3. 规则基于 Schema 字段运行；
4. 每条规则有 rule_id 和版本；
5. 每条规则有明确来源；
6. 规则命中结果能输出 RuleHitSchema；
7. 规则修改字段能记录 before / after；
8. 规则能输出 blocking_error、warning、info、auto_fixed、need_confirm；
9. 业务校验不通过时能阻断后续执行；
10. 风险规则能判断是否需要审批；
11. 审批规则能决定是否进入 WaitingApproval；
12. 规则结果能在前端展示；
13. 规则结果能进入 DryRun；
14. 规则结果能进入 Trace；
15. 规则可以通过测试样例验证。
```

---

## 19. 本模块结论

规则引擎层是可控工程 Agent 的确定性判断层。

它的核心作用是：

```text
把工程规则显式化；
把默认值来源显式化；
把参数推导显式化；
把校验结果显式化；
把风险和审批依据显式化。
```

正确设计是：

```text
LLM 负责理解；
Schema 负责结构；
Rule Engine 负责判断；
Tool Registry 负责工具；
Orchestrator 负责流程；
用户负责关键确认。
```

一句话：

```text
规则引擎是防止工程 Agent 变成“模型自由发挥”的关键闸门。
```
