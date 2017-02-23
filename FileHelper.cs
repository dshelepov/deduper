// Copyright (C) Daniel Shelepov.  All rights reserved.
using System;
using System.Collections.Generic;
using System.IO;

namespace Deduper
{
    /// <summary>
    ///     Static helper for handling files
    /// </summary>
    internal static class FileHelper
    {
        /// <summary>
        ///     The low hash will only hash this many bytes from the tail end of a file
        /// </summary>
        private const long LOW_HASH_OFFSET = 10240; // 10 kb

        /// <summary>
        ///     The initial part of a file that will never be hashed (unless it's a low hash)
        /// </summary>
        private const float DISALLOWED_HASH_HEADER_RATIO = 0.1f;

        /// <summary>
        ///     Returns the size of a file
        /// </summary>
        /// <param name="filename">file to look up</param>
        /// <returns>number of bytes that this byte's data stream contains</returns>
        internal static long GetSize(string filename)
        {
            var info = new FileInfo(filename);

            return info.Length;
        }

        /// <summary>
        ///     Returns a hash over the smallest part of the file allowed.
        /// </summary>
        /// <param name="filename">File to get a hash for</param>
        /// <returns>64 bit hash</returns>
        internal static long GetLowHash(string filename)
        {
            return FileHelper.GetTieredHash(filename, 0);
        }

        /// <summary>
        ///     Returns a hash over a variable-sized part of the file.
        /// </summary>
        /// <param name="filename">File to get a hash for</param>
        /// <param name="tier">controls how much of this file to hash.  0 == low hash, each extra consecutive level is larger</param>
        /// <returns>64 bit hash</returns>
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

        /// <summary>
        ///     Whether this file supports a hash of the specified tier (the file can be too small for a high-tier hash).
        /// </summary>
        /// <param name="filename">File to get a hash for</param>
        /// <param name="tier">what tier to look up</param>
        /// <returns>true iff file supports hash of this size</returns>
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

        // array of tiers.  Each slot contains a filename->hash map for that tier.  Used for caching
        private static List<Dictionary<string, long>> tieredHashes;
    }
}
