// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Core;
using System.Text;

namespace NewRelic.Agent.Core.Utilities
{
    public class TrimmedEncodedBuffer
    {
        public TrimmedEncodedBuffer(Encoding encoding, byte[] buffer, int offset, int count)
        {
            _originalOffset = offset;
            _originalCount = count;
            Encoding = encoding;
            Buffer = buffer;
        }

        public byte[] Buffer { get; }

        public Encoding Encoding { get; }

        public int Offset => _originalOffset + LeadingExtraBytesCount;
        public int Length => _originalCount - LeadingExtraBytesCount - TrailingExtraBytesCount;

        public int LeadingExtraBytesOffset => _originalOffset;

        public bool HasLeadingExtraBytes => _hasLeadingExtraBytes;

        public int LeadingExtraBytesCount
        {
            get
            {
                if (!_leadingExtraBytesCount.HasValue)
                {
                    _leadingExtraBytesCount = GetLeadingBytesCount(Encoding, Buffer, _originalOffset, _originalCount);
                    _hasLeadingExtraBytes = _leadingExtraBytesCount > 0;
                }


                return _leadingExtraBytesCount.Value;
            }
        }

        public int TrailingExtraBytesOffset
        {
            get
            {
                if (!_trailingExtraBytesOffset.HasValue)
                    _trailingExtraBytesOffset = _originalOffset + _originalCount - TrailingExtraBytesCount;

                return _trailingExtraBytesOffset.Value;
            }
        }

        public bool HasTrailingExtraBytes => _hasTrailingExtraBytes;

        public int TrailingExtraBytesCount
        {
            get
            {
                if (!_trailingExtraBytesCount.HasValue)
                {
                    _trailingExtraBytesCount = GetTrailingBytesCount(Encoding, Buffer, _originalOffset + LeadingExtraBytesCount, _originalCount - LeadingExtraBytesCount);
                    _hasTrailingExtraBytes = _trailingExtraBytesCount > 0;
                }

                return _trailingExtraBytesCount.Value;
            }
        }

        private readonly int _originalOffset;
        private readonly int _originalCount;
        private int? _leadingExtraBytesCount;
        private int? _trailingExtraBytesOffset;
        private int? _trailingExtraBytesCount;
        private bool _hasLeadingExtraBytes = false;
        private bool _hasTrailingExtraBytes = false;

        private static int GetLeadingBytesCount(Encoding encoding, byte[] buffer, int offset, int count)
        {
            var result = 0;

            if (encoding.Equals(Encoding.UTF8))
            {
                // Bytes 2, 3, and 4 of a UTF-8 character always have the form 0x10xxxxxx - https://en.wikipedia.org/wiki/UTF-8
                for (var i = offset; i < offset + count && i < offset + 3 && buffer[i] >> 6 == 2; ++i)
                    ++result;
            }

            return result;
        }

        private static int GetTrailingBytesCount(Encoding encoding, byte[] buffer, int offset, int count)
        {
            var result = 0;

            var decoder = encoding.GetDecoder();
            var decodedBuffer = Strings.GetStringBufferFromBytes(decoder, buffer, offset, count);

            var newBufferLength = encoding.GetBytes(decodedBuffer).Length;
            if (newBufferLength < count)
                result = count - newBufferLength;

            return result;
        }
    }
}
