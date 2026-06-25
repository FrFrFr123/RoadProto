#pragma once

#include "domain/cross_section/FullRoadPavementTemplateModel.h"

#ifndef ROADPROTO_TEST_BUILD
#include "gept3dar.h"
#endif

#include <string>

namespace roadproto::cad_adapter::objectarx::cross_section {

enum class FullRoadPavementTemplateDialogAction {
    None,
    PickReferenceSubgradeTemplate
};

struct FullRoadPavementTemplateDialogRequest {
    std::wstring handle;
    std::wstring responsePath;
#ifndef ROADPROTO_TEST_BUILD
    AcGePoint3d insertionPoint;
#endif
    int currentComponentIndex = -1;
    bool applyDefaultPresets = false;
    roadproto::domain::cross_section::FullRoadPavementTemplateData data;
};

struct FullRoadPavementTemplateDialogResponse {
    bool accepted = false;
    FullRoadPavementTemplateDialogAction action = FullRoadPavementTemplateDialogAction::None;
    std::wstring handle;
#ifndef ROADPROTO_TEST_BUILD
    AcGePoint3d insertionPoint;
#endif
    int currentComponentIndex = -1;
    roadproto::domain::cross_section::FullRoadPavementTemplateData data;
};

const wchar_t* fullRoadPavementTemplateDialogActionCode(FullRoadPavementTemplateDialogAction action);

FullRoadPavementTemplateDialogAction fullRoadPavementTemplateDialogActionFromCode(
    const std::wstring& code);

bool queueFullRoadPavementTemplateWpfDialog(
    const FullRoadPavementTemplateDialogRequest& request,
    std::wstring& errorMessage);

bool readFullRoadPavementTemplateDialogResponse(
    const std::wstring& responsePath,
    FullRoadPavementTemplateDialogResponse& response,
    std::wstring& errorMessage);

} // namespace roadproto::cad_adapter::objectarx::cross_section
