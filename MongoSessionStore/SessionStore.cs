using System;
using System.Collections.Generic;
using System.Configuration;
using MongoDB;
using MongoDB.Configuration;
using MongoDB.Connections;
using System.Text;

namespace MongoSessionStore
{
    public sealed class SessionStore
    {
        static MongoConfigurationBuilder configure = new MongoConfigurationBuilder();
        static MongoConfiguration config;
        private static volatile SessionStore instance;
        private static object syncRoot = new Object();

        private SessionStore()
        {
            configure = new MongoConfigurationBuilder();
            configure.ConnectionStringAppSettingKey("mongoserver");
            config = configure.BuildConfiguration();
        }

        public static SessionStore Instance
        {
            get
            {
                return SessionStoreInternal.Instance;
            }

        }

        internal class SessionStoreInternal
        {
            internal static readonly SessionStore Instance = new SessionStore();

            static SessionStoreInternal() { }
        }



        public void Insert(Session session)
        {
            Document newSession = new Document() { { "SessionId",session.SessionID }, {"ApplicationName",session.ApplicationName},{"Created",session.Created},
            {"Expires",session.Expires},{"LockDate",session.LockDate},{"LockId",session.LockID},{"Timeout",session.Timeout},{"Locked",session.Locked},
            {"SessionItems",session.SessionItems},{"SessionItemsCount",session.SessionItemsCount},{"Flags",session.Flags}};
            try
            {
                using (var mongo = new Mongo(config))
                {
                    mongo.Connect();
                    mongo["session_store"]["sessions"].Insert(newSession);
                }
            }
            catch (MongoException ex)
            {
                throw new Exception("Could not insert a new session", ex);
            }

        }

        public Session Get(string id, string applicationName)
        {
            Document selector = new Document() { { "SessionId", id }, { "ApplicationName", applicationName } };
            Session session;
            try
            {

                Document sessionDoc;

                using (var mongo = new Mongo(config))
                {
                    mongo.Connect();
                    sessionDoc = mongo["session_store"]["sessions"].FindOne(selector);
                }

                if (sessionDoc == null)
                {
                    session = null;
                }
                else
                {
                    session = new Session(sessionDoc);
                }

            }
            catch (MongoException ex)
            {
                throw new Exception("Could not insert a new session", ex);
            }

            return session;
        }

        public void UpdateSession(string id, int timeout, Binary sessionItems, string applicationName, int sessionItemsCount, object lockId)
        {
            try
            {

                Document selector = new Document() { { "SessionId", id }, { "ApplicationName", applicationName }, { "LockId", lockId } };
                Document session = new Document() { { "$set", new Document() { { "Expires", DateTime.Now.AddMinutes((double)timeout) }, { "Timeout", timeout }, { "Locked", false }, { "SessionItems", sessionItems }, { "SessionItemsCount", sessionItemsCount } } } };
                using (var mongo = new Mongo(config))
                {
                    mongo.Connect();
                    mongo["session_store"]["sessions"].Update(session, selector, 0, false);
                }
            }
            catch (MongoException ex)
            {
                throw new Exception("Could not insert a new session", ex);
            }

        }

        public void UpdateSessionExpiration(string id, string applicationName, double timeout)
        {
            try
            {
                Document selector = new Document() { { "SessionId", id }, { "ApplicationName", applicationName } };
                Document sessionUpdate = new Document() { { "$set", new Document() { { "Expires", DateTime.Now.AddMinutes(timeout) } } } };
                using (var mongo = new Mongo(config))
                {
                    mongo.Connect();
                    mongo["session_store"]["sessions"].Update(sessionUpdate, selector, 0, false);
                }
            }
            catch (MongoException ex)
            {
                throw new Exception("Could not update Session Expiration", ex);
            }
        }

        public void EvictSession(Session session)
        {
            Document selector = new Document() { { "SessionId", session.SessionID }, { "ApplicationName", session.ApplicationName }, { "LockId", session.LockID } };
            try
            {
                using (var mongo = new Mongo(config))
                {
                    mongo.Connect();
                    mongo["session_store"]["sessions"].Remove(selector);
                }
            }
            catch (MongoException ex)
            {
                throw new Exception("There was a problem when evicting the session with SessionId:" + session.SessionID, ex);
            }

        }

        public void EvictSession(string id, string applicationName, object lockId)
        {
            Document selector = new Document() { { "SessionId", id }, { "ApplicationName", applicationName }, { "LockId", lockId } };
            try
            {
                using (var mongo = new Mongo(config))
                {
                    mongo.Connect();
                    mongo["session_store"]["sessions"].Remove(selector);
                }
            }
            catch (MongoException ex)
            {
                throw new Exception("There was a problem when evicting the session with SessionId:" + id, ex);
            }
        }

        public void EvictExpiredSession(string id, string applicationName)
        {
            Document selector = new Document() { { "SessionId", id }, { "ApplicationName", applicationName },
            {"Expires",new Document(){{"$lt",DateTime.Now}} }};
            try
            {
                using (var mongo = new Mongo(config))
                {
                    mongo.Connect();
                    mongo["session_store"]["sessions"].Remove(selector);
                }
            }
            catch (MongoException ex)
            {
                throw new Exception("There was a problem when evicting the session with SessionId:" + id, ex);
            }
        }

        public void LockSession(Session session)
        {
            Document selector = new Document() { { "SessionId", session.SessionID }, { "ApplicationName", session.ApplicationName } };
            Document sessionLock = new Document() { { "$set", new Document() {{"LockDate", DateTime.Now }, 
            {"LockId", session.LockID }, {"Locked", true }, {"Flags",0} } } };
            try
            {
                using (var mongo = new Mongo(config))
                {
                    mongo.Connect();
                    mongo["session_store"]["sessions"].Update(sessionLock, selector, 0, false);
                }
            }
            catch (MongoException ex)
            {
                throw new Exception("There was a problem when locking the session with SessionId:" + session.SessionID, ex);
            }

        }

        public void ReleaseLock(string id, string applicationName, object lockId, double timeout)
        {
            Document selector = new Document() { { "SessionId", id }, { "ApplicationName", applicationName }, { "LockId", lockId } };
            Document sessionLock = new Document() { { "$set", new Document() { { "Expires", DateTime.Now.AddMinutes(timeout) }, { "Locked", false } } } };

            try
            {
                using (var mongo = new Mongo(config))
                {
                    mongo.Connect();
                    mongo["session_store"]["sessions"].Update(sessionLock, selector, 0, false);
                }
            }
            catch (MongoException ex)
            {
                throw new Exception("There was a problem when releasing the lock for the session with SessionId:" + id, ex);
            }
        }
    }
}
