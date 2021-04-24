﻿using System;
using System.IO;
using System.Linq;
using System.Threading;
using Win10BloatRemover.Utils;
using static System.Environment;
using Version = System.Version;

namespace Win10BloatRemover.Operations
{
    public class EdgeRemover : IOperation
    {
        private readonly IUserInterface ui;
        private readonly IOperation legacyEdgeRemover;

        public EdgeRemover(IUserInterface ui, IOperation legacyEdgeRemover)
        {
            this.ui = ui;
            this.legacyEdgeRemover = legacyEdgeRemover;
        }

        public void Run()
        {
            UninstallEdgeChromiumIfPresent();
            legacyEdgeRemover.Run();
        }

        private void UninstallEdgeChromiumIfPresent()
        {
            ui.PrintHeading("卸载新版Edge...");
            string? installerPath = RetrieveEdgeChromiumInstallerPath();
            if (installerPath == null)
            {
                ui.PrintMessage("新版Edge尚未安装.");
                return;
            }

            ui.PrintMessage("运行卸载程序...");
            SystemUtils.RunProcessBlocking(installerPath, "--uninstall --force-uninstall --system-level");
            // Since actual uninstallation happens in another process launched by the installer, we wait
            // for a reasonable amount of time to let this process do its work before continuing
            Thread.Sleep(TimeSpan.FromSeconds(5));
        }

        private string? RetrieveEdgeChromiumInstallerPath()
        {
            string edgeChromiumBaseFolder = $@"{GetFolderPath(SpecialFolder.ProgramFilesX86)}\Microsoft\Edge\Application";
            if (!Directory.Exists(edgeChromiumBaseFolder))
                return null;

            Version? edgeChromiumVersion = null;
            string? folderOfSpecificEdgeVersion = Directory.EnumerateDirectories(edgeChromiumBaseFolder)
                .FirstOrDefault(folder => Version.TryParse(new DirectoryInfo(folder).Name, out edgeChromiumVersion));

            if (folderOfSpecificEdgeVersion != null)
            {
                string installerPath = $@"{folderOfSpecificEdgeVersion}\Installer\setup.exe";
                if (File.Exists(installerPath))
                {
                    ui.PrintMessage($"检测到的新版Edge版本 {edgeChromiumVersion}.");
                    return installerPath;
                }
            }
            return null;
        }
    }
}
