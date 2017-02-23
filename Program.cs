// Copyright (C) Daniel Shelepov.  All rights reserved.
using System;
using System.Collections.Generic;
using System.IO;

namespace Deduper
{
    /// <summary>
    ///     Main executable
    /// </summary>
    class App
    {
        /// <summary>
        ///     Main entry point
        /// </summary>
        /// <param name="args">console args</param>
        public static void Main(string[] args)
        {
            Console.Out.WriteLine("Deduper run starting");

            // validate args as paths
            var dirs = new HashSet<string>();
            foreach (var arg in args)
            {
                var dir = Path.GetFullPath(arg);
                if (!Directory.Exists(dir))
                {
                    Console.Error.WriteLine(string.Format("W001: Directory {0} doesn't exist; ignoring", arg));
                    continue;
                }

                if (dirs.Contains(dir))
                {
                    Console.Error.WriteLine(string.Format("W002: Directory {0} already submitted; ignoring duplicate", arg));
                    continue;
                }

                var pendingDeletes = new List<string>();
                foreach (var knownDir in dirs)
                {
                    if (dir.StartsWith(knownDir))
                    {
                        Console.Error.WriteLine(string.Format("W003: Directory {0} is a descendant of {1} ; ignoring", arg, knownDir));
                        continue;
                    }

                    if (knownDir.StartsWith(dir))
                    {
                        Console.Error.WriteLine(string.Format("W004: Directory {1} is a descendant of {0} ; ignoring", arg, knownDir));
                        pendingDeletes.Add(knownDir);
                    }
                }

                foreach (var pending in pendingDeletes)
                {
                    dirs.Remove(pending);
                }

                dirs.Add(dir);
            }

            // build map of duplicates
            var deduper = new Deduper(Console.Out, Console.Error);
            foreach (var dir in dirs)
            {
                deduper.ProcessDirectory(dir);
            }

            // report results
            var probableDuplicates = deduper.GetProbableDuplicates();

            Console.Out.WriteLine("Probable duplicates: ");
            foreach (var set in probableDuplicates)
            {
                Console.Out.WriteLine("  Begin set: ");
                foreach (var member in set)
                {
                    Console.Out.WriteLine(String.Format("\t{0}", member));
                }
                Console.Out.WriteLine("  End set: ");
            }

            Console.Out.WriteLine("Deduper run finished");
        }
    }
}
