using System.Web.Http;

namespace OwinService.Controllers
{
	public class DefaultController : ApiController
	{
		[HttpGet]
		[Route("")]
		public string Index()
		{
			return "Hello World!";
		}
	}
}
