// Copyright (C) Daniel Shelepov.  All rights reserved.
using System;
using System.Collections.Generic;
using System.IO;

namespace Deduper
{
    class App
    {
        public static void Main(string[] args)
        {
            Console.Out.WriteLine("Deduper run starting");

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

                // TODO: verify nesting

                dirs.Add(dir);
            }

            var deduper = new Deduper(Console.Out, Console.Error);

            foreach (var dir in dirs)
            {
                deduper.ProcessDirectory(dir);
            }

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
