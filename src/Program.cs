using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace WorkspaceLauncher
{
    internal static class Program
    {
        internal static readonly uint WM_SHOW_MANAGER =
            NativeWin.RegisterWindowMessage("WorkspaceLauncher.ShowManager.v1");

        private static Mutex _mutex;

        [STAThread]
        private static int Main(string[] argv)
        {
            // 명시적 AppUserModelID 는 일부러 설정하지 않는다.
            // 윈도우가 exe 경로에서 만들어 주는 ID 를 쓰면, 시작 메뉴 바로가기로 고정하든
            // exe 를 직접 끌어다 고정하든 같은 ID 가 되어 점프 목록이 항상 붙는다.

            string mode = "new";
            string target = null;
            bool verbose = false;

            for (int i = 0; i < argv.Length; i++)
            {
                string a = argv[i];
                switch (a.ToLowerInvariant())
                {
                    case "--open":
                    case "-o":
                        mode = "open";
                        if (i + 1 < argv.Length) { target = argv[++i]; }
                        break;
                    case "--new":
                    case "-n":
                        mode = "new";
                        break;
                    case "--manage":
                    case "-m":
                        mode = "manage";
                        break;
                    case "--refresh":
                    case "-r":
                        mode = "refresh";
                        break;
                    case "--install":
                        mode = "install";
                        break;
                    case "--clear":
                        mode = "clear";
                        break;
                    case "--verbose":
                    case "-v":
                        verbose = true;
                        break;
                    case "--help":
                    case "-h":
                    case "/?":
                        mode = "help";
                        break;
                    default:
                        // 인자 없이 경로만 넘어온 경우도 지원
                        if (!a.StartsWith("-") && target == null)
                        {
                            mode = "open";
                            target = a;
                        }
                        break;
                }
            }

            switch (mode)
            {
                case "open": return DoOpen(target);
                case "manage": return DoManage();
                case "refresh": return DoRefresh(verbose);
                case "install": return DoInstall();
                case "clear": return DoClear();
                case "help": return DoHelp();
                default: return DoNew();
            }
        }

        private static int DoOpen(string target)
        {
            AppConfig cfg = ConfigStore.Load();
            string err;
            if (!VsCode.OpenPath(cfg, target, false, out err))
            {
                Err(err);
                return 1;
            }
            return 0;
        }

        private static int DoNew()
        {
            AppConfig cfg = ConfigStore.Load();
            string err;
            bool ok = VsCode.OpenEmpty(cfg, out err);
            if (!ok) Err(err);

            // 클릭할 때마다 워크스페이스 폴더를 다시 훑어 점프 목록을 최신으로 유지
            try
            {
                if (cfg.AutoScan)
                {
                    int added = ConfigStore.SyncFromDisk(cfg);
                    if (added > 0 || !File.Exists(ConfigStore.ConfigPath))
                        ConfigStore.Save(cfg);
                }
                JumpListBuilder.Build(cfg);
            }
            catch (Exception ex) { Log.Write("DoNew refresh: " + ex.Message); }

            return ok ? 0 : 1;
        }

        private static int DoRefresh(bool verbose)
        {
            AppConfig cfg = ConfigStore.Load();
            int added = ConfigStore.SyncFromDisk(cfg);
            ConfigStore.Save(cfg);
            JumpListResult r = JumpListBuilder.Build(cfg);

            if (verbose)
            {
                string msg;
                if (r.Ok)
                {
                    msg = "점프 목록을 갱신했습니다.\r\n\r\n" +
                          "새로 찾은 워크스페이스: " + added + "개\r\n" +
                          "등록된 항목: " + r.TotalItems + "개\r\n" +
                          "점프 목록에 표시: " + r.ShownItems + "개\r\n" +
                          "윈도우가 허용한 슬롯: " + r.MaxSlots + "개";
                }
                else
                {
                    msg = "점프 목록 갱신 실패: " + r.Error;
                }
                MessageBox.Show(msg, "VS Code 워크스페이스 런처",
                                MessageBoxButtons.OK,
                                r.Ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            }
            return r.Ok ? 0 : 1;
        }

        private static int DoInstall()
        {
            AppConfig cfg = ConfigStore.Load();
            ConfigStore.SyncFromDisk(cfg);
            ConfigStore.Save(cfg);
            JumpListBuilder.Build(cfg);
            string lnk = ShortcutMaker.CreateStartMenuShortcut(
                "VS Code 워크스페이스", IconPath());
            Console.WriteLine(lnk);
            return 0;
        }

        private static int DoClear()
        {
            JumpListBuilder.Clear();
            return 0;
        }

        private static int DoHelp()
        {
            MessageBox.Show(
                "VS Code 워크스페이스 런처\r\n\r\n" +
                "(인자 없음)        빈 VS Code 창 열기\r\n" +
                "--open <경로>      해당 워크스페이스/폴더 열기\r\n" +
                "--manage           관리 창 열기 (전체 목록 · 순서 변경)\r\n" +
                "--refresh [-v]     폴더 재스캔 후 점프 목록 갱신\r\n" +
                "--install          시작 메뉴 바로가기 생성 + 점프 목록 등록\r\n" +
                "--clear            점프 목록 삭제\r\n\r\n" +
                "설정 파일: " + ConfigStore.ConfigPath,
                "도움말", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }

        private static int DoManage()
        {
            bool createdNew;
            _mutex = new Mutex(true, "Local\\WorkspaceLauncher.Manager.v1", out createdNew);
            if (!createdNew)
            {
                // 이미 떠 있으면 그 창을 앞으로
                NativeWin.PostMessage(NativeWin.HWND_BROADCAST, WM_SHOW_MANAGER,
                                      IntPtr.Zero, IntPtr.Zero);
                return 0;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ManagerForm());
            return 0;
        }

        internal static string IconPath()
        {
            List<string> candidates = new List<string>();
            try
            {
                string dir = ConfigStore.ExeDir;
                for (int up = 0; up < 3 && dir != null; up++)
                {
                    candidates.Add(Path.Combine(dir, "workspace-icon.ico"));
                    dir = Path.GetDirectoryName(dir);
                }
            }
            catch { }
            foreach (string c in candidates)
                if (File.Exists(c)) return c;
            return ConfigStore.ExePath;
        }

        internal static void Err(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return;
            MessageBox.Show(msg, "VS Code 워크스페이스 런처",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
