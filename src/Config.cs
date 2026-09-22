using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace WorkspaceLauncher
{
    public class WsItem
    {
        public string Name;       // 점프 목록에 표시할 이름
        public string Path;       // .code-workspace 파일 또는 폴더 경로
        public bool Show;         // 점프 목록에 노출할지 여부
        public string Group;      // 점프 목록 그룹(카테고리) 이름. 비우면 기본 그룹
        public string ExtraArgs;  // code.exe 에 추가로 넘길 인자 (선택)

        public WsItem()
        {
            Show = true;
            Group = "";
            ExtraArgs = "";
        }

        [ScriptIgnore]
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(Name)) return Name;
                if (string.IsNullOrEmpty(Path)) return "(이름 없음)";
                string f = System.IO.Path.GetFileName(Path);
                if (f.EndsWith(".code-workspace", StringComparison.OrdinalIgnoreCase))
                    f = f.Substring(0, f.Length - ".code-workspace".Length);
                return f;
            }
        }

        [ScriptIgnore]
        public bool Exists
        {
            get
            {
                if (string.IsNullOrEmpty(Path)) return false;
                return File.Exists(Path) || Directory.Exists(Path);
            }
        }
    }

    public class AppConfig
    {
        public List<string> ScanRoots;      // 자동 스캔할 폴더들
        public int ScanDepth;               // 하위 폴더 탐색 깊이
        public bool AutoScan;               // 실행할 때마다 자동 스캔
        public string CodePath;             // Code.exe 경로 ("" 이면 자동 탐색)
        public int MaxJumpItems;            // 0 = 윈도우가 허용하는 최대치 자동 사용
        public bool GroupByCategory;        // Group 별로 카테고리 나눠 표시
        public string CategoryTitle;        // 기본 카테고리 제목
        public string LeftClickMode;        // "new" = 빈 새 창, "folder" = 지정 폴더 열기
        public string LeftClickPath;        // LeftClickMode == "folder" 일 때 열 경로
        public List<WsItem> Items;

        public AppConfig()
        {
            ScanRoots = new List<string>();
            ScanDepth = 3;
            AutoScan = true;
            CodePath = "";
            MaxJumpItems = 0;
            GroupByCategory = false;
            CategoryTitle = "워크스페이스";
            LeftClickMode = "new";
            LeftClickPath = "";
            Items = new List<WsItem>();
        }
    }

    public static class ConfigStore
    {
        public const string AppId = "DevWorkspace.VSCodeWorkspaceLauncher";

        public static string ConfigDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "WorkspaceLauncher");
            }
        }

        public static string ConfigPath
        {
            get { return Path.Combine(ConfigDir, "config.json"); }
        }

        public static string ExeDir
        {
            get
            {
                return Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);
            }
        }

        public static string ExePath
        {
            get { return System.Reflection.Assembly.GetExecutingAssembly().Location; }
        }

        public static AppConfig Load()
        {
            AppConfig cfg = null;
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath, Encoding.UTF8);
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    ser.MaxJsonLength = int.MaxValue;
                    cfg = ser.Deserialize<AppConfig>(json);
                }
            }
            catch (Exception ex)
            {
                Log.Write("config load failed: " + ex.Message);
            }

            if (cfg == null) cfg = new AppConfig();
            if (cfg.Items == null) cfg.Items = new List<WsItem>();
            if (cfg.ScanRoots == null) cfg.ScanRoots = new List<string>();
            if (cfg.ScanDepth <= 0) cfg.ScanDepth = 3;
            if (string.IsNullOrEmpty(cfg.CategoryTitle)) cfg.CategoryTitle = "워크스페이스";
            if (string.IsNullOrEmpty(cfg.LeftClickMode)) cfg.LeftClickMode = "new";

            for (int i = 0; i < cfg.Items.Count; i++)
            {
                if (cfg.Items[i] == null) { cfg.Items.RemoveAt(i); i--; continue; }
                if (cfg.Items[i].Group == null) cfg.Items[i].Group = "";
                if (cfg.Items[i].ExtraArgs == null) cfg.Items[i].ExtraArgs = "";
            }

            if (cfg.ScanRoots.Count == 0)
            {
                string def = GuessWorkspaceRoot();
                if (def != null) cfg.ScanRoots.Add(def);
            }

            return cfg;
        }

        public static void Save(AppConfig cfg)
        {
            Directory.CreateDirectory(ConfigDir);
            JavaScriptSerializer ser = new JavaScriptSerializer();
            ser.MaxJsonLength = int.MaxValue;
            string json = JsonPretty.Format(ser.Serialize(cfg));
            File.WriteAllText(ConfigPath, json, new UTF8Encoding(false));
        }

        /// <summary>
        /// exe 위치에서 위로 올라가며 "workspace" 폴더를 찾는다.
        /// 못 찾으면 null 을 돌려주고, 사용자가 설정 탭에서 직접 지정한다.
        /// </summary>
        public static string GuessWorkspaceRoot()
        {
            try
            {
                string dir = ExeDir;
                for (int up = 0; up < 4 && dir != null; up++)
                {
                    string c = Path.Combine(dir, "workspace");
                    if (Directory.Exists(c)) return c;
                    dir = Path.GetDirectoryName(dir);
                }
            }
            catch { }
            return null;
        }

        /// <summary>Code.exe 경로를 찾는다.</summary>
        public static string ResolveCodeExe(AppConfig cfg)
        {
            if (cfg != null && !string.IsNullOrEmpty(cfg.CodePath) && File.Exists(cfg.CodePath))
                return cfg.CodePath;

            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            string[] candidates = new string[]
            {
                Path.Combine(local, @"Programs\Microsoft VS Code\Code.exe"),
                Path.Combine(pf, @"Microsoft VS Code\Code.exe"),
                Path.Combine(pf86, @"Microsoft VS Code\Code.exe"),
                Path.Combine(local, @"Programs\Microsoft VS Code Insiders\Code - Insiders.exe"),
                Path.Combine(pf, @"Microsoft VS Code Insiders\Code - Insiders.exe"),
            };

            foreach (string c in candidates)
                if (File.Exists(c)) return c;

            // PATH 검색
            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                foreach (string p in pathEnv.Split(';'))
                {
                    if (string.IsNullOrEmpty(p)) continue;
                    try
                    {
                        string c = Path.Combine(p.Trim(), "Code.exe");
                        if (File.Exists(c)) return c;
                    }
                    catch { }
                }
            }
            return null;
        }

        /// <summary>스캔 루트에서 .code-workspace 파일을 찾아 기존 목록과 병합(순서 보존).</summary>
        public static int SyncFromDisk(AppConfig cfg)
        {
            List<string> found = new List<string>();
            foreach (string root in cfg.ScanRoots)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                ScanDir(root, cfg.ScanDepth, found);
            }

            HashSet<string> known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (WsItem it in cfg.Items)
                if (!string.IsNullOrEmpty(it.Path)) known.Add(NormalizePath(it.Path));

            int added = 0;
            foreach (string f in found)
            {
                if (known.Contains(NormalizePath(f))) continue;
                WsItem it = new WsItem();
                it.Path = f;
                it.Name = null;
                it.Show = true;
                cfg.Items.Add(it);
                known.Add(NormalizePath(f));
                added++;
            }
            return added;
        }

        private static void ScanDir(string dir, int depthLeft, List<string> outList)
        {
            try
            {
                string[] files = Directory.GetFiles(dir, "*.code-workspace");
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                outList.AddRange(files);
            }
            catch { }

            if (depthLeft <= 1) return;

            try
            {
                string[] subs = Directory.GetDirectories(dir);
                Array.Sort(subs, StringComparer.OrdinalIgnoreCase);
                foreach (string s in subs)
                {
                    string name = Path.GetFileName(s);
                    if (name.StartsWith(".")) continue;
                    if (string.Equals(name, "node_modules", StringComparison.OrdinalIgnoreCase)) continue;
                    ScanDir(s, depthLeft - 1, outList);
                }
            }
            catch { }
        }

        public static string NormalizePath(string p)
        {
            try { return Path.GetFullPath(p).TrimEnd('\\'); }
            catch { return p; }
        }
    }

    /// <summary>JavaScriptSerializer 결과를 사람이 읽기 좋은 형태로 들여쓰기.</summary>
    internal static class JsonPretty
    {
        public static string Format(string json)
        {
            StringBuilder sb = new StringBuilder(json.Length * 2);
            int indent = 0;
            bool inStr = false;
            bool escape = false;

            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];

                if (inStr)
                {
                    sb.Append(c);
                    if (escape) { escape = false; }
                    else if (c == '\\') { escape = true; }
                    else if (c == '"') { inStr = false; }
                    continue;
                }

                switch (c)
                {
                    case '"':
                        inStr = true;
                        sb.Append(c);
                        break;
                    case '{':
                    case '[':
                        sb.Append(c);
                        indent++;
                        NewLine(sb, indent);
                        break;
                    case '}':
                    case ']':
                        indent--;
                        NewLine(sb, indent);
                        sb.Append(c);
                        break;
                    case ',':
                        sb.Append(c);
                        NewLine(sb, indent);
                        break;
                    case ':':
                        sb.Append(": ");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private static void NewLine(StringBuilder sb, int indent)
        {
            sb.Append("\r\n");
            sb.Append(' ', indent * 2);
        }
    }

    internal static class Log
    {
        public static void Write(string msg)
        {
            try
            {
                string path = Path.Combine(ConfigStore.ConfigDir, "launcher.log");
                Directory.CreateDirectory(ConfigStore.ConfigDir);
                File.AppendAllText(path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + "\r\n",
                    new UTF8Encoding(false));
            }
            catch { }
        }
    }
}
