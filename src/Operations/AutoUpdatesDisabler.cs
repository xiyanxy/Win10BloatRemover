﻿using Microsoft.Win32;

namespace Win10BloatRemover.Operations
{
    public class AutoUpdatesDisabler : IOperation
    {
        private readonly IUserInterface ui;
        public AutoUpdatesDisabler(IUserInterface ui) => this.ui = ui;

        public void Run()
        {
            ui.PrintMessage("将值写入注册表...");
            DisableAutomaticWindowsUpdates();
            DisableAutomaticStoreUpdates();
            DisableAutomaticSpeechModelUpdates();
        }

        private void DisableAutomaticWindowsUpdates()
        {
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "NoAutoUpdate", 1);
        }

        private void DisableAutomaticStoreUpdates()
        {
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\WindowsStore", "AutoDownload", 2);
        }

        private void DisableAutomaticSpeechModelUpdates()
        {
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Speech", "AllowSpeechModelUpdate", 0);
        }
    }
}
