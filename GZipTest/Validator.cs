using System;
using System.IO;
using System.Linq;

namespace GZipTest
{
    static class ArgsValidator
    {
        public static void Check(string[] args)
        {
            if (args.Length != 3)
                throw new Exception($"Input error. Follow the pattern: compress(decompress) source destination");

            FileInfo inputFile = new FileInfo(args[1]);
            FileInfo outputFile = new FileInfo(args[2]);

            // информационная избыточность входного файла неизвестна, поэтому необходимо грубо проанализировать, хватит ли места.
            long totalFreeSpace = DriveInfo.GetDrives().Where(d => d.Name.Equals(Path.GetPathRoot(Environment.CurrentDirectory))).ToArray()[0].TotalFreeSpace;

            if (!(args[0].ToLower().Equals($"compress") || args[0].ToLower().Equals($"decompress")))
                throw new Exception($"First argument must be 'compress' or 'decompress'");

            // проверка метки файла
            if (args[0].ToLower().Equals($"decompress"))
            {
                byte[] SignatureGZ = new byte[] { 0x1F, 0x8B, 0x08 };
                using (var stream = new FileStream(inputFile.FullName, FileMode.Open))
                {
                    foreach (byte b in SignatureGZ)
                        if (!b.Equals((byte)stream.ReadByte()))
                            throw new Exception($"Wrong file format (not gzip archive)");
                }
            }

            if (inputFile.Name.Length == 0)
                throw new Exception($"Source file is not specified");

            if (!inputFile.Exists)
                throw new Exception($"Missing source file");

            if (inputFile.Extension.Equals(".gz") && args[0].ToLower().Equals("compress"))
                throw new Exception($"File has already been compressed");

            if ((!inputFile.Extension.Equals(".gz")) && args[0].ToLower().Equals("decompress"))
                throw new Exception($"Source file to be decompressed must have .gz extension");        

            if (outputFile.Name.Length == 0)
                throw new Exception($"Destination file is not specified");

            if (outputFile.Exists && outputFile.Extension.Equals(".gz"))
                throw new Exception($"Destination file already exists");

            if (inputFile.Name.Equals(outputFile.Name))
                throw new Exception($"Source and destination files must be different");           

            if (inputFile.Length > totalFreeSpace)
                throw new Exception($"Free space exhausted. Free space left: {totalFreeSpace} bytes.");
        }
    }
}
