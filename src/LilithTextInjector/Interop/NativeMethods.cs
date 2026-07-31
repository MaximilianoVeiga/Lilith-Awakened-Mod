namespace LilithTextInjector;

// Win32 P/Invoke surface and stateless keyboard/idle helpers.
internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte Reserved;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("user32.dll")]
    internal static extern bool GetLastInputInfo(ref LastInputInfo info);

    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    internal static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    internal static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    internal static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    internal static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    internal static extern bool BringWindowToTop(IntPtr window);

    [DllImport("user32.dll")]
    internal static extern IntPtr SetFocus(IntPtr window);

    [DllImport("user32.dll")]
    internal static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    internal static extern bool AttachThreadInput(uint attachThreadId, uint attachToThreadId, bool attach);

    [DllImport("kernel32.dll")]
    internal static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    internal static extern bool LockWorkStation();

    [DllImport("kernel32.dll")]
    internal static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

    [DllImport("PowrProf.dll", SetLastError = true)]
    internal static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;
    private const int SrcCopy = 0x00CC0020;
    private const uint DibRgbColors = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public int Size;
        public int Width;
        public int Height;
        public short Planes;
        public short BitCount;
        public int Compression;
        public int SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public int ClrUsed;
        public int ClrImportant;
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int width, int height, IntPtr hdcSrc, int xSrc, int ySrc, int rop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr obj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr bitmap, uint startScan, uint scanLines, byte[]? bits, ref BitmapInfoHeader bmi, uint usage);

    internal static void CaptureVirtualScreenToBmp(string outputPath)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Screenshots are only available on Windows.");

        var left = GetSystemMetrics(SmXVirtualScreen);
        var top = GetSystemMetrics(SmYVirtualScreen);
        var width = GetSystemMetrics(SmCxVirtualScreen);
        var height = GetSystemMetrics(SmCyVirtualScreen);
        if (width <= 0 || height <= 0)
            throw new InvalidOperationException("Could not determine the virtual screen size.");

        var screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
            throw new InvalidOperationException("Could not open the screen device context.");

        var memoryDc = IntPtr.Zero;
        var bitmap = IntPtr.Zero;
        var previous = IntPtr.Zero;
        try
        {
            memoryDc = CreateCompatibleDC(screenDc);
            if (memoryDc == IntPtr.Zero)
                throw new InvalidOperationException("Could not create a compatible device context.");

            bitmap = CreateCompatibleBitmap(screenDc, width, height);
            if (bitmap == IntPtr.Zero)
                throw new InvalidOperationException("Could not create a compatible bitmap.");

            previous = SelectObject(memoryDc, bitmap);
            if (!BitBlt(memoryDc, 0, 0, width, height, screenDc, left, top, SrcCopy))
                throw new InvalidOperationException("BitBlt failed while capturing the screen.");

            var header = new BitmapInfoHeader
            {
                Size = Marshal.SizeOf<BitmapInfoHeader>(),
                Width = width,
                Height = -height,
                Planes = 1,
                BitCount = 32,
                Compression = 0
            };
            var stride = width * 4;
            var pixels = new byte[stride * height];
            if (GetDIBits(memoryDc, bitmap, 0, (uint)height, pixels, ref header, DibRgbColors) == 0)
                throw new InvalidOperationException("GetDIBits failed while reading the screenshot.");

            // Convert BGRA → BGR and write a 24-bit BMP (bottom-up).
            var rowStride = ((width * 3) + 3) & ~3;
            var pixelData = new byte[rowStride * height];
            for (var y = 0; y < height; y++)
            {
                var srcRow = y * stride;
                var dstRow = (height - 1 - y) * rowStride;
                for (var x = 0; x < width; x++)
                {
                    var src = srcRow + (x * 4);
                    var dst = dstRow + (x * 3);
                    pixelData[dst] = pixels[src];
                    pixelData[dst + 1] = pixels[src + 1];
                    pixelData[dst + 2] = pixels[src + 2];
                }
            }

            var fileHeaderSize = 14;
            var infoHeaderSize = 40;
            var fileSize = fileHeaderSize + infoHeaderSize + pixelData.Length;
            using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new BinaryWriter(stream);
            writer.Write((byte)'B');
            writer.Write((byte)'M');
            writer.Write(fileSize);
            writer.Write(0);
            writer.Write(fileHeaderSize + infoHeaderSize);
            writer.Write(infoHeaderSize);
            writer.Write(width);
            writer.Write(height);
            writer.Write((short)1);
            writer.Write((short)24);
            writer.Write(0);
            writer.Write(pixelData.Length);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(pixelData);
        }
        finally
        {
            if (previous != IntPtr.Zero)
                SelectObject(memoryDc, previous);
            if (bitmap != IntPtr.Zero)
                DeleteObject(bitmap);
            if (memoryDc != IntPtr.Zero)
                DeleteDC(memoryDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    internal static bool IsVirtualKeyDown(int virtualKey) =>
        OperatingSystem.IsWindows() && (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    internal static bool IsKeyCurrentlyDown(KeyCode key)
    {
        if (TryGetWindowsVirtualKey(key, out var virtualKey))
            return IsVirtualKeyDown(virtualKey);
        try
        {
            return Input.GetKey(key);
        }
        catch
        {
            return false;
        }
    }

    internal static bool TryGetWindowsVirtualKey(KeyCode key, out int virtualKey)
    {
        virtualKey = 0;
        if (!OperatingSystem.IsWindows())
            return false;

        var code = (int)key;
        if (code >= (int)KeyCode.Alpha0 && code <= (int)KeyCode.Alpha9)
        {
            virtualKey = 0x30 + code - (int)KeyCode.Alpha0;
            return true;
        }
        if (code >= (int)KeyCode.A && code <= (int)KeyCode.Z)
        {
            virtualKey = 0x41 + code - (int)KeyCode.A;
            return true;
        }
        if (code >= (int)KeyCode.Keypad0 && code <= (int)KeyCode.Keypad9)
        {
            virtualKey = 0x60 + code - (int)KeyCode.Keypad0;
            return true;
        }
        if (code >= (int)KeyCode.F1 && code <= (int)KeyCode.F15)
        {
            virtualKey = 0x70 + code - (int)KeyCode.F1;
            return true;
        }

        virtualKey = key switch
        {
            KeyCode.Backspace => 0x08,
            KeyCode.Tab => 0x09,
            KeyCode.Clear => 0x0C,
            KeyCode.Return => 0x0D,
            KeyCode.Pause => 0x13,
            KeyCode.Escape => 0x1B,
            KeyCode.Space => 0x20,
            KeyCode.PageUp => 0x21,
            KeyCode.PageDown => 0x22,
            KeyCode.End => 0x23,
            KeyCode.Home => 0x24,
            KeyCode.LeftArrow => 0x25,
            KeyCode.UpArrow => 0x26,
            KeyCode.RightArrow => 0x27,
            KeyCode.DownArrow => 0x28,
            KeyCode.Insert => 0x2D,
            KeyCode.Delete => 0x2E,
            KeyCode.KeypadMultiply => 0x6A,
            KeyCode.KeypadPlus => 0x6B,
            KeyCode.KeypadMinus => 0x6D,
            KeyCode.KeypadPeriod => 0x6E,
            KeyCode.KeypadDivide => 0x6F,
            KeyCode.Numlock => 0x90,
            KeyCode.ScrollLock => 0x91,
            KeyCode.LeftShift => 0xA0,
            KeyCode.RightShift => 0xA1,
            KeyCode.LeftControl => 0xA2,
            KeyCode.RightControl => 0xA3,
            KeyCode.LeftAlt => 0xA4,
            KeyCode.RightAlt => 0xA5,
            KeyCode.Semicolon => 0xBA,
            KeyCode.Equals => 0xBB,
            KeyCode.Comma => 0xBC,
            KeyCode.Minus => 0xBD,
            KeyCode.Period => 0xBE,
            KeyCode.Slash => 0xBF,
            KeyCode.BackQuote => 0xC0,
            KeyCode.LeftBracket => 0xDB,
            KeyCode.Backslash => 0xDC,
            KeyCode.RightBracket => 0xDD,
            KeyCode.Quote => 0xDE,
            _ => 0
        };
        return virtualKey != 0;
    }

    internal static void SendVirtualKey(byte virtualKey)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows media keys are only available on Windows.");
        keybd_event(virtualKey, 0, 0, UIntPtr.Zero);
        keybd_event(virtualKey, 0, 0x0002, UIntPtr.Zero);
    }

    internal static void SendShortcut(params byte[] virtualKeys)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows shortcuts are only available on Windows.");
        foreach (var key in virtualKeys)
            keybd_event(key, 0, 0, UIntPtr.Zero);
        for (var index = virtualKeys.Length - 1; index >= 0; index--)
            keybd_event(virtualKeys[index], 0, 0x0002, UIntPtr.Zero);
    }

    internal static double GetWindowsIdleSeconds()
    {
        if (!OperatingSystem.IsWindows()) return 0;
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info)) return 0;
        return unchecked((uint)Environment.TickCount - info.Time) / 1000d;
    }
}
