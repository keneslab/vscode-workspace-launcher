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
                string p = Path.TrimEnd('\\', '/');
                string f = System.IO.Path.GetFileName(p);
                if (string.IsNullOrEmpty(f)) f = p;          // "C:\" 같은 드라이브 루트
                if (f.EndsWith(".code-workspace", StringComparison.OrdinalIgnoreCase))
                    f = f.Substring(0, f.Length - ".code-workspace".Length);
                return f;
            }
        }

        /// <summary>.code-workspace 파일이면 true, 프로젝트 폴더면 false.</summary>
        [ScriptIgnore]
        public bool IsWorkspaceFile
        {
            get
            {
                return !string.IsNullOrEmpty(Path)
                    && Path.EndsWith(".code-workspace", StringComparison.OrdinalIgnoreCase);
            }
        }

        [ScriptIgnore]
        public string KindLabel
        {
            get { return IsWorkspaceFile ? "워크스페이스" : "폴더"; }
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
        public int ScanDepth;               // .code-workspace 파일을 찾을 깊이
        public bool AutoScan;               // 실행할 때마다 자동 스캔
        public bool ScanFolders;            // 프로젝트 폴더도 등록할지
        public int FolderScanDepth;         // 폴더를 등록할 깊이 (1 = 스캔 루트 바로 아래)
        public bool RequireProjectMarker;   // 프로젝트 표식이 있는 폴더만 등록
        public List<string> ProjectMarkers; // 프로젝트 표식 (RequireProjectMarker 일 때만 사용)
        public List<string> ExcludeDirs;    // 스캔에서 건너뛸 폴더 이름
        public string CodePath;             // Code.exe 경로 ("" 이면 자동 탐색)
        public int MaxJumpItems;            // 0 = 윈도우가 허용하는 최대치 자동 사용
        public bool GroupByCategory;        // Group 별로 카테고리 나눠 표시
        public string CategoryTitle;        // 기본 카테고리 제목
        public string LeftClickMode;        // "new" = 빈 새 창, "folder" = 지정 폴더 열기
        public string LeftClickPath;        // LeftClickMode == "folder" 일 때 열 경로
        public List<WsItem> Items;

        public static readonly string[] DefaultProjectMarkers = new string[]
        {
            ".git", ".vscode", ".svn", ".idea",
            "package.json", "composer.json", "pyproject.toml", "requirements.txt",
            "Cargo.toml", "go.mod", "pom.xml", "build.gradle", "Gemfile",
            "CMakeLists.txt", "Makefile", "Dockerfile", "docker-compose.yml",
            "*.sln", "*.csproj", "pubspec.yaml"
        };

        public static readonly string[] DefaultExcludeDirs = new string[]
        {
            "node_modules", "vendor", "bin", "obj", "dist", "build", "out",
            "target", "packages", "__pycache__", "venv", ".venv", "AppData"
        };

        public AppConfig()
        {
            ScanRoots = new List<string>();
            ScanDepth = 3;
            AutoScan = true;
            ScanFolders = true;
            FolderScanDepth = 1;
            RequireProjectMarker = false;
            ProjectMarkers = new List<string>(DefaultProjectMarkers);
            ExcludeDirs = new List<string>(DefaultExcludeDirs);
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
        /// <summary>
        /// 1.0 에서 쓰던 명시적 AppUserModelID.
        /// 지금은 윈도우가 exe 경로에서 자동으로 만드는 ID 를 그대로 쓴다.
        /// 이렇게 해야 exe 를 직접 작업 표시줄에 끌어다 고정해도 점프 목록이 붙는다.
        /// (남아 있는 예전 목록을 지우는 데만 쓰인다)
        /// </summary>
        public const string LegacyAppId = "DevWorkspace.VSCodeWorkspaceLauncher";

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
            if (cfg.FolderScanDepth <= 0) cfg.FolderScanDepth = 1;
            if (cfg.ProjectMarkers == null || cfg.ProjectMarkers.Count == 0)
                cfg.ProjectMarkers = new List<string>(AppConfig.DefaultProjectMarkers);
            if (cfg.ExcludeDirs == null || cfg.ExcludeDirs.Count == 0)
                cfg.ExcludeDirs = new List<string>(AppConfig.DefaultExcludeDirs);
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

        /// <summary>
        /// 스캔 루트에서 .code-workspace 파일과 프로젝트 폴더를 찾아 기존 목록과 병합한다.
        /// 기존 항목의 순서는 그대로 두고 새 항목만 뒤에 붙인다.
        /// </summary>
        public static int SyncFromDisk(AppConfig cfg)
        {
            List<string> found = new List<string>();
            foreach (string root in cfg.ScanRoots)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                ScanDir(NormalizePath(root), 0, cfg, found);
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

        /// <param name="level">스캔 루트가 0, 그 바로 아래가 1.</param>
        private static void ScanDir(string dir, int level, AppConfig cfg, List<string> outList)
        {
            // 1) 이 폴더 안의 .code-workspace 파일
            string[] files;
            try
            {
                files = Directory.GetFiles(dir, "*.code-workspace");
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            }
            catch { files = new string[0]; }

            // 2) 프로젝트 폴더로 등록
            //    - 스캔 루트 자신은 등록하지 않는다 (프로젝트를 담는 그릇으로 본다)
            //    - .code-workspace 가 들어 있는 폴더는 그 워크스페이스 파일이 대표하므로 제외
            if (cfg.ScanFolders && level >= 1 && level <= cfg.FolderScanDepth && files.Length == 0)
            {
                if (!cfg.RequireProjectMarker || HasProjectMarker(dir, cfg))
                    outList.Add(dir);
            }

            outList.AddRange(files);

            // 3) 하위로. 워크스페이스 파일 탐색 깊이와 폴더 등록 깊이 중 더 깊은 쪽까지만.
            int maxLevel = cfg.ScanDepth - 1;
            if (cfg.ScanFolders && cfg.FolderScanDepth > maxLevel) maxLevel = cfg.FolderScanDepth;
            if (level >= maxLevel) return;

            try
            {
                string[] subs = Directory.GetDirectories(dir);
                Array.Sort(subs, StringComparer.OrdinalIgnoreCase);
                foreach (string s in subs)
                {
                    if (IsExcludedDir(s, cfg)) continue;
                    ScanDir(s, level + 1, cfg, outList);
                }
            }
            catch { }
        }

        private static bool IsExcludedDir(string dir, AppConfig cfg)
        {
            string name = Path.GetFileName(dir.TrimEnd('\\', '/'));
            if (string.IsNullOrEmpty(name)) return true;
            if (name.StartsWith(".")) return true;

            try
            {
                FileAttributes a = File.GetAttributes(dir);
                if ((a & FileAttributes.Hidden) != 0) return true;
                if ((a & FileAttributes.ReparsePoint) != 0) return true;   // 심볼릭 링크 순환 방지
            }
            catch { return true; }

            foreach (string ex in cfg.ExcludeDirs)
                if (string.Equals(name, ex, StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }

        private static bool HasProjectMarker(string dir, AppConfig cfg)
        {
            foreach (string marker in cfg.ProjectMarkers)
            {
                if (string.IsNullOrEmpty(marker)) continue;
                try
                {
                    if (marker.IndexOf('*') >= 0)
                    {
                        if (Directory.GetFiles(dir, marker).Length > 0) return true;
                    }
                    else
                    {
                        string p = Path.Combine(dir, marker);
                        if (File.Exists(p) || Directory.Exists(p)) return true;
                    }
                }
                catch { }
            }
            return false;
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
