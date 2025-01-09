// SettingsDialog.cpp
#include "framework.h"
#include "WindowsScreenLoggerCpp.h"
#include <commctrl.h>
#include <windowsx.h>
#include "SettingsDialog.h"

extern int captureInterval;
extern int imageSizePercentage;
extern int imageQuality;
extern bool startWithWindows; // Add this line

// Validation limits
constexpr int MIN_INTERVAL = 1;
constexpr int MAX_INTERVAL = 3600;
constexpr int MIN_SIZE = 10;
constexpr int MAX_SIZE = 100;

void SetStartupWithWindows(bool enable) {
    HKEY hKey;
    LONG result = RegOpenKeyEx(HKEY_CURRENT_USER, L"Software\\Microsoft\\Windows\\CurrentVersion\\Run", 0, KEY_SET_VALUE, &hKey);
    if (result == ERROR_SUCCESS) {
        if (enable) {
            wchar_t path[MAX_PATH];
            GetModuleFileName(NULL, path, MAX_PATH);
            RegSetValueEx(hKey, L"WindowsScreenLogger", 0, REG_SZ, (BYTE*)path, (lstrlen(path) + 1) * sizeof(wchar_t));
        }
        else {
            RegDeleteValue(hKey, L"WindowsScreenLogger");
        }
        RegCloseKey(hKey);
    }
}

// Center dialog on screen
void CenterDialog(HWND hDlg) {
    RECT rc, rcDlg;
    GetWindowRect(GetDesktopWindow(), &rc);
    GetWindowRect(hDlg, &rcDlg);

    // Calculate center position
    int x = (rc.right - rc.left - (rcDlg.right - rcDlg.left)) / 2;
    int y = (rc.bottom - rc.top - (rcDlg.bottom - rcDlg.top)) / 2;

    SetWindowPos(hDlg, HWND_TOP, x, y, 0, 0, SWP_NOSIZE);
}

// Update the quality label with current value
void UpdateQualityLabel(HWND hDlg) {
    int quality = static_cast<int>(SendDlgItemMessage(hDlg, IDC_QUALITY_SLIDER, TBM_GETPOS, 0, 0));
    SetDlgItemInt(hDlg, IDC_QUALITY_LABEL, quality, FALSE);
}

// Validate input values
bool ValidateSettings(HWND hDlg, int interval, int size) {
    if (interval < MIN_INTERVAL || interval > MAX_INTERVAL) {
        MessageBox(hDlg,
            L"Capture interval must be between 1 and 3600 seconds.",
            L"Invalid Input", MB_OK | MB_ICONWARNING);
        SetFocus(GetDlgItem(hDlg, IDC_INTERVAL_EDIT));
        return false;
    }

    if (size < MIN_SIZE || size > MAX_SIZE) {
        MessageBox(hDlg,
            L"Image size must be between 10% and 100%.",
            L"Invalid Input", MB_OK | MB_ICONWARNING);
        SetFocus(GetDlgItem(hDlg, IDC_IMAGE_SIZE_EDIT));
        return false;
    }

    return true;
}

INT_PTR CALLBACK SettingsDialogProc(HWND hDlg, UINT message, WPARAM wParam, LPARAM lParam) {
    UNREFERENCED_PARAMETER(lParam);

    switch (message) {
    case WM_INITDIALOG:
        // Center the dialog
        CenterDialog(hDlg);

        // Initialize controls with current values
        SetDlgItemInt(hDlg, IDC_INTERVAL_EDIT, captureInterval, FALSE);
        SetDlgItemInt(hDlg, IDC_IMAGE_SIZE_EDIT, imageSizePercentage, FALSE);
        CheckDlgButton(hDlg, IDC_STARTUP_CHECKBOX, startWithWindows ? BST_CHECKED : BST_UNCHECKED); // Add this line

        // Setup slider control
        SendDlgItemMessage(hDlg, IDC_QUALITY_SLIDER, TBM_SETRANGE, TRUE, MAKELPARAM(10, 100));
        SendDlgItemMessage(hDlg, IDC_QUALITY_SLIDER, TBM_SETPOS, TRUE, imageQuality);
        UpdateQualityLabel(hDlg);
        return (INT_PTR)TRUE;

    case WM_HSCROLL:
        if ((HWND)lParam == GetDlgItem(hDlg, IDC_QUALITY_SLIDER)) {
            UpdateQualityLabel(hDlg);
        }
        return (INT_PTR)TRUE;

    case WM_COMMAND:
        switch (LOWORD(wParam)) {
        case IDOK:
        case IDC_SAVE_BUTTON: {
            BOOL success;
            int interval = GetDlgItemInt(hDlg, IDC_INTERVAL_EDIT, &success, FALSE);
            if (!success) return (INT_PTR)TRUE;

            int size = GetDlgItemInt(hDlg, IDC_IMAGE_SIZE_EDIT, &success, FALSE);
            if (!success) return (INT_PTR)TRUE;

            if (!ValidateSettings(hDlg, interval, size)) {
                return (INT_PTR)TRUE;
            }

            // Save settings
            captureInterval = interval;
            imageSizePercentage = size;
            imageQuality = static_cast<int>(SendDlgItemMessage(hDlg, IDC_QUALITY_SLIDER, TBM_GETPOS, 0, 0));
            startWithWindows = (IsDlgButtonChecked(hDlg, IDC_STARTUP_CHECKBOX) == BST_CHECKED); // Add this line
            SetStartupWithWindows(startWithWindows); // Add this line

            EndDialog(hDlg, IDOK);
            return (INT_PTR)TRUE;
        }

        case IDCANCEL:
        case IDC_CANCEL_BUTTON:
            EndDialog(hDlg, IDCANCEL);
            return (INT_PTR)TRUE;
        }
        break;

    case WM_CLOSE:
        EndDialog(hDlg, IDCANCEL);
        return (INT_PTR)TRUE;
    }

    return (INT_PTR)FALSE;
}
