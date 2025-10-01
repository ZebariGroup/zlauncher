using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace ZLauncher;

public sealed class HotKeyListener : IDisposable
{
    private readonly Window _window;
    private readonly GlobalHotKey _hotKey;
    private bool _isRegistered;
    private HwndSource? _source;
    private IntPtr _handle;

    public event EventHandler? HotKeyTriggered;

    public HotKeyListener(Window window, GlobalHotKey hotKey)
    {
        _window = window;
        _hotKey = hotKey;
    }

    public void Register()
    {
        if (_isRegistered)
        {
            return;
        }

        var helper = new WindowInteropHelper(_window);
        _handle = helper.EnsureHandle();

        if (!NativeMethods.RegisterHotKey(_handle, _hotKey.Id, (uint)_hotKey.Modifiers, KeyInterop.VirtualKeyFromKey(_hotKey.Key)))
        {
            Debug.WriteLine("Failed to register global hotkey.");
            return;
        }

        _source = HwndSource.FromHwnd(_handle);
        _source?.AddHook(HotKeyHook);
        _isRegistered = true;
    }

    public void Unregister()
    {
        if (!_isRegistered)
        {
            return;
        }

        _source?.RemoveHook(HotKeyHook);
        _source = null;

        NativeMethods.UnregisterHotKey(_handle, _hotKey.Id);
        _isRegistered = false;
    }

    private IntPtr HotKeyHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == _hotKey.Id)
        {
            HotKeyTriggered?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
    }
}

public readonly record struct GlobalHotKey(ModifierKeys Modifiers, Key Key)
{
    public int Id { get; } = (int)Key + (int)Modifiers;
}

internal static class NativeMethods
{
    public const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, int vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}

