# 模块 09：工具与原子函数层 / Tool Registry MVP 能力说明

## 1. 当前模块

当前讨论模块：

```text
模块 09：工具与原子函数层 / Tool Registry
```

工具与原子函数层负责把工程软件能力封装成 Agent 可以受控调用的工具。

MVP 中，Agent 不能直接调用 EICAD / CAD / BIM 内部函数，也不能让 LLM 自由生成软件命令，而必须通过 Tool Registry 调用已注册的原子函数。

一句话：

```text
原子函数是软件能力的最小受控单元；
Tool Registry 是所有原子函数的注册、授权、校验和治理中心。
```

---

## 2. 本模块在 MVP 总体流程中的位置

工具与原子函数层位于规则引擎之后、软件 Adapter 和执行控制之前。

整体流程如下：

```text
用户输入
↓
LLM 提取参数
↓
Schema 校验
↓
规则引擎补默认值、推导、校验、风险判断
↓
Tool Planner 生成工具调用计划
↓
Tool Registry 检查工具是否注册
↓
Tool Scope 检查当前 Agent 是否允许调用
↓
ToolInputValidator 校验工具入参
↓
DryRun Executor 预演工具动作
↓
用户审批
↓
Execution Engine 调用原子函数
↓
Software Adapter 执行软件动作
↓
返回结构化结果
```

本模块主要覆盖：

```text
原子函数定义；
工具注册；
工具权限；
工具入参 Schema；
工具输出 Schema；
工具风险等级；
工具 DryRun 支持；
工具回滚能力；
工具版本；
工具调用日志；
工具测试。
```

---

## 3. 模块定位

Tool Registry 的定位是：

```text
让 Agent 只能调用被注册、被授权、被校验、可追踪的软件能力。
```

它解决的问题是：

```text
1. 防止模型直接调用软件内部函数；
2. 防止模型调用不存在的工具；
3. 防止业务 Agent 调用越权工具；
4. 防止非法参数进入软件执行层；
5. 防止高风险工具绕过 DryRun 和审批；
6. 防止工具能力不可追踪、不可测试、不可版本化；
7. 让多个 Agent 可以复用同一套软件能力。
```

Tool Registry 不是：

```text
1. 大模型；
2. 规则引擎；
3. 工作流编排器；
4. 软件 Adapter 本身；
5. 前端页面；
6. 业务规则库。
```

---

## 4. 原子函数的定义

原子函数是工程软件能力的最小受控单元。

设计原则：

```text
小粒度；
单职责；
结构化输入；
结构化输出；
可测试；
可预演；
可回滚；
有错误码；
有超时；
有日志；
有权限；
有版本；
有风险等级。
```

不建议把一个大功能直接暴露为一个粗工具，例如：

```text
auto_design_all_subgrade
```

更建议拆成：

```text
get_current_project_info
get_selected_alignment
get_selected_cross_section
get_current_template
validate_template_params
preview_subgrade_template
create_subgrade_template_draft
apply_subgrade_template
get_execution_result
rollback_last_operation
```

---

## 5. MVP 原子函数契约

每个原子函数至少需要定义：

```text
function_name；
description；
input_schema；
output_schema；
side_effect；
side_effect_scope；
risk_level；
requires_approval；
supports_dry_run；
supports_rollback；
rollback_strategy；
idempotent；
timeout_ms；
error_codes；
owner_module；
adapter_name；
version；
status。
```

示例：

```json
{
  "function_name": "preview_subgrade_template",
  "description": "预览路基模板创建结果，不修改真实工程数据。",
  "input_schema": "PreviewSubgradeTemplateInputSchema",
  "output_schema": "PreviewSubgradeTemplateOutputSchema",
  "side_effect": false,
  "side_effect_scope": "none",
  "risk_level": "low",
  "requires_approval": false,
  "supports_dry_run": true,
  "supports_rollback": false,
  "rollback_strategy": "none",
  "idempotent": true,
  "timeout_ms": 30000,
  "error_codes": [
    "PROJECT_NOT_OPEN",
    "INVALID_TEMPLATE_PARAM",
    "PREVIEW_FAILED"
  ],
  "owner_module": "subgrade",
  "adapter_name": "eicad_adapter",
  "version": "1.0.0",
  "status": "enabled"
}
```

---

## 6. MVP 工具分类

MVP 中工具建议分为四类：

```text
1. 查询类工具 Read Tool
2. 草稿类工具 Draft Tool
3. 预演类工具 DryRun Tool
4. 写入类工具 Write Tool
```

### 6.1 查询类工具

只读取工程信息，不修改数据。

示例：

```text
get_current_project_info；
get_selected_alignment；
get_selected_cross_section；
get_current_template；
get_project_default_params。
```

风险等级：

```text
low
```

审批要求：

```text
不需要审批
```

---

### 6.2 草稿类工具

生成业务草稿对象，不直接修改正式工程数据。

示例：

```text
create_subgrade_template_draft；
build_cross_section_capping_draft。
```

风险等级：

```text
low / medium
```

审批要求：

```text
一般不需要强审批，但需要前端展示草稿结果。
```

---

### 6.3 预演类工具

执行 DryRun，返回影响范围和预览结果。

示例：

```text
preview_subgrade_template；
preview_apply_template_to_sections；
preview_quantity_result。
```

风险等级：

```text
low
```

审批要求：

```text
不需要审批，但 DryRun 结果是正式执行前的必要依据。
```

---

### 6.4 写入类工具

真正创建、修改、保存工程对象。

示例：

```text
apply_subgrade_template；
apply_template_to_selected_sections；
save_project_result；
rollback_last_operation。
```

风险等级：

```text
medium / high
```

审批要求：

```text
必须审批。
```

---

## 7. Tool Registry 的核心职责

MVP 中 Tool Registry 至少要负责：

```text
登记工具；
读取工具契约；
检查工具是否存在；
检查工具是否启用；
检查当前 Agent 是否允许调用；
检查当前用户是否有权限；
检查工具入参是否符合 Schema；
标记工具风险等级；
判断是否必须 DryRun；
判断是否必须审批；
映射到 Software Adapter；
记录工具调用日志；
返回统一错误。
```

---

## 8. 工具调用六道门

MVP 中必须建立工具调用六道门：

```text
1. Tool exists：工具是否注册；
2. Tool enabled：工具是否启用；
3. Tool allowed：当前业务 Agent 是否授权；
4. Input valid：工具入参是否符合 Schema；
5. DryRun passed：是否完成并通过 DryRun；
6. User approved：是否完成必要用户审批。
```

只有全部通过，才能进入正式执行。

---

## 9. 业务 Agent 的工具范围 Tool Scope

每个业务 Agent 都必须配置自己的工具范围。

示例：

```json
{
  "agent_id": "subgrade_template_agent",
  "allowed_tools": [
    "get_current_project_info",
    "get_selected_alignment",
    "get_current_template",
    "validate_template_params",
    "create_subgrade_template_draft",
    "preview_subgrade_template",
    "apply_subgrade_template"
  ],
  "forbidden_tools": [
    "delete_project_file",
    "overwrite_original_design",
    "batch_modify_all_sections"
  ]
}
```

原则：

```text
业务 Agent 只能看到自己被授权的工具；
问答 Agent 不允许调用写入工具；
高风险工具默认不进入 MVP；
工具范围必须进入 Trace。
```

---

## 10. 工具输入输出 Schema

工具输入必须来自 Schema。

示例输入：

```json
{
  "tool_name": "preview_subgrade_template",
  "input": {
    "project_id": "P001",
    "alignment_id": "A001",
    "template_params": {
      "road_grade": "高速公路",
      "design_speed": 80,
      "subgrade_width": 24.5,
      "slope_type": "project_default_slope"
    }
  }
}
```

工具输出必须结构化。

示例输出：

```json
{
  "success": true,
  "affected_objects": [
    {
      "object_type": "cross_section",
      "object_id": "CS_001"
    }
  ],
  "preview_summary": "将生成 1 个路基模板草稿，并预览应用到当前选中断面。",
  "warnings": [],
  "errors": []
}
```

原则：

```text
工具不能接收自由文本；
工具不能返回纯自然语言；
工具输入输出必须可校验；
工具错误必须有错误码。
```

---

## 11. Tool Registry 与 Adapter 的关系

Tool Registry 不直接操作工程软件。

关系是：

```text
Orchestrator
↓
Tool Registry
↓
Software Adapter
↓
EICAD / CAD / BIM / 文件接口
```

Tool Registry 负责：

```text
工具注册；
权限控制；
入参校验；
风险标记；
版本管理；
调用记录。
```

Software Adapter 负责：

```text
真正调用 EICAD / CAD / BIM 能力；
处理软件 API；
处理事务；
处理异常；
处理数据转换；
返回标准结构。
```

---

## 12. MVP 推荐原子函数清单

以“路基模板创建 Agent”为例，MVP 最少需要：

```text
get_current_project_info
get_selected_alignment
get_selected_cross_section
get_project_default_params
validate_template_params
create_subgrade_template_draft
preview_subgrade_template
apply_subgrade_template
get_execution_result
rollback_last_operation
```

其中首批必须落地的是：

```text
get_current_project_info
get_project_default_params
create_subgrade_template_draft
preview_subgrade_template
apply_subgrade_template
```

如果时间有限，MVP 可以先做到：

```text
查询当前工程信息；
读取项目默认配置；
生成模板草稿；
DryRun 预览；
用户确认后应用模板。
```

---

## 13. 工具风险等级

工具必须标注风险等级。

建议分级：

```text
low：只读查询、解释、预览；
medium：生成草稿、局部创建、局部修改；
high：批量修改、保存、覆盖、删除；
blocked：MVP 禁止使用。
```

示例：

```text
get_current_project_info：low
preview_subgrade_template：low
create_subgrade_template_draft：medium
apply_subgrade_template：medium
save_project_result：high
delete_engineering_object：blocked
```

---

## 14. 工具错误码

工具必须返回标准错误码。

示例：

```text
PROJECT_NOT_OPEN：工程未打开；
OBJECT_NOT_SELECTED：未选择对象；
INVALID_INPUT：入参非法；
TOOL_NOT_ALLOWED：工具未授权；
DRYRUN_REQUIRED：必须先 DryRun；
APPROVAL_REQUIRED：必须先审批；
ADAPTER_ERROR：Adapter 调用失败；
EXECUTION_FAILED：执行失败；
ROLLBACK_FAILED：回滚失败。
```

错误输出示例：

```json
{
  "success": false,
  "error_code": "OBJECT_NOT_SELECTED",
  "message": "当前未选择可应用路基模板的断面对象。",
  "suggestion": "请先选择一个断面对象，或切换到允许按路线范围应用的模式。"
}
```

---

## 15. Tool Registry 与 LLM 的关系

LLM 不能直接执行工具。

正确关系：

```text
LLM 可以输出工具调用意图；
Orchestrator 生成工具计划；
Tool Registry 判断工具是否存在、是否授权、入参是否合法；
DryRun 和审批通过后；
Execution Engine 才能调用工具。
```

原则：

```text
LLM 不能调用未注册工具；
LLM 不能绕过 Tool Registry；
LLM 不能自己声明工具执行成功；
LLM 不能把自然语言结果当作工具结果。
```

---

## 16. MVP 不做范围

Tool Registry MVP 暂不做：

```text
复杂工具市场；
在线工具发布平台；
复杂工具灰度；
多版本工具动态路由；
复杂并行工具调用；
模型自由工具规划；
自动生成工具；
自动修复工具实现；
跨软件复杂事务。
```

但需要预留：

```text
工具版本；
工具状态；
工具负责人；
工具测试样例；
工具风险等级；
工具权限；
工具 DryRun 标记；
工具回滚策略；
工具调用 Trace。
```

---

## 17. MVP 验收标准

本模块完成后，应满足：

```text
1. 所有可调用软件能力都必须注册为工具；
2. 每个工具都有明确工具契约；
3. 每个工具有 input_schema 和 output_schema；
4. 每个工具有风险等级；
5. 每个工具标记是否支持 DryRun；
6. 每个工具标记是否需要审批；
7. 当前业务 Agent 只能调用授权工具；
8. LLM 不能直接调用工具；
9. 工具入参必须经过 Schema 校验；
10. 写入类工具必须经过 DryRun；
11. 写入类工具必须经过用户审批；
12. 工具调用结果必须结构化；
13. 工具失败必须返回错误码；
14. 工具调用必须进入 Trace；
15. 新增工具时，不需要修改 Orchestrator 主流程。
```

---

## 18. 本模块结论

工具与原子函数层是工程 Agent 连接软件能力的安全门。

它的核心作用是：

```text
把软件能力拆小；
把每个能力注册；
把每次调用校验；
把风险和权限显式化；
把执行结果结构化；
把调用过程可追踪。
```

正确设计是：

```text
LLM 只表达意图；
Tool Registry 控制工具；
Adapter 执行软件动作；
DryRun 先预演；
用户确认后执行；
Trace 记录全过程。
```

一句话：

```text
没有 Tool Registry，Agent 调软件就是不可控的；
有 Tool Registry，Agent 才能安全地调用工程软件能力。
```
