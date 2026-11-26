using System.Runtime.InteropServices;
using AceCombat8.Core;

namespace AceCombat8.Desktop;

// Enumerates capabilities without acquiring hardware or sending input.
internal static class DirectInputDevices
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);

    [DllImport("dinput8.dll")]
    private static extern int DirectInput8Create(nint instance, uint version, in Guid iid, out nint result, nint outer);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumerationCallback(nint info, nint context);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int Enumerate(nint self, uint deviceClass, EnumerationCallback callback, nint context, uint flags);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateDevice(nint self, in Guid instance, out nint device, nint outer);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetCapabilities(nint self, ref Caps caps);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumerateObjects(nint self, EnumerationCallback callback, nint context, uint flags);

    private static readonly Dictionary<uint, string> AxisGuids = new()
    {
        [0xA36D02E0] = "X", [0xA36D02E1] = "Y", [0xA36D02E2] = "Z",
        [0xA36D02F4] = "Rx", [0xA36D02F5] = "Ry", [0xA36D02E3] = "Rz", [0xA36D02E4] = "Slider"
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct Caps
    {
        public uint Size, Flags, Type, Axes, Buttons, Hats, FfPeriod, FfMinimum, Firmware, Hardware, DriverVersion;
    }

    private static T Method<T>(nint self, int index) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(self), index * IntPtr.Size));

    public static Dictionary<uint, ControllerCapabilities> Read()
    {
        var devices = new Dictionary<uint, ControllerCapabilities>();
        var iid = new Guid("BF798031-483A-4DA2-AA99-5D64ED369700");
        if (DirectInput8Create(GetModuleHandle(null), 0x800, iid, out nint directInput, 0) < 0)
            return devices;
        try
        {
            var create = Method<CreateDevice>(directInput, 3);
            EnumerationCallback callback = (info, _) =>
            {
                Guid instance = Marshal.PtrToStructure<Guid>(info + 4);
                uint product = unchecked((uint)Marshal.ReadInt32(info, 20));
                if (create(directInput, instance, out nint device, 0) < 0) return 1;
                try
                {
                    var caps = new Caps { Size = (uint)Marshal.SizeOf<Caps>() };
                    if (Method<GetCapabilities>(device, 3)(device, ref caps) >= 0)
                        devices.TryAdd(product, new((int)caps.Buttons, (int)caps.Axes, (int)caps.Hats, ReadInputs(device)));
                }
                finally { Marshal.Release(device); }
                return 1;
            };
            Method<Enumerate>(directInput, 4)(directInput, 4, callback, 0, 1);
            GC.KeepAlive(callback);
        }
        finally { Marshal.Release(directInput); }
        return devices;
    }

    private static IReadOnlyList<FlightInput> ReadInputs(nint device)
    {
        var inputs = new List<FlightInput>();
        int sliders = 0;
        EnumerationCallback callback = (info, _) =>
        {
            // DIDEVICEOBJECTINSTANCEW fields immediately after size and GUID.
            uint flags = unchecked((uint)Marshal.ReadInt32(info, 24));
            int instance = (int)((flags >> 8) & 0xffff);
            uint guid = unchecked((uint)Marshal.ReadInt32(info, 4));
            if ((flags & 0xff) is 4 or 8 or 12 && instance < 128)
                inputs.Add(new("Button", instance + 1));
            else if ((flags & 3) != 0 && AxisGuids.TryGetValue(guid, out string? type))
                inputs.Add(new(type, type == "Slider" ? sliders++ : 0));
            return 1;
        };
        int result = Method<EnumerateObjects>(device, 4)(device, callback, 0, 0);
        GC.KeepAlive(callback);
        return result < 0 ? [] : inputs.OrderBy(i => i.Type != "Button").ThenBy(i => i.Button).ToList();
    }
}
