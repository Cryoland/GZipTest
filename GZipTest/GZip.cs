using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace GZipTest
{
    abstract class GZip
    {
        protected static readonly int threadCount = Environment.ProcessorCount;
        protected ManualResetEvent[] flags = new ManualResetEvent[threadCount];
        protected const int chunkLength = 5242880;
        protected TaskManager taskmgr = new TaskManager();
        protected Stopwatch sw = new Stopwatch();
        protected volatile bool interrupted = false;
        protected bool ok = false;
        protected string inputName;
        protected string outputName;
        protected string mainRoutineName;
        protected string initState;
        protected string finalState;

        public GZip() { }
        public GZip(string input, string output)
        {
            inputName = input;
            outputName = output;
        }
        public void Interrupt()
        {
            interrupted = true;
            flags.ToList().ForEach(flag => flag.Set());
        }
        public int State() => !interrupted && ok ? 0 : 1;
        public void Run()
        {
            Console.WriteLine($"{mainRoutineName}..");
            sw.Start();

            new Thread(Read) { Name = $"{initState}DataReadThread" }.Start();

            for (int i = 0; i < threadCount; i++)
            {
                flags[i] = new ManualResetEvent(false);
                new Thread(MainRoutine) { Name = $"{mainRoutineName}DataThread#{i}" }.Start(flags[i]);
            }

            new Thread(() => {
                WaitHandle.WaitAll(flags);
                taskmgr.MainRoutineCompleted();
            }){ Name = $"ObserverThread" }.Start();

            var writeThread = new Thread(Write) { Name = $"{finalState}DataWriteThread" };
            writeThread.Start();
            writeThread.Join();
            sw.Stop();

            if (!interrupted)
            {
                Console.WriteLine($"{mainRoutineName} routine accomplished.\n" +
                    $"CPU threads utilized: {threadCount}.\n" +
                    $"Time elapsed: " + string.Format("{0:00}:{1:00}:{2:00}.{3:00}", sw.Elapsed.Hours, sw.Elapsed.Minutes, sw.Elapsed.Seconds, sw.Elapsed.Milliseconds / 10));
                ok = true;
            }
            else
            {
                Console.WriteLine("Interrupted");
                File.Delete(outputName);
            }
        }
        abstract protected void Read();
        abstract protected void MainRoutine(object n);
        private void Write()
        {
            try
            {
                using (var fstream = new FileStream(outputName, FileMode.Append))
                {
                    while (!interrupted)
                    {
                        var chunk = taskmgr.DequeueForWriting();
                        if (chunk == null)
                            return;
                        fstream.Write(chunk.Data, 0, chunk.Data.Length);
                    }
                }
                taskmgr.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error(Write): {ex.Message}.");
                interrupted = true;
            }
        }
    }

    class GZipCompressor : GZip
    {
        public GZipCompressor(string input, string output) : base(input, output)
        {
            mainRoutineName = "Compression";
            initState = "Raw";
            finalState = "Compressed";
        }        
        override protected void Read()
        {
            try
            {
                using (var fs = new FileStream(inputName, FileMode.Open))
                {
                    int selection = 0;
                    byte[] data;

                    while (!interrupted && fs.Position < fs.Length)
                    {
                        selection = (fs.Length - fs.Position <= chunkLength) ? (int)(fs.Length - fs.Position) : chunkLength;
                        data = new byte[selection];
                        fs.Read(data, 0, selection);
                        taskmgr.AddToRead(data);
                    }
                }
                taskmgr.ReadFinished();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error(Read): {ex.Message}.");
                interrupted = true;
            }
        }
        override protected void MainRoutine(object n)
        {
            try
            {
                while(!interrupted)
                {
                    var chunk = taskmgr.DequeueForProcessing();
                    if (chunk == null)
                    {
                        (n as ManualResetEvent).Set();
                        return;
                    }
                    using (var mStream = new MemoryStream())
                    {
                        using (var gzStream = new GZipStream(mStream, CompressionMode.Compress))                            
                            gzStream.Write(chunk.Data, 0, chunk.Data.Length);

                        var newChunk = new FileChunk(chunk.ID, mStream.ToArray());
                        BitConverter.GetBytes(newChunk.Data.Length).CopyTo(newChunk.Data, 4);

                        taskmgr.AddToWrite(newChunk);                        
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error({mainRoutineName}): {ex.Message}.");
                interrupted = true;
            }
        }
    }

    class GZipDecompressor : GZip
    {
        public GZipDecompressor(string input, string output) : base(input, output)
        {
            mainRoutineName = "Decompression";
            initState = "Compressed";
            finalState = "Decompressed";
        }
        override protected void Read()
        {
            try
            {
                using (var fs = new FileStream(inputName, FileMode.Open))
                {
                    int selection = 0;
                    byte[] data;                 
                    
                    while (!interrupted && fs.Position < fs.Length)
                    {
                        var header = new byte[8];
                        fs.Read(header, 0, header.Length);
                        
                        selection = BitConverter.ToInt32(header, 4);
                        data = new byte[selection];
                        header.CopyTo(data, 0);
                        fs.Read(data, header.Length, selection - header.Length);

                        taskmgr.AddToRead(data);
                    }
                }
                taskmgr.ReadFinished();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error(Read): {ex.Message}.");
                interrupted = true;
            }
        }
        override protected void MainRoutine(object n)
        {
            try
            {
                while (!interrupted)
                {
                    var chunk = taskmgr.DequeueForProcessing();
                    if (chunk == null)
                    {
                        (n as ManualResetEvent).Set();
                        return;
                    }
                    using (var mStream = new MemoryStream(chunk.Data))
                    {
                        using (var gzStream = new GZipStream(mStream, CompressionMode.Decompress))
                        {
                            using (var wStream = new MemoryStream())
                            {
                                gzStream.CopyTo(wStream);
                                taskmgr.AddToWrite(new FileChunk(chunk.ID, wStream.ToArray()));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error({mainRoutineName}): {ex.Message}.");
                interrupted = true;
            }
        }
    }
}