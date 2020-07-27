using System;
using System.Collections.Generic;
using System.Threading;
using System.Web;
using OpenRastaSite.Resources;

namespace OpenRastaSite.Handlers
{
    //https://support.newrelic.com/tickets/46801
    public class HomeHandler
    {
        public object Get()
        {
            return new Home { Title = "GET." };
        }

        public object Post()
        {
            return new Home { Title = "POST." };
        }

        public object Put()
        {
            return new Home { Title = "PUT." };
        }

        public object Delete()
        {
            return new Home { Title = "DELETE." };
        }

    }
}
