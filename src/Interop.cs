using System;
using System.Runtime.InteropServices;
using System.Text;

namespace WorkspaceLauncher
{
    // ---------- Shell COM interop (Jump List + Shortcut) ----------

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct PropertyKey
    {
        public Guid fmtid;
        public uint pid;
        public PropertyKey(Guid f, uint p) { fmtid = f; pid = p; }
    }

    // PROPVARIANT 의 union 부분 크기를 맞추기 위한 패딩.
    // 이게 없으면 구조체가 x64 에서 24바이트가 아닌 16바이트가 되어
    // PropVariantClear 가 스택 너머를 지운다.
    [StructLayout(LayoutKind.Sequential)]
    internal struct PropVariantUnionPad
    {
        public IntPtr a;
        public IntPtr b;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct PropVariant
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(2)] public ushort wReserved1;
        [FieldOffset(4)] public ushort wReserved2;
        [FieldOffset(6)] public ushort wReserved3;
        [FieldOffset(8)] public PropVariantUnionPad pad;
        [FieldOffset(8)] public IntPtr pointerValue;
        [FieldOffset(8)] public short boolValue;

        public const ushort VT_EMPTY = 0;
        public const ushort VT_BOOL = 11;
        public const ushort VT_LPWSTR = 31;

        public static PropVariant FromString(string s)
        {
            PropVariant pv = new PropVariant();
            pv.vt = VT_LPWSTR;
            pv.pointerValue = Marshal.StringToCoTaskMemUni(s);
            return pv;
        }

        public static PropVariant FromBool(bool b)
        {
            PropVariant pv = new PropVariant();
            pv.vt = VT_BOOL;
            pv.boolValue = (short)(b ? -1 : 0);
            return pv;
        }
    }

    [ComImport, Guid("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IObjectArray
    {
        void GetCount(out uint cObjects);
        void GetAt(uint iIndex, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
    }

    [ComImport, Guid("5632B1A4-E38A-400A-928A-D4CD63230295"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IObjectCollection
    {
        // IObjectArray
        void GetCount(out uint cObjects);
        void GetAt(uint iIndex, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        // IObjectCollection
        void AddObject([MarshalAs(UnmanagedType.Interface)] object pvObject);
        void AddFromArray([MarshalAs(UnmanagedType.Interface)] IObjectArray poaSource);
        void RemoveObjectAt(uint uiIndex);
        void Clear();
    }

    [ComImport, Guid("6332DEBF-87B5-4670-90C0-5E57B408A49E"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ICustomDestinationList
    {
        void SetAppID([MarshalAs(UnmanagedType.LPWStr)] string pszAppID);

        [PreserveSig]
        int BeginList(out uint cMaxSlots, ref Guid riid,
                      [MarshalAs(UnmanagedType.Interface)] out object ppv);

        [PreserveSig]
        int AppendCategory([MarshalAs(UnmanagedType.LPWStr)] string pszCategory, IObjectArray poa);

        [PreserveSig]
        int AppendKnownCategory(int category);

        [PreserveSig]
        int AddUserTasks(IObjectArray poa);

        void CommitList();
        void GetRemovedDestinations(ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        void DeleteList([MarshalAs(UnmanagedType.LPWStr)] string pszAppID);
        void AbortList();
    }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile,
                     int cchMaxPath, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath,
                             int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyStore
    {
        void GetCount(out uint cProps);
        void GetAt(uint iProp, out PropertyKey pkey);
        void GetValue(ref PropertyKey key, out PropVariant pv);
        void SetValue(ref PropertyKey key, ref PropVariant pv);
        void Commit();
    }

    [ComImport, Guid("0000010B-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig]
        int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName,
                  [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }

    internal static class Shell
    {
        internal static readonly Guid CLSID_DestinationList =
            new Guid("77F10CF0-3DB5-4966-B520-B7C54FD35ED6");
        internal static readonly Guid CLSID_EnumerableObjectCollection =
            new Guid("2D3468C1-36A7-43B6-AC24-D3F02FD9607A");
        internal static readonly Guid CLSID_ShellLink =
            new Guid("00021401-0000-0000-C000-000000000046");
        internal static readonly Guid IID_IObjectArray =
            new Guid("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9");
        internal static readonly Guid IID_IShellLinkW =
            new Guid("000214F9-0000-0000-C000-000000000046");

        // System.Title
        internal static PropertyKey PKEY_Title =
            new PropertyKey(new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"), 2);
        // System.AppUserModel.ID
        internal static PropertyKey PKEY_AppUserModel_ID =
            new PropertyKey(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);
        // System.AppUserModel.IsDestListSeparator
        internal static PropertyKey PKEY_AppUserModel_IsDestListSeparator =
            new PropertyKey(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 6);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        internal static extern void SetCurrentProcessExplicitAppUserModelID(
            [MarshalAs(UnmanagedType.LPWStr)] string AppID);

        [DllImport("ole32.dll")]
        internal static extern int PropVariantClear(ref PropVariant pvar);

        internal static object CreateInstance(Guid clsid)
        {
            Type t = Type.GetTypeFromCLSID(clsid);
            return Activator.CreateInstance(t);
        }

        internal static void SetStringProp(IPropertyStore store, PropertyKey key, string value)
        {
            PropVariant pv = PropVariant.FromString(value);
            try { store.SetValue(ref key, ref pv); }
            finally { PropVariantClear(ref pv); }
        }

        internal static void SetBoolProp(IPropertyStore store, PropertyKey key, bool value)
        {
            PropVariant pv = PropVariant.FromBool(value);
            try { store.SetValue(ref key, ref pv); }
            finally { PropVariantClear(ref pv); }
        }
    }

    internal static class NativeWin
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        internal static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);
        internal const int SW_RESTORE = 9;
    }
}
