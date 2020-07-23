using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(WebForms45Application.Startup))]
namespace WebForms45Application
{
    public partial class Startup {
        public void Configuration(IAppBuilder app) {

        }
    }
}
