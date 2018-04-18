using Owin;

namespace OwinService
{
	public interface IOwinAppBuilder
	{
		void Configuration(IAppBuilder appBuilder);
	}
}