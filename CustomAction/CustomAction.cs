using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using WixToolset.Dtf.WindowsInstaller;
using HanicWixToolsAndCompontents.DBUpdater;
using System.Text;

namespace CustomAction
{
    public class CustomActions
    {
        [CustomAction]
        public static ActionResult CA_UpdateDatabase(Session session)
        {
            session.Log("Begin CA_UpdateDatabase");
            bool success = false;

            string connectionstring = GenerateConnectionString(session);
            if (!uint.TryParse(session["MINDBVERSION"], out uint mindbVersion))
            {
                session.Log("Error while reading property 'MINDBVERSION'");
                return ActionResult.Failure;
            }
            if (!uint.TryParse(session["MAXDBVERSION"], out uint maxdbVersion))
            {
                session.Log("Error while reading property 'MAXDBVERSION'");
                return ActionResult.Failure;
            }
            string sqlScripfolderpath = session["HANICTEMPSQLFOLDER"];

            session.Log($"Properties have been read.{Environment.NewLine}connectionstring: {connectionstring}{Environment.NewLine}mindbVersion: {mindbVersion}; maxdbVersion: {maxdbVersion}{Environment.NewLine}sqlScripfolderpath: {sqlScripfolderpath}");
            try
            {

                DBUpdater dbUpdater = new DBUpdater(connectionstring, true);
                success = dbUpdater.UpdateViaArchive(mindbVersion, maxdbVersion, sqlScripfolderpath);
                session.Log(dbUpdater.LogInfo.ToString());
            }
            catch (Exception ex)
            {
                session.Log($"Commit Exception Type: {ex.GetType()}");
                session.Log($"Message: {ex.Message}");
                success = false;
            }


            session.Log("End CA_UpdateDatabase");
            if (success)
                return ActionResult.Success;
            else
                return ActionResult.Failure;

        }

        [CustomAction]
        public static ActionResult CA_TestConnectionString(Session session)
        {
            session.Log("Begin CA_TestConnectionString");
            try
            {
                session.Log($"Property DBSERVERPROP: {session["DBSERVERPROP"]} ");
                string connectionstring = GenerateConnectionString(session);

                session["ConnectionStringProp"] = connectionstring;

                StringBuilder logInfo = new StringBuilder();
                if (DBUpdater.CheckConnectionString(connectionstring, ref logInfo))
                    session["CONNECTIONSTRINGTESTRESULT"] = "1";
                else
                    session["CONNECTIONSTRINGTESTRESULT"] = "0";

                session.Log(logInfo.ToString());
                session.Log("End CA_TestConnectionString");
                return ActionResult.Success;
            }
            catch (Exception ex)
            {
                session.Log(ex.Message);
                return ActionResult.Failure;
            }
        }

        [CustomAction]
        public static ActionResult CA_LoadDatabaseInfo(Session session)
        {
            System.Diagnostics.Debugger.Launch();
            session.Log("Begin CA_LoadDatabaseInfo");
            string controlType = "ComboBox"; // Type der Control
            List<string> dbNameList = DBUpdater.ReadDbNames(GenerateConnectionString(session, true));
            if(dbNameList == null || dbNameList.Count == 0)
            {
                session.Log("Error while loading Database Names");
                return ActionResult.SkipRemainingActions;
            }

            try
            {
                //ggf. altlasten loswerden
                session.Database.Execute($"DELETE FROM {controlType} WHERE Property = 'DATABASECOMBOBOXPROP'");

                View view = session.Database.OpenView($"SELECT * FROM {controlType}");
                view.Execute();

                session.Log("Executed View");



                int index = 1;
                foreach (string dbName in dbNameList)
                {
                    Record record = session.Database.CreateRecord(4);
                    
                    record.SetString(1, "DATABASECOMBOBOXPROP");
                    record.SetInteger(2, index++);
                    record.SetString(3, dbName);
                    record.SetString(4, dbName);

                    view.Modify(ViewModifyMode.InsertTemporary, record);
                }




                session.Log("Modified View");

                view.Close();
            }
            catch(Exception ex)
            {
                session.Log("Exception was thrown: " + ex.Message);
                return ActionResult.Failure;
            }
            session.Log("End CA_LoadDatabaseInfo");
            return ActionResult.Success;
        }

        private static string GenerateConnectionString(Session session, bool masterDb = false)
        {
            string connectionstring;
            string server = session["DBSERVERPROP"];
            string database = masterDb ? "master" : session["DATABASECOMBOBOXPROP"];
            bool trustedConnection = session["DBTRUSTEDCONNECTIONPROP"] == "1" ? true : false;
            if (!trustedConnection)
            {
                string databaseUser = session["DBUSERPROP"];
                string databaseUserPassword = session["DBUSERPASSWORDPROP"];

                connectionstring = $"Server={server};Database={database};User Id={databaseUser};Password={databaseUserPassword};";
            }
            else
            {
                connectionstring = $"Server={server};Database={database};Trusted_Connection=true;";
            }

            return connectionstring;
        }
    }
}
