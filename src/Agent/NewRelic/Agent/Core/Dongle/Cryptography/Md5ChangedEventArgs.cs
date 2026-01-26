// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace Dongle.Cryptography;

/// <summary>
/// class for cahnged event args
/// </summary>
public class Md5ChangedEventArgs : EventArgs
{
    public readonly byte[] NewData;
    public readonly string FingerPrint;

    public Md5ChangedEventArgs(IList<byte> data, string hashedValue)
    {
        NewData = new byte[data.Count];
        for (var i = 0; i < data.Count; i++)
        {
            NewData[i] = data[i];
        }
        FingerPrint = hashedValue;
    }

    public Md5ChangedEventArgs(string data, string hashedValue)
    {
        NewData = new byte[data.Length];
        for (var i = 0; i < data.Length; i++)
        {
            NewData[i] = (byte)data[i];
        }
        FingerPrint = hashedValue;
    }

}
