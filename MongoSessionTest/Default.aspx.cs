using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace MongoSessionTest
{
    public partial class _Default : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            System.Web.SessionState.SessionIDManager manager = new System.Web.SessionState.SessionIDManager();
    
            string sessionID = manager.CreateSessionID(HttpContext.Current);
            Response.Write(sessionID);

        }
    }
}
