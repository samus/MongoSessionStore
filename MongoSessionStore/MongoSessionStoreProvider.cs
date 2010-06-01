using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Collections.Specialized;
using System.Web;
using System.Web.Configuration;
using System.Configuration;
using System.Configuration.Provider;
using System.Web.SessionState;
using MongoDB;
using MongoDB.Configuration;


namespace MongoSessionStore
{
    public sealed class MongoSessionStoreProvider : SessionStateStoreProviderBase
    {
        private SessionStateSection sessionStateSection = null;
        private string eventSource = "MongoSessionStore";
        private string eventLog = "Application";

        private bool _logExceptions = false;
        public bool WriteExceptionsToEventLog
        {
            get { return _logExceptions; }
            set { _logExceptions = value; }
        }

        //
        // The ApplicationName property is used to differentiate sessions
        // in the data source by application.
        //
        public string _applicationName;
        public string ApplicationName
        {
            get { return _applicationName; }
        }

        public override void Initialize(string name, NameValueCollection config)
        {
            if (config == null)
                throw new ArgumentNullException("config");

            if (name == null || name.Length == 0)
                name = "MongoSessionStore";

            if (String.IsNullOrEmpty(config["description"]))
            {
                config.Remove("description");
                config.Add("description", "MongoDB Session State Store provider");
            }
            // Initialize the abstract base class.
            base.Initialize(name, config);

            _applicationName = System.Web.Hosting.HostingEnvironment.ApplicationVirtualPath;

            Configuration cfg = WebConfigurationManager.OpenWebConfiguration(ApplicationName);
            sessionStateSection = (SessionStateSection)cfg.GetSection("system.web/sessionState");
            if (config["writeExceptionsToEventLog"] != null)
            {
                if (config["writeExceptionsToEventLog"].ToUpper() == "TRUE")
                    _logExceptions = true;
            }
        }

        public override void Dispose()
        {
        }

        public override bool SetItemExpireCallback(SessionStateItemExpireCallback expireCallback)
        {
            return false;
        }

        public override void SetAndReleaseItemExclusive(HttpContext context, string id, SessionStateStoreData item, object lockId, bool newItem)
        {
            var sessionStore = SessionStore.Instance;
            try
            {

                byte[] serializedItems = Serialize((SessionStateItemCollection)item.Items);
                Binary sessionItems = new Binary(serializedItems);

                if (newItem)
                {
                    // Delete an existing expired session if it exists.
                    sessionStore.EvictExpiredSession(id, _applicationName);

                    // insert new session item.
                    Session session = new Session(id, this._applicationName, item.Timeout, sessionItems, item.Items.Count, 0);
                    sessionStore.Insert(session);
                }
                else
                {
                    sessionStore.UpdateSession(id, item.Timeout, sessionItems, this._applicationName, item.Items.Count, lockId);
                }
            }
            catch (Exception e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "SetAndReleaseItemExclusive");
                    throw new ProviderException(e.Message, e.InnerException);
                }
                else
                    throw e;
            }
        }


        public override SessionStateStoreData GetItem(HttpContext context,
          string id,
          out bool locked,
          out TimeSpan lockAge,
          out object lockId,
          out SessionStateActions actionFlags)
        {
            return GetSessionStoreItem(false, context, id, out locked, out lockAge, out lockId, out actionFlags);
        }

        public override SessionStateStoreData GetItemExclusive(HttpContext context,
          string id,
          out bool locked,
          out TimeSpan lockAge,
          out object lockId,
          out SessionStateActions actionFlags)
        {
            return GetSessionStoreItem(true, context, id, out locked, out lockAge, out lockId, out actionFlags);
        }


        //
        // GetSessionStoreItem is called by both the GetItem and 
        // GetItemExclusive methods. GetSessionStoreItem retrieves the 
        // session data from the data source. If the lockRecord parameter
        // is true (in the case of GetItemExclusive), then GetSessionStoreItem
        // locks the record and sets a new LockId and LockDate.
        //
        private SessionStateStoreData GetSessionStoreItem(bool lockRecord,
            HttpContext context,
            string id,
            out bool locked,
            out TimeSpan lockAge,
            out object lockId,
            out SessionStateActions actionFlags)
        {
            // Initial values for return value and out parameters.
            SessionStateStoreData item = null;
            lockAge = TimeSpan.Zero;
            lockId = null;
            locked = false;
            actionFlags = 0;

            // byte array to hold serialized SessionStateItemCollection.
            byte[] serializedItems = new byte[0];
            
            var sessionStore = SessionStore.Instance;
            try
            {
                Session session = sessionStore.Get(id, this._applicationName);
                // lockRecord is true when called from GetItemExclusive and
                // false when called from GetItem.
                // Obtain a lock if possible. Evict the record if it is expired.
                if (session == null)
                {
                    // Not found. The locked value is false.
                    locked = false;
                }
                else if (session.Expires < DateTime.Now)
                {
                    locked = false;
                    sessionStore.EvictSession(session);

                }
                else if (session.Locked)
                {
                    locked = true;
                    lockAge = DateTime.Now.Subtract(session.LockDate);
                    lockId = session.LockID;
                }
                else
                {
                    locked = false;
                    lockId = session.LockID;
                    actionFlags = (SessionStateActions)session.Flags;


                    if (lockRecord)
                    {
                        lockId = (int)lockId + 1;
                        session.LockID = lockId;
                        session.Flags = 0;
                        sessionStore.LockSession(session);
                    }

                    if (actionFlags == SessionStateActions.InitializeItem)
                        item = CreateNewStoreData(context, sessionStateSection.Timeout.Minutes);
                    else
                        item = Deserialize(context, session.SessionItems.Bytes, session.Timeout);
                }

            }
            catch (Exception e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "GetSessionStoreItem");
                    throw new ProviderException(e.Message, e.InnerException);
                }
                else
                    throw e;
            }
            return item;
        }

        //
        // Serialize is called by the SetAndReleaseItemExclusive method to 
        // convert the SessionStateItemCollection into a Binary.
        //

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
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "GetSessionStoreItem");
                    throw new ProviderException(e.Message, e.InnerException);
                }
                else
                    throw e;

            }
            finally
            {
                writer.Close();
            }
            return ms.ToArray();
        }

        //
        // DeSerialize is called by the GetSessionStoreItem method to 
        // convert the Binary to a 
        // SessionStateItemCollection.
        //

        private SessionStateStoreData Deserialize(HttpContext context, byte[] serializedItems, int timeout)
        {
            MemoryStream ms =
              new MemoryStream(serializedItems);

            SessionStateItemCollection sessionItems =
              new SessionStateItemCollection();

            if (ms.Length > 0)
            {
                BinaryReader reader = new BinaryReader(ms);
                sessionItems = SessionStateItemCollection.Deserialize(reader);
            }

            return new SessionStateStoreData(sessionItems,
              SessionStateUtility.GetSessionStaticObjects(context),
              timeout);
        }

        //
        // SessionStateProviderBase.ReleaseItemExclusive
        //
        public override void ReleaseItemExclusive(HttpContext context, string id, object lockId)
        {
            var sessionStore = SessionStore.Instance;
            try
            {
                sessionStore.ReleaseLock(id, this._applicationName, lockId, sessionStateSection.Timeout.TotalMinutes);
            }
            catch (Exception e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "ReleaseItemExclusive");
                    throw new ProviderException(e.Message, e.InnerException);
                }
                else
                    throw e;
            }

        }


        //
        // SessionStateProviderBase.RemoveItem
        //

        public override void RemoveItem(HttpContext context, string id, object lockId, SessionStateStoreData item)
        {
            var sessionStore = SessionStore.Instance;
            try
            {
                sessionStore.EvictSession(id, this._applicationName, lockId);
            }
            catch (Exception e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "RemoveItem");
                    throw new ProviderException(e.Message, e.InnerException);
                }
                else
                    throw e;
            }
        }

        public override void CreateUninitializedItem(HttpContext context, string id, int timeout)
        {
            byte[] serializedItems = new byte[0];
            Binary sessionItems = new Binary(serializedItems);
            Session session = new Session(id, this._applicationName, timeout, sessionItems, 0, SessionStateActions.InitializeItem);
            var sessionStore = SessionStore.Instance;
            try
            {
                sessionStore.Insert(session);
            }
            catch (Exception e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "CreateUninitializedItem");
                    throw new ProviderException(e.Message, e.InnerException);
                }
                else
                    throw e;
            }
        }

        public override SessionStateStoreData CreateNewStoreData(HttpContext context, int timeout)
        {
            return new SessionStateStoreData(new SessionStateItemCollection(), SessionStateUtility.GetSessionStaticObjects(context), timeout);
        }

        public override void ResetItemTimeout(HttpContext context, string id)
        {
            var sessionStore = SessionStore.Instance;
            try
            {
                sessionStore.UpdateSessionExpiration(id, this._applicationName, sessionStateSection.Timeout.TotalMinutes);
            }
            catch (Exception e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "ResetItemTimeout");
                    throw new ProviderException(e.Message, e.InnerException);
                }
                else
                    throw e;
            }
        }

        public override void InitializeRequest(HttpContext context)
        {
        }


        public override void EndRequest(HttpContext context)
        {
        }

        private void WriteToEventLog(Exception e, string action)
        {
            EventLog log = new EventLog();
            log.Source = eventSource;
            log.Log = eventLog;

            string message =
              "An exception occurred ";
            message += "Action: " + action + "\n\n";
            message += "Exception: " + e.ToString();
            log.WriteEntry(message);
        }
    }
}
