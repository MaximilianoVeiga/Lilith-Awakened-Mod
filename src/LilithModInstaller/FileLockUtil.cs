using System.Runtime.InteropServices;

namespace LilithModInstaller;

// Uses the Windows Restart Manager API to report which processes lock a file.
internal static class FileLockUtil
{
    internal static List<string> GetLockingProcessNames(string path)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return result;

        uint handle = 0;
        var sessionKey = Guid.NewGuid().ToString("N");
        if (RmStartSession(out handle, 0, sessionKey) != 0)
            return result;

        try
        {
            string[] resources = [path];
            if (RmRegisterResources(handle, (uint)resources.Length, resources, 0, null, 0, null) != 0)
                return result;

            uint needed = 0;
            uint count = 0;
            var status = RmGetList(handle, out needed, ref count, null, out _);
            if (status == ERROR_MORE_DATA && needed > 0)
            {
                var array = new RM_PROCESS_INFO[needed];
                count = needed;
                if (RmGetList(handle, out needed, ref count, array, out _) == 0)
                {
                    for (var i = 0; i < count; i++)
                    {
                        var name = array[i].strAppName;
                        if (!string.IsNullOrWhiteSpace(name))
                            result.Add(name);
                        else
                            result.Add($"PID {array[i].Process.dwProcessId}");
                    }
                }
            }
        }
        finally
        {
            RmEndSession(handle);
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private const int ERROR_MORE_DATA = 234;

    [StructLayout(LayoutKind.Sequential)]
    private struct RM_UNIQUE_PROCESS
    {
        public int dwProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RM_PROCESS_INFO
    {
        public RM_UNIQUE_PROCESS Process;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strAppName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string strServiceShortName;
        public uint ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bRestartable;
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmEndSession(uint pSessionHandle);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(uint pSessionHandle, uint nFiles, string[] rgsFilenames, uint nApplications, [In] RM_UNIQUE_PROCESS[]? rgApplications, uint nServices, string[]? rgsServiceNames);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmGetList(uint dwSessionHandle, out uint pnProcInfoNeeded, ref uint pnProcInfo, [In, Out] RM_PROCESS_INFO[]? rgAffectedApps, out uint lpdwRebootReasons);
}
