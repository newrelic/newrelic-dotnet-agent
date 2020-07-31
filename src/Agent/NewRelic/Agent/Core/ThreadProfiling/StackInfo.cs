// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.InteropServices;

namespace NewRelic.Agent.Core.ThreadProfiling
{
    public interface IStackInfo
    {
        void StoreFunctionIds(IntPtr data, int length);
        int CurrentIndex { get; set; }
        IntPtr FunctionId { get; }
    }

    public class StackInfo : IStackInfo
    {
        private readonly object _lock = new object();
        private IntPtr[] _functionIds;

        public void StoreFunctionIds(IntPtr data, int length)
        {
            lock (_lock)
            {
                if (length > 0 && data != IntPtr.Zero)
                {
                    _functionIds = new IntPtr[length];
                    CurrentIndex = length - 1;
                    Marshal.Copy(data, _functionIds, 0, length);
                }
            }
        }

        public int CurrentIndex { get; set; }

        /// <summary>
        /// Returns the next function id in the stack.
        /// </summary>
        /// <remarks>
        /// Note that the functions are stored in reverse order so the top-most function is the last in the list.
        /// </remarks>
        public IntPtr FunctionId
        {
            get
            {
                lock (_lock)
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
        }
    }
}
