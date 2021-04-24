﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Data.Sqlite;
using Win10BloatRemover.Utils;

namespace Win10BloatRemover.Operations
{
    public class SystemAppsRemovalEnabler : IOperation
    {
        private enum EditingOutcome
        {
            NoChangesMade,
            ContentWasUpdated
        }

        private const string STATE_REPOSITORY_DB_NAME = "StateRepository-Machine.srd";
        private const string STATE_REPOSITORY_DB_PATH =
            @"C:\ProgramData\Microsoft\Windows\AppRepository\" + STATE_REPOSITORY_DB_NAME;
        private const string AFTER_PACKAGE_UPDATE_TRIGGER_NAME = "TRG_AFTER_UPDATE_Package_SRJournal";

        private /*lateinit*/ SqliteConnection dbConnection;
        private readonly IUserInterface ui;

        #nullable disable warnings
        public SystemAppsRemovalEnabler(IUserInterface ui) => this.ui = ui;
        #nullable restore warnings

        public void Run()
        {
            using (TokenPrivilege.Backup)
            using (TokenPrivilege.Restore)
            {
                string temporaryDatabaseCopy = CopyStateRepositoryDatabaseTo($"{STATE_REPOSITORY_DB_NAME}.tmp");
                try
                {
                    var outcome = EditStateRepositoryDatabase(temporaryDatabaseCopy);
                    if (outcome == EditingOutcome.ContentWasUpdated)
                        ReplaceStateRepositoryDatabaseWith(temporaryDatabaseCopy);
                    else
                        ui.PrintNotice("原始数据库无需更换：未进行任何更改.");
                }
                finally
                {
                    if (File.Exists(temporaryDatabaseCopy))
                        File.Delete(temporaryDatabaseCopy);
                }
            }
        }

        private string CopyStateRepositoryDatabaseTo(string databaseCopyPath)
        {
            EnsureAppXServicesAreStopped();
            var database = new FileInfo(STATE_REPOSITORY_DB_PATH);
            FileInfo copiedDatabase = database.CopyTo(databaseCopyPath, overwrite: true);
            return copiedDatabase.FullName;
        }

        private EditingOutcome EditStateRepositoryDatabase(string databaseCopyPath)
        {
            ui.PrintHeading("编辑状态存储库数据库的临时副本...");
            using (dbConnection = new SqliteConnection($"Data Source={databaseCopyPath}"))
            {
                dbConnection.Open();

                // There's an AFTER UPDATE trigger on the table we want to edit that causes problems,
                // so we must prevent it from being run
                string? createTriggerCode = RetrieveAfterPackageUpdateTriggerCode();
                DeleteAfterPackageUpdateTrigger();
                int updatedRows = EditPackageTable();
                if (createTriggerCode != null)
                    ReAddAfterPackageUpdateTrigger(createTriggerCode);

                ui.PrintMessage($"修改 {updatedRows} {(updatedRows == 1 ? "row" : "rows")}.");
                return updatedRows == 0 ? EditingOutcome.NoChangesMade : EditingOutcome.ContentWasUpdated;
            }
        }

        private void ReplaceStateRepositoryDatabaseWith(string databaseCopyPath)
        {
            ui.PrintHeading("用编辑后的副本替换原始状态存储库数据库...");
            EnsureAppXServicesAreStopped();
            // File.Copy can't be used to replace the file because it fails with access denied
            // even though we have Restore privilege, so we need to use File.Move instead
            File.Move(databaseCopyPath, STATE_REPOSITORY_DB_PATH, overwrite: true);
            ui.PrintMessage("替换完成.");
        }

        private void EnsureAppXServicesAreStopped()
        {
            ui.PrintMessage("在继续操作之前，请确保已停止与AppX相关的服务...");
            // Workaround for some rare circumstances in which attempts to stop these services fail for no apparent reason
            int currentAttempt = 1;
            bool servicesStoppedSuccessfully = false;
            while (!servicesStoppedSuccessfully)
            {
                try
                {
                    SystemUtils.StopServiceAndItsDependents("StateRepository");
                    servicesStoppedSuccessfully = true;
                }
                catch
                {
                    Debug.WriteLine($"尝试失败 {currentAttempt}.");
                    if (currentAttempt == 3)
                        throw;
                    currentAttempt++;
                    Thread.Sleep(TimeSpan.FromMilliseconds(30));
                }
            }
        }

        private string? RetrieveAfterPackageUpdateTriggerCode()
        {
            using var query = new SqliteCommand(
                $"SELECT sql FROM sqlite_master WHERE name='{AFTER_PACKAGE_UPDATE_TRIGGER_NAME}'",
                dbConnection
            );
            return (string?) query.ExecuteScalar();
        }

        private void DeleteAfterPackageUpdateTrigger()
        {
            using var query = new SqliteCommand(
                $"DROP TRIGGER IF EXISTS {AFTER_PACKAGE_UPDATE_TRIGGER_NAME}",
                dbConnection
            );
            query.ExecuteNonQuery();
        }

        private int EditPackageTable()
        {
            using var query = new SqliteCommand(
                "UPDATE Package SET IsInbox=0 WHERE IsInbox=1",
                dbConnection
            );
            int updatedRows = query.ExecuteNonQuery();
            return updatedRows;
        }

        private void ReAddAfterPackageUpdateTrigger(string createTriggerQuery)
        {
            using var query = new SqliteCommand(createTriggerQuery, dbConnection);
            query.ExecuteNonQuery();
        }
    }
}
