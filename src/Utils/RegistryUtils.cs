﻿using System;
using System.Collections.Generic;
using System.Runtime.Loader;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;

namespace Win10BloatRemover.Utils
{
    static class RegistryUtils
    {
        private const string DEFAULT_USER_HIVE_PATH = @"HKEY_USERS\_loaded_Default";
        private static RegistryKey? defaultUserKey;
        public static RegistryKey DefaultUser => defaultUserKey ??= LoadDefaultUserHive();

        private static RegistryKey LoadDefaultUserHive()
        {
            var loadExitCode = SystemUtils.RunProcessBlocking(
                "reg", $@"load ""{DEFAULT_USER_HIVE_PATH}"" ""C:\Users\Default\NTUSER.DAT"""
            );
            if (loadExitCode.IsNotSuccessful())
                throw new Exception("无法加载默认用户注册表配置单元.");

            AssemblyLoadContext.Default.Unloading += _ => UnloadDefaultUserHive();
            return Registry.Users.OpenSubKeyWritable("_loaded_Default");
        }

        private static void UnloadDefaultUserHive()
        {
            defaultUserKey?.Close();
            SystemUtils.RunProcessBlocking("reg", $@"unload ""{DEFAULT_USER_HIVE_PATH}""");
        }

        public static void SetForCurrentAndDefaultUser(string keyPath, string valueName, object value)
        {
            Registry.SetValue($@"HKEY_CURRENT_USER\{keyPath}", valueName, value);
            using (RegistryKey key = DefaultUser.CreateSubKey(keyPath))
                key.SetValue(valueName, value);
        }

        public static void DeleteSubKeyValue(this RegistryKey registryKey, string subkeyName, string valueName)
        {
            using RegistryKey? subKey = registryKey.OpenSubKey(subkeyName, writable: true);
            subKey?.DeleteValue(valueName, throwOnMissingValue: false);
        }

        public static RegistryKey OpenSubKeyWritable(this RegistryKey registryKey, string subkeyName, RegistryRights? rights = null)
        {
            RegistryKey? subKey;
            if (rights == null)
                subKey = registryKey.OpenSubKey(subkeyName, RegistryKeyPermissionCheck.ReadWriteSubTree);
            else
                subKey = registryKey.OpenSubKey(subkeyName, RegistryKeyPermissionCheck.ReadWriteSubTree, rights.Value);

            if (subKey == null)
                throw new KeyNotFoundException($"Subkey {subkeyName} not found.");

            return subKey;
        }

        // It's up to the caller to obtain the needed token privileges (TakeOwnership) for this operation
        public static void GrantFullControlOnSubKey(this RegistryKey registryKey, string subkeyName)
        {
            using RegistryKey subKey = registryKey.OpenSubKeyWritable(subkeyName,
                RegistryRights.TakeOwnership | RegistryRights.ChangePermissions
            );
            RegistrySecurity accessRules = subKey.GetAccessControl();
            SecurityIdentifier currentUser = RetrieveCurrentUserIdentifier();
            accessRules.SetOwner(currentUser);
            accessRules.ResetAccessRule(
                new RegistryAccessRule(
                    currentUser,
                    RegistryRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow
                )
            );
            subKey.SetAccessControl(accessRules);
        }

        // It's up to the caller to obtain the needed privileges (TakeOwnership) for this operation
        public static void TakeOwnershipOnSubKey(this RegistryKey registryKey, string subkeyName)
        {
            using RegistryKey subKey = registryKey.OpenSubKeyWritable(subkeyName, RegistryRights.TakeOwnership);
            RegistrySecurity accessRules = subKey.GetAccessControl();
            accessRules.SetOwner(RetrieveCurrentUserIdentifier());
            subKey.SetAccessControl(accessRules);
        }

        private static SecurityIdentifier RetrieveCurrentUserIdentifier() 
            => WindowsIdentity.GetCurrent().User ?? throw new Exception("无法检索当前用户SID.");
    }
}
