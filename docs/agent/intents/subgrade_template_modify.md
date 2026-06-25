# 意图：修改路基模板

## 1. 意图基本信息

| 项 | 内容 |
| --- | --- |
| 意图 ID | `subgrade_template.modify` |
| 所属 Agent | `roadproto_engineering_agent` |
| 所属 Skill | `subgrade_template` |
| 所属模块 | `AGENT` |
| 验证业务模块 | `CROSS_SECTION` |
| Skill 文档 | `docs/agent/skills/subgrade_template_skill.md` |
| 通用模板 | `docs/agent/intent_rule_template.md` |
| 关联 RoadProto 功能文档 | `docs/business/cross_section/路基模板_编辑.md` |
| 当前状态 | 草案 |

本意图负责把用户对已有路基模板的修改请求转成受控修改计划。修改必须以既有 `DnSubgradeTemplateEntity` 的当前参数为基础，只覆盖用户明确指定的字段。

## 2. 用户表达

用户可能这样说：

- 修改当前模板的行车道宽度。
- 把这个路基模板的行车道加宽 1 米。
- 将主线路基模板的硬路肩改成 3 米。
- 把当前路基模板名称改为主线标准路基。
- 左侧土路肩宽度改成 0.75 米。

不应匹配的表达：

- 帮我创建高速公路路基模板。
- 删除这个路基模板。
- 当前图里有哪些路基模板？
- 用当前模板进行戴帽。
- 修改路面结构层模板。

## 3. 意图边界

本意图负责：

- 修改已有路基模板。
- 解析用户指定的目标模板。
- 解析用户指定的字段变更。
- 保留未提及参数的实体原值。
- 展示修改前后差异。
- 用户确认后调用 RoadProto 本地工具修改实体。

本意图不负责：

- 创建新模板。
- 删除模板。
- 执行道路模型戴帽。
- 修改路面结构层模板实体。
- 根据总宽自动分配所有部件。

## 4. 必要参数

| 参数 | 类型 | 是否必填 | 来源 | 缺失时处理 |
| --- | --- | --- | --- | --- |
| `targetHandle` / 目标模板 handle | string | 是 | 当前选择集、用户输入、候选选择 | 追问或要求 CAD 点选 |
| `changeSet` / 修改项 | object | 是 | 用户输入 | 没有明确修改项时追问 |
| `confirmation` / 用户确认 | bool | 写 CAD 前必填 | WPF 确认面板 | 未确认不得写入 CAD |

## 5. 可选参数

| 参数 | 类型 | 默认值来源 | 说明 |
| --- | --- | --- | --- |
| `targetName` | string | 空 | 按模板名称定位，名称不唯一时必须追问 |
| `templateName` | string | 实体原值 | 用户明确要求改名时覆盖 |
| `roadGrade` | enum | 实体原值 | 修改道路等级时需要重新校验默认结构影响 |
| `displayScale` | number | 实体原值 | 支持 RoadProto 允许值 |
| `laneWidth` | number | 实体原值 | 可按左右侧或双侧覆盖 |
| `laneWidthDelta` | number | 无 | 在实体原值基础上增减 |
| `hardShoulderWidth` | number | 实体原值 | 可按左右侧或双侧覆盖 |
| `earthShoulderWidth` | number | 实体原值 | 可按左右侧或双侧覆盖 |
| `medianWidth` | number | 实体原值 | 修改中央分隔带 |
| `sideScope` | `Left` / `Right` / `Both` | `Both` | 歧义会影响风险时追问 |

## 6. 默认值规则

- 修改以实体当前值为默认值。
- 用户未提及字段保持原值。
- `Delta` 类型参数在原值基础上增减。
- 用户同时给出绝对值和增量时，以绝对值为准，并在确认页展示冲突处理。
- LLM 不得自行补目标 handle。

## 7. 校验规则

- 必须定位唯一 `DnSubgradeTemplateEntity`。
- 修改项不得为空。
- 宽度必须大于 `0`。
- `displayScale` 必须属于支持值。
- 修改后模板必须能生成有效断面结构。
- 写 CAD 前必须完成 DryRun 和用户确认。

## 8. 追问规则

必须追问：

- 无法确定目标模板。
- 当前选择集没有路基模板。
- 当前选择集有多个路基模板。
- 用户只说“改一下”“优化一下”，没有明确修改项。
- 用户只给总宽但未指定分配方式。

追问话术：

- 请问要修改哪个路基模板？
- 当前选择了多个路基模板，请指定要修改哪一个。
- 请问要修改哪个参数？
- 你希望把总宽变化分配到哪些部件？

## 9. 确认前展示

确认页必须展示：

- 当前 Agent、Skill、Intent。
- 目标模板名称和 handle。
- 修改项列表。
- 修改前值和修改后值。
- 未修改参数保持原值说明。
- 风险等级。
- 将调用的 Tool。
- TraceId / TaskId。

用户确认前允许修改所有 `changeSet` 字段。

## 10. 工具绑定

| 项 | 内容 |
| --- | --- |
| 后端 Tool 名称 | `SubgradeTemplate.Modify` |
| RoadProto 本地适配入口 | `RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE` |
| CAD 写入对象 | `DnSubgradeTemplateEntity` |
| 是否写 CAD | 是 |
| 是否必须用户确认 | 是 |
| 是否需要 DryRun | 是 |

## 11. 执行结果

成功时返回：

- 模板修改成功。
- 模板 handle。
- 修改项摘要。
- 修改后预览或参数摘要。

失败时返回：

- 目标不存在。
- 目标不唯一。
- 参数校验失败。
- 本地 Adapter 执行失败。
- 用户取消。

## 12. 日志与 Trace

必须记录：

- Agent 路由。
- Skill 识别。
- Intent 识别。
- 目标解析。
- 参数抽取。
- 实体原值读取。
- 修改差异生成。
- 参数校验。
- DryRun。
- 用户确认。
- Tool 调用。
- Adapter 执行。
- 结果返回。
