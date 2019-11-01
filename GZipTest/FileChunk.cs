using System;

namespace GZipTest
{
    class FileChunk
    {
        public int ID { get; }
        public byte[] Data { get; }
        public FileChunk(int id, byte[] fragment)
        {
            ID = id;
            Data = fragment;
        }
    }
}
