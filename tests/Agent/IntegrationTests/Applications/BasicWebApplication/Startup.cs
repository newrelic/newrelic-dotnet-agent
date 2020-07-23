using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(BasicWebApplication.Startup))]
namespace BasicWebApplication
{
    public partial class Startup {
        public void Configuration(IAppBuilder app) {
        }
    }
}
