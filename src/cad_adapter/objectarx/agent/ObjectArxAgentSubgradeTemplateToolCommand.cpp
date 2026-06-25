#include "cad_adapter/objectarx/agent/ObjectArxAgentSubgradeTemplateToolCommand.h"

#include <filesystem>
#include <fstream>
#include <map>
#include <sstream>
#include <string>

#ifndef ROADPROTO_TEST_BUILD
#include "aced.h"
#include "adscodes.h"
#endif

namespace roadproto::cad_adapter::objectarx::agent {
namespace {

std::wstring trimCommandPath(const wchar_t* raw)
{
    std::wstring value = raw == nullptr ? L"" : raw;
    while (!value.empty() && (value.front() == L'"' || value.front() == L' ' || value.front() == L'\t')) {
        value.erase(value.begin());
    }
    while (!value.empty() && (value.back() == L'"' || value.back() == L' ' || value.back() == L'\t')) {
        value.pop_back();
    }
    return value;
}

std::string unescapeValue(const std::string& value)
{
    std::ostringstream stream;
    for (std::size_t i = 0; i < value.size(); ++i) {
        if (value[i] == '%' && i + 2 < value.size()) {
            const auto hex = value.substr(i + 1, 2);
            char* end = nullptr;
            const auto code = std::strtol(hex.c_str(), &end, 16);
            if (end != nullptr && *end == '\0') {
                stream << static_cast<char>(code);
                i += 2;
                continue;
            }
        }
        stream << value[i];
    }
    return stream.str();
}

std::string escapeValue(const std::string& value)
{
    std::ostringstream stream;
    for (const auto ch : value) {
        if (ch == '\n') {
            stream << "%0A";
        } else if (ch == '\r') {
            stream << "%0D";
        } else if (ch == '%' || ch == '=') {
            stream << '%' << std::uppercase << std::hex << static_cast<int>(static_cast<unsigned char>(ch)) << std::nouppercase << std::dec;
        } else {
            stream << ch;
        }
    }
    return stream.str();
}

std::map<std::string, std::string> readRequest(const std::wstring& path)
{
    std::ifstream stream{std::filesystem::path(path)};
    std::map<std::string, std::string> values;
    std::string line;
    while (std::getline(stream, line)) {
        const auto separator = line.find('=');
        if (separator == std::string::npos || separator == 0) {
            continue;
        }
        values[line.substr(0, separator)] = unescapeValue(line.substr(separator + 1));
    }
    return values;
}

std::string getValue(const std::map<std::string, std::string>& values, const std::string& key)
{
    const auto item = values.find(key);
    return item == values.end() ? std::string{} : item->second;
}

void writeResult(
    const std::string& resultPath,
    bool succeeded,
    const std::string& entityId,
    const std::string& templateName,
    const std::string& message)
{
    if (resultPath.empty()) {
        return;
    }

    std::ofstream stream(resultPath, std::ios::trunc);
    stream << "succeeded=" << (succeeded ? "1" : "0") << '\n';
    stream << "entityId=" << escapeValue(entityId) << '\n';
    stream << "templateName=" << escapeValue(templateName) << '\n';
    stream << "message=" << escapeValue(message) << '\n';
}

void runAgentSubgradeTemplateToolFileCommand()
{
#ifndef ROADPROTO_TEST_BUILD
    ACHAR pathBuffer[1024] = {};
    if (acedGetString(Adesk::kTrue, L"\nRoadProto Agent subgrade template tool request file: ", pathBuffer) != RTNORM) {
        return;
    }
    const auto requestPath = trimCommandPath(pathBuffer);
    const auto values = readRequest(requestPath);
    const auto resultPath = getValue(values, "resultPath");
    const auto skillId = getValue(values, "skillId");
    const auto operation = getValue(values, "operation");
    const auto targetHandle = getValue(values, "targetHandle");
    const auto targetName = getValue(values, "targetName");
    const auto templateName = getValue(values, "templateName");

    if (skillId != "subgrade_template") {
        writeResult(resultPath, false, targetHandle, templateName, "skillId 不是 subgrade_template，已拒绝执行。");
        return;
    }

    if (operation != "modify" && operation != "delete" && operation != "query") {
        writeResult(resultPath, false, targetHandle, templateName, "未知路基模板工具操作。");
        return;
    }

    if ((operation == "modify" || operation == "delete") && targetHandle.empty() && targetName.empty()) {
        writeResult(resultPath, false, targetHandle, templateName, "缺少目标路基模板。");
        return;
    }

    writeResult(resultPath, false, targetHandle, templateName, "路基模板 Agent CRUD 原生命令入口已接收请求，实体读写将在下一步接入。");
#endif
}

} // namespace

core::CommandProcedure agentSubgradeTemplateToolFileCommandProcedure()
{
    return &runAgentSubgradeTemplateToolFileCommand;
}

} // namespace roadproto::cad_adapter::objectarx::agent
