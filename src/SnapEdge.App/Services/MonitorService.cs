using SnapEdge.App.Interop;

namespace SnapEdge.App.Services;

internal sealed record MonitorDetails(nint Handle, NativeMethods.NativeRect Bounds, NativeMethods.NativeRect WorkArea, string DeviceName, bool IsPrimary);

internal static class MonitorService
{
    public static IReadOnlyList<MonitorDetails> Enumerate()
    {
        var monitors = new List<MonitorDetails>();
        NativeMethods.MonitorEnumProc callback = (handle, _, _, _) =>
        {
            var info = NativeMethods.MonitorInfoEx.Create();
            if (NativeMethods.GetMonitorInfo(handle, ref info))
                monitors.Add(new MonitorDetails(handle, info.Monitor, info.Work, info.DeviceName, (info.Flags & 1) != 0));
            return true;
        };
        NativeMethods.EnumDisplayMonitors(0, 0, callback, 0);
        GC.KeepAlive(callback);
        return monitors;
    }

    public static MonitorDetails? FindByDeviceName(string? deviceName) =>
        Enumerate().FirstOrDefault(m => !string.IsNullOrEmpty(deviceName) && string.Equals(m.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));

    public static MonitorDetails? FindNearest(NativeMethods.NativePoint point)
    {
        var handle = NativeMethods.MonitorFromPoint(point, NativeMethods.MonDefaultToNearest);
        if (handle == 0)
            return null;
        var info = NativeMethods.MonitorInfoEx.Create();
        return NativeMethods.GetMonitorInfo(handle, ref info)
            ? new MonitorDetails(handle, info.Monitor, info.Work, info.DeviceName, (info.Flags & 1) != 0)
            : null;
    }

    public static MonitorDetails? GetPrimaryOrFirst()
    {
        var monitors = Enumerate();
        return monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault();
    }
}
