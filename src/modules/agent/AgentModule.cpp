#include "modules/agent/AgentModule.h"

#include "cad_adapter/objectarx/agent/ObjectArxAgentConsoleCommand.h"
#include "cad_adapter/objectarx/agent/ObjectArxAgentSubgradeTemplateToolCommand.h"
#include "ui/ribbon/RibbonModel.h"

namespace roadproto::modules::agent {
namespace {

void registerAgentCommands(core::CommandRegistry& commandRegistry)
{
    commandRegistry.registerCommand(core::CommandDefinition{
        L"RD_AGENT_CONSOLE",
        L"Agent 控制台",
        L"AGENT",
        L"Opens the dockable RoadProto Agent console.",
        cad_adapter::objectarx::agent::agentConsoleCommandProcedure(),
        true,
        true,
        L"docs/business/agent/Agent控制台_MVP.md",
        true});

    commandRegistry.registerCommand(core::CommandDefinition{
        L"RD_AGENT_HEALTH",
        L"Agent 后端健康检查",
        L"AGENT",
        L"Prints the RoadProto Agent backend health and local diagnostic paths.",
        cad_adapter::objectarx::agent::agentHealthCommandProcedure(),
        true,
        true,
        L"docs/business/agent/Agent控制台_MVP.md",
        false});

    commandRegistry.registerCommand(core::CommandDefinition{
        L"RD_AGENT_LOGS",
        L"Agent 日志目录",
        L"AGENT",
        L"Prints or opens RoadProto Agent diagnostic log locations.",
        cad_adapter::objectarx::agent::agentLogsCommandProcedure(),
        true,
        true,
        L"docs/business/agent/Agent控制台_MVP.md",
        false});

    commandRegistry.registerCommand(core::CommandDefinition{
        L"RD_AGENT_SUBGRADE_TEMPLATE_TOOL_FILE",
        L"Agent 路基模板工具",
        L"AGENT",
        L"Executes a RoadProto Agent subgrade template tool request file.",
        cad_adapter::objectarx::agent::agentSubgradeTemplateToolFileCommandProcedure(),
        true,
        true,
        L"docs/business/agent/路基模板Skill_增删改查_MVP.md",
        false});
}

void registerAgentRibbon(ui::RibbonModel& ribbonModel)
{
    ribbonModel.ensurePanel(L"AGENT", L"Agent");
}

} // namespace

core::ModuleDefinition createAgentModule()
{
    return core::ModuleDefinition{
        L"Agent 控制台",
        L"AGENT",
        L"Dockable controlled engineering Agent integration module.",
        []() { return true; },
        []() { return true; },
        &registerAgentCommands,
        &registerAgentRibbon,
        L"docs/modules/agent.md"};
}

} // namespace roadproto::modules::agent
