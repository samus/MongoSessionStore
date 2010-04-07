using System;
using System.Collections.Generic;
using System.Configuration;
using MongoDB.Driver;
using MongoDB.Driver.Configuration;
using MongoDB.Driver.Connections;
using MongoDB.Driver.Serialization;
using System.Text;

namespace MongoSessionStore
{
    public sealed class SessionStore
    {

        static MongoConfiguration config = (MongoConfiguration)System.Configuration.ConfigurationManager.GetSection("Mongo");
        static Connection conn = ConnectionFactory.GetConnection(config.Connections["mongoserver"].ConnectionString);
        static MongoDatabase db = new MongoDatabase(SerializationFactory.Default,conn, "SessionTest");
        static IMongoCollection sessions = db.GetCollection("sessions");

        public SessionStore()
        {
        }

        public static void Insert(Session session)
        {
            Document newSession = new Document() { { "SessionId",session.SessionID }, {"ApplicationName",session.ApplicationName},{"Created",session.Created},
            {"Expires",session.Expires},{"LockDate",session.LockDate},{"LockId",session.LockID},{"Timeout",session.Timeout},{"Locked",session.Locked},
            {"SessionItems",session.SessionItems},{"SessionItemsCount",session.SessionItemsCount},{"Flags",session.Flags}};
            try
            {
                conn.Open();
                sessions.Insert(newSession);
            }
            catch (MongoException ex)
            {
                throw new Exception("Could not insert a new session", ex);
            }
            finally
            {
                conn.Close();
            }

        }

        public static Session Get(string id, string applicationName)
        {
            Document selector = new Document() { { "SessionId", id }, { "ApplicationName", applicationName } };
            Session session;
            try
            {
                conn.Open();
                Document sessionDoc  = sessions.FindOne(selector);
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
            finally
            {
                conn.Close();
            }
            return session;            
        }

        public static void UpdateSession(string id, int timeout, Binary sessionItems, string applicationName, int sessionItemsCount, object lockId)
        {
            try
            {

                Document selector = new Document() { { "SessionId", id }, { "ApplicationName", applicationName },{"LockId",lockId} };
                Document session = new Document() { { "$set", new Document() { { "Expires", DateTime.Now.AddMinutes((double)timeout) },{"Timeout",timeout},{"Locked",false},{ "SessionItems", sessionItems },{"SessionItemsCount",sessionItemsCount}}} };
                conn.Open();
                sessions.Update(session, selector, 0, false);
            }
            catch (MongoException ex)
            {
                throw new Exception("Could not insert a new session", ex);
            }
            finally
            {
                conn.Close();
            }
        }

        public static void UpdateSessionExpiration(string id, string applicationName, double timeout)
        {
            try
            {
                Document selector = new Document() { { "SessionId", id }, { "ApplicationName", applicationName } };
                Document sessionUpdate = new Document() { { "$set", new Document() { { "Expires", DateTime.Now.AddMinutes(timeout) } } } };
                conn.Open();
                sessions.Update(sessionUpdate, selector, 0, false);
            }
            catch (MongoException ex)
            {
                throw new Exception("Could not update Session Expiration", ex);
            }
            finally
            {
                conn.Close();
            }
        }

        public static void EvictSession(Session session)
        {
            Document selector = new Document() { { "SessionId", session.SessionID }, { "ApplicationName", session.ApplicationName },{"LockId",session.LockID} };
            try
            {
                conn.Open();
                sessions.Delete(selector);
            }
            catch (MongoException ex)
            {
                throw new Exception("There was a problem when evicting the session with SessionId:" + session.SessionID, ex);
            }
            finally
            {
                conn.Close();
            }
        }

        public static void EvictSession(string id, string applicationName, object lockId)
        {
            Document selector = new Document() {{"SessionId", id }, { "ApplicationName", applicationName }, { "LockId", lockId}};
            try
            {
                conn.Open();
                sessions.Delete(selector);
            }
            catch (MongoException ex)
            {
                throw new Exception("There was a problem when evicting the session with SessionId:" + id, ex);
            }
            finally
            {
                conn.Close();
            }
        }

        public static void EvictExpiredSession(string id, string applicationName)
        {
            Document selector = new Document() { { "SessionId", id }, { "ApplicationName", applicationName },
            {"Expires",new Document(){{"$lt",DateTime.Now}} }};
            try
            {
                conn.Open();
                sessions.Delete(selector);
            }
            catch (MongoException ex)
            {
                throw new Exception("There was a problem when evicting the session with SessionId:" + id, ex);
            }
            finally
            {
                conn.Close();
            }
        }

        public static void LockSession(Session session)
        {
            Document selector = new Document() {{"SessionId", session.SessionID }, {"ApplicationName", session.ApplicationName}};
            Document sessionLock = new Document() { { "$set", new Document() {{"LockDate", DateTime.Now }, 
            {"LockId", session.LockID }, {"Locked", true }, {"Flags",0} } } };
            try
            {
                conn.Open();
                sessions.Update(sessionLock, selector, 0, false);
            }
            catch (MongoException ex)
            {
                throw new Exception("There was a problem when locking the session with SessionId:" + session.SessionID, ex);
            }
            finally
            {
                conn.Close();
            }
        }

        public static void ReleaseLock(string id, string applicationName, object lockId, double timeout)
        {
            Document selector = new Document() { { "SessionId", id }, { "ApplicationName", applicationName},{"LockId",lockId }};
            Document sessionLock = new Document() { { "$set", new Document() {{"Expires", DateTime.Now.AddMinutes(timeout)}, {"Locked", false }}}};

            try
            {
                conn.Open();
                sessions.Update(sessionLock, selector, 0, false);
            }
            catch (MongoException ex)
            {
                throw new Exception("There was a problem when releasing the lock for the session with SessionId:" + id, ex);
            }
            finally
            {
                conn.Close();
            }
        }
    }
}
