using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace MongoSessionTest
{
    public partial class RedirectedPage : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Label1.Text = (string)Session["S1"];
        }

        protected void Button1_Click(object sender, EventArgs e)
        {

        }
    }
}
