// Copyright (C) Daniel Shelepov.  All rights reserved.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security;

namespace Deduper
{
    internal class FileIterator : IEnumerable<string>
    {
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public IEnumerator<string> GetEnumerator()
        {
            var stack = new Stack<DirectoryInfo>();

            DirectoryInfo root = null;
            try
            {
                root = new DirectoryInfo(this.rootPath);
            }
            catch (SecurityException)
            {
                this.streamError.WriteLine(string.Format("E001: cannot access directory {0}", this.rootPath));
                yield break;
            }

            stack.Push(root);
            while (stack.Count > 0)
            {
                var dir = stack.Pop();

                IEnumerable<FileInfo> files = new FileInfo[0];
                try
                {
                    files = dir.EnumerateFiles();
                }
                catch (SecurityException)
                {
                    this.streamError.WriteLine(string.Format("E002: cannot enumerate directory {0}", dir.FullName));
                }

                foreach (var file in files)
                {
                    yield return file.FullName;
                }

                IEnumerable<DirectoryInfo> subdirs = new DirectoryInfo[0];
                try
                {
                    subdirs = dir.GetDirectories();
                }
                catch (SecurityException)
                {
                    this.streamError.WriteLine(string.Format("E003: cannot enumerate directory {0}", dir.FullName));
                }
                catch (System.UnauthorizedAccessException)
                {
                    this.streamError.WriteLine(string.Format("E004: cannot access directory {0}", dir.FullName));
                }

                foreach (var subdir in subdirs)
                {
                    stack.Push(subdir);
                }
            }
        }

        internal FileIterator(string dir, TextWriter streamOut, TextWriter streamError)
        {
            this.rootPath = dir;
            this.streamOut = streamOut;
            this.streamError = streamError;
        }

        private string rootPath;
        private TextWriter streamOut;
        private TextWriter streamError;
    }
}
