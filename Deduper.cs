// Copyright (C) Daniel Shelepov.  All rights reserved.
using System;
using System.Collections.Generic;
using System.IO;

namespace Deduper
{
    /// <summary>
    ///     Deduplication worker.
    /// </summary>
    internal class Deduper
    {
        /// <summary>
        ///     If the size ratio of two files is wider than this, they will never be considered duplicates
        /// </summary>
        private const double FILESIZE_RATIO_FAR_ENOUGH_TO_REJECT_DUPLICATES = 0.9;

        /// <summary>
        ///     Will truncate filenames longer than this when printing
        /// </summary>
        private const int MAX_OUTPUT_FILE_LABEL_LENGTH = 40;

        /// <summary>
        ///     Constructor
        /// </summary>
        /// <param name="streamOut">output stream</param>
        /// <param name="streamError">error stream</param>
        internal Deduper(TextWriter streamOut, TextWriter streamError)
        {
            this.streamOut = streamOut;
            this.streamError = streamError;

            this.fileHashes = new Dictionary<long, List<string>>();
            this.probableDuplicatesByFirst = new Dictionary<string, List<string>>();
        }

        /// <summary>
        ///     Scans through a subtree rooted at a directory
        /// </summary>
        /// <param name="dir">path to scan.  Must be valid</param>
        internal void ProcessDirectory(string dir)
        {
            BeginReportDirectory(dir);

            foreach (var file in new FileIterator(dir, this.streamOut, this.streamError))
            {
                ReportFile(file);

                try
                {
                    // ensure low hash for file
                    long lowHash = FileHelper.GetLowHash(file);

                    // query low hash
                    if (!this.fileHashes.ContainsKey(lowHash))
                    {
                        // hash unknown
                        var newBucket = new List<string>();
                        this.fileHashes[lowHash] = newBucket;
                    }
                    else
                    {
                        // known hash; deep compare against known hits (records duplicates)
                        this.DeepInspect(file);
                    }

                    // map low hash
                    this.fileHashes[lowHash].Add(file);
                }
                catch (System.Security.SecurityException)
                {
                    this.streamError.WriteLine(string.Format("E005: cannot access file {0}", file));
                }
                catch (UnauthorizedAccessException)
                {
                    this.streamError.WriteLine(string.Format("E006: cannot access file {0}", file));
                }
                catch (IOException)
                {
                    this.streamError.WriteLine(string.Format("E007: cannot access file {0}", file));
                }
            }

            EndReportDirectory();
        }

        /// <summary>
        ///     Reports probable (likely) duplicates based on the directories scanned so far
        /// </summary>
        /// <returns>Set of sets of path names.  Each minor set represents a set of duplicates.</returns>
        internal IReadOnlyCollection<IReadOnlyCollection<string>> GetProbableDuplicates()
        {
            var duplicates = new List<IReadOnlyCollection<string>>();

            foreach (var duplicate in this.probableDuplicatesByFirst.Values)
            {
                duplicates.Add(duplicate);
            }

            return duplicates;
        }

        /// <summary>
        ///     Reports byte-for-byte duplicates based on the directories scanned so far
        /// </summary>
        /// <returns>Set of sets of path names.  Each minor set represents a set of duplicates.</returns>
        internal IReadOnlyCollection<IReadOnlyCollection<string>> GetAbsoluteDuplicates()
        {
            // TODO
            throw new NotImplementedException();
        }

        private void DeepInspect(string file)
        {
            long myHash = FileHelper.GetLowHash(file);
            long mySize = FileHelper.GetSize(file);
            var maybeDupes = new List<string>(this.fileHashes[myHash]);

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

        private void BeginReportDirectory(string dir)
        {
            this.fileCounter = 0;
            this.streamOut.Write("Processing directory {0}...", dir);
        }

        private void EndReportDirectory()
        {
            this.RewindReport();
            this.streamOut.WriteLine("done");
        }

        private void ReportFile(string file)
        {
            this.fileCounter++;

            var fileLabel = file;
            if (fileLabel.Length > Deduper.MAX_OUTPUT_FILE_LABEL_LENGTH)
            {
                fileLabel = string.Format("..." + file.Substring(file.Length - Deduper.MAX_OUTPUT_FILE_LABEL_LENGTH + 3));
            }
            var toOutput = string.Format("{0}    ({1}) ", this.fileCounter, fileLabel);

            this.RewindReport();

            this.lastReportCharCount = (uint)toOutput.Length;
            this.streamOut.Write(toOutput);
        }

        private void RewindReport()
        {
            uint charsToRewind = this.lastReportCharCount;

            while (charsToRewind-- > 0)
            {
                this.streamOut.Write('\b');
            }

            charsToRewind = this.lastReportCharCount;
            while (charsToRewind-- > 0)
            {
                this.streamOut.Write(' ');
            }

            charsToRewind = this.lastReportCharCount;
            while (charsToRewind-- > 0)
            {
                this.streamOut.Write('\b');
            }
        }

        // stores hashes of all scanned files
        private Dictionary<long, List<string>> fileHashes;

        // set of duplicate sets, indexed by first duplicate found in each set
        private Dictionary<string, List<string>> probableDuplicatesByFirst;

        // progress reporting
        private uint fileCounter;           // # of files scanned so far in this directory
        private uint lastReportCharCount;   // # of chars of last printed message -- for rewinding the cursor

        // outputting
        private TextWriter streamOut;
        private TextWriter streamError;
    }
}
