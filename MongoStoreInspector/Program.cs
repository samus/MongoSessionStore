using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;
using MongoDB.Driver;
using MongoDB.Driver.Connections;
using MongoDB.Driver.Configuration;
using NDesk.Options;

namespace MongoStoreInspector
{
    class Program
    {
        static Connection conn;
        static Database db;
        static IMongoCollection sessions;

        static void Main(string[] args)
        {

            MongoConfiguration config = (MongoConfiguration)System.Configuration.ConfigurationManager.GetSection("Mongo");
            conn = ConnectionFactory.GetConnection(config.Connections["mongoserver"].ConnectionString);
            db = new Database(conn, "SessionTest");
            sessions = db.GetCollection("sessions");

            Console.WriteLine("Press any key to dump the current session store or Q to quit");
            string s = Console.ReadLine();
            if (s.ToLower() == "q")
            {                
                System.Environment.Exit(1);
            }
            else
            {
                conn.Open();
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
                    Console.WriteLine("SessionId:" + id + " | Created:" + created.ToString() + " | Expires:" + expires.ToString() + " | Timeout:" + timeout.ToString() + " | Locked?: " + locked.ToString() + " | Application:" + applicationName + " | Total Items:" + sessionItemsCount.ToString());
                }
                conn.Close();
            }
            Console.ReadLine();

        }
    }
}
