#pragma once

// Windows SDK 10.0.26100 declares DllGetClassObject and DllCanUnloadNow.
// Atlas Drop implements and exports those same COM entry points in its own DLL.
// Rename only the SDK declarations while the Windows headers are parsed, then
// restore the standard names before AtlasDropContextMenu.cpp is compiled.
#define DllGetClassObject AtlasDrop_WindowsSdk_DllGetClassObject
#define DllCanUnloadNow AtlasDrop_WindowsSdk_DllCanUnloadNow

#include <windows.h>
#include <shlobj_core.h>
#include <shobjidl_core.h>
#include <shellapi.h>
#include <shlwapi.h>

#undef DllGetClassObject
#undef DllCanUnloadNow
