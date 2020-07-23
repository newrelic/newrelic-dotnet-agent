using System;

namespace OwinRemotingShared
{
	public class MyMarshalByRefClass : MarshalByRefObject
	{
		public int MyMethod()
		{
			return 666;
		}
	}
}
