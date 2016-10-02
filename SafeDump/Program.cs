using Ionic.Zip;
using System;
using System.IO;
using System.Linq;

namespace SafeDump
{
    internal class Program
    {
        private static void ExitMsg()
        {
            Console.WriteLine("Press any key to exit...");

            Console.ReadKey(true);
        }

        private static void Main(string[] args)
        {
            if (args.Length != 1 || !Directory.Exists(args[0]))
            {
                Console.WriteLine("Drag and drop the directory containing VPKs and/or MaiDumps here.");

                ExitMsg();

                return;
            }

            Console.WriteLine("Processing directory. Please wait...");

            string target = args[0];

            ProcessEBOOTs(target, "eboot.bin");

            ProcessEBOOTs(target, "eboot_origin.bin");

            ProcessVPKs(target, "*.vpk");

            ProcessVPKs(target, "*.zip");

            ProcessDoubleRepack(target);

            Console.WriteLine("All tasks have been completed." + Environment.NewLine);

            ExitMsg();
        }

        private static void ProcessDoubleRepack(string target)
        {
            foreach (var zip in Directory.EnumerateFiles(target, "*.zip", SearchOption.AllDirectories))
                using (var zf = new ZipFile(zip))
                using (var ms = new MemoryStream())
                {
                    var vpkEntry = zf.SelectEntries("*.vpk").FirstOrDefault();

                    if (vpkEntry == null)
                        return;

                    vpkEntry.Extract(ms);

                    byte[] newZIP = ProcessZippedVPK(ms.ToArray(), zip);

                    zf.UpdateEntry(vpkEntry.FileName, newZIP);

                    zf.Save();
                }
        }

        private static void ProcessEBOOTs(string target, string targetEboot)
        {
            foreach (var eboot in Directory.EnumerateFiles(target, targetEboot, SearchOption.AllDirectories))
                using (var fs = File.Open(eboot, FileMode.Open, FileAccess.ReadWrite))
                {
                    fs.Position = 0x80;

                    if (fs.ReadByte() == 1)
                    {
                        fs.Position--;

                        fs.WriteByte(2);

                        Console.WriteLine(eboot + " was unsafe. It has been toggled.");
                    }
                }
        }

        private static void ProcessVPKs(string target, string filter)
        {
            foreach (var file in Directory.EnumerateFiles(target, filter, SearchOption.AllDirectories))
                using (var zf = new ZipFile(file))
                using (var ms = new MemoryStream())
                {
                    var ebootEntry = zf.SelectEntries("eboot.bin").FirstOrDefault();
                    var ebootoriginEntry = zf.SelectEntries("eboot_origin.bin").FirstOrDefault();

                    ZipEntry[] entries = { ebootEntry, ebootoriginEntry };

                    foreach (var entry in entries)
                    {
                        if (entry == null)
                            continue;

                        ms.SetLength(0);

                        entry.Extract(ms);

                        ms.Position = 0x80;

                        if (ms.ReadByte() == 1)
                        {
                            ms.Position--;

                            ms.WriteByte(2);

                            Console.WriteLine(file + " was unsafe. The EBOOT.BIN or EBOOT_ORIGIN.BIN contained within has been toggled.");
                        }

                        ms.Position = 0;

                        zf.UpdateEntry(entry.FileName, ms);
                    }

                    zf.Save();
                }
        }

        private static byte[] ProcessZippedVPK(byte[] vpk, string sourceFile)
        {
            using (var vpkMS = new MemoryStream(vpk))
            using (var vpkOutMS = new MemoryStream())
            using (var zis = new ZipInputStream(vpkMS))
            using (var zos = new ZipOutputStream(vpkOutMS))
            using (var ms = new MemoryStream())
            {
                ZipEntry entry;

                while ((entry = zis.GetNextEntry()) != null)
                {
                    ms.SetLength(0);

                    byte[] buffer = new byte[4096];

                    int readBytes;

                    while ((readBytes = zis.Read(buffer, 0, buffer.Length)) > 0)
                        ms.Write(buffer, 0, readBytes);

                    if (entry.FileName.EndsWith("eboot.bin", StringComparison.InvariantCultureIgnoreCase) || entry.FileName.EndsWith("eboot_origin.bin", StringComparison.InvariantCultureIgnoreCase))
                    {
                        ms.Position = 0x80;

                        if (ms.ReadByte() == 1)
                        {
                            ms.Position--;

                            ms.WriteByte(2);

                            Console.WriteLine(sourceFile + " was unsafe. The EBOOT.BIN or EBOOT_ORIGIN.BIN contained within has been toggled.");
                        }
                    }

                    byte[] file = ms.ToArray();

                    zos.PutNextEntry(entry.FileName);

                    // Make sure it's not a directory
                    if (file.Length > 0)
                        zos.Write(file, 0, file.Length);
                }

                return vpkOutMS.ToArray();
            }
        }
    }
}
