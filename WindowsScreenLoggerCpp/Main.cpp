#include <windows.h>
#include <gdiplus.h>
#include <shellapi.h>
#include <string>
#include <filesystem>
#include <chrono>
#include <thread>
#include "resource.h"
#include <commctrl.h>

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

std::wstring GetSavePath();
void CaptureScreen();
void CALLBACK TimerProc(HWND hWnd, UINT uMsg, UINT_PTR idEvent, DWORD dwTime);
void ShowSettings();
void OpenSaveFolder();
void Exit();
LRESULT CALLBACK WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam);
int GetEncoderClsid(const WCHAR* format, CLSID* pClsid);
INT_PTR CALLBACK SettingsDialogProc(HWND hDlg, UINT message, WPARAM wParam, LPARAM lParam);

std::wstring GetSavePath() {
    wchar_t* userProfile;
    size_t len;
    _wdupenv_s(&userProfile, &len, L"USERPROFILE");
    std::wstring path = std::wstring(userProfile) + L"\\WindowsScreenLogger\\" + std::to_wstring(std::chrono::system_clock::to_time_t(std::chrono::system_clock::now()));
    fs::create_directories(path);
    return path;
}

void CaptureScreen() {
    if (isSessionLocked) return;

    HDC hdcScreen = GetDC(NULL);
    HDC hdcMem = CreateCompatibleDC(hdcScreen);
    RECT rc;
    GetClientRect(GetDesktopWindow(), &rc);
    int width = rc.right - rc.left;
    int height = rc.bottom - rc.top;

    HBITMAP hbmScreen = CreateCompatibleBitmap(hdcScreen, width, height);
    SelectObject(hdcMem, hbmScreen);
    BitBlt(hdcMem, 0, 0, width, height, hdcScreen, 0, 0, SRCCOPY);

    Gdiplus::Bitmap bitmap(hbmScreen, NULL);
    int newWidth = width * imageSizePercentage / 100;
    int newHeight = height * imageSizePercentage / 100;
    Gdiplus::Bitmap resizedBitmap(newWidth, newHeight, PixelFormat32bppARGB);
    Gdiplus::Graphics graphics(&resizedBitmap);
    graphics.DrawImage(&bitmap, 0, 0, newWidth, newHeight);

    CLSID clsid;
    GetEncoderClsid(L"image/jpeg", &clsid);
    Gdiplus::EncoderParameters encoderParameters;
    encoderParameters.Count = 1;
    encoderParameters.Parameter[0].Guid = Gdiplus::EncoderQuality;
    encoderParameters.Parameter[0].Type = Gdiplus::EncoderParameterValueTypeLong;
    encoderParameters.Parameter[0].NumberOfValues = 1;
    ULONG quality = imageQuality;
    encoderParameters.Parameter[0].Value = &quality;

    std::wstring savePath = GetSavePath();
    std::wstring filePath = savePath + L"\\screenshot_" + std::to_wstring(std::chrono::system_clock::to_time_t(std::chrono::system_clock::now())) + L".jpg";
    resizedBitmap.Save(filePath.c_str(), &clsid, &encoderParameters);

    DeleteObject(hbmScreen);
    DeleteDC(hdcMem);
    ReleaseDC(NULL, hdcScreen);
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

    HWND hWnd = CreateWindow(L"WindowsScreenLoggerCpp", L"Windows Screen Logger", WS_OVERLAPPEDWINDOW,
        CW_USEDEFAULT, 0, CW_USEDEFAULT, 0, NULL, NULL, hInstance, NULL);

    ShowWindow(hWnd, nCmdShow);
    UpdateWindow(hWnd);

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

INT_PTR CALLBACK SettingsDialogProc(HWND hDlg, UINT message, WPARAM wParam, LPARAM lParam) {
    UNREFERENCED_PARAMETER(lParam);
    switch (message) {
    case WM_INITDIALOG:
        SetDlgItemInt(hDlg, IDC_INTERVAL_EDIT, captureInterval, FALSE);
        SetDlgItemInt(hDlg, IDC_IMAGE_SIZE_EDIT, imageSizePercentage, FALSE);
        SendDlgItemMessage(hDlg, IDC_QUALITY_SLIDER, TBM_SETRANGE, TRUE, MAKELPARAM(10, 100));
        SendDlgItemMessage(hDlg, IDC_QUALITY_SLIDER, TBM_SETPOS, TRUE, imageQuality);
        return (INT_PTR)TRUE;

    case WM_COMMAND:
        if (LOWORD(wParam) == IDC_SAVE_BUTTON) {
            BOOL success;
            int interval = GetDlgItemInt(hDlg, IDC_INTERVAL_EDIT, &success, FALSE);
            if (success) captureInterval = interval;

            int size = GetDlgItemInt(hDlg, IDC_IMAGE_SIZE_EDIT, &success, FALSE);
            if (success) imageSizePercentage = size;

            imageQuality = SendDlgItemMessage(hDlg, IDC_QUALITY_SLIDER, TBM_GETPOS, 0, 0);

            EndDialog(hDlg, LOWORD(wParam));
            return (INT_PTR)TRUE;
        }
        else if (LOWORD(wParam) == IDC_CANCEL_BUTTON) {
            EndDialog(hDlg, LOWORD(wParam));
            return (INT_PTR)TRUE;
        }
        break;
    }
    return (INT_PTR)FALSE;
}



