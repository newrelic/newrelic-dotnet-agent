using System.Threading;
using OpenRastaSite.Resources;

namespace OpenRastaSite.Handlers
{
    //https://support.newrelic.com/tickets/46801
    public class BasketHandler
    {
        public object Get()
        {
            return new Basket { Title = "GET." };
        }

        public object Post()
        {
            return new Basket { Title = "POST." };
        }

        public object Put()
        {
            return new Basket { Title = "PUT." };
        }

        public object Delete()
        {
            return new Basket { Title = "DELETE." };
        }

    }
}
