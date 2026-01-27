// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;

namespace Dongle.Cryptography;

/// <summary>
/// Summary description for MD5.
/// </summary>
public class Md5
{
    /// <summary>
    /// lookup table 4294967296*sin(i)
    /// </summary>
    protected static readonly uint[] T =
    {
        0xd76aa478,0xe8c7b756,0x242070db,0xc1bdceee,
        0xf57c0faf,0x4787c62a,0xa8304613,0xfd469501,
        0x698098d8,0x8b44f7af,0xffff5bb1,0x895cd7be,
        0x6b901122,0xfd987193,0xa679438e,0x49b40821,
        0xf61e2562,0xc040b340,0x265e5a51,0xe9b6c7aa,
        0xd62f105d,0x2441453,0xd8a1e681,0xe7d3fbc8,
        0x21e1cde6,0xc33707d6,0xf4d50d87,0x455a14ed,
        0xa9e3e905,0xfcefa3f8,0x676f02d9,0x8d2a4c8a,
        0xfffa3942,0x8771f681,0x6d9d6122,0xfde5380c,
        0xa4beea44,0x4bdecfa9,0xf6bb4b60,0xbebfbc70,
        0x289b7ec6,0xeaa127fa,0xd4ef3085,0x4881d05,
        0xd9d4d039,0xe6db99e5,0x1fa27cf8,0xc4ac5665,
        0xf4292244,0x432aff97,0xab9423a7,0xfc93a039,
        0x655b59c3,0x8f0ccc92,0xffeff47d,0x85845dd1,
        0x6fa87e4f,0xfe2ce6e0,0xa3014314,0x4e0811a1,
        0xf7537e82,0xbd3af235,0x2ad7d2bb,0xeb86d391
    };

    /*****instance variables**************/
    /// <summary>
    /// X used to proces data in 
    ///	512 bits chunks as 16 32 bit word
    /// </summary>
    protected uint[] X = new uint[16];

    /// <summary>
    /// the finger print obtained. 
    /// </summary>
    internal Digest DgFingerPrint;

    /// <summary>
    /// the input bytes
    /// </summary>
    protected byte[] ByteInput;

    /**********************EVENTS AND DELEGATES*******************************************/
    public delegate void ValueChanging(object sender, Md5ChangingEventArgs changing);
    public delegate void ValueChanged(object sender, Md5ChangedEventArgs changed);

    public event ValueChanging OnValueChanging;
    public event ValueChanged OnValueChanged;



    /********************************************************************/
    /***********************PROPERTIES ***********************/
    /// <summary>
    ///gets or sets as string
    /// </summary>
    public string Value
    {
        get
        {
            var tempCharArray = new char[ByteInput.Length];
            for (var i = 0; i < ByteInput.Length; i++)
            {
                tempCharArray[i] = (char)ByteInput[i];
            }
            return new string(tempCharArray);
        }
        set
        {
            if (OnValueChanging != null)
            {
                OnValueChanging(this, new Md5ChangingEventArgs(value));
            }

            ByteInput = new byte[value.Length];
            for (var i = 0; i < value.Length; i++)
            {
                ByteInput[i] = (byte)value[i];
            }
            DgFingerPrint = CalculateMd5Value();

            if (OnValueChanged != null)
            {
                OnValueChanged(this, new Md5ChangedEventArgs(value, DgFingerPrint.ToString()));
            }
        }
    }

    /// <summary>
    /// get/sets as  byte array 
    /// </summary>
    public byte[] ValueAsByte
    {
        get
        {
            var bt = new byte[ByteInput.Length];
            for (var i = 0; i < ByteInput.Length; i++)
            {
                bt[i] = ByteInput[i];
            }
            return bt;
        }
        set
        {
            if (OnValueChanging != null)
            {
                OnValueChanging(this, new Md5ChangingEventArgs(value));
            }

            ByteInput = new byte[value.Length];
            for (var i = 0; i < value.Length; i++)
            {
                ByteInput[i] = value[i];
            }
            DgFingerPrint = CalculateMd5Value();


            if (OnValueChanged != null)
            {
                OnValueChanged(this, new Md5ChangedEventArgs(value, DgFingerPrint.ToString()));
            }
        }
    }

    public void SetValueFromStream(Stream stream)
    {
        var content = ReadAllBytes(stream);
        ValueAsByte = content;
    }

    //gets the signature/figner print as string
    public string Hash
    {
        get
        {
            return DgFingerPrint.ToString();
        }
    }

    //gets the signature/figner print as string
    public byte[] HashAsByteArray
    {
        get
        {
            return Md5.HexStringToByteArray(this.DgFingerPrint.ToString());
        }
    }

    private static byte[] HexStringToByteArray(string hex)
    {
        int ToInt(char c)
        {
            if (c <= '9' && c >= '0')
            {
                return c - '0';
            }

            if (c >= 'A' && c <= 'F')
            {
                return 10 + (c - 'A');
            }

            if (c >= 'a' && c <= 'f')
            {
                return 10 + (c - 'a');
            }

            throw new ArgumentException($"Invalid Hexadecimal string '{hex}'.", "hex");
        }

        int length = hex.Length;
        byte[] array = new byte[length / 2];

        for (int i = 0; i < length; i += 2)
        {
            array[i / 2] = (byte)((ToInt(hex[i]) << 4) + ToInt(hex[i + 1]));
        }
        return array;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    public Md5()
    {
        Value = string.Empty;
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        var binaryReader = new BinaryReader(stream);
        return ReadAllBytes(binaryReader);
    }

    private static byte[] ReadAllBytes(BinaryReader reader)
    {
        const int bufferSize = 4096;
        using (var ms = new MemoryStream())
        {
            var buffer = new byte[bufferSize];
            int count;
            while ((count = reader.Read(buffer, 0, buffer.Length)) != 0)
            {
                ms.Write(buffer, 0, count);
            }
            return ms.ToArray();
        }
    }

    /// <summary>
    /// calculat md5 signature of the string in Input
    /// </summary>
    /// <returns> Digest: the finger print of msg</returns>
    internal Digest CalculateMd5Value()
    {
        var dg = new Digest();
        var msg = CreatePaddedBuffer();
        var n = (uint)(msg.Length * 8) / 32;
        for (uint i = 0; i < n / 16; i++)
        {
            CopyBlock(msg, i);
            PerformTransformation(ref dg.A, ref dg.B, ref dg.C, ref dg.D);
        }
        return dg;
    }

    /********************************************************
     * TRANSFORMATIONS :  FF , GG , HH , II  acc to RFC 1321
     * where each Each letter represnets the aux function used
     *********************************************************/

    /// <summary>
    /// perform transformation using f(((b&c) | (~(b)&d))
    /// </summary>
    protected void TransF(ref uint a, uint b, uint c, uint d, uint k, ushort s, uint i)
    {
        a = b + Md5Helper.RotateLeft((a + ((b & c) | (~(b) & d)) + X[k] + T[i - 1]), s);
    }

    /// <summary>
    /// perform transformation using g((b&d) | (c & ~d) )
    /// </summary>
    protected void TransG(ref uint a, uint b, uint c, uint d, uint k, ushort s, uint i)
    {
        a = b + Md5Helper.RotateLeft((a + ((b & d) | (c & ~d)) + X[k] + T[i - 1]), s);
    }

    /// <summary>
    /// perform transformation using h(b^c^d)
    /// </summary>
    protected void TransH(ref uint a, uint b, uint c, uint d, uint k, ushort s, uint i)
    {
        a = b + Md5Helper.RotateLeft((a + (b ^ c ^ d) + X[k] + T[i - 1]), s);
    }

    /// <summary>
    /// perform transformation using i (c^(b|~d))
    /// </summary>
    protected void TransI(ref uint a, uint b, uint c, uint d, uint k, ushort s, uint i)
    {
        a = b + Md5Helper.RotateLeft((a + (c ^ (b | ~d)) + X[k] + T[i - 1]), s);
    }

    /// <summary>
    /// Perform All the transformation on the data
    /// </summary>
    /// <param name="a">A</param>
    /// <param name="b">B </param>
    /// <param name="c">C</param>
    /// <param name="d">D</param>
    protected void PerformTransformation(ref uint a, ref uint b, ref uint c, ref uint d)
    {
        //// saving  ABCD  to be used in end of loop
        var aa = a;
        var bb = b;
        var cc = c;
        var dd = d;

        /* Round 1
         * [ABCD  0  7  1]  [DABC  1 12  2]  [CDAB  2 17  3]  [BCDA  3 22  4]
         * [ABCD  4  7  5]  [DABC  5 12  6]  [CDAB  6 17  7]  [BCDA  7 22  8]
         * [ABCD  8  7  9]  [DABC  9 12 10]  [CDAB 10 17 11]  [BCDA 11 22 12]
         * [ABCD 12  7 13]  [DABC 13 12 14]  [CDAB 14 17 15]  [BCDA 15 22 16]*/
        TransF(ref a, b, c, d, 0, 7, 1); TransF(ref d, a, b, c, 1, 12, 2); TransF(ref c, d, a, b, 2, 17, 3); TransF(ref b, c, d, a, 3, 22, 4);
        TransF(ref a, b, c, d, 4, 7, 5); TransF(ref d, a, b, c, 5, 12, 6); TransF(ref c, d, a, b, 6, 17, 7); TransF(ref b, c, d, a, 7, 22, 8);
        TransF(ref a, b, c, d, 8, 7, 9); TransF(ref d, a, b, c, 9, 12, 10); TransF(ref c, d, a, b, 10, 17, 11); TransF(ref b, c, d, a, 11, 22, 12);
        TransF(ref a, b, c, d, 12, 7, 13); TransF(ref d, a, b, c, 13, 12, 14); TransF(ref c, d, a, b, 14, 17, 15); TransF(ref b, c, d, a, 15, 22, 16);
        /* Round 2
         *[ABCD  1  5 17]  [DABC  6  9 18]  [CDAB 11 14 19]  [BCDA  0 20 20]
         *[ABCD  5  5 21]  [DABC 10  9 22]  [CDAB 15 14 23]  [BCDA  4 20 24]
         *[ABCD  9  5 25]  [DABC 14  9 26]  [CDAB  3 14 27]  [BCDA  8 20 28]
         *[ABCD 13  5 29]  [DABC  2  9 30]  [CDAB  7 14 31]  [BCDA 12 20 32]*/
        TransG(ref a, b, c, d, 1, 5, 17); TransG(ref d, a, b, c, 6, 9, 18); TransG(ref c, d, a, b, 11, 14, 19); TransG(ref b, c, d, a, 0, 20, 20);
        TransG(ref a, b, c, d, 5, 5, 21); TransG(ref d, a, b, c, 10, 9, 22); TransG(ref c, d, a, b, 15, 14, 23); TransG(ref b, c, d, a, 4, 20, 24);
        TransG(ref a, b, c, d, 9, 5, 25); TransG(ref d, a, b, c, 14, 9, 26); TransG(ref c, d, a, b, 3, 14, 27); TransG(ref b, c, d, a, 8, 20, 28);
        TransG(ref a, b, c, d, 13, 5, 29); TransG(ref d, a, b, c, 2, 9, 30); TransG(ref c, d, a, b, 7, 14, 31); TransG(ref b, c, d, a, 12, 20, 32);
        /* Round 3
         * [ABCD  5  4 33]  [DABC  8 11 34]  [CDAB 11 16 35]  [BCDA 14 23 36]
         * [ABCD  1  4 37]  [DABC  4 11 38]  [CDAB  7 16 39]  [BCDA 10 23 40]
         * [ABCD 13  4 41]  [DABC  0 11 42]  [CDAB  3 16 43]  [BCDA  6 23 44]
         * [ABCD  9  4 45]  [DABC 12 11 46]  [CDAB 15 16 47]  [BCDA  2 23 48]*/
        TransH(ref a, b, c, d, 5, 4, 33); TransH(ref d, a, b, c, 8, 11, 34); TransH(ref c, d, a, b, 11, 16, 35); TransH(ref b, c, d, a, 14, 23, 36);
        TransH(ref a, b, c, d, 1, 4, 37); TransH(ref d, a, b, c, 4, 11, 38); TransH(ref c, d, a, b, 7, 16, 39); TransH(ref b, c, d, a, 10, 23, 40);
        TransH(ref a, b, c, d, 13, 4, 41); TransH(ref d, a, b, c, 0, 11, 42); TransH(ref c, d, a, b, 3, 16, 43); TransH(ref b, c, d, a, 6, 23, 44);
        TransH(ref a, b, c, d, 9, 4, 45); TransH(ref d, a, b, c, 12, 11, 46); TransH(ref c, d, a, b, 15, 16, 47); TransH(ref b, c, d, a, 2, 23, 48);
        /* Round  4
         *[ABCD  0  6 49]  [DABC  7 10 50]  [CDAB 14 15 51]  [BCDA  5 21 52]
         *[ABCD 12  6 53]  [DABC  3 10 54]  [CDAB 10 15 55]  [BCDA  1 21 56]
         *[ABCD  8  6 57]  [DABC 15 10 58]  [CDAB  6 15 59]  [BCDA 13 21 60]
         *[ABCD  4  6 61]  [DABC 11 10 62]  [CDAB  2 15 63]  [BCDA  9 21 64]*/
        TransI(ref a, b, c, d, 0, 6, 49); TransI(ref d, a, b, c, 7, 10, 50); TransI(ref c, d, a, b, 14, 15, 51); TransI(ref b, c, d, a, 5, 21, 52);
        TransI(ref a, b, c, d, 12, 6, 53); TransI(ref d, a, b, c, 3, 10, 54); TransI(ref c, d, a, b, 10, 15, 55); TransI(ref b, c, d, a, 1, 21, 56);
        TransI(ref a, b, c, d, 8, 6, 57); TransI(ref d, a, b, c, 15, 10, 58); TransI(ref c, d, a, b, 6, 15, 59); TransI(ref b, c, d, a, 13, 21, 60);
        TransI(ref a, b, c, d, 4, 6, 61); TransI(ref d, a, b, c, 11, 10, 62); TransI(ref c, d, a, b, 2, 15, 63); TransI(ref b, c, d, a, 9, 21, 64);

        a = a + aa;
        b = b + bb;
        c = c + cc;
        d = d + dd;
    }

    /// <summary>
    /// Create Padded buffer for processing , buffer is padded with 0 along 
    /// with the size in the end
    /// </summary>
    /// <returns>the padded buffer as byte array</returns>
    protected byte[] CreatePaddedBuffer()
    {
        var temp = (448 - ((ByteInput.Length * 8) % 512));
        var pad = (uint)((temp + 512) % 512);
        if (pad == 0)
        {
            pad = 512;
        }

        var sizeMsgBuff = (uint)((ByteInput.Length) + (pad / 8) + 8);
        var sizeMsg = (ulong)ByteInput.Length * 8;
        var msg = new byte[sizeMsgBuff];

        for (var i = 0; i < ByteInput.Length; i++)
        {
            msg[i] = ByteInput[i];
        }

        msg[ByteInput.Length] |= 0x80;
        for (var i = 8; i > 0; i--)
        {
            msg[sizeMsgBuff - i] = (byte)(sizeMsg >> ((8 - i) * 8) & 0x00000000000000ff);
        }

        return msg;
    }


    /// <summary>
    /// Copies a 512 bit block into X as 16 32 bit words
    /// </summary>
    /// <param name="msg"> source buffer</param>
    /// <param name="block">no of block to copy starting from 0</param>
    protected void CopyBlock(byte[] msg, uint block)
    {
        block = block << 6;
        for (uint j = 0; j < 61; j += 4)
        {
            X[j >> 2] = (((uint)msg[block + (j + 3)]) << 24) |
                        (((uint)msg[block + (j + 2)]) << 16) |
                        (((uint)msg[block + (j + 1)]) << 8) |
                        msg[block + (j)];

        }
    }
}
#pragma warning restore 1570
