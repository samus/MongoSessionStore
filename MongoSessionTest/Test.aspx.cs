using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;


namespace MongoSessionTest
{
    public partial class Test : System.Web.UI.Page
    {
        
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Page.IsPostBack)
            {
                MockUser user = new MockUser();
                Session.Add("User", user);
            }
        }

        protected void Button1_Click(object sender, EventArgs e)
        {
            MockUser user = (MockUser)Session["User"];
            Label1.Text = user.UserID.ToString();
            Label2.Text = user.UserName;
            Label3.Text = user.DateCreated.ToString();
        }

        protected void Button2_Click(object sender, EventArgs e)
        {
            Session.Abandon();
        }
    }
}
