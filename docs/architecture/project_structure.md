# 推荐目录结构

```text
src/
  app/
    arx_entry/
    startup/
  core/
    command/
    module/
    logging/
    config/
    error/
    version/
  cad_adapter/
    objectarx/
      agent/
    transaction/
    selection/
    entity/
    layer/
    annotation/
    block/
    geometry/
  domain/
    agent/
    common/
    geometry/
    road/
    terrain/
    alignment/
    intersection/
    profile/
    cross_section/
    quantity/
    relation/
  application/
    agent/
    terrain/
    alignment/
    intersection/
    profile/
    cross_section/
    drawing_quantity/
  modules/
    agent/
    terrain/
    alignment/
    interchange/
    intersection/
    profile/
    cross_section/
    drawing_quantity/
    utils/
  ui/
    ribbon/
    dialogs/
    wpf/
      RoadProto.Terrain.UI/
        AutoCad/                         # AutoCAD 托管命令、Ribbon 入口和双击转发
        Bridge/                          # WPF 与 C++ Bridge DTO 和请求/响应文件
        ViewModels/
        AgentConsolePalette.xaml         # 规划中的可停靠 Agent Console
    resources/
docs/
  agent/
  agent_builder/
  architecture/
  business/
  rules/
  modules/
  reuse/
  dev/
assets/
  icons/
artifacts/
  x64/
    Debug/
    Release/
  managed/
    Debug/
    Release/
samples/
tests/
third_party/
  delaunator-cpp/
```

部分模块目录目前只是预留。这样做是为了后续新增命令时不必重新调整仓库结构。

## 独立 Agent 后端仓库

可控工程 Agent 后端不放在 RoadProto 仓库内，固定规划为独立仓库：

```text
F:\0_GPT_RoadProtoAgentBackend
```

该仓库使用 `.NET 8 / ASP.NET Core`，承载 Agent 编排、任务状态机、模型网关、Credential、Tool Registry、Trace、日志和评测。RoadProto 仓库只保留本地 `AGENT` 薄模块、WPF 可停靠面板、HTTP 客户端、CAD Adapter 和接口契约文档。
