﻿using System.Linq;
using System.Management.Automation;
using Win10BloatRemover.Utils;

namespace Win10BloatRemover.Operations
{
    public class FeaturesRemover : IOperation
    {
        private readonly string[] featuresToRemove;
        private readonly IUserInterface ui;

        private /*lateinit*/ PowerShell powerShell;

        public bool IsRebootRecommended { get; private set; }

        #nullable disable warnings
        public FeaturesRemover(string[] featuresToRemove, IUserInterface ui)
        {
            this.featuresToRemove = featuresToRemove;
            this.ui = ui;
        }
        #nullable restore warnings

        public void Run()
        {
            using (powerShell = PowerShellExtensions.CreateWithImportedModules("Dism").WithOutput(ui))
            {
                foreach (string capabilityName in featuresToRemove)
                    RemoveCapabilitiesWhoseNameStartsWith(capabilityName);
            }
        }

        private void RemoveCapabilitiesWhoseNameStartsWith(string capabilityName)
        {
            var capabilities = powerShell.Run($"Get-WindowsCapability -Online -Name {capabilityName}*");
            if (capabilities.Length == 0)
            {
                ui.PrintWarning($"找不到具有名称的功能 {capabilityName}.");
                return;
            }

            foreach (var capability in capabilities)
                RemoveCapability(capability);
        }

        private void RemoveCapability(dynamic capability)
        {
            if (capability.State.ToString() != "Installed")
            {
                ui.PrintMessage($"{capability.Name} 功能尚未安装.");
                return;
            }

            ui.PrintMessage($"移除 {capability.Name} 功能...");
            var result = powerShell.Run($"Remove-WindowsCapability -Online -Name {capability.Name}").First();
            if (result.RestartNeeded)
                IsRebootRecommended = true;

            if (capability.Name.StartsWith("Hello.Face"))
                new ScheduledTasksDisabler(new[] { @"\Microsoft\Windows\HelloFace\FODCleanupTask" }, ui).Run();
        }
    }
}
