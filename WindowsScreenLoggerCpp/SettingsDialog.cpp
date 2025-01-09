#include "framework.h"
#include "WindowsScreenLoggerCpp.h"
#include <commctrl.h>

extern int captureInterval;
extern int imageSizePercentage;
extern int imageQuality;

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
