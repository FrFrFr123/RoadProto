# 路基模板 WPF 桥接回写

## 功能范围

本说明记录路基模板参数窗口与 C++ ObjectARX 之间的桥接约定。桥接使用临时键值文件传递请求和响应，保持 WPF 与 CAD 核心对象解耦。

## 命令

- 内部回写命令：`RD_SECTION_SUBGRADE_TEMPLATE_APPLY_DIALOG_FILE`
- WPF 弹窗命令：`RD_SECTION_SUBGRADE_TEMPLATE_SHOW_WPF_DIALOG`

## 请求文件

C++ 写入请求文件，并把请求文件路径写入：

```text
%TEMP%\RoadProtoSubgradeTemplateDialog_<pid>.pending
```

主要字段：

- `handle`：已有实体句柄；新建时为空。
- `responsePath`：WPF 写回响应文件路径。
- `insertionX/Y/Z`：实体插入点。
- `templateName`、`displayScale`、`roadGrade`。
- `roadCenterlineHandle`：后续关联道路中线预留。
- `componentCount` 和 `component.<index>.*` 部件字段。
- `component.<index>.slopeTable*` 表示坡度变化数据表；当 `slopeMode=VariableByStation` 时，固定坡度只保留兼容字段，不作为编辑和图形显示来源。
- `component.<index>.height` 为旧高度差兼容字段，不再作为用户可编辑参数；新响应中按 `0` 写回。
- `component.<index>.hasInnerCurb`、`innerCurbWidth`、`innerCurbHeight`、`innerCurbEmbedDepth` 表示内侧路缘石。
- `component.<index>.hasOuterCurb`、`outerCurbWidth`、`outerCurbHeight`、`outerCurbEmbedDepth` 表示外侧路缘石。

## 响应文件

WPF 写入响应文件后，发送：

```text
RD_SECTION_SUBGRADE_TEMPLATE_APPLY_DIALOG_FILE "<responsePath>"
```

C++ 读取响应：

- `accepted=0`：取消，不修改 DWG。
- `accepted=1` 且 `handle` 为空：创建新 `DnSubgradeTemplateEntity`。
- `accepted=1` 且 `handle` 非空：按句柄回写已有实体。

## 数据边界

- 桥接层只负责数据转换和命令转发。
- C++ 写入道路等级、侧别、部件类型和坡度方式等枚举字段时，必须写入文本编码，不得让字符串指针被重载解析为布尔值后写成 `1`；否则 WPF 会把 `1` 误解析为一级道路、右侧、行车道或变化坡度。
- 默认模板、参数校验、DWG 持久化和实体绘制由 C++ domain/application/cad_adapter 承担。
- WPF 只负责输入、选择、预览和二级表格编辑。

## 2026-06-23 更新

- 请求和响应文件新增内外侧路缘石字段，字段缺失时按未启用路缘石兼容读取。
- C++ 回写后会通过 `SubgradeTemplateRules::normalize` 校验路缘石宽度、高度和埋深必须为非负有限值；未启用的一侧尺寸会归零。
- 路缘石宽度只影响预览和实体叠画，不扣减 `component.<index>.width`。
- 旧 `component.<index>.height` 字段不再驱动部件高差；部件高差由内外侧路缘石高度派生。
- WPF 预览和 CAD 实体绘制按部件颜色填充路缘石并增加白色描边，路缘石顶部与当前部件顶部一致。
