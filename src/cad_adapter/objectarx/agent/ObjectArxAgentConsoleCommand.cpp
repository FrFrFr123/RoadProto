#include "cad_adapter/objectarx/agent/ObjectArxAgentConsoleCommand.h"

#ifndef ROADPROTO_TEST_BUILD
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <Windows.h>
#include "acdocman.h"
#endif

#include <algorithm>
#include <string>

namespace roadproto::cad_adapter::objectarx::agent {
namespace {

#ifndef ROADPROTO_TEST_BUILD
std::wstring parentDirectory(const std::wstring& path)
{
    const auto index = path.find_last_of(L"\\/");
    return index == std::wstring::npos ? L"" : path.substr(0, index);
}

std::wstring fileName(const std::wstring& path)
{
    const auto index = path.find_last_of(L"\\/");
    return index == std::wstring::npos ? path : path.substr(index + 1);
}

std::wstring currentModulePath()
{
    HMODULE module = nullptr;
    if (!GetModuleHandleExW(
            GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCWSTR>(&currentModulePath),
            &module)) {
        return L"";
    }

    wchar_t buffer[MAX_PATH] = {};
    if (GetModuleFileNameW(module, buffer, MAX_PATH) == 0) {
        return L"";
    }
    return buffer;
}

std::wstring findManagedRibbonPlugin()
{
    const auto modulePath = currentModulePath();
    const auto arxDirectory = parentDirectory(modulePath);
    if (arxDirectory.empty()) {
        return L"";
    }

    const auto configuration = fileName(arxDirectory);
    const auto x64Directory = parentDirectory(arxDirectory);
    const auto artifactsDirectory = parentDirectory(x64Directory);
    if (configuration.empty() || artifactsDirectory.empty()) {
        return L"";
    }

    const auto candidate = artifactsDirectory
        + L"\\managed\\" + configuration + L"\\net48\\RoadProto.Terrain.UI.dll";
    if (GetFileAttributesW(candidate.c_str()) != INVALID_FILE_ATTRIBUTES) {
        return candidate;
    }
    return L"";
}

std::wstring lispStringPath(std::wstring path)
{
    std::replace(path.begin(), path.end(), L'\\', L'/');
    return path;
}
#endif

void showAgentConsole()
{
#ifndef ROADPROTO_TEST_BUILD
    if (acDocManager == nullptr || acDocManager->curDocument() == nullptr) {
        return;
    }

    const auto pluginPath = findManagedRibbonPlugin();
    const auto command = pluginPath.empty()
        ? std::wstring(L"RD_AGENT_CONSOLE_UI ")
        : L"(command \"_.NETLOAD\" \"" + lispStringPath(pluginPath)
            + L"\")(command \"RD_AGENT_CONSOLE_UI\") ";

    acDocManager->sendStringToExecute(
        acDocManager->curDocument(),
        command.c_str(),
        true,
        false,
        false);
#endif
}

void printAgentHealth()
{
}

void printAgentLogs()
{
}

} // namespace

core::CommandProcedure agentConsoleCommandProcedure()
{
    return &showAgentConsole;
}

core::CommandProcedure agentHealthCommandProcedure()
{
    return &printAgentHealth;
}

core::CommandProcedure agentLogsCommandProcedure()
{
    return &printAgentLogs;
}

} // namespace roadproto::cad_adapter::objectarx::agent
