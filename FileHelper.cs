// Copyright (C) Daniel Shelepov.  All rights reserved.
using System;
using System.Collections.Generic;
using System.IO;

namespace Deduper
{
    internal static class FileHelper
    {
        private const long LOW_HASH_OFFSET = 10240; // 10 kb
        private const float DISALLOWED_HASH_HEADER_RATIO = 0.1f;

        internal static long GetSize(string filename)
        {
            var info = new FileInfo(filename);

            return info.Length;
        }

        internal static long GetLowHash(string filename)
        {
            return FileHelper.GetTieredHash(filename, 0);
        }

        internal static long GetTieredHash(string filename, ushort tier)
        {
            FileHelper.EnsureTier(tier);

            if (!tieredHashes[tier].ContainsKey(filename))
            {
                long tieredBackOffset = FileHelper.GetTieredOffset(tier);

                long size = FileHelper.GetSize(filename);
                long offset = size >= tieredBackOffset ? size - tieredBackOffset : 0;

                long hash = FileHelper.CalculateHash(filename, offset);

                tieredHashes[tier].Add(filename, hash);
            }

            return tieredHashes[tier][filename];
        }

        internal static bool SupportsTieredHash(string filename, ushort tier)
        {
            long size = FileHelper.GetSize(filename);
            long offset = FileHelper.GetTieredOffset(tier);

            return size * (1 - FileHelper.DISALLOWED_HASH_HEADER_RATIO) >= offset;
        }

        private static long CalculateHash(string filename, long offset)
        {
            var hasher = new System.Data.HashFunction.MurmurHash3(128);

            var stream = FileHelper.GetStreamAtOffset(filename, offset);

            var rawHash = hasher.ComputeHash(stream);

            return BitConverter.ToInt64(rawHash, 0) ^ BitConverter.ToInt64(rawHash, 8);
        }

        private static void EnsureTier(uint tier)
        {
            for (int i = FileHelper.tieredHashes.Count; i <= tier; i++)
            {
                FileHelper.tieredHashes.Add(new Dictionary<string, long>());
            }
        }

        private static long GetTieredOffset(ushort tier)
        {
            long offset = LOW_HASH_OFFSET;
            while (tier-- > 0)
            {
                offset <<= 1;
            }

            return offset;
        }

        private static Stream GetStreamAtOffset(string filename, long offset)
        {
            var stream = new FileStream(filename, FileMode.Open, FileAccess.Read);

            stream.Seek(offset, SeekOrigin.Begin);

            return stream;
        }

        static FileHelper()
        {
            FileHelper.tieredHashes = new List<Dictionary<string, long>>();
            FileHelper.tieredHashes.Add(new Dictionary<string, long>());
        }

        private static List<Dictionary<string, long>> tieredHashes;
    }
}
