// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Dongle.Cryptography;

/// <summary>
/// Represent digest with ABCD
/// </summary>
internal sealed class Digest
{
    public uint A;
    public uint B;
    public uint C;
    public uint D;

    public Digest()
    {
        A = (uint)Md5InitializerConstant.A;
        B = (uint)Md5InitializerConstant.B;
        C = (uint)Md5InitializerConstant.C;
        D = (uint)Md5InitializerConstant.D;
    }
    public override string ToString()
    {
        var st = Md5Helper.ReverseByte(A).ToString("X8") +
                 Md5Helper.ReverseByte(B).ToString("X8") +
                 Md5Helper.ReverseByte(C).ToString("X8") +
                 Md5Helper.ReverseByte(D).ToString("X8");

        return st;
    }
}
