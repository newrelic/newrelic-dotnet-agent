using OpenRasta.Configuration;
using OpenRastaSite.Handlers;
using OpenRastaSite.Resources;

namespace OpenRastaSite
{
    public class Configuration : IConfigurationSource
    {

        public void Configure()
        {
            using (OpenRastaConfiguration.Manual)
            {
                ResourceSpace.Has.ResourcesOfType<Home>()
                    .AtUri("/home")
                    .HandledBy<HomeHandler>()
                    .RenderedByAspx("~/Views/HomeView.aspx");
                ResourceSpace.Has.ResourcesOfType<Basket>()
                    .AtUri("/basket")
                    .HandledBy<BasketHandler>()
                    .RenderedByAspx("~/Views/BasketView.aspx");
            }
        }

    }
}
