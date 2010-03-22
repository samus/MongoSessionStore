using System;
using System.Collections.Generic;
using System.Configuration;
using MongoDB.Driver;
using MongoDB.Driver.Configuration;
using MongoDB.Driver.Connections;
using System.Text;

namespace MongoSessionStore
{
    public sealed class SessionStore
    {

        static MongoConfiguration config = (MongoConfiguration)System.Configuration.ConfigurationManager.GetSection("Mongo");
        static Connection conn = ConnectionFactory.GetConnection(config.Connections["mongoserver"].ConnectionString);
        static Database db = new Database(conn, "SessionTest");
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
                session = new Session(sessionDoc);

                if (session == null)
                {
                    throw new Exception("The session was not found. SessionID: + " + id);
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
                Document session = new Document() { { "$set", new Document() { { "LockDate", DateTime.Now.AddMinutes((double)timeout) },{"Timeout",timeout},{ "SessionItems", sessionItems },{"SessionItemsCount",sessionItemsCount}} } };
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
             Document selector = new Document() { { "SessionId", id}, { "ApplicationName", applicationName} };
            Document sessionUpdate = new Document() { { "$set", new Document() { { "Expires", DateTime.Now.AddMinutes(timeout)} }}};
        }

        public static void EvictSession(Session session)
        {
            Document selector = new Document() { { "SessionId", session.SessionID }, { "ApplicationName", session.ApplicationName },{"LockId",session.LockID} };
            try
            {
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
            Document sessionLock = new Document() { { "$set", new Document() {{"LockDate", session.LockDate }, 
            {"LockId", session.LockID }, {"Locked", session.Locked }, {"Flags",session.Flags} } } };
            sessions.Update(sessionLock, selector, 0, false);
        }

        public static void ReleaseLock(string id, string applicationName, object lockId, double timeout)
        {
            Document selector = new Document() { { "SessionId", id }, { "ApplicationName", applicationName},{"LockId",lockId }};
            Document sessionLock = new Document() { { "$set", new Document() {{"Expires", DateTime.Now.AddMinutes(timeout)}, {"Locked", false }}}};
            sessions.Update(sessionLock, selector, 0, false);
        }
    }
}
