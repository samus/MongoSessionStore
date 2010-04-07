using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;
using MongoDB.Driver;
using MongoDB.Driver.Serialization;
using MongoDB.Driver.Connections;
using MongoDB.Driver.Configuration;
using NDesk.Options;

namespace MongoStoreInspector
{
    class Program
    {
        private static Connection conn;
        private static MongoDatabase db;
        private static IMongoCollection sessions;

        static void Main(string[] args)
        {
            OptionSet options = new OptionSet(){
            {"i|inspect_sessions", i => InspectSessions()},
            {"fe|flush_expired", fe => FlushExpiredSession()},
            {"aix|add_indexes", aix => AddIndexes()},
            {"?|h|help", h => ShowHelp()}};

            try
            {
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
            MongoConfiguration config = (MongoConfiguration)System.Configuration.ConfigurationManager.GetSection("Mongo");
            conn = ConnectionFactory.GetConnection(config.Connections["mongoserver"].ConnectionString);
            db = new MongoDatabase(SerializationFactory.Default,conn, "SessionTest");
            try
            {
                conn.Open();
                sessions = db.GetCollection("sessions");
                ICursor allSessions = sessions.FindAll();
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
                    string dump = "SessionId:" + id + " | Created:" + created.ToString() + " | Expires:" + expires.ToString() + " | Timeout:" + timeout.ToString();
                    dump += " | Locked?: " + locked.ToString() + " | Application:" + applicationName + " | Total Items:" + sessionItemsCount.ToString();
                    Console.WriteLine(dump);
                }
            }
            catch (MongoException e)
            {
                Console.WriteLine("There was an error while inspecting session: " + e.Message);
            }
            finally
            {
                conn.Close();
            }

            Console.ReadLine();
        }


        static void FlushExpiredSession()
        {
            MongoConfiguration config = (MongoConfiguration)System.Configuration.ConfigurationManager.GetSection("Mongo");
            conn = ConnectionFactory.GetConnection(config.Connections["mongoserver"].ConnectionString);
            db = new MongoDatabase(SerializationFactory.Default,conn, "SessionTest");
            try
            {
                conn.Open();
                sessions = db.GetCollection("sessions");
                Document expiredSelector = new Document() { { "Expires", new Document() { { "$lt", DateTime.Now } } } };
                sessions.Delete(expiredSelector);
                Console.WriteLine("Successfully flushed any expired sessions.");
            }
            catch (MongoException e)
            {
                Console.WriteLine("Error while flushing expired sessions: " + e.Message);
            }
            finally
            {
                conn.Close();
            }

        }

        static void AddIndexes()
        {
             MongoConfiguration config = (MongoConfiguration)System.Configuration.ConfigurationManager.GetSection("Mongo");
            conn = ConnectionFactory.GetConnection(config.Connections["mongoserver"].ConnectionString);
            db = new MongoDatabase(SerializationFactory.Default,conn, "SessionTest");
            try
            {
                conn.Open();
                Document index_spec = new Document(){{"SessionId",1},{"ApplicationName",1}};
                sessions = db.GetCollection("sessions");
                sessions.MetaData.CreateIndex(index_spec, false);
                Console.WriteLine("Created Indexes on SessionId and ApplicationName");
            }
            catch (MongoException e)
            {
                Console.WriteLine("Error while adding indexes: " + e.Message);
            }
            finally
            {
                conn.Close();
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
