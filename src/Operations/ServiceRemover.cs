using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Win10BloatRemover.Utils;

namespace Win10BloatRemover.Operations
{
    public class ServiceRemovalOperation : IOperation
    {
        private readonly string[] servicesToRemove;
        private readonly IUserInterface ui;
        private readonly ServiceRemover serviceRemover;

        public bool IsRebootRecommended => serviceRemover.IsRebootRecommended;

        public ServiceRemovalOperation(string[] servicesToRemove, IUserInterface ui, ServiceRemover serviceRemover)
        {
            this.servicesToRemove = servicesToRemove;
            this.ui = ui;
            this.serviceRemover = serviceRemover;
        }

        public void Run()
        {
            ui.PrintHeading("备份服务...");
            string[] actualBackuppedServices = serviceRemover.PerformBackup(servicesToRemove);

            if (actualBackuppedServices.Length > 0)
            {
                ui.PrintHeading("移除服务...");
                serviceRemover.PerformRemoval(actualBackuppedServices);
            }
        }
    }

    /*
     *  Performs backup (export of registry keys) and removal of those services whose name starts with the given service names.
     *  This is made in order to include services that end with a random code.
     */
    public class ServiceRemover
    {
        private readonly DirectoryInfo backupDirectory;
        private readonly IUserInterface ui;

        private const int SC_EXIT_CODE_MARKED_FOR_DELETION = 1072;

        public bool IsRebootRecommended { get; private set; }

        public ServiceRemover(IUserInterface ui) : this(ui, DateTime.Now) {}
        public ServiceRemover(IUserInterface ui, DateTime now)
        {
            this.ui = ui;
            backupDirectory = new DirectoryInfo($"servicesBackup_{now:yyyy-MM-dd_HH-mm-ss}");
        }

        public void BackupAndRemove(params string[] servicesToRemove)
        {
            IsRebootRecommended = false;
            string[] actualBackuppedServices = PerformBackup(servicesToRemove);
            PerformRemoval(actualBackuppedServices);
        }

        public string[] PerformBackup(string[] servicesToBackup)
        {
            string[] existingServices = FindExistingServicesWithNames(servicesToBackup);
            foreach (string service in existingServices)
                BackupService(service);
            return existingServices;
        }

        private string[] FindExistingServicesWithNames(string[] servicesNames)
        {
            string[] allExistingServices = GetAllServicesNames();
            List<string> allMatchingServices = new List<string>();
            foreach (string serviceName in servicesNames)
            {
                var matchingServices = allExistingServices.Where(name => name.StartsWith(serviceName)).ToArray();
                if (matchingServices.Length == 0)
                    ui.PrintMessage($"找不到名称为 {serviceName} 的服务.");
                else
                    allMatchingServices.AddRange(matchingServices);
            }

            return allMatchingServices.ToArray();
        }

        private string[] GetAllServicesNames()
        {
            using RegistryKey servicesKey = Registry.LocalMachine.OpenSubKeyWritable(@"SYSTEM\CurrentControlSet\Services");
            return servicesKey.GetSubKeyNames();
        }

        private void BackupService(string service)
        {
            EnsureBackupDirectoryExists();
            var regExportExitCode = SystemUtils.RunProcessBlocking(
                "reg", $@"export ""HKLM\SYSTEM\CurrentControlSet\Services\{service}"" ""{backupDirectory.FullName}\{service}.reg""");
            if (regExportExitCode.IsSuccessful())
                ui.PrintMessage($"服务{service} 完成备份.");
            else
                throw new Exception($"无法备份 {service} 服务.");
        }

        private void EnsureBackupDirectoryExists()
        {
            if (!backupDirectory.Exists)
                backupDirectory.Create();
        }

        public void PerformRemoval(string[] backuppedServices)
        {
            foreach (string service in backuppedServices)
                RemoveService(service);
        }

        private void RemoveService(string service)
        {
            var scExitCode = SystemUtils.RunProcessBlocking("sc", $"delete \"{service}\"");
            if (IsScRemovalSuccessful(scExitCode))
            {
                PrintSuccessMessage(scExitCode, service);
                if (scExitCode == SC_EXIT_CODE_MARKED_FOR_DELETION)
                    IsRebootRecommended = true;
            }
            else
            {
                // Unstoppable (but not protected) system services are not removable with SC,
                // but can be removed by deleting their Registry keys
                Debug.WriteLine($"对于 {service} SC删除失败，并显示退出代码 {scExitCode}.");
                DeleteServiceRegistryKey(service);
            }
        }

        private bool IsScRemovalSuccessful(ExitCode exitCode)
        {
            return exitCode.IsSuccessful() ||
                   exitCode == SC_EXIT_CODE_MARKED_FOR_DELETION;
        }

        private void PrintSuccessMessage(ExitCode scExitCode, string service)
        {
            if (scExitCode == SC_EXIT_CODE_MARKED_FOR_DELETION)
                ui.PrintMessage($"服务 {service} 将在系统重启后完成移除.");
            else
                ui.PrintMessage($"服务 {service} 成功移除.");
        }

        private void DeleteServiceRegistryKey(string service)
        {
            try
            {
                using var allServicesKey = Registry.LocalMachine.OpenSubKeyWritable(@"SYSTEM\CurrentControlSet\Services");
                allServicesKey.DeleteSubKeyTree(service);
                ui.PrintMessage($"服务 {service} 已移除, 但它将继续运行，直到下一次系统重新启动.");
                IsRebootRecommended = true;
            }
            catch (Exception exc)
            {
                ui.PrintError($"服务 {service} 移除失败: 无法删除其注册表项 ({exc.Message}).");
            }
        }
    }
}
