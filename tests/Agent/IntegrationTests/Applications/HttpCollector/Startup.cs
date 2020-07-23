using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(HttpCollector.Startup))]
namespace HttpCollector
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
        }
    }
}
