using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WorkspaceLauncher
{
    public class JumpListResult
    {
        public uint MaxSlots;       // 윈도우가 알려준 표시 가능 슬롯 수
        public int TotalItems;      // 설정에 등록된 전체 항목 수
        public int ShownItems;      // 점프 목록에 실제로 넣은 항목 수
        public bool Ok;
        public string Error;
    }

    public static class JumpListBuilder
    {
        private const string ARG_OPEN = "--open";

        public static JumpListResult Build(AppConfig cfg)
        {
            JumpListResult res = new JumpListResult();
            ICustomDestinationList cdl = null;

            try
            {
                cdl = (ICustomDestinationList)Shell.CreateInstance(Shell.CLSID_DestinationList);
                cdl.SetAppID(ConfigStore.AppId);

                uint maxSlots;
                object removedObj;
                Guid iid = Shell.IID_IObjectArray;
                int hr = cdl.BeginList(out maxSlots, ref iid, out removedObj);
                if (hr < 0) throw Marshal.GetExceptionForHR(hr);

                res.MaxSlots = maxSlots;
                HashSet<string> removed = ReadRemovedArgs(removedObj);

                string exe = ConfigStore.ExePath;
                string codeExe = ConfigStore.ResolveCodeExe(cfg);
                string iconSrc = string.IsNullOrEmpty(codeExe) ? exe : codeExe;

                // ---- 1) 사용자 작업(Tasks) 영역 ----
                IObjectCollection tasks =
                    (IObjectCollection)Shell.CreateInstance(Shell.CLSID_EnumerableObjectCollection);

                tasks.AddObject(MakeLink(exe, "--new", "새 창 (빈 프로젝트)",
                                         "VS Code 를 빈 창으로 엽니다", iconSrc, 0));
                tasks.AddObject(MakeSeparator());
                tasks.AddObject(MakeLink(exe, "--manage", "워크스페이스 관리 / 전체 목록…",
                                         "전체 목록 열기, 순서 변경, 추가/삭제", exe, 0));
                tasks.AddObject(MakeLink(exe, "--refresh", "점프 목록 새로 고침",
                                         "폴더를 다시 스캔하고 목록을 갱신합니다", exe, 0));

                hr = cdl.AddUserTasks((IObjectArray)tasks);
                if (hr < 0) Log.Write("AddUserTasks hr=0x" + hr.ToString("X8"));

                // ---- 2) 워크스페이스 카테고리 ----
                List<WsItem> visible = new List<WsItem>();
                foreach (WsItem it in cfg.Items)
                {
                    if (it == null || !it.Show) continue;
                    if (string.IsNullOrEmpty(it.Path)) continue;
                    visible.Add(it);
                }
                res.TotalItems = visible.Count;

                int capacity = (int)maxSlots;
                if (capacity <= 0) capacity = 10;
                if (cfg.MaxJumpItems > 0 && cfg.MaxJumpItems < capacity) capacity = cfg.MaxJumpItems;

                // 그룹 구성 (순서 유지)
                List<string> groupOrder = new List<string>();
                Dictionary<string, List<WsItem>> groups = new Dictionary<string, List<WsItem>>();
                foreach (WsItem it in visible)
                {
                    string g = cfg.GroupByCategory && !string.IsNullOrEmpty(it.Group)
                        ? it.Group
                        : cfg.CategoryTitle;
                    if (!groups.ContainsKey(g))
                    {
                        groups[g] = new List<WsItem>();
                        groupOrder.Add(g);
                    }
                    groups[g].Add(it);
                }

                int budget = capacity;
                int shown = 0;

                foreach (string g in groupOrder)
                {
                    if (budget <= 0) break;

                    IObjectCollection col =
                        (IObjectCollection)Shell.CreateInstance(Shell.CLSID_EnumerableObjectCollection);
                    int inThis = 0;

                    foreach (WsItem it in groups[g])
                    {
                        if (budget <= 0) break;
                        string args = BuildOpenArgs(it);
                        if (removed.Contains(args.ToLowerInvariant())) continue;

                        string desc = it.Path;
                        if (!it.Exists) desc = "(경로 없음) " + it.Path;

                        col.AddObject(MakeLink(exe, args, it.DisplayName, desc, iconSrc, 0));
                        inThis++;
                        budget--;
                    }

                    if (inThis == 0) continue;

                    hr = cdl.AppendCategory(g, (IObjectArray)col);
                    if (hr < 0)
                    {
                        Log.Write("AppendCategory('" + g + "') hr=0x" + hr.ToString("X8"));
                        budget += inThis; // 실패했으니 예산 복구
                    }
                    else
                    {
                        shown += inThis;
                    }
                }

                res.ShownItems = shown;
                cdl.CommitList();
                res.Ok = true;
            }
            catch (Exception ex)
            {
                res.Ok = false;
                res.Error = ex.Message;
                Log.Write("JumpList build failed: " + ex);
                if (cdl != null) { try { cdl.AbortList(); } catch { } }
            }
            finally
            {
                if (cdl != null) { try { Marshal.ReleaseComObject(cdl); } catch { } }
            }

            return res;
        }

        public static void Clear()
        {
            try
            {
                ICustomDestinationList cdl =
                    (ICustomDestinationList)Shell.CreateInstance(Shell.CLSID_DestinationList);
                cdl.DeleteList(ConfigStore.AppId);
                Marshal.ReleaseComObject(cdl);
            }
            catch (Exception ex) { Log.Write("Clear failed: " + ex.Message); }
        }

        public static string BuildOpenArgs(WsItem it)
        {
            string s = ARG_OPEN + " \"" + it.Path + "\"";
            if (!string.IsNullOrEmpty(it.ExtraArgs)) s += " " + it.ExtraArgs.Trim();
            return s;
        }

        private static HashSet<string> ReadRemovedArgs(object removedObj)
        {
            HashSet<string> set = new HashSet<string>();
            IObjectArray arr = removedObj as IObjectArray;
            if (arr == null) return set;

            try
            {
                uint count;
                arr.GetCount(out count);
                Guid iidLink = Shell.IID_IShellLinkW;
                for (uint i = 0; i < count; i++)
                {
                    object o;
                    try { arr.GetAt(i, ref iidLink, out o); }
                    catch { continue; }
                    IShellLinkW link = o as IShellLinkW;
                    if (link == null) continue;
                    StringBuilder sb = new StringBuilder(2048);
                    link.GetArguments(sb, sb.Capacity);
                    set.Add(sb.ToString().ToLowerInvariant());
                    Marshal.ReleaseComObject(link);
                }
            }
            catch (Exception ex) { Log.Write("ReadRemoved: " + ex.Message); }

            return set;
        }

        internal static IShellLinkW MakeLink(string exe, string args, string title,
                                             string description, string iconPath, int iconIndex)
        {
            IShellLinkW link = (IShellLinkW)Shell.CreateInstance(Shell.CLSID_ShellLink);
            link.SetPath(exe);
            link.SetArguments(args);
            if (!string.IsNullOrEmpty(description))
            {
                string d = description;
                if (d.Length > 255) d = d.Substring(0, 255);
                link.SetDescription(d);
            }
            try { link.SetWorkingDirectory(Path.GetDirectoryName(exe)); }
            catch { }
            if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                link.SetIconLocation(iconPath, iconIndex);

            IPropertyStore store = (IPropertyStore)link;
            Shell.SetStringProp(store, Shell.PKEY_Title, title);
            store.Commit();
            return link;
        }

        internal static IShellLinkW MakeSeparator()
        {
            IShellLinkW link = (IShellLinkW)Shell.CreateInstance(Shell.CLSID_ShellLink);
            IPropertyStore store = (IPropertyStore)link;
            Shell.SetBoolProp(store, Shell.PKEY_AppUserModel_IsDestListSeparator, true);
            store.Commit();
            return link;
        }
    }

    public static class ShortcutMaker
    {
        /// <summary>
        /// 시작 메뉴에 AppUserModelID 가 찍힌 바로가기를 만든다.
        /// (이 바로가기를 작업 표시줄에 고정해야 점프 목록이 붙는다)
        /// </summary>
        public static string CreateStartMenuShortcut(string linkName, string iconPath)
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Start Menu\Programs");
            Directory.CreateDirectory(dir);
            string lnkPath = Path.Combine(dir, linkName + ".lnk");

            IShellLinkW link = (IShellLinkW)Shell.CreateInstance(Shell.CLSID_ShellLink);
            link.SetPath(ConfigStore.ExePath);
            link.SetArguments("");
            link.SetWorkingDirectory(ConfigStore.ExeDir);
            link.SetDescription("VS Code 워크스페이스 런처");
            if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                link.SetIconLocation(iconPath, 0);

            IPropertyStore store = (IPropertyStore)link;
            Shell.SetStringProp(store, Shell.PKEY_AppUserModel_ID, ConfigStore.AppId);
            store.Commit();

            IPersistFile pf = (IPersistFile)link;
            pf.Save(lnkPath, true);

            Marshal.ReleaseComObject(link);
            return lnkPath;
        }
    }
}
