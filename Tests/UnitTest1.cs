using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests
{
    [TestClass]
    public class ValidatorTests
    {
        [TestMethod]
        public void Compression()
        {
            string output = "test-out.gz";
            var actual = GZipTest.Program.Main(new[] { "compress", "test.mp4", output });
            var expected = 0;

            System.IO.File.Delete(output);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void Decompression()
        {
            string output = "test-out.mp4";
            var actual = GZipTest.Program.Main(new[] { "decompress", "test.gz", output });
            var expected = 0;

            System.IO.File.Delete(output);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void PatternMismatch()
        {
            string output = "test-out.mp4";
            var actual = GZipTest.Program.Main(new[] { "test.gz", output });
            var expected = 1;
            
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void NotGZipExtension()
        {
            string output = "test-out.mp4";
            var actual = GZipTest.Program.Main(new[] { "decompress", "test.mp4", output });
            var expected = 1;

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void NotGZipHeader()
        {
            string output = "test-out.mp4";
            var actual = GZipTest.Program.Main(new[] { "decompress", "test.mp4.gz", output });
            var expected = 1;

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void MissingSourceFile()
        {
            string output = "test-out.gz";
            var actual = GZipTest.Program.Main(new[] { "compress", "test.mp44", output });
            var expected = 1;

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void DestinationAlreadyExist()
        {
            string input = "test.mp4";
            string output = "test.gz";
            var actual = GZipTest.Program.Main(new[] { "compress", input, output });
            var expected = 1;

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void DestinationMustHaveGZExtension()
        {
            string input = "test.gz0";
            string output = "test.mp4";
            var actual = GZipTest.Program.Main(new[] { "decompress", input, output });
            var expected = 1;

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void SourceDestinationMatch()
        {
            string input = "test.mp4";
            var actual = GZipTest.Program.Main(new[] { "compress", input, input });
            var expected = 1;

            Assert.AreEqual(expected, actual);
        }




    }
}
