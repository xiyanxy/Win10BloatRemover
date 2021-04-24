﻿using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Win10BloatRemover.Operations;

namespace Win10BloatRemover
{
    class ConfigurationException : Exception
    {
        public ConfigurationException() {}
        public ConfigurationException(string message) : base(message) {}
        public ConfigurationException(string message, Exception inner) : base(message, inner) {}
    }

    public class Configuration
    {
        private const string CONFIGURATION_FILE_NAME = "config.json";

        #nullable disable warnings
        [JsonProperty(Required = Required.Always)]
        public string[] ServicesToRemove { private set; get; }

        [JsonProperty(Required = Required.Always, ItemConverterType = typeof(StringEnumConverter))]
        public UWPAppGroup[] UWPAppsToRemove { private set; get; }

        [JsonProperty(Required = Required.Always)]
        [JsonConverter(typeof(StringEnumConverter))]
        public UWPAppRemovalMode UWPAppsRemovalMode { private set; get; }

        [JsonProperty(Required = Required.Always)]
        public string[] ScheduledTasksToDisable { private set; get; }

        [JsonProperty(Required = Required.Always)]
        public string[] WindowsFeaturesToRemove { private set; get; }
        #nullable restore warnings

        public static Configuration LoadFromFileOrDefault()
        { 
            if (File.Exists(CONFIGURATION_FILE_NAME))
                return ParseConfigFile();

            Default.WriteToFile();
            return Default;
        }

        private static Configuration ParseConfigFile()
        {
            try
            {
                var parsedConfiguration =
                    JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(CONFIGURATION_FILE_NAME));
                return parsedConfiguration;
            }
            catch (Exception exc)
            {
                throw new ConfigurationException($"加载自定义设置文件时出错: {exc.Message}\n" +
                                                 "默认设置已加载.\n");
            }
        }

        private void WriteToFile()
        {
            try
            {
                string settingsFileContent = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(CONFIGURATION_FILE_NAME, settingsFileContent);
            }
            catch (Exception exc)
            {
                throw new ConfigurationException($"无法使用默认设置写入配置文件: {exc.Message}\n");
            }
        }

        public static readonly Configuration Default = new Configuration {
            ServicesToRemove = new[] {
                "dmwappushservice",
                "RetailDemo",
                "TroubleshootingSvc"
            },
            UWPAppsToRemove = new[] {   
                UWPAppGroup.Bing,
                UWPAppGroup.Cortana,
                UWPAppGroup.CommunicationsApps,
                UWPAppGroup.OneNote,
                UWPAppGroup.OfficeHub,
                UWPAppGroup.HelpAndFeedback,
                UWPAppGroup.Maps,
                UWPAppGroup.Messaging,
                UWPAppGroup.Mobile,
                UWPAppGroup.Skype,
                UWPAppGroup.Zune
            },
            WindowsFeaturesToRemove = new[] {
                "App.StepsRecorder",
                "App.Support.QuickAssist",
                "App.WirelessDisplay.Connect",
                "Browser.InternetExplorer",
                "Hello.Face",
                "MathRecognizer"
            },
            ScheduledTasksToDisable = new[] {
                @"\Microsoft\Windows\ApplicationData\DsSvcCleanup",
                @"\Microsoft\Windows\CloudExperienceHost\CreateObjectTask",
                @"\Microsoft\Windows\DiskFootprint\Diagnostics",
                @"\Microsoft\Windows\Maintenance\WinSAT",
                @"\Microsoft\Windows\Shell\FamilySafetyMonitor",
                @"\Microsoft\Windows\Shell\FamilySafetyRefreshTask",
                @"\Microsoft\Windows\License Manager\TempSignedLicenseExchange",
                @"\Microsoft\Windows\Clip\License Validation",
                @"\Microsoft\Windows\Power Efficiency Diagnostics\AnalyzeSystem",
                @"\Microsoft\Windows\PushToInstall\LoginCheck",
                @"\Microsoft\Windows\PushToInstall\Registration",
                @"\Microsoft\Windows\Subscription\EnableLicenseAcquisition",
                @"\Microsoft\Windows\Subscription\LicenseAcquisition",
                @"\Microsoft\Windows\Diagnosis\Scheduled",
                @"\Microsoft\Windows\Diagnosis\RecommendedTroubleshootingScanner"
            },
            UWPAppsRemovalMode = UWPAppRemovalMode.AllUsers
        };
    }
}
