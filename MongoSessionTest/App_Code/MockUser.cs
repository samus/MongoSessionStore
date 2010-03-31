using System;
using System.Collections.Generic;
using System.Web;

namespace MongoSessionTest
{
    [Serializable]
    public class MockUser
    {

        public MockUser()
        {
            this.UserID = Guid.NewGuid();
            this.UserName = "Fake User";
            this.DateCreated = DateTime.Now;
        }

        public Guid UserID { get; set; }
        public string UserName { get; set; }
        public DateTime DateCreated { get; set; }

    }
}
