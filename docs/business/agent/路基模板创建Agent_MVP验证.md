# 路基模板创建 Agent MVP 验证

## 基本信息

- 功能名称：路基模板创建 Agent MVP 验证
- 所属模块：`AGENT`
- 命令名称：用户入口 `RD_AGENT_CONSOLE`，业务 Agent 标识 `subgrade_template_create_agent`
- 对应代码入口：规划为 `src/application/agent/subgrade/SubgradeTemplateAgentMapper.*` 和 `src/cad_adapter/objectarx/agent/ObjectArxAgentToolAdapter.*`
- 业务文档维护人：RoadProto
- 原型版本：V0.1 Agent MVP 文档阶段
- 是否可复用：部分可复用

## 功能背景

RoadProto 已有路基模板创建能力，具备领域默认值、参数规则、WPF 桥接和 `DnSubgradeTemplateEntity` 自定义实体。该场景适合作为 Agent MVP 首个验证对象，用于证明 Agent 架构可以受控调用现有业务功能。

## 业务目标

- 验证自然语言输入到结构化参数的转换。
- 验证默认值和业务校验由规则引擎处理。
- 验证工具计划、DryRun、审批和执行闭环。
- 验证外置后端不能直接写 DWG，必须通过 RoadProto 本地 Adapter。
- 验证执行前由 WPF 展示结构化执行计划。
- 验证 Trace 和流转日志能记录完整过程。

## 适用场景

- 创建独立路基模板。
- 用户未选择道路中线。
- 用户希望先看参数和 DryRun，再确认创建。
- 用户需要查看每一步流转定位问题。
- 当前 DWG 已加载 RoadProto ARX 和 WPF 托管插件。

## 输入条件

- CAD 选择对象：MVP 可为空。
- 用户输入参数：模板名称、道路等级、显示比例、设计速度、路基宽度等自然语言字段。
- 用户审批动作：确认执行、取消、返回修改参数、重新 DryRun。
- 已有设计实体：MVP 不要求已有实体。
- 外部数据：Agent 后端配置、路基模板业务 Agent Schema 和规则。

## 输出结果

- CAD 图形实体：用户确认后创建 `DnSubgradeTemplateEntity`。
- 领域实体：`SubgradeTemplateData`。
- Trace：`TraceId` / `SessionId` / `TaskId`。
- 日志：参数、规则、DryRun、审批、Bridge 和执行结果摘要。
- 表格或报告：无。
- 更新通知或重建请求：无。

## 操作流程

1. 用户打开 Agent 控制台。
2. 控制台确认后端可用；如不可用则自动启动后端并打印日志。
3. 用户输入“创建一个高速公路路基模板，名称叫主线路基模板，显示比例 1:10”。
4. 后端识别意图并路由到 `subgrade_template_create_agent`。
5. LLM 提取用户明确表达的参数。
6. Schema 校验字段类型、单位、来源和置信度。
7. Rule Engine 补默认值并校验显示比例和道路等级。
8. Tool Registry 生成工具计划。
9. RoadProto 本地 Adapter 执行 DryRun，返回模板摘要。
10. WPF 展示规则命中、DryRun 结果、结构化执行计划和需要点取插入点的提示。
11. 用户确认执行。
12. RoadProto 本地 Adapter 点取插入点，并调用现有路基模板创建能力写入实体。
13. WPF 展示新实体 handle、执行结果、Trace 和流转日志。

## 关键业务规则

- LLM 只提取用户明确表达的参数。
- 道路等级缺失时先追问用户，不直接默认 `Expressway`；原生 RoadProto 空白新建窗口仍可默认高速公路，但 Agent 写入链路必须让道路等级成为可追溯参数。
- 模板名称缺失时由规则默认 `默认路基模板`。
- 显示比例缺失时由规则默认 `10`。
- 当用户补充或直接表达任意受支持道路等级时，后端规则必须把中文等级归一化为 RoadProto 稳定编码，并从 `defaultComponentsByRoadGrade` 下发非空 `Components`：
  - 高速公路：`Expressway`
  - 一级公路：`FirstClass`
  - 二级公路：`SecondClass`
  - 三级公路：`ThirdClass`
  - 四级公路：`FourthClass`
  - 城市快速路：`UrbanExpressway`
  - 城市主干路 / 城市主干道：`UrbanArterial`
  - 城市次干路 / 城市次干道：`UrbanSubArterial`
  - 城市支路：`UrbanBranch`
- `Components=0` 是规则缺失或道路等级未覆盖的失败信号，不得交给 RoadProto 本地 Adapter 自行补默认部件。
- 显示比例只能为 1、10、20、50、100。
- 用户输入总宽时，MVP 只记录和展示，不自动拆分到各部件宽度。
- 正式创建前必须 DryRun。
- 正式创建前必须展示结构化执行计划。
- 正式创建前必须用户审批。
- 插入点必须由 RoadProto 本地 Adapter 在 AutoCAD 中获取。
- 后端不得直接创建 CAD 对象。
- WPF 不得直接操作 ObjectARX。

## 流转日志

该功能必须打印并记录：

- 用户输入摘要。
- 业务 Agent 路由。
- 模型 Provider 和模型名。
- 参数提取结果。
- Schema 校验结果。
- 规则命中结果。
- 工具计划。
- DryRun 请求和结果。
- WPF 执行计划展示。
- 用户确认或取消。
- 插入点点取结果。
- RoadProto Bridge 调用结果。
- CAD 实体创建结果。
- 异常、重试、取消。

日志保存在：

```text
F:\0_GPT_RoadProtoAgentRuntime\logs\backend\
F:\0_GPT_RoadProtoAgentRuntime\logs\roadproto\
```

默认保留最近 14 天，或总量最多 1GB。

## 可复用性说明

- 可复用内容：Agent 参数提取、规则命中、DryRun、审批、Tool Adapter、Trace 和日志的通用链路。
- 临时原型内容：只验证路基模板创建，不处理路基模板编辑和批量生成。
- 正式复用前需要改造的内容：补充更多业务 Agent、完善后端服务部署、补齐评测集和权限策略。

## 与其他模块的依赖关系

- 上游模块：`AGENT`。
- 下游模块：`CROSS_SECTION` 路基模板领域模型、创建服务和 ObjectARX 实体。
- 实体联动行为：MVP 不新增联动，创建后的实体仍按现有路基模板规则独立存在。

## 后续对接正式 EICAD 功能的注意事项

- 路基总宽到部件宽度拆分必须由可测试规则或标准模板库完成，不能由模型自由推断。
- 路基模板与道路中线、路面结构层模板、横断面戴帽道路模型的联动应由后续业务 Agent 和统一实体关系机制表达。
- 写入、覆盖和批量生成能力必须继续保持 DryRun、审批、Trace 和日志。
