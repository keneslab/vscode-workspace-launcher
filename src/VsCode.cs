using System;
using System.Diagnostics;
using System.IO;

namespace WorkspaceLauncher
{
    public static class VsCode
    {
        /// <summary>빈 새 창을 연다.</summary>
        public static bool OpenEmpty(AppConfig cfg, out string error)
        {
            if (string.Equals(cfg.LeftClickMode, "folder", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(cfg.LeftClickPath))
            {
                return OpenPath(cfg, cfg.LeftClickPath, true, out error);
            }
            return Run(cfg, "-n", null, out error);
        }

        /// <summary>워크스페이스 파일이나 폴더를 연다.</summary>
        public static bool OpenPath(AppConfig cfg, string path, bool newWindow, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(path))
            {
                error = "경로가 비어 있습니다.";
                return false;
            }
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                error = "경로를 찾을 수 없습니다:\r\n" + path;
                return false;
            }

            string args = "\"" + path + "\"";
            if (newWindow) args = "-n " + args;

            string wd = null;
            try
            {
                wd = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
            }
            catch { }

            return Run(cfg, args, wd, out error);
        }

        public static bool Run(AppConfig cfg, string args, string workingDir, out string error)
        {
            error = null;
            string exe = ConfigStore.ResolveCodeExe(cfg);
            if (string.IsNullOrEmpty(exe))
            {
                error = "VS Code (Code.exe) 를 찾지 못했습니다.\r\n" +
                        "설정 탭에서 Code.exe 경로를 직접 지정하세요.";
                return false;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe, args);
                psi.UseShellExecute = true;
                if (!string.IsNullOrEmpty(workingDir) && Directory.Exists(workingDir))
                    psi.WorkingDirectory = workingDir;
                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                error = "VS Code 실행 실패: " + ex.Message;
                Log.Write(error);
                return false;
            }
        }
    }
}
