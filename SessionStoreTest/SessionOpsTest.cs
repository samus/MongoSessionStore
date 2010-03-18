using System;
using System.IO;
using System.Configuration;
using System.Collections.Generic;
using System.Web;
using System.Web.Configuration;
using System.Configuration;
using System.Configuration.Provider;
using System.Web.SessionState;
using System.Text;
using NUnit.Framework;
using MongoDB.Driver;
using MongoDB.Driver.Connections;
using MongoDB.Driver.Configuration;

namespace SessionStoreTest
{
    [TestFixture]
    public class SessionOpsTest
    {
        Connection conn;
        Database db;
        IMongoCollection sessions;
        string ApplicationName = "TestApp";
        SessionStateStoreData item;


        Oid sessionID;

        [SetUp]
        public void SetUp()
        {
            MongoConfiguration config = (MongoConfiguration)System.Configuration.ConfigurationManager.GetSection("Mongo");
            conn = ConnectionFactory.GetConnection(config.Connections["mongoserver"].ConnectionString);
            db = new Database(conn, "SessionTest");
            sessions = db.GetCollection("sessions");

            SessionStateItemCollection sessionItems = new SessionStateItemCollection();
            HttpStaticObjectsCollection staticObjects = new HttpStaticObjectsCollection();
            item = new SessionStateStoreData(sessionItems, staticObjects, 1);    
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
            string sessionItems = Serialize((SessionStateItemCollection)item.Items);
            OidGenerator oGen = new OidGenerator();
            sessionID = oGen.Generate();
            Document newSession = new Document() { { "SessionId",sessionID }, {"ApplicationName",ApplicationName},{"Created",DateTime.Now},
            {"Expires",DateTime.Now.AddMinutes((Double)item.Timeout)},{"LockDate",DateTime.Now},{"LockId",0},{"Timeout",item.Timeout},{"Locked",false},
            {"SessionItems",sessionItems},{"SessionItemsCount",item.Items.Count},{"Flags",0}};

            conn.Open();
            sessions.Insert(newSession);
            Document storedSession = sessions.FindOne(new Document() { { "SessionId", sessionID } });
            conn.Close();
            
            Assert.AreEqual(newSession["SessionItems"], storedSession["SessionItems"]);
        }

        [Test]
        public void LockSession()
        {
            conn.Open();
            string sessionItems = Serialize((SessionStateItemCollection)item.Items);
            OidGenerator oGen = new OidGenerator();
            sessionID = oGen.Generate();
            Document newSession = new Document() { { "SessionId",sessionID }, {"ApplicationName",ApplicationName},{"Created",DateTime.Now},
            {"Expires",DateTime.Now.AddMinutes((Double)item.Timeout)},{"LockDate",DateTime.Now},{"LockId",0},{"Timeout",item.Timeout},{"Locked",false},
            {"SessionItems",sessionItems},{"SessionItemsCount",item.Items.Count},{"Flags",0}};
                    
            sessions.Insert(newSession);
            Document storedSession = sessions.FindOne(new Document() { { "SessionId", sessionID } });
            Document selector = new Document() { { "SessionId", sessionID }, { "ApplicationName", ApplicationName }, { "Expires", new Document() { { "$gt", DateTime.Now } } } };
            Document session = new Document() { { "$set", new Document(){{ "LockDate", DateTime.Now }, { "LockId",1 }, { "Locked", true } }}};
            sessions.Update(session, selector, 0, false);
          
            Document lockedSession = sessions.FindOne(selector);
            Assert.IsTrue((bool)lockedSession["Locked"]);

            conn.Close();

        }

        [Test]
        public void UpdateOrInsertSession()
        {
            conn.Open();
            Document existingSession = sessions.FindOne(new Document(){{"Expires", new Document(){{"$gt", DateTime.Now}}}});
            if (existingSession != null)
            {
                sessionID = (Oid)existingSession["SessionId"];
            }
            else
            {
                OidGenerator oGen = new OidGenerator();
                sessionID = oGen.Generate();
            }
  
            object lockId = 0;
            item.Items["ItemOne"] = "test one value updated";
            item.Items["ItemTwo"] = 3;
            item.Items["ItemThree"] = false;

            string sessionItems = Serialize((SessionStateItemCollection)item.Items);
            Document session = new Document() { { "SessionId",sessionID }, {"ApplicationName",ApplicationName},{"Created",DateTime.Now},
            {"Expires",DateTime.Now.AddMinutes((Double)item.Timeout)},{"LockDate",DateTime.Now},{"LockId",0},{"Timeout",item.Timeout},{"Locked",false},
            {"SessionItems",sessionItems},{"SessionItemsCount",item.Items.Count},{"Flags",0}};
         
            Document selector = new Document(){{"SessionId",sessionID}};
            sessions.Update(session,selector,1,false);
            

            Document updatedSession = sessions.FindOne(session);
            SessionStateItemCollection updatedItems = Deserialize((string)session["SessionItems"],item.Timeout);
            Assert.AreEqual("test one value updated", (string)updatedItems["ItemOne"]);
            Assert.AreEqual(3, (int)updatedItems["ItemTwo"]);
            Assert.AreEqual(false, (bool)updatedItems["ItemThree"]);
            Console.WriteLine((string)updatedItems["ItemOne"]);
            conn.Close();
        }

        [Test]
        public void DeleteExpiredSessions()
        {
           
            conn.Open();
            //Add a Sessions that is expired by a couple of minutes;
            string sessionItemsExpired = Serialize((SessionStateItemCollection)item.Items);
            OidGenerator oGen = new OidGenerator();
            sessionID = oGen.Generate();
            Document expiredSession = new Document() { { "SessionId",sessionID }, {"ApplicationName",ApplicationName},{"Created",DateTime.Now},
            {"Expires",DateTime.Now.Subtract(new TimeSpan(0,2,0))},{"LockDate",DateTime.Now},{"LockId",0},{"Timeout",item.Timeout},{"Locked",false},
            {"SessionItems",sessionItemsExpired},{"SessionItemsCount",item.Items.Count},{"Flags",0}};
            sessions.Insert(expiredSession);

            //Add a Session that is not expired
            string sessionItems = Serialize((SessionStateItemCollection)item.Items);
            sessionID = oGen.Generate();
            Document newSession = new Document() { { "SessionId",sessionID }, {"ApplicationName",ApplicationName},{"Created",DateTime.Now},
            {"Expires",DateTime.Now.AddMinutes((Double)item.Timeout)},{"LockDate",DateTime.Now},{"LockId",0},{"Timeout",item.Timeout},{"Locked",false},
            {"SessionItems",sessionItems},{"SessionItemsCount",item.Items.Count},{"Flags",0}};
           
            sessions.Insert(newSession);

            int i = 0;
            ICursor before = sessions.FindAll();
            foreach (Document d in before.Documents)
            {
                i++;
            }
            Assert.AreEqual(2, i);
            conn.Close();

            //Delete expired sessions
            
            Document expiredSelector = new Document(){{ "Expires", new Document(){{ "$lt", DateTime.Now }}}};
            sessions.Delete(expiredSelector);
            ICursor after = sessions.FindAll();
            
            i = 0;
            foreach (Document d in after.Documents)
            {
                i++;
            }
            Assert.AreEqual(1,i);
            
            
        }

        [Test]
        public void FindExpired()
        {
            Document expiredSelector = new Document() {{"Expires", new Document(){{"$lt", DateTime.Now }}}};
            conn.Open();
            ICursor expired = sessions.Find(expiredSelector);
            Console.WriteLine(DateTime.Now.ToString());
            foreach (Document session in expired.Documents)
            {
                string id = (string)session["SessionId"];
                DateTime created = (DateTime)session["Created"];
                created = created.ToLocalTime();
                DateTime expires = (DateTime)session["Expires"];
                expires = expires.ToLocalTime();
                string applicationName = (string)session["ApplicationName"];
                int sessionItemsCount = (int)session["SessionItemsCount"];
                Console.WriteLine("SessionId:" + id + "| Created:" + created.ToString() + "| Expires:" + expires.ToString() + "| Application:" + applicationName + "| Total Items:" + sessionItemsCount.ToString());
            }
            conn.Close();
        }


        [Test]
        public void DumpSessions()
        {
            conn.Open();
            ICursor allSessions = sessions.FindAll();
            foreach (Document session in allSessions.Documents)
            {
                string sessionid = (string)session["SessionId"];
                              
                DateTime created = (DateTime)session["Created"];
                created = created.ToLocalTime();
                DateTime expires = (DateTime)session["Expires"];
                expires = expires.ToLocalTime();
                string applicationName = (string)session["ApplicationName"];
                int sessionItemsCount = (int)session["SessionItemsCount"];
                bool locked = (bool)session["Locked"];
                Console.WriteLine("SessionId:" + sessionid + " | Created:" + created.ToString() + " | Expires:" + expires.ToString() +" | Locked?: "+ locked.ToString() +" | Application:" + applicationName + " | Total Items:" + sessionItemsCount.ToString());
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
