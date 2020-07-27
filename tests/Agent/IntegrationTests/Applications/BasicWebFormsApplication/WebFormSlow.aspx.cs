using System;
using System.Threading;

namespace BasicWebFormsApplication
{
    public partial class WebFormSlow : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Thread.Sleep(2500);
        }
    }
}
