#pragma once

#include "domain/cross_section/PavementLayerTemplateModel.h"
#include "domain/cross_section/SubgradeTemplateModel.h"

#include <cstddef>
#include <string>
#include <vector>

namespace roadproto::domain::cross_section {

struct FullRoadPavementTemplateProperties {
    std::wstring name = L"\u6574\u5e45\u8def\u8def\u9762\u7ed3\u6784\u5c42\u6a21\u677f";
    double displayScale = 10.0;
    std::wstring referenceSubgradeTemplateHandle;
    std::wstring referenceSubgradeTemplateName;
    RoadGrade referenceRoadGrade = RoadGrade::Expressway;
};

struct FullRoadPavementComponentKey {
    SubgradeSide side = SubgradeSide::Right;
    SubgradeComponentType type = SubgradeComponentType::TravelLane;
    int sameSideTypeOrdinal = 0;
};

bool operator==(const FullRoadPavementComponentKey& lhs, const FullRoadPavementComponentKey& rhs);

struct FullRoadPavementComponentSnapshot {
    FullRoadPavementComponentKey key;
    SubgradeTemplateComponent subgrade;
    PavementLayerTemplateData pavement;
};

struct FullRoadPavementTemplateData {
    FullRoadPavementTemplateProperties properties;
    std::vector<FullRoadPavementComponentSnapshot> components;
};

class FullRoadPavementTemplateDefaults {
public:
    static FullRoadPavementTemplateData create();
};

class FullRoadPavementTemplateRules {
public:
    static FullRoadPavementTemplateData createFromSubgradeSnapshot(
        const SubgradeTemplateData& subgrade,
        const std::wstring& referenceHandle,
        const std::wstring& referenceName);

    static FullRoadPavementTemplateData refreshFromSubgradeSnapshot(
        const FullRoadPavementTemplateData& existing,
        const SubgradeTemplateData& subgrade,
        const std::wstring& referenceHandle,
        const std::wstring& referenceName);

    static bool normalize(FullRoadPavementTemplateData& data, std::wstring& errorMessage);

    static std::vector<std::size_t> componentDisplayOrder(const FullRoadPavementTemplateData& data);
};

} // namespace roadproto::domain::cross_section
