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
using MongoDB;
using MongoDB.Configuration;
using MongoDB.Connections;
using MongoSessionStore;


namespace SessionStoreTest
{
    [TestFixture]
    public class SessionStoreTest
    {
   
        string ApplicationName = "TestApp";
        int Timeout = 2;
        SessionStateStoreData item;
        MongoConfiguration config;

        [SetUp]
        public void SetUp()
        {
            var configure = new MongoConfigurationBuilder();
            configure.ConnectionStringAppSettingKey("mongoserver");
            config = configure.BuildConfiguration();
            SessionStateItemCollection sessionItemsCollection = new SessionStateItemCollection();
            HttpStaticObjectsCollection staticObjectsCollection = new HttpStaticObjectsCollection();
            item = new SessionStateStoreData(sessionItemsCollection, staticObjectsCollection, 1);
        }


        /// <summary>
        /// Not a real TearDown(). Sometimes it helps to leave the sessions in the database to be dumped and analyzed. 
        /// </summary>
        [Test]
        public void TearDown()
        {
            using (var mongo = new Mongo(config))
            {
                mongo.Connect();
                mongo["session_store"]["$cmd"].FindOne(new Document().Add("drop", "sessions"));
            }
        }

        [Test]
        public void InsertNewSession()
        {
            var sessionStore = SessionStore.Instance;
            byte[] serializedItems = Serialize((SessionStateItemCollection)item.Items);
            Binary sessionItems = new Binary(serializedItems);
            string id = Guid.NewGuid().ToString();
            Session session = new Session(id, this.ApplicationName, this.Timeout, sessionItems, item.Items.Count, SessionStateActions.None);
            sessionStore.Insert(session);
            Session storedSession = sessionStore.Get(id, this.ApplicationName);
            Assert.AreEqual(session.SessionID, storedSession.SessionID);
            Assert.AreEqual(session.ApplicationName, storedSession.ApplicationName);
            Assert.AreEqual(session.SessionItems.Bytes.Length, storedSession.SessionItems.Bytes.Length);
        }

        [Test]
        public void UpdateSession()
        {
            var sessionStore = SessionStore.Instance;
            byte[] serializedItems = Serialize((SessionStateItemCollection)item.Items);
            Binary sessionItems = new Binary(serializedItems);
            string id = Guid.NewGuid().ToString();
            Session session = new Session(id, this.ApplicationName, this.Timeout, sessionItems, item.Items.Count, SessionStateActions.None);
            SessionStore.Instance.Insert(session);
            sessionStore.UpdateSession(id, 5, new Binary(serializedItems), this.ApplicationName, 3, 0);
            Session updatedSession = sessionStore.Get(id, this.ApplicationName);
            Assert.AreEqual(5, updatedSession.Timeout);
            Assert.AreEqual(3, updatedSession.SessionItemsCount);
            Assert.AreEqual(DateTime.Now.AddMinutes(5).Minute, updatedSession.Expires.Minute);
        }

        [Test]
        public void LockSessionAndReleaseLock()
        {
            var sessionStore = SessionStore.Instance;
            byte[] serializedItems = Serialize((SessionStateItemCollection)item.Items);
            Binary sessionItems = new Binary(serializedItems);
            string id = Guid.NewGuid().ToString();
            Session session = new Session(id, this.ApplicationName, this.Timeout, sessionItems, item.Items.Count, SessionStateActions.None);
            SessionStore.Instance.Insert(session);
            DateTime timestamp = DateTime.Now;
            session.LockID = 1;
            sessionStore.LockSession(session);
            Session lockedSesssion = sessionStore.Get(id, this.ApplicationName);
            Assert.AreEqual(true, lockedSesssion.Locked);
            Assert.AreEqual(1,session.LockID);
            Assert.AreNotEqual(session.LockDate,lockedSesssion.LockDate);
            Assert.AreEqual(0, lockedSesssion.Flags);
            sessionStore.ReleaseLock(lockedSesssion.SessionID, lockedSesssion.ApplicationName, lockedSesssion.LockID, item.Timeout);
            Session unlockedSession = sessionStore.Get(id, this.ApplicationName);
            Assert.AreEqual(false, unlockedSession.Locked);
            Assert.AreEqual(lockedSesssion.LockDate, unlockedSession.LockDate);
            Assert.AreNotEqual(lockedSesssion.Expires, unlockedSession.Expires);
        }

        [Test]
        public void InsertNewSessionAndEvictHard()
        {
            var sessionStore = SessionStore.Instance;
            byte[] serializedItems = Serialize((SessionStateItemCollection)item.Items);
            Binary sessionItems = new Binary(serializedItems);
            string id = Guid.NewGuid().ToString();
            Session session = new Session(id, this.ApplicationName, this.Timeout, sessionItems, item.Items.Count, SessionStateActions.None);
            SessionStore.Instance.Insert(session);
            SessionStore.Instance.EvictSession(session);
            Session storedSession = sessionStore.Get(id, this.ApplicationName);
            Assert.IsNull(storedSession); 
        }

        [Test]
        public void AddExpiredSessionAndEvictSoft()
        {
            var sessionStore = SessionStore.Instance;
            byte[] serializedItems = Serialize((SessionStateItemCollection)item.Items);
            Binary sessionItems = new Binary(serializedItems);
            string id = Guid.NewGuid().ToString();
            Session session = new Session(id, this.ApplicationName, this.Timeout, sessionItems, item.Items.Count, SessionStateActions.None);
            session.Expires = DateTime.Now.Subtract(new TimeSpan(0,2,0));
            sessionStore.Insert(session);
            sessionStore.EvictExpiredSession(session.SessionID,session.ApplicationName);
            Session storedSession = sessionStore.Get(session.SessionID, session.ApplicationName);
            Assert.IsNull(storedSession);
        }

        public void TestSerializeAndDeserialize()
        {
            SessionStateItemCollection items = new SessionStateItemCollection();
            items["S1"] = "Test1";
            items["S2"] = "Test2";

            byte[] serializedItems = Serialize(items);
            SessionStateItemCollection items2 = Deserialize(serializedItems, 1);
            Assert.AreEqual("Test1", items["S1"]);
            Assert.AreEqual("Test2", items["S2"]);
        }


        [Test]
        public void DumpSessions()
        {
            ICursor allSessions;
            using (var mongo = new Mongo(config))
            {
                mongo.Connect();
                allSessions = mongo["session_store"]["sessions"].FindAll();
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
            }    

        }


        private byte[] Serialize(SessionStateItemCollection items)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(ms);
            if (items != null)
                items.Serialize(writer);
            writer.Close();
            return ms.ToArray();
        }


        private SessionStateItemCollection Deserialize(byte[] serializedItems, int timeout)
        {
            MemoryStream ms = new MemoryStream(serializedItems);
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
