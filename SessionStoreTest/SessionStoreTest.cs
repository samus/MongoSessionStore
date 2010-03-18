using System;
using System.IO;
using System.Configuration;
using System.Collections.Generic;
using System.Web;
using System.Web.SessionState;
using System.Web.Configuration;
using System.Configuration.Provider;
using System.Text;
using NUnit.Framework;
using MongoDB.Driver;
using MongoDB.Driver.Configuration;
using MongoDB.Driver.Connections;
using MongoSessionStore;


namespace SessionStoreTest
{
    [TestFixture]
    public class SessionStoreTest
    {
        Connection conn;
        Database db;
        IMongoCollection sessions;
        string ApplicationName = "TestApp";
        int Timeout = 2;
        SessionStateStoreData item;
        string sessionID;

        [SetUp]
        public void SetUp()
        {
            MongoConfiguration config = (MongoConfiguration)System.Configuration.ConfigurationManager.GetSection("Mongo");
            conn = ConnectionFactory.GetConnection(config.Connections["mongoserver"].ConnectionString);
            db = new Database(conn, "SessionTest");
            sessions = db.GetCollection("sessions");

            SessionStateItemCollection sessionItemsCollection = new SessionStateItemCollection();
            HttpStaticObjectsCollection staticObjectsCollection = new HttpStaticObjectsCollection();
            item = new SessionStateStoreData(sessionItemsCollection, staticObjectsCollection, 1);
        }

        [Test]
        public void TearDown()
        {
            conn.Open();
            db["$cmd"].FindOne(new Document().Append("drop", "sessions"));
            conn.Close();
        }

        [Test]
        public void InsertNewSession()
        {         

            try
            {
                string sessionItems = Serialize((SessionStateItemCollection)item.Items);
                OidGenerator oGen = new OidGenerator();
                string id = oGen.Generate().ToString();
                Session session = new Session(id, this.ApplicationName, this.Timeout, sessionItems, item.Items.Count, SessionStateActions.None);
                SessionStore.Insert(session);
                //Session storedSession = SessionStore.Get(id, this.ApplicationName);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + " " + ex.InnerException.Message);
            }

            //Session storedSession = SessionStore.Get(id, this.ApplicationName);
            //if (storedSession == null)
            //{
            //    Console.WriteLine("It's null");
            //}
            //Assert.AreEqual(session.SessionID, storedSession.SessionID);
            //Assert.AreEqual(session.ApplicationName, storedSession.ApplicationName);
            //Assert.AreEqual(session.Created, storedSession.Created);
        }

        [Test]
        public void DumpSessions()
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
                bool locked = (bool)session["Locked"];
                Console.WriteLine("SessionId:" + id + " | Created:" + created.ToString() + " | Expires:" + expires.ToString() + " | Locked?: " + locked.ToString() + " | Application:" + applicationName + " | Total Items:" + sessionItemsCount.ToString());
            }
            conn.Close();
        }

        private string Serialize(SessionStateItemCollection items)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(ms);
            if (items != null)
                items.Serialize(writer);
            writer.Close();
            return Convert.ToBase64String(ms.ToArray());
        }


        private SessionStateItemCollection Deserialize(string serializedItems, int timeout)
        {
            MemoryStream ms = new MemoryStream(Convert.FromBase64String(serializedItems));
            SessionStateItemCollection sessionItems = new SessionStateItemCollection();

            if (ms.Length > 0)
            {
                BinaryReader reader = new BinaryReader(ms);
                sessionItems = SessionStateItemCollection.Deserialize(reader);
            }

            return sessionItems;
        }
    }
}
