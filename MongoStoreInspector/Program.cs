using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;
using MongoDB;
using MongoDB.Connections;
using MongoDB.Configuration;
using NDesk.Options;

namespace MongoStoreInspector
{
    class Program
    {
        private static MongoConfiguration config = (MongoConfiguration)System.Configuration.ConfigurationManager.GetSection("Mongo");
        private static string conn;


        static void Main(string[] args)
        {
            OptionSet options = new OptionSet(){
            {"i|inspect_sessions", i => InspectSessions()},
            {"fe|flush_expired", fe => FlushExpiredSession()},
            {"aix|add_indexes", aix => AddIndexes()},
            {"?|h|help", h => ShowHelp()}};

            try
            {
                var configure = new MongoConfigurationBuilder();
                configure.ConnectionStringAppSettingKey("mongoserver");
                var config = configure.BuildConfiguration();
                options.Parse(args);
            }
            catch (OptionException e)
            {
                Console.WriteLine(e.Message);
                return;
            }

        }

        static void InspectSessions()
        {
          
            try
            {

                using (var mongo = new Mongo(config))
                {
                    mongo.Connect();
                    ICursor allSessions = mongo["session_store"]["sessions"].FindAll();
                    foreach (Document session in allSessions.Documents)
                    {
                        string id = (string)session["SessionId"];
                        DateTime created = (DateTime)session["Created"];
                        created = created.ToLocalTime();
                        DateTime expires = (DateTime)session["Expires"];
                        expires = expires.ToLocalTime();
                        string applicationName = (string)session["ApplicationName"];
                        int sessionItemsCount = (int)session["SessionItemsCount"];
                        int timeout = (int)session["Timeout"];
                        bool locked = (bool)session["Locked"];
                        string dump = "SessionId:" + id + 
                            "\nCreated:" + created.ToString() + 
                            "\nExpires:" + expires.ToString() + 
                            "\nTimeout:" + timeout.ToString();
                        dump += 
                            "\nLocked?: " + locked.ToString() + 
                            "\nApplication:" + applicationName + 
                            "\nTotal Items:" + sessionItemsCount.ToString();
                        Console.WriteLine(dump);
                    }
                }
            }
            catch (MongoException e)
            {
                Console.WriteLine("There was an error while inspecting session: " + e.Message);
            }
            Console.ReadLine();
        }


        static void FlushExpiredSession()
        {
           try
            {
                using (var mongo = new Mongo(config))
                {
                    mongo.Connect();
                    Document expiredSelector = new Document() { { "Expires", new Document() { { "$lt", DateTime.Now } } } };
                    mongo["session_store"]["sessions"].Delete(expiredSelector);
                }
                Console.WriteLine("Successfully flushed any expired sessions.");
            }
            catch (MongoException e)
            {
                Console.WriteLine("Error while flushing expired sessions: " + e.Message);
            }
        }

        static void AddIndexes()
        {
         
            try
            {
                using (var mongo = new Mongo(config))
                {
                    mongo.Connect();
                    Document index_spec = new Document() { { "SessionId", 1 }, { "ApplicationName", 1 } };
                    mongo["session_store"]["sessions"].MetaData.CreateIndex(index_spec, false);
                }
                Console.WriteLine("Created Indexes on SessionId and ApplicationName");
            }
            catch (MongoException e)
            {
                Console.WriteLine("Error while adding indexes: " + e.Message);
            }
            Console.ReadLine();
        }

        static void ShowHelp()
        {
            Console.WriteLine("available options are i:inspect sessions, aix:add indexes, fe:flush expired sessions");
            Console.ReadLine();
        }
    }
}
