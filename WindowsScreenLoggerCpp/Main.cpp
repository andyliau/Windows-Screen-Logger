#include <windows.h>
#include <gdiplus.h>
#include <shellapi.h>
#include <string>
#include <filesystem>
#include <chrono>
#include <thread>
#include "resource.h"
#include <commctrl.h>
#include "SettingsDialog.h"

#pragma comment (lib,"Gdiplus.lib")

using namespace Gdiplus;
using namespace std::chrono_literals;
namespace fs = std::filesystem;

HINSTANCE hInst;
NOTIFYICONDATA nid;
UINT_PTR timerId;
bool isSessionLocked = false;
int captureInterval = 5; // Default capture interval in seconds
int imageSizePercentage = 100; // Default image size percentage
int imageQuality = 30; // Default image quality
bool startWithWindows = false;

std::wstring GetSavePath();
void CaptureScreen();
void CALLBACK TimerProc(HWND hWnd, UINT uMsg, UINT_PTR idEvent, DWORD dwTime);
void ShowSettings();
void OpenSaveFolder();
void Exit();
LRESULT CALLBACK WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam);
int GetEncoderClsid(const WCHAR* format, CLSID* pClsid);

std::wstring GetSavePath() {
    wchar_t* userProfile;
    size_t len;
    _wdupenv_s(&userProfile, &len, L"USERPROFILE");

    // Get current time
    auto now = std::chrono::system_clock::now();
    std::time_t now_c = std::chrono::system_clock::to_time_t(now);
    std::tm local_tm;
    localtime_s(&local_tm, &now_c);

    // Format date as YYYY-MM-DD
    wchar_t dateStr[11];
    wcsftime(dateStr, sizeof(dateStr), L"%Y-%m-%d", &local_tm);

    std::wstring path = std::wstring(userProfile) + L"\\WindowsScreenLogger\\" + dateStr;
    fs::create_directories(path);
    return path;
}

void CaptureScreen() {
    if (isSessionLocked) return;

    // Set DPI awareness
    SetProcessDPIAware();

    // Get the total width and height of the virtual screen
    int totalWidth = GetSystemMetrics(SM_CXVIRTUALSCREEN);
    int totalHeight = GetSystemMetrics(SM_CYVIRTUALSCREEN);
    int left = GetSystemMetrics(SM_XVIRTUALSCREEN);
    int top = GetSystemMetrics(SM_YVIRTUALSCREEN);

    // Initialize GDI+ objects with RAII
    HDC hdcScreen = nullptr;
    HDC hdcMem = nullptr;
    HBITMAP hbmScreen = nullptr;

    try {
        // Get the screen DC
        hdcScreen = GetDC(NULL);
        if (!hdcScreen) throw std::runtime_error("Failed to get screen DC");

        // Create a compatible DC
        hdcMem = CreateCompatibleDC(hdcScreen);
        if (!hdcMem) {
            ReleaseDC(NULL, hdcScreen);
            throw std::runtime_error("Failed to create compatible DC");
        }

        // Create a compatible bitmap
        hbmScreen = CreateCompatibleBitmap(hdcScreen, totalWidth, totalHeight);
        if (!hbmScreen) {
            DeleteDC(hdcMem);
            ReleaseDC(NULL, hdcScreen);
            throw std::runtime_error("Failed to create compatible bitmap");
        }

        // Select the bitmap into the compatible DC
        HBITMAP hbmOld = (HBITMAP)SelectObject(hdcMem, hbmScreen);

        // Capture the entire virtual screen
        BOOL bResult = BitBlt(hdcMem, 0, 0, totalWidth, totalHeight,
            hdcScreen, left, top, SRCCOPY);
        if (!bResult) {
            throw std::runtime_error("BitBlt failed");
        }

        // Convert to Gdiplus::Bitmap
        Gdiplus::Bitmap bitmap(hbmScreen, NULL);

        // Calculate new dimensions
        int newWidth = totalWidth * imageSizePercentage / 100;
        int newHeight = totalHeight * imageSizePercentage / 100;

        // Create resized bitmap with correct pixel format
        Gdiplus::Bitmap resizedBitmap(newWidth, newHeight, PixelFormat32bppARGB);

        // Set up high-quality scaling
        Gdiplus::Graphics graphics(&resizedBitmap);
        graphics.SetInterpolationMode(Gdiplus::InterpolationModeHighQualityBicubic);
        graphics.SetPixelOffsetMode(Gdiplus::PixelOffsetModeHighQuality);
        graphics.DrawImage(&bitmap, 0, 0, newWidth, newHeight);

        // Set up JPEG encoding
        CLSID clsid;
        GetEncoderClsid(L"image/jpeg", &clsid);
        Gdiplus::EncoderParameters encoderParameters;
        encoderParameters.Count = 1;
        encoderParameters.Parameter[0].Guid = Gdiplus::EncoderQuality;
        encoderParameters.Parameter[0].Type = Gdiplus::EncoderParameterValueTypeLong;
        encoderParameters.Parameter[0].NumberOfValues = 1;
        ULONG quality = imageQuality;
        encoderParameters.Parameter[0].Value = &quality;

        // Save the image
        std::wstring savePath = GetSavePath();
        std::wstring filePath = savePath + L"\\screenshot_" +
            std::to_wstring(std::chrono::system_clock::to_time_t(std::chrono::system_clock::now())) +
            L".jpg";

        Gdiplus::Status saveStatus = resizedBitmap.Save(filePath.c_str(), &clsid, &encoderParameters);
        if (saveStatus != Gdiplus::Ok) {
            throw std::runtime_error("Failed to save image");
        }

        // Clean up GDI resources
        SelectObject(hdcMem, hbmOld);
        DeleteObject(hbmScreen);
        DeleteDC(hdcMem);
        ReleaseDC(NULL, hdcScreen);
    }
    catch (const std::exception& e) {
        // Clean up on error
        if (hbmScreen) DeleteObject(hbmScreen);
        if (hdcMem) DeleteDC(hdcMem);
        if (hdcScreen) ReleaseDC(NULL, hdcScreen);
        throw; // Re-throw the exception
    }
}

void CALLBACK TimerProc(HWND hWnd, UINT uMsg, UINT_PTR idEvent, DWORD dwTime) {
    CaptureScreen();
}

void ShowSettings() {
    DialogBox(hInst, MAKEINTRESOURCE(IDD_SETTINGS_DIALOG), NULL, SettingsDialogProc);
}

void OpenSaveFolder() {
    std::wstring savePath = GetSavePath();
    ShellExecute(NULL, L"open", savePath.c_str(), NULL, NULL, SW_SHOW);
}

void Exit() {
    Shell_NotifyIcon(NIM_DELETE, &nid);
    PostQuitMessage(0);
}

void ShowTrayMenu(HWND hWnd) {
    POINT pt;
    GetCursorPos(&pt);
    HMENU hMenu = LoadMenu(hInst, MAKEINTRESOURCE(IDR_TRAY_MENU));
    if (hMenu) {
        HMENU hSubMenu = GetSubMenu(hMenu, 0);
        if (hSubMenu) {
            SetForegroundWindow(hWnd);
            TrackPopupMenu(hSubMenu, TPM_BOTTOMALIGN | TPM_RIGHTBUTTON, pt.x, pt.y, 0, hWnd, NULL);
        }
        DestroyMenu(hMenu);
    }
}

LRESULT CALLBACK WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam) {
    switch (message) {
    case WM_CREATE:
        timerId = SetTimer(hWnd, 1, captureInterval * 1000, TimerProc);
        break;
    case WM_DESTROY:
        KillTimer(hWnd, timerId);
        PostQuitMessage(0);
        break;
    case WM_COMMAND:
        switch (LOWORD(wParam)) {
        case 1:
            OpenSaveFolder();
            break;
        case 2:
            ShowSettings();
            break;
        case 3:
            Exit();
            break;
        case ID_TRAY_OPEN_SAVE_FOLDER:
            OpenSaveFolder();
            break;
        case ID_TRAY_SHOW_SETTINGS:
            ShowSettings();
            break;
        case ID_TRAY_EXIT:
            Exit();
            break;
        }
        break;
    case WM_POWERBROADCAST:
        if (wParam == PBT_APMSUSPEND) {
            KillTimer(hWnd, timerId);
        }
        else if (wParam == PBT_APMRESUMEAUTOMATIC) {
            timerId = SetTimer(hWnd, 1, captureInterval * 1000, TimerProc);
        }
        break;
    case WM_WTSSESSION_CHANGE:
        if (wParam == WTS_SESSION_LOCK) {
            isSessionLocked = true;
        }
        else if (wParam == WTS_SESSION_UNLOCK) {
            isSessionLocked = false;
        }
        break;
    case WM_USER + 1:
        if (lParam == WM_LBUTTONDBLCLK) {
            OpenSaveFolder();
        }
        else if (lParam == WM_RBUTTONDOWN) {
            ShowTrayMenu(hWnd);
        }
        break;
    default:
        return DefWindowProc(hWnd, message, wParam, lParam);
    }
    return 0;
}

int APIENTRY wWinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPWSTR lpCmdLine, int nCmdShow) {
    GdiplusStartupInput gdiplusStartupInput;
    ULONG_PTR gdiplusToken;
    GdiplusStartup(&gdiplusToken, &gdiplusStartupInput, NULL);

    WNDCLASSEX wcex;
    wcex.cbSize = sizeof(WNDCLASSEX);
    wcex.style = CS_HREDRAW | CS_VREDRAW;
    wcex.lpfnWndProc = WndProc;
    wcex.cbClsExtra = 0;
    wcex.cbWndExtra = 0;
    wcex.hInstance = hInstance;
    wcex.hIcon = LoadIcon(NULL, IDI_APPLICATION);
    wcex.hCursor = LoadCursor(NULL, IDC_ARROW);
    wcex.hbrBackground = (HBRUSH)(COLOR_WINDOW + 1);
    wcex.lpszMenuName = NULL;
    wcex.lpszClassName = L"WindowsScreenLoggerCpp";
    wcex.hIconSm = LoadIcon(NULL, IDI_APPLICATION);

    RegisterClassEx(&wcex);

    // Create a message-only window
    HWND hWnd = CreateWindowEx(0, L"WindowsScreenLoggerCpp", NULL, 0, 0, 0, 0, 0, HWND_MESSAGE, NULL, hInstance, NULL);

    if (!hWnd) {
        return FALSE;
    }

    nid.cbSize = sizeof(NOTIFYICONDATA);
    nid.hWnd = hWnd;
    nid.uID = 1;
    nid.uFlags = NIF_ICON | NIF_MESSAGE | NIF_TIP;
    nid.uCallbackMessage = WM_USER + 1;
    nid.hIcon = LoadIcon(NULL, IDI_APPLICATION);
    wcscpy_s(nid.szTip, L"Screen Logger");
    Shell_NotifyIcon(NIM_ADD, &nid);

    MSG msg;
    while (GetMessage(&msg, NULL, 0, 0)) {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }

    Shell_NotifyIcon(NIM_DELETE, &nid);
    GdiplusShutdown(gdiplusToken);
    return (int)msg.wParam;
}


int GetEncoderClsid(const WCHAR* format, CLSID* pClsid) {
    UINT num = 0;
    UINT size = 0;
    ImageCodecInfo* pImageCodecInfo = NULL;

    GetImageEncodersSize(&num, &size);
    if (size == 0) return -1;

    pImageCodecInfo = (ImageCodecInfo*)(malloc(size));
    if (pImageCodecInfo == NULL) return -1;

    GetImageEncoders(num, size, pImageCodecInfo);
    for (UINT j = 0; j < num; ++j) {
        if (wcscmp(pImageCodecInfo[j].MimeType, format) == 0) {
            *pClsid = pImageCodecInfo[j].Clsid;
            free(pImageCodecInfo);
            return j;
        }
    }
    free(pImageCodecInfo);
    return -1;
}
