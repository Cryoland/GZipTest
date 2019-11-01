using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace GZipTest
{
    sealed class TaskManager
    {
        private readonly object rflag = new object();
        private readonly object wflag = new object();
        private volatile bool terminated;
        private volatile bool readFinished;
        private volatile bool processed;
        private Queue<FileChunk> queueToRead = new Queue<FileChunk>();
        private Queue<FileChunk> queueToWrite = new Queue<FileChunk>();
        private const int queueLimit = 5;
        private int readBlockID;
        private volatile int writeBlockID;

        public void AddToRead(byte[] data)
        {
            lock (rflag)
            {
                // ждать осбождения места в очереди, чтобы не переполнять память
                while (!terminated && queueToRead.Count >= queueLimit)
                    Monitor.Wait(rflag);

                if (terminated)
                    throw new Exception("Terminated");

                // наполнение очереди новыми необработанными фрагментами
                queueToRead.Enqueue(new FileChunk(readBlockID++, data));
                #region debug
                ColoredConsole.WriteLine(ConsoleColor.Green, $"chunk#{queueToRead.Last().ID} added to compress in Thread#{Thread.CurrentThread.ManagedThreadId}");
                #endregion
                // просигналить всем ожидающим потокам
                Monitor.PulseAll(rflag);
            }
        }
        public FileChunk DequeueForProcessing()
        {
            lock(rflag)
            {
                // ожидать наполнения очереди необработанными фрагментами
                while (!terminated && !readFinished && queueToRead.Count < 1)
                    Monitor.Wait(rflag);             
                
                var chunk = (queueToRead.Count == 0) ? null : queueToRead.Dequeue();
                #region debug
                if (chunk != null) ColoredConsole.WriteLine(ConsoleColor.Blue, $"chunk#{chunk.ID} released for compressing in Thread#{Thread.CurrentThread.ManagedThreadId}");
                #endregion
                // просигналить потоку наполнения очереди
                Monitor.Pulse(rflag);
                return chunk;
            }
        }
        public void ReadFinished() => readFinished = true;
        public void MainRoutineCompleted() => processed = true;
        public void AddToWrite(FileChunk chunk)
        {
            lock (wflag)
            {
                // ждать осбождения места в очереди, чтобы не переполнять память
                while (!terminated && queueToWrite.Count >= queueLimit)
                    Monitor.Wait(wflag);

                // ждем неуспевающий поток
                if (chunk.ID != writeBlockID)
                    Monitor.Wait(wflag);

                if (terminated)
                    throw new Exception("Terminated");

                // наполнение очереди обработанными фрагментами
                queueToWrite.Enqueue(chunk);
                writeBlockID++;
                #region debug
                ColoredConsole.WriteLine(ConsoleColor.Red, $"chunk#{chunk.ID} added to write");  
                #endregion
                Monitor.Pulse(wflag);
            }
        }
        public FileChunk DequeueForWriting()
        {
            lock (wflag)
            {
                // ожидать наполнения очереди
                while (!terminated && !processed && queueToWrite.Count < 1)
                    Monitor.Wait(wflag);

                var chunk = (queueToWrite.Count == 0) ? null : queueToWrite.Dequeue();
                #region debug
                if (queueToWrite.Count != 0) ColoredConsole.WriteLine(ConsoleColor.DarkYellow, $"chunk#{chunk.ID} released for writing in Thread#{Thread.CurrentThread.ManagedThreadId}");
                #endregion
                Monitor.Pulse(wflag);
                return chunk;
            }
        }

        public void Stop()
        {
            lock (rflag)
            {
                lock (wflag)
                {
                    terminated = true;
                    Monitor.PulseAll(wflag);
                }
                Monitor.PulseAll(rflag);
            }
        }
    }

    static class ColoredConsole
    {       
        public static void WriteLine(ConsoleColor color, params object[] output)
        {
            Console.ForegroundColor = color;
            foreach(object obj in output)
                Console.WriteLine(obj);
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
}
