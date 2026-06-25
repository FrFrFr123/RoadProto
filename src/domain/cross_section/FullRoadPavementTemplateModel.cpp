#include "domain/cross_section/FullRoadPavementTemplateModel.h"

#include <algorithm>
#include <cmath>
#include <map>
#include <utility>

namespace roadproto::domain::cross_section {
namespace {

bool isPositiveFinite(double value)
{
    return std::isfinite(value) && value > 0.0;
}

void clearSinglePartPavementReference(SubgradeTemplateComponent& component)
{
    component.pavementLayerLinked = false;
    component.pavementLayerHandle.clear();
    component.pavementLayerName.clear();
    component.pavementLayerThickness = 0.0;
}

PavementLayerTemplateData emptyPavementForComponent(const SubgradeTemplateComponent& component)
{
    PavementLayerTemplateData pavement;
    pavement.properties.name = std::wstring(subgradeComponentTypeDisplayName(component.type)) + L"\u7ed3\u6784\u5c42";
    pavement.properties.displayScale = 10.0;
    pavement.properties.previewWidth = std::max(0.1, component.width);
    return pavement;
}

std::vector<SubgradeTemplateComponent> normalizedSubgradeComponents(
    const std::vector<SubgradeTemplateComponent>& components,
    std::wstring& errorMessage)
{
    SubgradeTemplateData data;
    data.properties.displayScale = 10.0;
    data.components = components;
    for (auto& component : data.components) {
        clearSinglePartPavementReference(component);
    }

    if (!SubgradeTemplateRules::normalize(data, errorMessage)) {
        return {};
    }

    return data.components;
}

void assignComponentKeys(std::vector<FullRoadPavementComponentSnapshot>& components)
{
    std::map<std::pair<SubgradeSide, SubgradeComponentType>, int> ordinals;
    for (auto& component : components) {
        const auto keyBase = std::make_pair(component.subgrade.side, component.subgrade.type);
        component.key.side = component.subgrade.side;
        component.key.type = component.subgrade.type;
        component.key.sameSideTypeOrdinal = ordinals[keyBase]++;
    }
}

const FullRoadPavementComponentSnapshot* findByKey(
    const std::vector<FullRoadPavementComponentSnapshot>& components,
    const FullRoadPavementComponentKey& key)
{
    const auto found = std::find_if(components.begin(), components.end(), [&](const auto& component) {
        return component.key == key;
    });
    return found == components.end() ? nullptr : &*found;
}

} // namespace

bool operator==(const FullRoadPavementComponentKey& lhs, const FullRoadPavementComponentKey& rhs)
{
    return lhs.side == rhs.side
        && lhs.type == rhs.type
        && lhs.sameSideTypeOrdinal == rhs.sameSideTypeOrdinal;
}

FullRoadPavementTemplateData FullRoadPavementTemplateDefaults::create()
{
    FullRoadPavementTemplateData data;
    data.properties.name = L"\u6574\u5e45\u8def\u8def\u9762\u7ed3\u6784\u5c42\u6a21\u677f";
    data.properties.displayScale = 10.0;
    data.properties.referenceRoadGrade = RoadGrade::Expressway;
    return data;
}

FullRoadPavementTemplateData FullRoadPavementTemplateRules::createFromSubgradeSnapshot(
    const SubgradeTemplateData& subgrade,
    const std::wstring& referenceHandle,
    const std::wstring& referenceName)
{
    FullRoadPavementTemplateData data = FullRoadPavementTemplateDefaults::create();
    data.properties.displayScale = subgrade.properties.displayScale;
    data.properties.referenceSubgradeTemplateHandle = referenceHandle;
    data.properties.referenceSubgradeTemplateName = referenceName.empty()
        ? subgrade.properties.name
        : referenceName;
    data.properties.referenceRoadGrade = subgrade.properties.roadGrade;

    std::wstring errorMessage;
    const auto normalizedComponents = normalizedSubgradeComponents(subgrade.components, errorMessage);
    data.components.reserve(normalizedComponents.size());
    for (const auto& component : normalizedComponents) {
        FullRoadPavementComponentSnapshot snapshot;
        snapshot.subgrade = component;
        snapshot.pavement = emptyPavementForComponent(component);
        data.components.push_back(std::move(snapshot));
    }
    assignComponentKeys(data.components);

    normalize(data, errorMessage);
    return data;
}

FullRoadPavementTemplateData FullRoadPavementTemplateRules::refreshFromSubgradeSnapshot(
    const FullRoadPavementTemplateData& existing,
    const SubgradeTemplateData& subgrade,
    const std::wstring& referenceHandle,
    const std::wstring& referenceName)
{
    auto refreshed = createFromSubgradeSnapshot(subgrade, referenceHandle, referenceName);
    for (auto& component : refreshed.components) {
        if (const auto* previous = findByKey(existing.components, component.key)) {
            component.pavement = previous->pavement;
        }
    }

    std::wstring errorMessage;
    normalize(refreshed, errorMessage);
    return refreshed;
}

bool FullRoadPavementTemplateRules::normalize(
    FullRoadPavementTemplateData& data,
    std::wstring& errorMessage)
{
    errorMessage.clear();
    if (data.properties.name.empty()) {
        data.properties.name = L"\u6574\u5e45\u8def\u8def\u9762\u7ed3\u6784\u5c42\u6a21\u677f";
    }
    if (!SubgradeTemplateRules::isSupportedDisplayScale(data.properties.displayScale)) {
        errorMessage = L"Full road pavement template display scale must be 1, 10, 20, 50, or 100.";
        return false;
    }

    std::vector<SubgradeTemplateComponent> subgradeComponents;
    subgradeComponents.reserve(data.components.size());
    for (auto& component : data.components) {
        clearSinglePartPavementReference(component.subgrade);
        subgradeComponents.push_back(component.subgrade);
    }

    auto normalizedComponents = normalizedSubgradeComponents(subgradeComponents, errorMessage);
    if (!errorMessage.empty()) {
        return false;
    }

    for (std::size_t index = 0; index < data.components.size(); ++index) {
        data.components[index].subgrade = normalizedComponents[index];
        auto& pavement = data.components[index].pavement;
        if (pavement.properties.name.empty()) {
            pavement.properties.name = std::wstring(subgradeComponentTypeDisplayName(data.components[index].subgrade.type)) + L"\u7ed3\u6784\u5c42";
        }
        if (!isPositiveFinite(pavement.properties.displayScale)) {
            pavement.properties.displayScale = data.properties.displayScale;
        }
        if (!isPositiveFinite(pavement.properties.previewWidth)) {
            pavement.properties.previewWidth = std::max(0.1, data.components[index].subgrade.width);
        }
        if (!pavement.layers.empty() &&
            !PavementLayerTemplateRules::normalize(pavement, errorMessage)) {
            return false;
        }
    }

    assignComponentKeys(data.components);
    return true;
}

std::vector<std::size_t> FullRoadPavementTemplateRules::componentDisplayOrder(
    const FullRoadPavementTemplateData& data)
{
    std::vector<std::size_t> order;
    order.reserve(data.components.size());

    for (auto index = data.components.size(); index > 0; --index) {
        const auto componentIndex = index - 1;
        if (data.components[componentIndex].key.side == SubgradeSide::Left) {
            order.push_back(componentIndex);
        }
    }
    for (std::size_t index = 0; index < data.components.size(); ++index) {
        if (data.components[index].key.side == SubgradeSide::Right) {
            order.push_back(index);
        }
    }
    return order;
}

} // namespace roadproto::domain::cross_section
