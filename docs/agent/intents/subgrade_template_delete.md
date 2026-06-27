# 意图：删除路基模板

## 1. 意图基本信息

| 项 | 内容 |
| --- | --- |
| 意图 ID | `subgrade_template.delete` |
| 所属 Agent | `roadproto_engineering_agent` |
| 所属 Skill | `subgrade_template` |
| 所属模块 | `AGENT` |
| 验证业务模块 | `CROSS_SECTION` |
| Skill 文档 | `docs/agent/skills/subgrade_template_skill.md` |
| 通用模板 | `docs/agent/intent_rule_template.md` |
| 关联 RoadProto 功能文档 | `docs/business/cross_section/路基模板_编辑.md` |
| 当前状态 | 草案 |

本意图负责删除已有路基模板实体。删除是高风险写入动作，必须定位唯一目标、展示风险，并经过用户确认。

## 2. 用户表达

用户可能这样说：

- 删除这个路基模板。
- 把当前路基模板删掉。
- 删除刚才创建的模板。
- delete this template.
- 删除名为主线路基模板的模板。
- 移除选中的路基模板。

不应匹配的表达：

- 创建一个路基模板。
- 修改当前模板的行车道宽度。
- 查询有哪些路基模板。
- 删除路面结构层模板。
- 用当前模板戴帽。

## 3. 意图边界

本意图负责：

- 定位已有路基模板。
- 校验删除风险。
- 展示高风险确认。
- 用户确认后删除 `DnSubgradeTemplateEntity`。

本意图不负责：

- 批量删除多个模板。
- 自动解除道路模型或配置中的引用。
- 删除路面结构层模板或边坡模板。
- 清理历史 Trace 或日志。

## 4. 必要参数

| 参数 | 类型 | 是否必填 | 来源 | 缺失时处理 |
| --- | --- | --- | --- | --- |
| `targetHandle` / `TargetMode` | string | 是 | 最近对象上下文、当前选择集、用户输入、候选选择、执行时点选 | 没有目标引用、名称、handle 和可用上下文时追问 |
| `confirmation` | bool | 是 | WPF 高风险确认 | 未确认不得删除 |

## 5. 可选参数

| 参数 | 类型 | 默认值来源 | 说明 |
| --- | --- | --- | --- |
| `targetName` | string | 空 | 按名称定位模板 |
| `deleteReason` | string | 空 | 用户说明，写入 Trace |

## 6. 默认值规则

- 删除不补默认目标。
- 用户说“刚才创建的 / 上一次创建的 / 上一个模板”时，优先使用会话最近对象上下文。
- 用户说“这个模板 / this template”时，不追问目标，生成 `TargetMode=PickOnExecute`，由 RoadProto 本地 Adapter 在执行时点选并校验对象类型。
- 查询到多个同名模板时，不得默认选择第一个。

## 7. 校验规则

- 必须定位唯一 `DnSubgradeTemplateEntity`。
- `TargetMode=PickOnExecute` 的唯一性和类型检查由 RoadProto 本地 Adapter 在 AutoCAD 点选阶段完成。
- 如果目标被道路模型、配置表或其他实体引用，MVP 默认阻断删除。
- 删除前必须完成 DryRun 和高风险确认。
- Tool 必须由 `subgrade_template` Skill 白名单授权。

## 8. 追问规则

必须追问：

- 目标模板不明确。
- 没有目标引用、目标名称、目标 handle，也没有可用会话上下文。
- 当前选择集为空。
- 当前选择集中有多个路基模板。
- 名称匹配多个模板。

追问话术：

- 请问要删除哪个路基模板？
- 当前匹配到多个路基模板，请选择一个。
- 删除后不可直接恢复，是否确认继续？

## 9. 确认前展示

确认页必须展示：

- 当前 Agent、Skill、Intent。
- 目标模板名称和 handle。
- 是否存在引用。
- 删除风险等级：高。
- 删除不可恢复提示。
- 将调用的 Tool。
- TraceId / TaskId。

用户确认前只允许取消或重新选择目标，不允许跳过风险确认。

## 10. 工具绑定

| 项 | 内容 |
| --- | --- |
| 后端 Tool 名称 | `SubgradeTemplate.Delete` |
| RoadProto 本地适配入口 | `RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE` |
| CAD 写入对象 | `DnSubgradeTemplateEntity` |
| 是否写 CAD | 是 |
| 是否必须用户确认 | 是 |
| 是否需要 DryRun | 是 |

本地 Adapter 执行时按 `targetHandle`、`targetName` 或 `TargetMode=PickOnExecute` 定位唯一 `DnSubgradeTemplateEntity`。定位成功后以写方式打开实体并执行删除；点选到非路基模板对象、名称匹配失败或目标不存在时必须返回失败结果，不得删除其他对象。

## 11. 执行结果

成功时返回：

- 模板删除成功。
- 删除的模板 handle。
- 删除的模板名称。

失败时返回：

- 目标不存在。
- 目标不唯一。
- 存在引用，拒绝删除。
- 用户取消。
- 本地 Adapter 执行失败。

## 12. 日志与 Trace

必须记录：

- Agent 路由。
- Skill 识别。
- Intent 识别。
- 目标解析。
- 引用检查。
- DryRun。
- 高风险确认。
- Tool 调用。
- Adapter 删除结果。
- 错误或取消。
