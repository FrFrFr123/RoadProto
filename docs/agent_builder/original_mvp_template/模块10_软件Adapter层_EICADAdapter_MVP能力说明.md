# 模块 10：软件 Adapter 层 / EICAD Adapter MVP 能力说明

## 1. 当前模块

当前讨论模块：

```text
模块 10：软件 Adapter 层 / Software Adapter
```

MVP 中优先落地：

```text
EICAD Adapter
```

软件 Adapter 层负责把 Tool Registry 中的标准工具调用，转换成具体工程软件可以执行的 API、插件函数、脚本命令或文件操作。

一句话：

```text
Tool Registry 负责“工具是否能调”；
Software Adapter 负责“软件里怎么执行”。
```

Agent、LLM、Orchestrator、Tool Registry 都不能直接调用 EICAD / CAD 内部函数，必须通过 Adapter 隔离。

---

## 2. 本模块在 MVP 总体流程中的位置

整体流程如下：

```text
用户输入
↓
LLM 提取参数
↓
Schema 校验
↓
规则引擎处理
↓
Tool Registry 校验工具
↓
Execution Engine 准备执行
↓
Software Adapter 接收标准工具调用
↓
Adapter 转换为 EICAD / CAD 可执行动作
↓
EICAD / CAD 执行
↓
Adapter 捕获结果、异常和变更
↓
返回标准结构
↓
Execution Engine 汇总结果
↓
Trace 记录
```

本模块主要覆盖：

```text
软件 API 封装；
工具到软件函数的映射；
数据格式转换；
DryRun 实现；
正式执行实现；
结果标准化；
错误码转换；
事务与锁定；
快照与回滚；
软件异常隔离；
Adapter 版本管理。
```

---

## 3. 模块定位

软件 Adapter 的定位是：

```text
隔离 Agent 系统与具体工程软件之间的差异和风险。
```

它解决的问题是：

```text
1. 不让 Agent 直接依赖 EICAD 内部函数；
2. 不让 Tool Registry 关心具体软件 API；
3. 不让不同软件差异污染主框架；
4. 统一软件执行结果；
5. 统一软件错误码；
6. 支持 DryRun、快照、执行、回滚；
7. 后续更换或增加软件时，不重写 Agent 主链路。
```

Adapter 不是：

```text
1. LLM；
2. 规则引擎；
3. Agent 配置中心；
4. Tool Registry；
5. Orchestrator；
6. 前端页面。
```

---

## 4. Adapter 的基本调用关系

标准调用关系：

```text
Orchestrator
↓
Execution Engine
↓
Tool Registry
↓
Software Adapter
↓
EICAD / CAD / BIM API
```

Adapter 只接受来自 Execution Engine / Tool Registry 的标准请求。

不允许：

```text
LLM → Adapter
前端 → Adapter
Agent Prompt → Adapter
未注册工具 → Adapter
```

---

## 5. MVP 中 EICAD Adapter 的职责

MVP 中 EICAD Adapter 至少负责：

```text
1. 接收标准工具请求；
2. 校验当前 EICAD 工程状态；
3. 读取当前项目信息；
4. 读取当前选中对象；
5. 读取项目默认配置；
6. 创建业务草稿；
7. 执行 DryRun 预览；
8. 正式应用路基模板；
9. 返回标准执行结果；
10. 捕获并转换软件异常；
11. 支持基础回滚；
12. 记录 Adapter 调用日志。
```

---

## 6. Adapter 输入输出契约

### 6.1 Adapter 输入

Adapter 输入来自 Tool Registry 校验后的工具调用。

示例：

```json
{
  "run_id": "RUN_001",
  "tool_name": "preview_subgrade_template",
  "tool_version": "1.0.0",
  "adapter_name": "eicad_adapter",
  "operation_mode": "dry_run",
  "project_id": "P001",
  "object_ids": ["CS_001"],
  "input": {
    "template_params": {
      "road_grade": "高速公路",
      "design_speed": 80,
      "subgrade_width": 24.5,
      "slope_type": "project_default_slope"
    }
  }
}
```

### 6.2 Adapter 输出

Adapter 输出必须是标准结构。

示例：

```json
{
  "success": true,
  "adapter_name": "eicad_adapter",
  "tool_name": "preview_subgrade_template",
  "operation_mode": "dry_run",
  "affected_objects": [
    {
      "object_type": "cross_section",
      "object_id": "CS_001",
      "action": "preview"
    }
  ],
  "created_objects": [],
  "modified_objects": [],
  "warnings": [],
  "errors": [],
  "rollback_token": null,
  "result_summary": "已完成路基模板预览，未修改真实工程数据。"
}
```

---

## 7. Adapter 的操作模式

MVP 中 Adapter 至少支持三种模式：

```text
read：只读查询；
dry_run：预演；
execute：正式执行。
```

后续可扩展：

```text
rollback：回滚；
validate：执行后验收；
snapshot：快照；
restore：恢复。
```

### 7.1 read 模式

用于读取当前工程、路线、断面、模板、项目默认配置等信息。

特征：

```text
不修改工程；
不需要用户审批；
风险低；
可频繁调用。
```

### 7.2 dry_run 模式

用于预演动作影响范围。

特征：

```text
不修改真实工程数据；
生成预览结果；
返回影响范围；
返回风险提示；
返回是否支持回滚。
```

### 7.3 execute 模式

用于正式执行软件动作。

特征：

```text
可能创建或修改工程对象；
必须经过 Tool Registry 校验；
必须经过 DryRun；
必须经过用户审批；
执行前应记录快照或 rollback_token；
执行后返回结构化结果。
```

---

## 8. Adapter 与 DryRun 的关系

Adapter 必须支持 DryRun 的底层实现。

DryRun 不是前端假展示，也不是模型编一句说明。

DryRun 应由 Adapter 或工具实现层基于真实工程上下文计算。

DryRun 输出至少包括：

```text
将执行的动作；
将影响的对象；
将创建的对象；
将修改的对象；
使用的参数；
潜在错误；
是否可回滚；
预览摘要。
```

原则：

```text
DryRun 不修改真实工程数据；
DryRun 与正式执行使用同一套输入参数；
DryRun 结果必须可用于用户审批；
DryRun 失败不能进入正式执行。
```

---

## 9. Adapter 与回滚的关系

MVP 中至少要支持基础回滚能力。

回滚策略可以分级：

```text
创建对象：删除新对象；
修改对象：恢复修改前快照；
生成草稿：丢弃草稿；
文件保存：恢复备份文件；
不可完全回滚：执行前明确提示。
```

Adapter 输出中应包含：

```text
rollback_supported；
rollback_token；
rollback_strategy；
rollback_warning。
```

示例：

```json
{
  "rollback_supported": true,
  "rollback_token": "RB_20260616_001",
  "rollback_strategy": "restore_object_snapshot",
  "rollback_warning": ""
}
```

MVP 中可以先做到：

```text
局部对象创建可撤销；
局部对象修改有快照；
保存 / 覆盖文件类动作暂不进入 MVP 或强审批。
```

---

## 10. Adapter 与软件事务

工程软件执行动作时，可能出现部分成功、对象锁定、文件未保存、CAD 异常等问题。

Adapter 需要处理：

```text
工程未打开；
对象未选择；
对象不存在；
对象被锁定；
当前模块不支持；
参数与软件状态不匹配；
软件 API 调用失败；
执行中断；
部分成功；
结果未保存；
回滚失败。
```

MVP 中可先不做完整事务系统，但必须做到：

```text
执行前检查基本状态；
执行前记录必要快照；
执行失败返回明确错误码；
部分成功必须明确标记；
不能把未知状态当作成功。
```

---

## 11. Adapter 错误码

Adapter 应把软件内部异常转换成标准错误码。

建议错误码：

```text
PROJECT_NOT_OPEN：工程未打开；
PROJECT_READONLY：工程只读；
OBJECT_NOT_SELECTED：未选择对象；
OBJECT_NOT_FOUND：对象不存在；
OBJECT_LOCKED：对象被锁定；
MODULE_NOT_ACTIVE：当前模块不支持；
INVALID_ADAPTER_INPUT：Adapter 入参非法；
SOFTWARE_API_ERROR：软件 API 调用失败；
DRYRUN_FAILED：预演失败；
EXECUTION_FAILED：执行失败；
PARTIAL_SUCCESS：部分成功；
ROLLBACK_FAILED：回滚失败；
UNKNOWN_ADAPTER_ERROR：未知错误。
```

错误输出示例：

```json
{
  "success": false,
  "error_code": "OBJECT_NOT_SELECTED",
  "message": "当前未选择可应用路基模板的断面对象。",
  "suggestion": "请先选择断面对象，或切换为按路线范围应用。"
}
```

---

## 12. MVP 推荐 EICAD Adapter 能力清单

以路基模板创建 Agent 为例，MVP 中 EICAD Adapter 建议支持：

```text
get_current_project_info
get_project_default_params
get_selected_alignment
get_selected_cross_section
create_subgrade_template_draft
preview_subgrade_template
apply_subgrade_template
get_execution_result
rollback_last_operation
```

首批最小落地：

```text
get_current_project_info；
get_project_default_params；
create_subgrade_template_draft；
preview_subgrade_template；
apply_subgrade_template。
```

如果要更稳，建议再加：

```text
get_selected_cross_section；
rollback_last_operation。
```

---

## 13. Adapter 版本与能力声明

每个 Adapter 应有能力声明。

示例：

```json
{
  "adapter_id": "eicad_adapter",
  "adapter_name": "EICAD Adapter",
  "software": "EICAD",
  "software_version": "2026",
  "supported_modules": [
    "subgrade",
    "cross_section"
  ],
  "supported_operations": [
    "read",
    "dry_run",
    "execute",
    "rollback"
  ],
  "version": "1.0.0",
  "status": "enabled"
}
```

作用：

```text
让 Tool Registry 知道工具可以映射到哪个 Adapter；
让 Trace 能记录本次使用哪个 Adapter；
让后续多软件适配时不改主框架。
```

---

## 14. Adapter 与其他模块的关系

Adapter 依赖：

```text
Tool Registry；
Execution Engine；
软件 API / SDK / 插件接口；
工程文件状态；
对象查询接口；
数据转换器；
错误码映射。
```

Adapter 服务于：

```text
DryRun Executor；
Execution Engine；
Rollback Manager；
Trace Logger；
Result Validator。
```

Adapter 不能直接：

```text
调用 LLM；
判断业务规则；
决定是否审批；
决定是否允许越权工具；
自由修改工程数据；
绕过 Tool Registry 被前端直接调用。
```

---

## 15. MVP 不做范围

Adapter MVP 暂不做：

```text
完整跨软件适配；
复杂批量事务；
复杂分布式执行；
完整 CAD 对象级版本管理；
复杂图形预览引擎；
完整文件级备份恢复；
自动修复所有软件异常；
跨模块联动执行。
```

但需要预留：

```text
adapter_base；
adapter_version；
capability_manifest；
operation_mode；
rollback_token；
error_code_mapping；
software_version；
supported_modules；
adapter_trace。
```

---

## 16. MVP 验收标准

本模块完成后，应满足：

```text
1. Tool Registry 不能直接操作 EICAD，必须通过 Adapter；
2. 前端不能直接调用 Adapter；
3. LLM 不能直接调用 Adapter；
4. Adapter 能读取当前工程信息；
5. Adapter 能读取项目默认配置；
6. Adapter 能执行 DryRun 且不修改真实工程；
7. Adapter 能正式执行至少一个写入类动作；
8. 写入动作执行前能记录必要快照或回滚标记；
9. Adapter 返回结构化结果；
10. Adapter 返回标准错误码；
11. 执行失败不能被当作成功；
12. 部分成功必须明确标记；
13. Adapter 调用过程进入 Trace；
14. 后续新增 Civil 3D / BIM Adapter 时，不需要重写 Orchestrator。
```

---

## 17. 本模块结论

软件 Adapter 层是 Agent 系统和工程软件之间的隔离层。

它的核心作用是：

```text
隔离软件差异；
隔离软件异常；
隔离工程数据风险；
统一调用方式；
统一返回结构；
支持 DryRun、执行和回滚。
```

正确设计是：

```text
Agent 不直接碰软件；
LLM 不直接碰软件；
Tool Registry 控制工具；
Adapter 执行软件动作；
Adapter 返回标准结果；
Trace 记录调用过程。
```

一句话：

```text
Adapter 是工程 Agent 落地到 EICAD / CAD / BIM 的安全接口层。
```
