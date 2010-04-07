using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace MongoSessionTest
{
    public partial class LoadTestTwo : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            MockUser user = (MockUser)Session["User"];
            Session.Abandon();
        }
    }
}
