// Copyright (C) Daniel Shelepov.  All rights reserved.
using System.Collections.Generic;
using System.IO;

namespace Deduper
{
    internal class Deduper
    {
        private const double FILESIZE_RATIO_FAR_ENOUGH_TO_REJECT_DUPLICATES = 0.9;

        internal Deduper(TextWriter streamOut, TextWriter streamError)
        {
            this.streamOut = streamOut;
            this.streamError = streamError;

            this.lowHashes = new Dictionary<long, List<string>>();
            this.probableDuplicatesByFirst = new Dictionary<string, List<string>>();
        }

        internal void ProcessDirectory(string dir)
        {
            streamOut.WriteLine("Processing directory {0}", dir);
            foreach (var file in new FileIterator(dir, this.streamOut, this.streamError))
            {
                //streamOut.WriteLine("Processing file {0}", file);

                // ensure low hash for file
                long lowHash = FileHelper.GetLowHash(file);

                // query low hash
                if (!this.lowHashes.ContainsKey(lowHash))
                {
                    // hash unknown
                    var newBucket = new List<string>();
                    this.lowHashes[lowHash] = newBucket;
                }
                else
                {
                    // known hash; deep compare against known hits (records duplicates)
                    this.DeepInspect(file);
                }

                // map low hash
                this.lowHashes[lowHash].Add(file);
            }
        }

        internal IReadOnlyCollection<IReadOnlyCollection<string>> GetProbableDuplicates()
        {
            var duplicates = new List<IReadOnlyCollection<string>>();

            foreach (var duplicate in this.probableDuplicatesByFirst.Values)
            {
                duplicates.Add(duplicate);
            }

            return duplicates;
        }

        internal IReadOnlyCollection<IReadOnlyCollection<string>> GetAbsoluteDuplicates()
        {
            // TODO
            throw new System.NotImplementedException();
        }

        private void DeepInspect(string file)
        {
            long myHash = FileHelper.GetLowHash(file);
            long mySize = FileHelper.GetSize(file);
            var maybeDupes = new List<string>(this.lowHashes[myHash]);

            // 1. remove duplicates that are of obviously different size
            for (int i = maybeDupes.Count - 1; i >= 0; i--)
            {
                var maybeDupe = maybeDupes[i];
                double sizeRatio = (double)mySize / FileHelper.GetSize(maybeDupe);
                if (sizeRatio > 1)
                {
                    sizeRatio = 1 / sizeRatio;
                }

                if (sizeRatio <= Deduper.FILESIZE_RATIO_FAR_ENOUGH_TO_REJECT_DUPLICATES)
                {
                    maybeDupes.RemoveAt(i);
                }
            }

            // 2. remove similar sized files of different hashes
            ushort hashTier = 1;
            while (maybeDupes.Count > 0 && FileHelper.SupportsTieredHash(file, hashTier))
            {
                myHash = FileHelper.GetTieredHash(file, hashTier);
                for (int i = maybeDupes.Count - 1; i >= 0; i--)
                {
                    var maybeDupe = maybeDupes[i];
                    if (FileHelper.SupportsTieredHash(maybeDupe, hashTier))
                    {
                        long maybeDupeHash = FileHelper.GetTieredHash(maybeDupe, hashTier);
                        if (maybeDupeHash != myHash)
                        {
                            maybeDupes.RemoveAt(i);
                        }
                    }
                    // else it's ok, the last checked hash is a good proxy for the whole file
                }
                hashTier++;
            }

            if (maybeDupes.Count > 0)
            {
                // it is theoretically possible that all these remaining duplicates do not consider some of each 
                // other to be duplicates, but because this is inexact anyway, we'll ignore it.  Absolute 
                // duplicates will always succeed in finding each other.
                this.AddAsProbableDuplicate(file, maybeDupes[0]);
            }
        }

        private void AddAsProbableDuplicate(string fileToAdd, string canonicalDuplicate)
        {
            if (!probableDuplicatesByFirst.ContainsKey(canonicalDuplicate))
            {
                // ensure dupe set and key it by canonical
                var dupeSet = new List<string>();
                dupeSet.Add(canonicalDuplicate);

                probableDuplicatesByFirst.Add(canonicalDuplicate, dupeSet);
            }

            probableDuplicatesByFirst[canonicalDuplicate].Add(fileToAdd);
        }

        private Dictionary<long, List<string>> lowHashes;

        // set of duplicate sets, indexed by first duplicate found in each set
        private Dictionary<string, List<string>> probableDuplicatesByFirst;

        private TextWriter streamOut;
        private TextWriter streamError;
    }
}
