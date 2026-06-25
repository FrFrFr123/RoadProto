#pragma once

#include "core/command/CommandRegistry.h"

namespace roadproto::cad_adapter::objectarx::cross_section {

core::CommandProcedure fullRoadPavementTemplateCreateCommandProcedure();
core::CommandProcedure fullRoadPavementTemplateEditHandleCommandProcedure();
core::CommandProcedure fullRoadPavementTemplateApplyDialogFileCommandProcedure();

} // namespace roadproto::cad_adapter::objectarx::cross_section
