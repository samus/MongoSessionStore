using System;
using System.Configuration;
using System.Collections.Generic;
using System.Web.SessionState;
using System.Text;
using MongoDB.Driver;
using MongoDB.Driver.Configuration;

namespace MongoDBSessionStore
{
    public class Session
    {
        private string _sessionID;
        private DateTime _created;
        private DateTime _expires;
        private string _applicationName;
        private DateTime _lockDate;
        private object _lockID;
        private int _timeout;
        private bool _locked;
        private string _sessionItems;
        private int _sessionItemsCount;
        private int _flags;

      
        private OidGenerator oGen;

        public Session() { }

        public Session(string id, string applicationName, int timeout, string sessionItems, int sessionItemsCount,SessionStateActions actionFlags )
        {
            this._sessionID = id;
            this._applicationName = applicationName;
            this._lockDate = DateTime.Now;
            this._lockID = 0;
            this._timeout = timeout;
            this._locked = false;
            this._sessionItems = sessionItems;
            this._sessionItemsCount = sessionItemsCount;
            this._flags = (int)actionFlags;
            this._created = DateTime.Now;
            this._expires = DateTime.Now.AddMinutes((Double)this._timeout);         
        }

        #region Properties
        public string SessionID
        {
            get { return this._sessionID; }
            set { this._sessionID = value; }
        }

        public DateTime Created
        {
            get { return this._created; }
            set { this._created = value; }
        }

        public DateTime Expires
        {
            get { return this._expires; }
            set { this._expires = value; }
        }

        public string ApplicationName
        {
            get { return this._applicationName; }
            set { this._applicationName = value; }
        }

        public DateTime LockDate
        {
            get { return this._lockDate; }
            set { this._lockDate = value; }
        }

        public object LockID
        {
            get { return this._lockID; }
            set { this._lockID = value; }
        }

        public int Timeout
        {
            get { return this._timeout; }
            set { this._timeout = value; }
        }

        public bool Locked
        {
            get { return this._locked; }
            set { this._locked = value; }
        }

        public string SessionItems
        {
            get { return this._sessionItems; }
            set { this._sessionItems = value; }
        }

        public int SessionItemsCount
        {
            get { return this._sessionItemsCount; }
            set { this._sessionItemsCount = value; }
        }

        public int Flags
        {
            get { return this._flags; }
            set { this._flags = value; }
        }
        #endregion

    }
}
