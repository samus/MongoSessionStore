using System;
using System.IO;
using System.Collections.Generic;
using System.Web.SessionState;
using System.Text;
using NUnit.Framework;
using MongoDB;
using MongoDB.Connections;
using MongoDB.Configuration;
using MongoSessionStore;

namespace SessionStoreTest
{
    [TestFixture]
    public class ConnectionTest
    {

        MongoConfiguration config;

        [SetUp]
        public void SetUp()
        {
            var configure = new MongoConfigurationBuilder();
            configure.ConnectionStringAppSettingKey("mongoserver");           
            config = configure.BuildConfiguration();
        }

        [Test]
        public void TestInserts()
        {
            int i = 0;
            while (i < 200)
            {
                string id = Guid.NewGuid().ToString();
                SessionStateItemCollection items = new SessionStateItemCollection();
                items["Val1"] = "value";
                byte[] serializedItems = Serialize(items);
                Binary b = new Binary(serializedItems);
                Session session = new Session(id, "AppName", 2, b, items.Count, SessionStateActions.None);
                using (var mongo = new Mongo(config))
                {
                    SessionStore.Instance.Insert(session);
                    i++;
                }

            }
        }

        [Test]
        public void TestUpdates()
        {
            SessionStateItemCollection items = new SessionStateItemCollection();
            items["Val1"] = "value";
            byte[] serializedItems = Serialize(items);
            Binary b = new Binary(serializedItems);
            List<string> ids = new List<string>();
            ICursor allSessions;
            using (var mongo = new Mongo(config))
            {
                allSessions = mongo["session_store"]["sessions"].FindAll();
            }
            foreach (Document session in allSessions.Documents)
            {
                string id = (string)session["SessionId"];
                ids.Add(id);

            }
            foreach (string s in ids)
            {
                SessionStore.UpdateSession(s, 2, b, "AppName", items.Count, 0);
            }
            
        }



        private byte[] Serialize(SessionStateItemCollection items)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(ms);
            try
            {
                if (items != null)
                    items.Serialize(writer);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                writer.Close();
            }
            return ms.ToArray();
        }
    }
}
