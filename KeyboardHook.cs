using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.LibraryLoader;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MetroOsd;

/// <summary>
/// Global low-level keyboard hook. Raises <see cref="KeyPressed"/> once per physical CapsLock
/// press (key-up edge); auto-repeat keydown events are suppressed via a pressed flag.
/// </summary>
internal sealed class KeyboardHook : IDisposable
{
    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_KEYUP = 0x0101;

    private readonly HOOKPROC _proc;
    private readonly HHOOK _hook;
    private bool _pressed;

    public event Action? KeyPressed;

    public KeyboardHook()
    {
        _proc = HookCallback;
        HMODULE hMod = PInvoke.GetModuleHandle(default(PCWSTR));
        HINSTANCE hInst = hMod; // implicit HMODULE -> HINSTANCE
        _hook = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, _proc, hInst, 0);
        if (_hook.IsNull)
        {
            throw new InvalidOperationException($"SetWindowsHookEx failed, error={Marshal.GetLastWin32Error()}");
        }
    }

    private LRESULT HookCallback(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode >= 0)
        {
            var kbd = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam.Value);
            if (kbd.vkCode == (uint)VIRTUAL_KEY.VK_CAPITAL)
            {
                uint msg = (uint)wParam.Value;
                if (msg == WM_KEYDOWN)
                {
                    // First keydown arms the flag; auto-repeat keydowns are ignored.
                    _pressed = true;
                }
                else if (msg == WM_KEYUP && _pressed)
                {
                    _pressed = false;
                    KeyPressed?.Invoke();
                }
            }
        }

        return PInvoke.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose() => PInvoke.UnhookWindowsHookEx(_hook);
}

