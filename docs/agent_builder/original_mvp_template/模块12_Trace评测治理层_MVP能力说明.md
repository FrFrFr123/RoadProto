# 模块 12：Trace / 评测 / 治理层 MVP 能力说明

## 1. 当前模块

当前讨论模块：

```text
模块 12：Trace / 评测 / 治理层
```

该模块负责记录 Agent 运行全过程，沉淀失败样例，支撑问题回放、效果评测、版本追踪和后续治理。

MVP 中不需要一开始做复杂平台，但必须有最小 Trace、最小评测集、最小版本记录。

一句话：

```text
没有 Trace，就无法证明 Agent 可控；
没有评测，就无法持续迭代；
没有版本记录，就无法定位问题来源。
```

---

## 2. 本模块在 MVP 总体流程中的位置

Trace / 评测 / 治理层贯穿整个 Agent 运行链路。

```text
用户输入
↓
Agent 配置加载
↓
模型调用
↓
上下文构造
↓
意图识别
↓
参数提取
↓
Schema 校验
↓
规则命中
↓
工具计划
↓
DryRun
↓
用户审批
↓
正式执行
↓
结果校验
↓
结果解释
↓
Trace 记录
↓
失败样例沉淀
↓
评测集更新
```

它不是某一步，而是覆盖所有关键节点。

---

## 3. 模块定位

Trace / 评测 / 治理层的定位是：

```text
让每一次 Agent 运行都可追踪、可回放、可复盘、可测试、可改进。
```

它解决的问题是：

```text
1. 出错后不知道错在哪一步；
2. 模型输出不可复现；
3. 规则命中无法解释；
4. 工具调用无法追踪；
5. 用户确认行为没有记录；
6. 执行结果无法回放；
7. 失败案例不能沉淀；
8. Prompt、Schema、规则、工具版本变更后无法对比效果。
```

该模块不是：

```text
1. 业务执行模块；
2. 模型调用模块；
3. 规则引擎；
4. Tool Registry；
5. Adapter；
6. 前端页面本身。
```

---

## 4. MVP 必须实现的 Trace 能力

每次任务运行必须生成：

```text
run_id
```

run_id 是整条链路的唯一标识。

MVP Trace 至少记录：

```text
用户输入；
总体 Agent；
业务 Agent；
工作流状态；
模型配置；
Prompt 版本；
Skill 版本；
Schema 版本；
规则版本；
工具版本；
Adapter 版本；
Context Package 摘要；
LLM 原始输出；
LLM 结构化解析结果；
Schema 校验结果；
规则命中结果；
工具调用计划；
DryRun 结果；
用户审批记录；
正式执行结果；
保存结果；
回滚结果；
错误信息；
耗时；
token 用量。
```

注意：

```text
Trace 不是只记最终结果；
Trace 要记录过程。
```

---

## 5. Trace 数据结构

MVP 建议 Trace 结构如下：

```json
{
  "run_id": "RUN_001",
  "session_id": "S001",
  "user_input": "",
  "main_agent": {
    "agent_id": "engineering_assistant",
    "version": "1.0.0"
  },
  "routed_agent": {
    "agent_id": "subgrade_template_agent",
    "version": "1.0.0"
  },
  "workflow": {
    "workflow_id": "subgrade_template_workflow",
    "states": []
  },
  "model_trace": [],
  "context_trace": {},
  "schema_trace": {},
  "rule_trace": [],
  "tool_trace": [],
  "dryrun_trace": {},
  "approval_trace": {},
  "execution_trace": {},
  "rollback_trace": {},
  "errors": [],
  "metrics": {
    "latency_ms": 0,
    "input_tokens": 0,
    "output_tokens": 0
  }
}
```

---

## 6. Trace 分类

MVP 中可以先按文件或数据库表分类记录。

建议分类：

```text
workflow_trace：工作流状态变化；
llm_trace：模型输入输出；
context_trace：上下文摘要；
schema_trace：Schema 校验；
rule_trace：规则命中；
tool_trace：工具计划和工具调用；
adapter_trace：Adapter 调用；
dryrun_trace：DryRun 预演；
approval_trace：用户审批；
execution_trace：正式执行；
rollback_trace：回滚；
error_trace：错误；
evaluation_trace：评测结果。
```

---

## 7. 前端 Trace 查看入口

前端不需要一开始做复杂日志平台，但必须有基础入口。

MVP 前端至少支持：

```text
查看本次运行详情；
复制 run_id；
查看失败节点；
查看参数来源；
查看规则命中；
查看 DryRun 结果；
查看工具调用结果；
查看错误信息；
导出 Trace。
```

对于普通用户，展示：

```text
任务理解；
参数来源；
规则提示；
执行结果；
错误原因；
下一步建议。
```

对于研发 / 测试人员，展示：

```text
模型原始输出；
Schema 校验结果；
规则命中详情；
工具入参；
Adapter 返回；
错误堆栈摘要；
版本信息。
```

---

## 8. 失败样例沉淀

MVP 必须支持把失败案例转成测试样例。

失败样例来源：

```text
意图识别错误；
业务 Agent 路由错误；
参数提取错误；
Schema 校验失败；
默认值补全错误；
规则误判；
工具计划错误；
DryRun 失败；
执行失败；
结果解释错误；
用户反馈错误。
```

失败样例至少记录：

```json
{
  "case_id": "case_001",
  "source_run_id": "RUN_001",
  "task_type": "create_subgrade_template",
  "user_input": "",
  "context_summary": {},
  "actual_result": {},
  "expected_result": {},
  "failure_type": "param_extract_error",
  "fix_status": "pending"
}
```

原则：

```text
失败不是只修一次；
失败要进入回归测试集。
```

---

## 9. MVP 评测体系

MVP 不需要完整评测平台，但必须有评测集目录和基础执行脚本。

评测类型至少包括：

```text
意图识别测试；
业务 Agent 路由测试；
参数提取测试；
Schema 合规测试；
默认值补全测试；
规则命中测试；
风险判断测试；
审批判断测试；
工具计划测试；
DryRun 测试；
异常输入测试；
结果解释测试。
```

每个评测样例建议包含：

```json
{
  "case_id": "case_001",
  "case_name": "创建高速公路路基模板",
  "task_type": "create_subgrade_template",
  "user_input": "帮我创建一个高速公路路基模板，设计速度80，路基宽度24.5米",
  "context": {},
  "expected_intent": "create_subgrade_template",
  "expected_agent": "subgrade_template_agent",
  "expected_params": {
    "road_grade": "高速公路",
    "design_speed": 80,
    "subgrade_width": 24.5
  },
  "expected_missing_fields": [],
  "expected_rule_hits": [],
  "expected_risk_level": "medium",
  "expected_approval_required": true
}
```

---

## 10. MVP 评测指标

MVP 中先看核心指标：

```text
意图识别是否正确；
业务 Agent 路由是否正确；
参数提取是否正确；
Schema 是否合规；
默认值来源是否正确；
规则命中是否正确；
风险等级是否正确；
是否正确进入审批；
是否正确阻断危险动作；
DryRun 是否能返回影响范围；
工具计划是否符合预期；
失败原因是否可解释；
Trace 是否完整。
```

不建议 MVP 一开始追求复杂分数系统。

先要求：

```text
关键链路必须可验证；
失败样例必须可复测；
上线前核心用例必须通过。
```

---

## 11. 版本治理 MVP

MVP 中每次运行必须记录关键版本。

至少包括：

```text
Agent 版本；
Workflow 版本；
Prompt 版本；
Skill 版本；
Schema 版本；
Rule 版本；
Tool 版本；
Adapter 版本；
Model 配置版本；
测试集版本。
```

目的：

```text
出错后知道是哪个版本导致；
模型切换后能比较效果；
规则修改后能跑回归；
Prompt 修改后能评估影响；
工具升级后能定位问题。
```

---

## 12. 最小发布治理

MVP 不需要复杂发布平台，但需要最小发布规则：

```text
Prompt 修改后，跑参数提取核心测试；
Schema 修改后，跑 Schema 合规测试；
规则修改后，跑规则回归测试；
工具修改后，跑 DryRun 和执行测试；
模型切换后，跑意图识别和参数提取测试；
Adapter 修改后，跑读、预演、执行、回滚测试。
```

不建议直接线上修改：

```text
关键规则；
工具契约；
Schema 字段；
审批策略；
Adapter 执行逻辑。
```

---

## 13. 权限与审计 MVP

MVP 中审计先做最小版。

至少记录：

```text
谁发起任务；
使用了哪个总体 Agent；
调用了哪个业务 Agent；
使用了哪个模型；
调用了哪些工具；
影响了哪些对象；
谁确认了执行；
是否保存；
是否回滚；
是否失败。
```

注意：

```text
审计不等于复杂权限系统；
但关键动作必须留痕。
```

---

## 14. 敏感信息处理

Trace 中不能记录：

```text
API Key；
完整 Credential；
客户敏感原始数据；
完整工程原始文件；
不必要的个人信息；
未脱敏路径或账号。
```

可记录：

```text
credential_ref；
工程摘要；
对象 ID；
参数摘要；
错误码；
版本信息；
模型名称；
API 通道 ID。
```

---

## 15. MVP 不做范围

Trace / 评测 / 治理层 MVP 暂不做：

```text
复杂日志平台；
完整 BI 看板；
自动化质量评分平台；
多环境灰度发布；
复杂审计报表；
全量客户配置隔离；
多租户计费；
复杂权限矩阵；
大规模在线评测平台。
```

但需要预留：

```text
Trace 存储接口；
Trace 查询接口；
失败样例转测试样例接口；
评测执行接口；
版本记录字段；
审计字段；
脱敏策略；
发布状态字段。
```

---

## 16. 与其他模块的关系

本模块依赖：

```text
前端；
Agent 配置中心；
模型网关；
Orchestrator；
Context Manager；
Schema Validator；
Rule Engine；
Tool Registry；
Software Adapter；
Execution Control。
```

本模块服务于：

```text
问题定位；
结果解释；
失败复盘；
测试评测；
版本治理；
权限审计；
后续持续优化。
```

本模块不能替代：

```text
业务规则；
模型调用；
工具执行；
用户审批；
软件 Adapter；
工程设计责任判断。
```

---

## 17. MVP 验收标准

本模块完成后，应满足：

```text
1. 每次运行都有 run_id；
2. 每次运行能记录完整状态链路；
3. 能看到模型输入输出；
4. 能看到 Schema 校验结果；
5. 能看到规则命中结果；
6. 能看到工具调用计划；
7. 能看到 DryRun 结果；
8. 能看到用户审批记录；
9. 能看到执行结果；
10. 能看到错误节点和错误码；
11. 能记录关键版本号；
12. 能导出或查看 Trace；
13. 失败案例能转成测试样例；
14. 有最小评测集；
15. 规则、Prompt、Schema、工具变更后能跑核心回归测试；
16. Trace 中不暴露 API Key 和敏感原始数据。
```

---

## 18. 本模块结论

Trace / 评测 / 治理层是 MVP 从“能跑”变成“可控、可测、可迭代”的关键。

正确设计是：

```text
每次运行必须可追踪；
每次失败必须可复盘；
每个失败样例都应该能沉淀；
每次配置变更都应该能回归；
每个关键版本都必须记录；
每个高风险动作都必须审计。
```

一句话：

```text
Trace 让问题可定位；
评测让能力可迭代；
治理让系统可长期维护。
```
