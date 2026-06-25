#include "application/cross_section/FullRoadPavementTemplateCreateService.h"

namespace roadproto::application::cross_section {

FullRoadPavementTemplateCreateResult FullRoadPavementTemplateCreateService::create(
    const FullRoadPavementTemplateCreateInput& input) const
{
    FullRoadPavementTemplateCreateResult result;
    result.data = domain::cross_section::FullRoadPavementTemplateDefaults::create();
    if (!input.name.empty()) {
        result.data.properties.name = input.name;
    }
    result.data.properties.displayScale = input.displayScale;

    std::wstring errorMessage;
    if (!domain::cross_section::FullRoadPavementTemplateRules::normalize(result.data, errorMessage)) {
        result.errorMessage = errorMessage;
        return result;
    }

    result.succeeded = true;
    return result;
}

} // namespace roadproto::application::cross_section
