using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace LoqRgb
{
    internal static class Native
    {
        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint FILE_SHARE_READ_WRITE = 3;
        public const uint OPEN_EXISTING = 3;
        public const int DIGCF_PRESENT = 0x2;
        public const int DIGCF_DEVICEINTERFACE = 0x10;

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HIDD_ATTRIBUTES
        {
            public int Size;
            public ushort VendorID;
            public ushort ProductID;
            public ushort VersionNumber;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HIDP_CAPS
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public ushort[] Reserved;
            public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps,
                NumberInputDataIndices, NumberOutputButtonCaps, NumberOutputValueCaps,
                NumberOutputDataIndices, NumberFeatureButtonCaps, NumberFeatureValueCaps,
                NumberFeatureDataIndices;
        }

        [DllImport("hid.dll")] public static extern void HidD_GetHidGuid(out Guid guid);
        [DllImport("hid.dll", SetLastError = true)] public static extern bool HidD_GetAttributes(SafeFileHandle h, ref HIDD_ATTRIBUTES a);
        [DllImport("hid.dll", SetLastError = true)] public static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr data);
        [DllImport("hid.dll")] public static extern bool HidD_FreePreparsedData(IntPtr data);
        [DllImport("hid.dll")] public static extern int HidP_GetCaps(IntPtr data, out HIDP_CAPS caps);
        [DllImport("hid.dll", SetLastError = true)] public static extern bool HidD_SetFeature(SafeFileHandle h, byte[] buffer, int length);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern IntPtr SetupDiGetClassDevs(ref Guid guid, IntPtr enumerator, IntPtr hwnd, int flags);
        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr devInfo, ref Guid guid, int index, ref SP_DEVICE_INTERFACE_DATA data);
        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref SP_DEVICE_INTERFACE_DATA data, IntPtr detail, int size, out int required, IntPtr devInfo);
        [DllImport("setupapi.dll")]
        public static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr sa, uint disposition, uint flags, IntPtr template);
    }

    /// <summary>
    /// The ITE 8295-family controller Lenovo uses for 4-zone RGB keyboards (Legion / LOQ).
    /// It exposes a HID collection on vendor usage page 0xFF89 with a 33-byte feature report (ID 0xCC).
    /// </summary>
    public sealed class RgbKeyboard : IDisposable
    {
        public const ushort VendorId = 0x048D;
        const ushort ProductMask = 0xFF00, ProductFamily = 0xC900;
        const ushort UsagePage = 0xFF89;
        const int ReportLength = 33;

        readonly SafeFileHandle handle;
        public ushort ProductId { get; private set; }

        RgbKeyboard(SafeFileHandle handle, ushort pid)
        {
            this.handle = handle;
            ProductId = pid;
        }

        /// <summary>
        /// Looks for Lenovo keyboard controllers this app can't drive yet, to explain why no keyboard was found.
        /// Read-only: it only inspects HID descriptors and never sends anything to the device.
        /// </summary>
        public static string DescribeUnsupported()
        {
            string found = null;
            ForEachHid(path =>
            {
                using (SafeFileHandle probe = Native.CreateFile(path, 0, Native.FILE_SHARE_READ_WRITE, IntPtr.Zero, Native.OPEN_EXISTING, 0, IntPtr.Zero))
                {
                    if (probe.IsInvalid) return false;
                    var attr = new Native.HIDD_ATTRIBUTES();
                    attr.Size = Marshal.SizeOf(attr);
                    if (!Native.HidD_GetAttributes(probe, ref attr) || attr.VendorID != VendorId || (attr.ProductID & ProductMask) != ProductFamily) return false;
                    IntPtr pre;
                    if (!Native.HidD_GetPreparsedData(probe, out pre)) return false;
                    Native.HIDP_CAPS caps;
                    try { Native.HidP_GetCaps(pre, out caps); }
                    finally { Native.HidD_FreePreparsedData(pre); }
                    if (caps.UsagePage == UsagePage && caps.FeatureReportByteLength == 960)
                    {
                        found = string.Format("This laptop has a per-key RGB (\"Spectrum\") keyboard (ITE 048D:{0:X4}). Only 4-zone RGB keyboards are supported so far.", attr.ProductID);
                        return true;
                    }
                }
                return false;
            });
            return found ?? "No Lenovo 4-zone RGB keyboard was found. Keyboards with a white-only backlight are controlled by Lenovo's driver (Fn+Space) and aren't supported.";
        }

        static void ForEachHid(Func<string, bool> visit)
        {
            Guid guid;
            Native.HidD_GetHidGuid(out guid);
            IntPtr set = Native.SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, Native.DIGCF_PRESENT | Native.DIGCF_DEVICEINTERFACE);
            if (set == new IntPtr(-1)) return;
            try
            {
                var data = new Native.SP_DEVICE_INTERFACE_DATA();
                data.cbSize = Marshal.SizeOf(data);
                for (int i = 0; Native.SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, i, ref data); i++)
                {
                    string path = GetPath(set, ref data);
                    if (path != null && visit(path)) return;
                }
            }
            finally
            {
                Native.SetupDiDestroyDeviceInfoList(set);
            }
        }

        public static RgbKeyboard Find()
        {
            Guid guid;
            Native.HidD_GetHidGuid(out guid);
            IntPtr set = Native.SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, Native.DIGCF_PRESENT | Native.DIGCF_DEVICEINTERFACE);
            if (set == new IntPtr(-1)) return null;
            try
            {
                var data = new Native.SP_DEVICE_INTERFACE_DATA();
                data.cbSize = Marshal.SizeOf(data);
                for (int i = 0; Native.SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, i, ref data); i++)
                {
                    string path = GetPath(set, ref data);
                    if (path == null) continue;
                    RgbKeyboard kb = TryOpen(path);
                    if (kb != null) return kb;
                }
            }
            finally
            {
                Native.SetupDiDestroyDeviceInfoList(set);
            }
            return null;
        }

        static string GetPath(IntPtr set, ref Native.SP_DEVICE_INTERFACE_DATA data)
        {
            int required;
            Native.SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out required, IntPtr.Zero);
            if (required <= 0) return null;
            IntPtr buffer = Marshal.AllocHGlobal(required);
            try
            {
                Marshal.WriteInt32(buffer, IntPtr.Size == 8 ? 8 : 6);
                if (!Native.SetupDiGetDeviceInterfaceDetail(set, ref data, buffer, required, out required, IntPtr.Zero)) return null;
                return Marshal.PtrToStringUni(new IntPtr(buffer.ToInt64() + 4));
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        static RgbKeyboard TryOpen(string path)
        {
            ushort pid;
            using (SafeFileHandle probe = Native.CreateFile(path, 0, Native.FILE_SHARE_READ_WRITE, IntPtr.Zero, Native.OPEN_EXISTING, 0, IntPtr.Zero))
            {
                if (probe.IsInvalid) return null;
                var attr = new Native.HIDD_ATTRIBUTES();
                attr.Size = Marshal.SizeOf(attr);
                if (!Native.HidD_GetAttributes(probe, ref attr)) return null;
                if (attr.VendorID != VendorId || (attr.ProductID & ProductMask) != ProductFamily) return null;

                IntPtr pre;
                if (!Native.HidD_GetPreparsedData(probe, out pre)) return null;
                Native.HIDP_CAPS caps;
                try { Native.HidP_GetCaps(pre, out caps); }
                finally { Native.HidD_FreePreparsedData(pre); }
                if (caps.UsagePage != UsagePage || caps.FeatureReportByteLength != ReportLength) return null;
                pid = attr.ProductID;
            }

            SafeFileHandle h = Native.CreateFile(path, Native.GENERIC_READ | Native.GENERIC_WRITE, Native.FILE_SHARE_READ_WRITE, IntPtr.Zero, Native.OPEN_EXISTING, 0, IntPtr.Zero);
            if (h.IsInvalid)
            {
                h.Dispose();
                h = Native.CreateFile(path, 0, Native.FILE_SHARE_READ_WRITE, IntPtr.Zero, Native.OPEN_EXISTING, 0, IntPtr.Zero);
                if (h.IsInvalid) { h.Dispose(); return null; }
            }
            return new RgbKeyboard(h, pid);
        }

        public bool SetFeature(byte[] report, out int win32Error)
        {
            bool ok = Native.HidD_SetFeature(handle, report, report.Length);
            win32Error = ok ? 0 : Marshal.GetLastWin32Error();
            return ok;
        }

        public void Dispose()
        {
            handle.Dispose();
        }
    }

    /// <summary>Finds the keyboard lazily and reconnects after sleep / device resets.</summary>
    public sealed class KeyboardController : IDisposable
    {
        RgbKeyboard device;
        string unsupportedReason; // why no supported keyboard was found (computed once)

        public string DeviceName
        {
            get { return device == null ? null : string.Format("ITE {0:X4}:{1:X4}", RgbKeyboard.VendorId, device.ProductId); }
        }

        public bool Apply(LightState state, out string error)
        {
            return Send(state.ToReport(), out error);
        }

        public bool Send(byte[] report, out string error)
        {
            int code = 0;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (device == null)
                {
                    try { device = RgbKeyboard.Find(); }
                    catch (Exception ex) { error = ex.Message; return false; }
                    if (device == null)
                    {
                        if (unsupportedReason == null)
                        {
                            try { unsupportedReason = RgbKeyboard.DescribeUnsupported(); }
                            catch { unsupportedReason = "No Lenovo 4-zone RGB keyboard was found."; }
                        }
                        error = unsupportedReason;
                        return false;
                    }
                }
                if (device.SetFeature(report, out code)) { error = null; return true; }
                device.Dispose();
                device = null;
            }
            error = "Writing to the keyboard failed (Win32 error " + code + ").";
            return false;
        }

        public void Dispose()
        {
            if (device != null) device.Dispose();
            device = null;
        }
    }
}
