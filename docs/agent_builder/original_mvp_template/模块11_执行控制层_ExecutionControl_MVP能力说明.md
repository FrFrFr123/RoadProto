# 模块 11：执行控制层 / Execution Control MVP 能力说明

## 1. 当前模块

当前讨论模块：

```text
模块 11：执行控制层 / Execution Control
```

执行控制层负责控制工程动作从“计划”到“预演”再到“用户确认”和“正式执行”的全过程。

MVP 中，执行控制层至少包含：

```text
DryRun 预演；
Preview 预览；
Approval 用户审批；
Execution 正式执行；
Save 保存控制；
Rollback 回滚控制；
Result Validation 执行结果校验。
```

一句话：

```text
执行控制层决定工程动作能不能真正落到工程数据里。
```

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
Tool Registry 生成并校验工具计划
↓
Execution Control 接收工具计划
↓
DryRun 预演
↓
Preview 展示影响范围
↓
Approval 等待用户确认
↓
Execution 正式执行
↓
Result Validation 校验执行结果
↓
Save 控制是否保存
↓
Rollback 在失败或撤销时回滚
↓
Trace 记录全过程
```

本模块主要覆盖：

```text
DryRunExecutor；
PreviewRenderer；
ApprovalGate；
ExecutionEngine；
SaveController；
RollbackManager；
ResultValidator；
ExecutionTrace。
```

---

## 3. 模块定位

执行控制层的定位是：

```text
把工程动作从“模型建议”变成“可预演、可确认、可执行、可回滚、可追踪”的受控操作。
```

它解决的问题是：

```text
1. 防止没有预演就修改工程数据；
2. 防止用户没确认就执行写入动作；
3. 防止执行失败后状态不清；
4. 防止部分成功被误判为成功；
5. 防止保存、覆盖、批量修改等高风险动作失控；
6. 防止执行结果不可追溯；
7. 防止无法撤销或回滚时不提前提示。
```

执行控制层不是：

```text
1. LLM；
2. 规则引擎；
3. Tool Registry；
4. Software Adapter；
5. 前端页面；
6. 业务 Agent 本身。
```

---

## 4. 执行控制层的主流程

MVP 主流程建议为：

```text
ToolPlanReady
↓
DryRunExecuting
↓
DryRunPassed
↓
PreviewReady
↓
WaitingApproval
↓
Approved
↓
Executing
↓
ResultValidating
↓
ExecutionSucceeded
↓
SaveControlled
↓
Completed
```

异常分支：

```text
DryRunFailed → Failed
UserRejected → Cancelled
UserEditedParams → 回到 RulesApplied / PlanGenerated
ExecutionFailed → RollingBack / Failed
RollbackSucceeded → RolledBack
RollbackFailed → FailedNeedManualCheck
ResultInvalid → RollingBack / Failed
```

---

## 5. DryRun 预演

### 5.1 DryRun 的作用

DryRun 是正式执行前的预演机制。

DryRun 不是模型生成一段说明，也不是前端模拟一段文字。

DryRun 应基于真实工程上下文、真实工具输入、真实 Adapter 能力进行预演。

DryRun 需要回答：

```text
将要执行什么；
会影响哪些对象；
会创建哪些对象；
会修改哪些对象；
使用哪些参数；
哪些参数来自用户输入；
哪些参数来自规则补全；
命中了哪些规则；
有哪些风险；
是否支持回滚；
是否需要用户确认。
```

### 5.2 DryRun 输入

DryRun 输入包括：

```text
run_id；
业务 Agent；
工具调用计划；
最终参数；
参数来源；
规则命中结果；
当前工程上下文；
当前选中对象；
工具风险等级。
```

示例：

```json
{
  "run_id": "RUN_001",
  "agent_id": "subgrade_template_agent",
  "tool_plan": [
    {
      "tool_name": "preview_subgrade_template",
      "input": {
        "template_params": {
          "road_grade": "高速公路",
          "design_speed": 80,
          "subgrade_width": 24.5,
          "slope_type": "project_default_slope"
        }
      }
    }
  ]
}
```

### 5.3 DryRun 输出

DryRun 输出建议：

```json
{
  "success": true,
  "actions": [
    "读取当前工程信息",
    "生成路基模板草稿",
    "预览模板应用到当前断面"
  ],
  "affected_objects": [
    {
      "object_type": "cross_section",
      "object_id": "CS_001",
      "action": "preview"
    }
  ],
  "created_objects": [],
  "modified_objects": [],
  "params_used": [],
  "rule_hits": [],
  "warnings": [],
  "risk_level": "medium",
  "rollback_supported": true,
  "need_approval": true,
  "preview_summary": "将生成 1 个路基模板草稿，并可应用到当前选中断面。"
}
```

### 5.4 DryRun 原则

```text
DryRun 不修改真实工程数据；
DryRun 和正式执行必须使用同一套输入参数；
DryRun 必须返回影响范围；
DryRun 失败不能进入正式执行；
写入类动作必须先 DryRun；
DryRun 结果必须进入 Trace。
```

---

## 6. Preview 预览

Preview 是给用户看的 DryRun 结果表达。

MVP 中前端至少展示：

```text
动作清单；
影响对象；
使用参数；
参数来源；
规则命中；
风险等级；
是否可回滚；
是否需要确认；
错误和警告。
```

Preview 的原则：

```text
不是只给一句“可以执行”；
必须让用户知道将要发生什么；
必须让用户知道哪些内容来自规则补全；
必须让用户知道是否修改工程对象。
```

示例：

```text
本次将执行：
1. 读取当前工程默认参数；
2. 创建路基模板草稿；
3. 将模板应用到当前选中断面。

影响范围：
当前选中断面 CS_001。

风险等级：
中风险，执行前需要确认。

回滚能力：
支持撤销本次创建和修改。
```

---

## 7. 用户审批 Approval

### 7.1 审批触发条件

MVP 中建议：

```text
只读查询：不需要审批；
问答解释：不需要审批；
参数提取：不需要审批；
生成草稿：可弱确认；
DryRun 预演：不需要审批；
修改工程对象：必须审批；
保存工程文件：必须审批；
覆盖文件：强审批；
删除对象：MVP 不建议支持。
```

### 7.2 审批内容

用户审批的不是一句“确定”，而是一份动作说明。

审批内容包括：

```text
即将执行什么；
影响哪些对象；
使用哪些参数；
参数来源是什么；
命中了哪些规则；
有哪些警告；
是否可回滚；
执行后是否保存；
失败后如何处理。
```

### 7.3 审批动作

用户审批动作至少包括：

```text
approve：同意执行；
reject：拒绝执行；
edit：修改参数；
ask_more：要求解释；
cancel：取消任务。
```

示例：

```json
{
  "run_id": "RUN_001",
  "approval_action": "approve",
  "approved_params": {},
  "comment": "确认执行"
}
```

审批记录必须进入 Trace。

---

## 8. 正式执行 Execution

### 8.1 执行前置条件

进入正式执行前必须满足：

```text
工具已注册；
当前 Agent 有权限调用工具；
工具入参已通过 Schema 校验；
业务规则没有阻断错误；
风险策略允许执行；
DryRun 已通过；
需要审批时用户已确认；
当前工程状态允许写入。
```

任何一项不满足，都不能执行。

### 8.2 执行原则

```text
执行前记录必要快照；
执行中记录每个工具调用；
执行失败返回明确错误码；
部分成功必须明确标记；
执行结果必须结构化；
执行结果不能由模型自由判断；
执行完成后必须进行结果校验。
```

### 8.3 执行输出

执行输出示例：

```json
{
  "execution_status": "success",
  "executed_tools": [
    "apply_subgrade_template"
  ],
  "created_objects": [
    {
      "object_type": "subgrade_template",
      "object_id": "TPL_001"
    }
  ],
  "modified_objects": [
    {
      "object_type": "cross_section",
      "object_id": "CS_001"
    }
  ],
  "saved": false,
  "rollback_available": true,
  "errors": [],
  "warnings": [],
  "result_summary": "已创建路基模板并应用到当前断面，尚未保存工程文件。"
}
```

---

## 9. 保存控制 Save Control

保存是高风险动作，不能和执行混在一起默认完成。

MVP 中建议区分：

```text
生成草稿；
写入当前工程内存对象；
保存到当前工程文件；
另存为新文件；
覆盖已有文件；
批量保存结果。
```

MVP 默认建议：

```text
执行可以修改当前工程内存对象；
不自动保存工程文件；
保存需要单独确认；
覆盖文件类动作暂不进入 MVP 或强审批。
```

保存控制原则：

```text
保存前必须明确保存位置；
覆盖前必须备份；
保存结果必须可校验；
保存失败必须明确提示；
保存动作必须进入 Trace。
```

---

## 10. 回滚 Rollback

### 10.1 回滚触发场景

回滚可由以下情况触发：

```text
执行失败；
执行结果校验失败；
用户主动撤销；
部分成功需要恢复；
保存前取消；
系统异常中断。
```

### 10.2 回滚策略

MVP 中建议支持：

```text
创建对象：删除新对象；
修改对象：恢复修改前快照；
生成草稿：丢弃草稿；
工具执行失败：回滚已执行步骤；
不可回滚：执行前明确提示。
```

### 10.3 回滚输出

```json
{
  "rollback_status": "success",
  "rollback_strategy": "restore_object_snapshot",
  "restored_objects": [
    {
      "object_type": "cross_section",
      "object_id": "CS_001"
    }
  ],
  "failed_items": [],
  "message": "已恢复执行前对象状态。"
}
```

### 10.4 回滚原则

```text
不是所有动作都可完全回滚；
不可完全回滚必须在审批前提示；
回滚本身也要记录 Trace；
回滚失败必须提示人工处理；
保存和覆盖类操作必须更谨慎。
```

---

## 11. 执行结果校验 Result Validation

正式执行后，不能只看工具返回 success。

还需要校验：

```text
预期对象是否创建；
预期对象是否修改；
影响范围是否符合 DryRun；
是否产生额外对象；
是否有未处理错误；
是否部分成功；
是否需要回滚。
```

结果校验输出：

```text
valid：执行结果有效；
invalid：执行结果无效；
partial_success：部分成功；
need_manual_check：需要人工检查；
need_rollback：需要回滚。
```

---

## 12. 与其他模块的关系

执行控制层依赖：

```text
Orchestrator；
Rule Engine；
Tool Registry；
Software Adapter；
Schema；
Approval Policy；
Risk Policy；
Trace Logger。
```

执行控制层服务于：

```text
前端预览；
用户审批；
正式执行；
回滚；
结果解释；
Trace 回放；
Evaluation。
```

执行控制层不能直接：

```text
调用 LLM 自由判断是否执行；
跳过 Tool Registry；
跳过 DryRun；
跳过用户审批；
跳过 Adapter；
把执行失败解释为成功。
```

---

## 13. MVP 不做范围

执行控制层 MVP 暂不做：

```text
复杂多级审批流；
多人协同审批；
复杂批量事务；
复杂文件级版本管理；
跨模块长事务；
跨软件分布式事务；
完整撤销历史树；
自动修复所有执行失败。
```

但需要预留：

```text
approval_level；
rollback_token；
snapshot_id；
save_policy；
execution_mode；
partial_success 标记；
manual_check 标记；
execution_trace。
```

---

## 14. MVP 验收标准

本模块完成后，应满足：

```text
1. 写入类工具必须先 DryRun；
2. DryRun 不修改真实工程数据；
3. DryRun 输出影响范围；
4. DryRun 失败不能正式执行；
5. 修改工程对象前必须用户确认；
6. 用户取消后不能继续执行；
7. 用户修改参数后必须重新校验和 DryRun；
8. 正式执行前必须检查工具权限和入参；
9. 执行结果必须结构化；
10. 执行失败必须有错误码；
11. 部分成功必须明确标记；
12. 执行后必须做结果校验；
13. 支持基础回滚；
14. 不可回滚动作必须提前提示；
15. 默认不自动保存工程文件；
16. 保存动作必须单独确认；
17. 全过程进入 Trace。
```

---

## 15. 本模块结论

执行控制层是工程 Agent 从“建议”走向“真实工程动作”的最后控制层。

它的核心作用是：

```text
先预演；
再展示；
再确认；
再执行；
再校验；
可回滚；
可追踪。
```

正确设计是：

```text
没有 DryRun，不执行；
没有审批，不写入；
没有结构化结果，不算成功；
没有回滚提示，不做高风险动作；
没有 Trace，不允许上线。
```

一句话：

```text
执行控制层是保护工程数据安全的最后一道闸门。
```
