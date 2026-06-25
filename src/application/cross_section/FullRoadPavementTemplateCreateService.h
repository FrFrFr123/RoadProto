#pragma once

#include "domain/cross_section/FullRoadPavementTemplateModel.h"

#include <string>

namespace roadproto::application::cross_section {

struct FullRoadPavementTemplateCreateInput {
    std::wstring name;
    double displayScale = 10.0;
};

struct FullRoadPavementTemplateCreateResult {
    bool succeeded = false;
    std::wstring errorMessage;
    domain::cross_section::FullRoadPavementTemplateData data;
};

class FullRoadPavementTemplateCreateService {
public:
    FullRoadPavementTemplateCreateResult create(const FullRoadPavementTemplateCreateInput& input) const;
};

} // namespace roadproto::application::cross_section
