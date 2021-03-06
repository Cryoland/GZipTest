﻿using System;
using System.Threading;

namespace GZipTest
{
    public class Program
    {
        static GZip gzip;
        public static int Main(string[] args)
        {
            //args = new string[3];
            //args[0] = @"compress";
            //args[1] = @"2.txt";
            //args[2] = @"2.gz";

            using (var mutex = new Mutex(false, "GZipTest|Cryoland"))
            {
                if (!mutex.WaitOne(TimeSpan.FromSeconds(3), false))
                {
                    Console.WriteLine("Another application instance is running. Terminating..");
                    Thread.Sleep(1000);
                    return 1;
                }

                Console.CancelKeyPress += new ConsoleCancelEventHandler((s, e) =>
                {
                    if (e.SpecialKey == ConsoleSpecialKey.ControlC)
                    {
                        Console.WriteLine("\nInterruption..");
                        e.Cancel = true;
                        gzip?.Interrupt();
                    }
                });

                try
                {
                    ArgsValidator.Check(args);

                    if (args[0].Equals($"compress"))
                        gzip = new GZipCompressor(args[1], args[2]);

                    if (args[0].Equals($"decompress"))
                        gzip = new GZipDecompressor(args[1], args[2]);

                    gzip.Run();
                    return gzip.State();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error: {e.Message}");
                    return 1;
                }
            }          
        }
    }
}
