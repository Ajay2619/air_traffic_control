using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ClearToLand.EditorTools
{
    /// <summary>
    /// Minimal ZIP writer (stored entries, no compression, no Zip64) used to package the WebGL build for itch.io.
    /// The bulky WebGL files are already gzip-compressed, so storing them costs almost nothing, and this avoids depending
    /// on System.IO.Compression assemblies that are not referenced by every Unity API compatibility level.
    /// </summary>
    internal static class StoredZipWriter
    {
        private const uint LocalHeaderSignature = 0x04034b50;
        private const uint CentralHeaderSignature = 0x02014b50;
        private const uint EndOfCentralDirectorySignature = 0x06054b50;
        private const ushort ZipVersion = 20;     // 2.0: stored entries, directories in names
        private const ushort Utf8NamesFlag = 0x0800;
        private const ushort MethodStored = 0;

        private struct Entry
        {
            public byte[] Name;
            public uint Crc;
            public uint Size;
            public uint Offset;
            public ushort Time;
            public ushort Date;
        }

        private static uint[] _crcTable;

        /// <summary>
        /// Zips every file under <paramref name="sourceDirectory"/> (paths relative to it, '/' separators) into
        /// <paramref name="zipPath"/>, replacing any existing file. Returns the number of entries written.
        /// </summary>
        public static int CreateFromDirectory(string sourceDirectory, string zipPath)
        {
            string root = Path.GetFullPath(sourceDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);

            string fullZip = Path.GetFullPath(zipPath);
            string tempZip = fullZip + ".tmp";
            string zipFolder = Path.GetDirectoryName(fullZip);
            if (!string.IsNullOrEmpty(zipFolder)) Directory.CreateDirectory(zipFolder);

            string[] files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            if (files.Length > ushort.MaxValue) throw new IOException($"Too many files for a basic zip ({files.Length}).");

            List<Entry> entries = new List<Entry>(files.Length);
            try
            {
                using (FileStream stream = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    foreach (string file in files)
                    {
                        string full = Path.GetFullPath(file);
                        if (string.Equals(full, fullZip, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(full, tempZip, StringComparison.OrdinalIgnoreCase)) continue;

                        byte[] data = File.ReadAllBytes(full);
                        string relative = full.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
                        ushort time;
                        ushort date;
                        ToDosDateTime(File.GetLastWriteTime(full), out time, out date);

                        Entry entry = new Entry
                        {
                            Name = Encoding.UTF8.GetBytes(relative),
                            Crc = Crc32(data),
                            Size = (uint)data.Length,
                            Offset = ToOffset(stream.Position),
                            Time = time,
                            Date = date,
                        };

                        writer.Write(LocalHeaderSignature);
                        writer.Write(ZipVersion);
                        writer.Write(Utf8NamesFlag);
                        writer.Write(MethodStored);
                        writer.Write(entry.Time);
                        writer.Write(entry.Date);
                        writer.Write(entry.Crc);
                        writer.Write(entry.Size); // compressed size
                        writer.Write(entry.Size); // uncompressed size
                        writer.Write((ushort)entry.Name.Length);
                        writer.Write((ushort)0);  // extra field length
                        writer.Write(entry.Name);
                        writer.Write(data);
                        entries.Add(entry);
                    }

                    uint centralStart = ToOffset(stream.Position);
                    foreach (Entry entry in entries)
                    {
                        writer.Write(CentralHeaderSignature);
                        writer.Write(ZipVersion); // version made by (MS-DOS attributes)
                        writer.Write(ZipVersion); // version needed to extract
                        writer.Write(Utf8NamesFlag);
                        writer.Write(MethodStored);
                        writer.Write(entry.Time);
                        writer.Write(entry.Date);
                        writer.Write(entry.Crc);
                        writer.Write(entry.Size);
                        writer.Write(entry.Size);
                        writer.Write((ushort)entry.Name.Length);
                        writer.Write((ushort)0); // extra field length
                        writer.Write((ushort)0); // comment length
                        writer.Write((ushort)0); // disk number start
                        writer.Write((ushort)0); // internal attributes
                        writer.Write(0u);        // external attributes
                        writer.Write(entry.Offset);
                        writer.Write(entry.Name);
                    }
                    uint centralEnd = ToOffset(stream.Position);

                    writer.Write(EndOfCentralDirectorySignature);
                    writer.Write((ushort)0); // this disk
                    writer.Write((ushort)0); // disk with the central directory
                    writer.Write((ushort)entries.Count);
                    writer.Write((ushort)entries.Count);
                    writer.Write(centralEnd - centralStart);
                    writer.Write(centralStart);
                    writer.Write((ushort)0); // comment length
                }

                if (File.Exists(fullZip)) File.Delete(fullZip);
                File.Move(tempZip, fullZip);
            }
            catch
            {
                try
                {
                    if (File.Exists(tempZip)) File.Delete(tempZip);
                }
                catch (Exception)
                {
                    // best effort cleanup; the original exception matters more
                }
                throw;
            }

            return entries.Count;
        }

        private static uint ToOffset(long position)
        {
            if (position > uint.MaxValue) throw new IOException("Zip exceeds 4 GB (Zip64 is not supported).");
            return (uint)position;
        }

        private static void ToDosDateTime(DateTime t, out ushort time, out ushort date)
        {
            int year = Math.Min(Math.Max(t.Year, 1980), 2107);
            time = (ushort)((t.Hour << 11) | (t.Minute << 5) | (t.Second / 2));
            date = (ushort)(((year - 1980) << 9) | (t.Month << 5) | t.Day);
        }

        private static uint Crc32(byte[] data)
        {
            if (_crcTable == null) _crcTable = BuildCrcTable();
            uint[] table = _crcTable;
            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < data.Length; i++)
                crc = table[(crc ^ data[i]) & 0xFFu] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFFu;
        }

        private static uint[] BuildCrcTable()
        {
            uint[] table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                    c = (c & 1u) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                table[n] = c;
            }
            return table;
        }
    }
}
