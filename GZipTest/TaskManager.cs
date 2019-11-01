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
        private volatile bool terminated = false;
        private volatile bool readFinished = false;
        private volatile bool processed = false;
        private Queue<FileChunk> queueToRead = new Queue<FileChunk>();
        private Queue<FileChunk> queueToWrite = new Queue<FileChunk>();
        private const int queueLimit = 5;
        private volatile int readBlockID = 0;
        private volatile int writeBlockID = 0;

        public void AddToRead(byte[] data)
        {
            lock (rflag)
            {
                // ждать освобождения места в очереди, чтобы не переполнять память
                while (!terminated && queueToRead.Count >= queueLimit)
                    Monitor.Wait(rflag);

                if (terminated)
                    throw new Exception("Terminated");

                // наполнение очереди новыми необработанными фрагментами
                queueToRead.Enqueue(new FileChunk(readBlockID++, data));

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

                // просигналить всем ожидающим потокам
                Monitor.PulseAll(rflag);
                return chunk;
            }
        }
        public void ReadFinished() => readFinished = true;
        public void ProcessingCompleted() => processed = true;
        public void AddToWrite(FileChunk chunk)
        {
            lock (wflag)
            {
                // ждать неуспевающий поток
                while (!terminated && chunk.ID != writeBlockID)
                    Monitor.Wait(wflag);

                // ждать освобождения места в очереди, чтобы не переполнять память
                while (!terminated && queueToWrite.Count >= queueLimit)
                    Monitor.Wait(wflag);

                if (terminated)
                    throw new Exception("Terminated");

                // наполнение очереди обработанными фрагментами
                queueToWrite.Enqueue(chunk);
                writeBlockID++;

                Monitor.PulseAll(wflag);
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

                Monitor.PulseAll(wflag);
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
}
