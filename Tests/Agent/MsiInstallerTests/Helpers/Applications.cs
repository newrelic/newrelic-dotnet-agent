namespace FunctionalTests.Helpers
{
	public class Applications
	{
		public static Application DotNet_Functional_InstallTestApp = new Application { Name = "DotNet-Functional-InstallTestApp", BaseUrlFormatter = "http://{0}/DotNet-Functional-InstallTestApp/" };
	}
}
