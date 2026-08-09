#include <windows.h>
#include <shlobj_core.h>
#include <shobjidl_core.h>
#include <shellapi.h>
#include <shlwapi.h>
#include <new>
#include <string>

#pragma comment(lib, "Ole32.lib")
#pragma comment(lib, "Shell32.lib")
#pragma comment(lib, "Shlwapi.lib")

namespace
{
    LONG g_objectCount = 0;
    LONG g_lockCount = 0;

    // Must stay identical to the CLSID declared in the sparse-package manifest.
    const CLSID CLSID_AtlasDropCommand =
    { 0x6b1c4c31, 0x7f71, 0x4c75, { 0x9e, 0x16, 0x8d, 0x3f, 0xb7, 0x51, 0x1e, 0x34 } };

    std::wstring GetAtlasDropExecutablePath()
    {
        PWSTR localAppData = nullptr;
        if (FAILED(SHGetKnownFolderPath(FOLDERID_LocalAppData, KF_FLAG_DEFAULT, nullptr, &localAppData)) ||
            localAppData == nullptr)
        {
            return {};
        }

        std::wstring path(localAppData);
        CoTaskMemFree(localAppData);
        path += L"\\AtlasDrop\\AtlasDrop.App.exe";
        return path;
    }

    class AtlasDropCommand final : public IExplorerCommand
    {
    public:
        AtlasDropCommand()
        {
            InterlockedIncrement(&g_objectCount);
        }

        ~AtlasDropCommand() override
        {
            InterlockedDecrement(&g_objectCount);
        }

        IFACEMETHODIMP QueryInterface(REFIID iid, void** result) override
        {
            if (result == nullptr)
                return E_POINTER;

            *result = nullptr;
            if (IsEqualIID(iid, IID_IUnknown) || IsEqualIID(iid, IID_IExplorerCommand))
            {
                *result = static_cast<IExplorerCommand*>(this);
                AddRef();
                return S_OK;
            }

            return E_NOINTERFACE;
        }

        IFACEMETHODIMP_(ULONG) AddRef() override
        {
            return InterlockedIncrement(&_refCount);
        }

        IFACEMETHODIMP_(ULONG) Release() override
        {
            const auto count = InterlockedDecrement(&_refCount);
            if (count == 0)
                delete this;
            return count;
        }

        IFACEMETHODIMP GetTitle(IShellItemArray*, PWSTR* title) override
        {
            if (title == nullptr)
                return E_POINTER;
            return SHStrDupW(L"Ranger avec Atlas Drop", title);
        }

        IFACEMETHODIMP GetIcon(IShellItemArray*, PWSTR* icon) override
        {
            if (icon == nullptr)
                return E_POINTER;

            const auto executable = GetAtlasDropExecutablePath();
            if (executable.empty() || GetFileAttributesW(executable.c_str()) == INVALID_FILE_ATTRIBUTES)
            {
                *icon = nullptr;
                return E_NOTIMPL;
            }

            return SHStrDupW(executable.c_str(), icon);
        }

        IFACEMETHODIMP GetToolTip(IShellItemArray*, PWSTR* toolTip) override
        {
            if (toolTip == nullptr)
                return E_POINTER;
            return SHStrDupW(L"Classer cet élément avec Atlas Drop", toolTip);
        }

        IFACEMETHODIMP GetCanonicalName(GUID* canonicalName) override
        {
            if (canonicalName == nullptr)
                return E_POINTER;
            *canonicalName = CLSID_AtlasDropCommand;
            return S_OK;
        }

        IFACEMETHODIMP GetState(IShellItemArray* items, BOOL, EXPCMDSTATE* state) override
        {
            if (state == nullptr)
                return E_POINTER;

            *state = ECS_HIDDEN;
            if (items == nullptr)
                return S_OK;

            DWORD count = 0;
            if (SUCCEEDED(items->GetCount(&count)) && count == 1)
                *state = ECS_ENABLED;

            return S_OK;
        }

        IFACEMETHODIMP Invoke(IShellItemArray* items, IBindCtx*) override
        {
            if (items == nullptr)
                return E_INVALIDARG;

            DWORD count = 0;
            if (FAILED(items->GetCount(&count)) || count != 1)
                return E_INVALIDARG;

            IShellItem* item = nullptr;
            auto result = items->GetItemAt(0, &item);
            if (FAILED(result) || item == nullptr)
                return FAILED(result) ? result : E_FAIL;

            PWSTR selectedPath = nullptr;
            result = item->GetDisplayName(SIGDN_FILESYSPATH, &selectedPath);
            item->Release();
            if (FAILED(result) || selectedPath == nullptr)
                return FAILED(result) ? result : E_FAIL;

            const auto executable = GetAtlasDropExecutablePath();
            if (executable.empty() || GetFileAttributesW(executable.c_str()) == INVALID_FILE_ATTRIBUTES)
            {
                CoTaskMemFree(selectedPath);
                return HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND);
            }

            std::wstring arguments = L"\"";
            arguments += selectedPath;
            arguments += L"\"";
            CoTaskMemFree(selectedPath);

            const auto shellResult = ShellExecuteW(
                nullptr,
                L"open",
                executable.c_str(),
                arguments.c_str(),
                nullptr,
                SW_SHOWNORMAL);

            return reinterpret_cast<INT_PTR>(shellResult) > 32 ? S_OK : E_FAIL;
        }

        IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override
        {
            if (flags == nullptr)
                return E_POINTER;
            *flags = ECF_DEFAULT;
            return S_OK;
        }

        IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** commands) override
        {
            if (commands == nullptr)
                return E_POINTER;
            *commands = nullptr;
            return E_NOTIMPL;
        }

    private:
        LONG _refCount = 1;
    };

    class AtlasDropClassFactory final : public IClassFactory
    {
    public:
        IFACEMETHODIMP QueryInterface(REFIID iid, void** result) override
        {
            if (result == nullptr)
                return E_POINTER;

            *result = nullptr;
            if (IsEqualIID(iid, IID_IUnknown) || IsEqualIID(iid, IID_IClassFactory))
            {
                *result = static_cast<IClassFactory*>(this);
                AddRef();
                return S_OK;
            }

            return E_NOINTERFACE;
        }

        IFACEMETHODIMP_(ULONG) AddRef() override
        {
            return InterlockedIncrement(&_refCount);
        }

        IFACEMETHODIMP_(ULONG) Release() override
        {
            const auto count = InterlockedDecrement(&_refCount);
            if (count == 0)
                delete this;
            return count;
        }

        IFACEMETHODIMP CreateInstance(IUnknown* outer, REFIID iid, void** result) override
        {
            if (result == nullptr)
                return E_POINTER;
            *result = nullptr;

            if (outer != nullptr)
                return CLASS_E_NOAGGREGATION;

            auto* command = new (std::nothrow) AtlasDropCommand();
            if (command == nullptr)
                return E_OUTOFMEMORY;

            const auto hr = command->QueryInterface(iid, result);
            command->Release();
            return hr;
        }

        IFACEMETHODIMP LockServer(BOOL lock) override
        {
            if (lock)
                InterlockedIncrement(&g_lockCount);
            else
                InterlockedDecrement(&g_lockCount);
            return S_OK;
        }

    private:
        LONG _refCount = 1;
    };
}

extern "C" HRESULT __declspec(dllexport) __stdcall DllGetClassObject(
    REFCLSID classId,
    REFIID iid,
    void** result)
{
    if (!IsEqualCLSID(classId, CLSID_AtlasDropCommand))
        return CLASS_E_CLASSNOTAVAILABLE;

    auto* factory = new (std::nothrow) AtlasDropClassFactory();
    if (factory == nullptr)
        return E_OUTOFMEMORY;

    const auto hr = factory->QueryInterface(iid, result);
    factory->Release();
    return hr;
}

extern "C" HRESULT __declspec(dllexport) __stdcall DllCanUnloadNow()
{
    return g_objectCount == 0 && g_lockCount == 0 ? S_OK : S_FALSE;
}

BOOL APIENTRY DllMain(HMODULE, DWORD, LPVOID)
{
    return TRUE;
}
