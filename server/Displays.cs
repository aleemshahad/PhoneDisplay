using System.Runtime.InteropServices;

namespace PhoneDisplay.Server;

internal sealed record DisplayInfo(
    int Index,
    string DeviceName,
    string FriendlyName,
    int X,
    int Y,
    int Width,
    int Height,
    bool IsPrimary)
{
    public override string ToString()
    {
        var size = $"{Width}x{Height}";
        var at = $"({X},{Y})";
        var primary = IsPrimary ? " PRIMARY" : string.Empty;
        return $"[{Index}] {FriendlyName}  {DeviceName}  {size} @{at}{primary}";
    }
}

internal static class Displays
{
    private static Interop.MonitorEnumProc? _enumProc;

    internal static List<DisplayInfo> All()
    {
        var result = new List<DisplayInfo>();
        var index = 0;

        _enumProc = (IntPtr hMonitor, IntPtr hdcMonitor, ref Interop.RECT rect, IntPtr data) =>
        {
            var info = new Interop.MONITORINFOEX
            {
                cbSize = Marshal.SizeOf<Interop.MONITORINFOEX>()
            };

            if (!Interop.GetMonitorInfo(hMonitor, ref info))
            {
                return true;
            }

            var device = new Interop.DISPLAY_DEVICE
            {
                cb = Marshal.SizeOf<Interop.DISPLAY_DEVICE>()
            };

            var attached = Interop.EnumDisplayDevices(info.szDevice, 0, ref device, 0);
            var isAttached = attached && (device.StateFlags & Interop.DISPLAYDEVICE_ATTACHED_TO_DESKTOP) != 0;
            var isPrimary = attached && (device.StateFlags & Interop.DISPLAY_DEVICE_PRIMARY_DEVICE) != 0;

            if (!isAttached)
            {
                return true;
            }

            result.Add(new DisplayInfo(
                index,
                info.szDevice,
                attached ? device.DeviceString : "Unknown display",
                info.rcMonitor.Left,
                info.rcMonitor.Top,
                info.rcMonitor.Right - info.rcMonitor.Left,
                info.rcMonitor.Bottom - info.rcMonitor.Top,
                isPrimary));

            index++;
            return true;
        };

        Interop.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, _enumProc, IntPtr.Zero);
        return result;
    }

    internal static DisplayInfo? Find(IReadOnlyList<DisplayInfo> displays, string? selector)
    {
        if (displays.Count == 0)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(selector))
        {
            return displays[0];
        }

        if (int.TryParse(selector, out var index) && index >= 0 && index < displays.Count)
        {
            return displays[index];
        }

        var query = selector.Trim();
        return displays.FirstOrDefault(d =>
            d.FriendlyName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            d.DeviceName.Equals(query, StringComparison.OrdinalIgnoreCase));
    }
}