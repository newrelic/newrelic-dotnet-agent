using System;
using System.Linq;

namespace NewRelic.Agent.Core.ThreadProfiling
{
	public class MockStackInfo : IStackInfo
	{
		private IntPtr[] _functionIds;

		public MockStackInfo() { }

		public MockStackInfo(IntPtr[] functionIdentifiers)
		{
			_functionIds = new IntPtr[functionIdentifiers.Count()];
			int index = 0;

			foreach (IntPtr fid in functionIdentifiers)
			{
				_functionIds[index++] = fid;
			}

			CurrentIndex = functionIdentifiers.Count() - 1;
		}

		public void StoreFunctionIds(IntPtr data, Int32 length)
		{
		}

		public int CurrentIndex {get; set;}

		public IntPtr FunctionId
		{
			get
			{
				if (_functionIds != null && CurrentIndex >= 0 && CurrentIndex < _functionIds.Length)
				{
					return _functionIds[CurrentIndex];
				}
				else
				{
					return IntPtr.Zero;
				}
			}
		}

		#region Stack Snapshot Generators

		static public IntPtr[] GenerateStackSnapshot(int numFunctions, int start, int increment, bool randomize = false)
		{
			IntPtr[] functionIds = new IntPtr[numFunctions];

			for (int i = 0; i < numFunctions; i++)
			{
				if (randomize)
				{
					Random rand = new Random(DateTime.UtcNow.Millisecond);
					int multiplier = rand.Next(2, 300);
					functionIds[i] = new IntPtr(start + (i * multiplier));
				}
				else
				{
					functionIds[i] = new IntPtr(start + (i * increment));
				}
			}
				
			return functionIds;
		}

		#endregion
	}
}
