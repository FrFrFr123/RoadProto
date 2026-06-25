#pragma once

#include "core/command/CommandRegistry.h"

namespace roadproto::cad_adapter::objectarx::agent {

core::CommandProcedure agentConsoleCommandProcedure();
core::CommandProcedure agentHealthCommandProcedure();
core::CommandProcedure agentLogsCommandProcedure();

} // namespace roadproto::cad_adapter::objectarx::agent
