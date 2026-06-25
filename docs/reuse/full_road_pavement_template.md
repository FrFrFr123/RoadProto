# 整幅路路面结构层模板复用说明

## 能力定位

整幅路路面结构层模板用于把一个路基模板实体的左右侧部件快照和每部件结构层配置保存在同一个独立模板实体中。它面向整条路的标准断面配置，不替代既有单部件 `DnPavementLayerTemplateEntity`。

## 可复用边界

- `FullRoadPavementTemplateModel` 是领域数据和刷新规则核心，不依赖 ObjectARX。
- `FullRoadPavementTemplateData` 保存模板名称、显示比例、插入点、参考路基模板信息、部件快照和每部件结构层。
- `FullRoadPavementComponentSnapshot` 保存路基部件宽度、坡度、类型、颜色、变宽表、坡度变化表和内外侧路缘石参数。
- 每个部件直接内嵌 `PavementLayerTemplateData` 语义的结构层参数，不保存单部件模板 handle。
- `FullRoadPavementTemplateRules::refreshFromSubgradeSnapshot` 按侧别、部件类型和同侧同类型序号匹配刷新，匹配成功时保留结构层，只刷新路基部件参数。
- `FullRoadPavementTemplateRules::componentDisplayOrder` 提供从左外侧到中线、再到右外侧的预览和切换顺序。
- WPF 整幅路预览以路基部件快照生成坡度和路缘石高差线形，以每部件内嵌结构层生成颜色/填充多边形，并支持滚轮缩放、中键拖动、部件点选和结构层点选。

## 与既有能力关系

- 复用 `SubgradeTemplateModel` 的部件参数和道路等级枚举。
- 复用 `PavementLayerTemplateModel` 的结构层字段、结构层类型、材料名称、颜色、填充、厚度、加宽和坡度语义。
- WPF 侧通过 `PavementLayerTemplateLayerEditorHelper` 复用单层编辑、默认层和默认预设套用逻辑，并保持厚度、加宽、坡度的统一值/内外侧拆分交互与单部件模板一致。
- CAD 侧通过 `DnFullRoadPavementTemplateEntity` 独立持久化和显示，不依赖 `DnPavementLayerTemplateEntity`。

## 非目标

- 不接入道路模型生成。
- 不接入横断面图生成。
- 不参与工程量统计。
- 不做 XML 导入导出。
- 不自动监听或联动参考路基模板变化。

## 后续扩展口子

- 道路模型或横断面图配置可按 handle 读取 `FullRoadPavementTemplateData`。
- 后续可将整幅路模板作为横断面图配置的默认结构层来源。
- 后续可接入统一关系管理机制，在参考路基模板变更或整幅路模板被引用时提示影响范围。
- 后续可扩展模板库、标准断面库、差异比对和批量套用。
