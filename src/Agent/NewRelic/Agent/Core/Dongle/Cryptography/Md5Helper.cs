// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace Dongle.Cryptography;

/// <summary>
/// helper class providing suporting function
/// </summary>
internal sealed class Md5Helper
{
    /// <summary>
    /// Left rotates the input word
    /// </summary>
    /// <param name="uiNumber">a value to be rotated</param>
    /// <param name="shift">no of bits to be rotated</param>
    /// <returns>the rotated value</returns>
    public static uint RotateLeft(uint uiNumber, ushort shift)
    {
        return ((uiNumber >> 32 - shift) | (uiNumber << shift));
    }

    /// <summary>
    /// perform a ByteReversal on a number
    /// </summary>
    /// <param name="uiNumber">value to be reversed</param>
    /// <returns>reversed value</returns>
    public static uint ReverseByte(uint uiNumber)
    {
        return (((uiNumber & 0x000000ff) << 24) |
                (uiNumber >> 24) |
                ((uiNumber & 0x00ff0000) >> 8) |
                ((uiNumber & 0x0000ff00) << 8));
    }
}
