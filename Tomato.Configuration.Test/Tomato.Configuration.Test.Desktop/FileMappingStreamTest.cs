using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tomato.Configuration.Native;
using System.Linq;
using System.Security.Cryptography;
using System.Diagnostics;

namespace Tomato.Configuration.Test.Desktop
{
    [TestClass]
    public class FileMappingStreamTest
    {
        private readonly string testFileName = "123.db";

        void DeleteTestFile()
        {
            System.IO.File.Delete(testFileName);
        }
        public string GetRandomString(int length)
        {
            byte[] randBuffer = new byte[length];
            RandomNumberGenerator.Create().GetBytes(randBuffer);
            return String.Concat(System.Convert.ToBase64String(randBuffer).Take(length));
        }

        [TestMethod]
        public void TestConstructor()
        {
            using (var file = new FileMappingStream(testFileName, 1024))
            {

            }
        }

        [TestMethod]
        public void TestLength()
        {
            var list = new long[] { 1, 100, 1000, 10000 };
            foreach (var size in list)
            {
                using (var file = new FileMappingStream(testFileName, (ulong)size))
                {
                    Assert.AreEqual(file.Length, size);
                }
            }
            DeleteTestFile();
        }

        [TestMethod]
        public void TestSeekBeginAndPosition()
        {
            var rand = new Random();
            var list = new long[] { 1, 100, 1000, 10000 };
            foreach (var size in list)
            {
                using (var file = new FileMappingStream(testFileName, (ulong)size))
                {
                    var pos = rand.Next(0, (int)size);
                    file.Seek(pos, System.IO.SeekOrigin.Begin);
                    Assert.AreEqual(file.Position, pos);
                }
            }
            DeleteTestFile();
        }

        [TestMethod]
        public void TestSeekCurrentAndPosition()
        {
            var rand = new Random();
            var list = new long[] { 1, 100, 1000, 10000 };
            foreach (var size in list)
            {
                using (var file = new FileMappingStream(testFileName, (ulong)size))
                {
                    var org = rand.Next(0, (int)size);
                    file.Seek(org, System.IO.SeekOrigin.Begin);

                    var pos = rand.Next(-org, (int)size - org);
                    file.Seek(pos, System.IO.SeekOrigin.Current);
                    Assert.AreEqual(file.Position, org + pos);
                }
            }
            DeleteTestFile();
        }

        [TestMethod]
        public void TestSeekEndAndPosition()
        {
            var rand = new Random();
            var list = new long[] { 1, 100, 1000, 10000 };
            foreach (var size in list)
            {
                using (var file = new FileMappingStream(testFileName, (ulong)size))
                {
                    var pos = rand.Next(-(int)size, 0);
                    file.Seek(pos, System.IO.SeekOrigin.End);
                    Assert.AreEqual(file.Position, file.Length + pos);
                }
            }
            DeleteTestFile();
        }

        [TestMethod]
        public void TestRead()
        {
            var rand = new Random();
            for (int i = 0; i < 10; i++)
            {
                var length = rand.Next(1, 10 * 1024 * 1024);
                var randText = GetRandomString(length);

                System.IO.File.WriteAllText(testFileName, randText);
                using (var sr = new System.IO.StreamReader(new FileMappingStream(testFileName, 0, true)))
                {
                    Assert.AreEqual(randText, sr.ReadToEnd());
                }
            }
            DeleteTestFile();
        }

        [TestMethod]
        public void TestSeekRead()
        {
            var rand = new Random();
            for (int i = 0; i < 10; i++)
            {
                var length = rand.Next(1, 10 * 1024 * 1024);
                var randText = GetRandomString(length);

                System.IO.File.WriteAllText(testFileName, randText);
                using (var sr = new System.IO.StreamReader(new FileMappingStream(testFileName, 0, true)))
                {
                    var skip = rand.Next(length);
                    sr.BaseStream.Seek(skip, System.IO.SeekOrigin.Begin);
                    Assert.AreEqual(string.Concat(randText.Skip(skip)), sr.ReadToEnd());
                }
            }
            DeleteTestFile();
        }

        [TestMethod]
        public void TestWrite()
        {
            var rand = new Random();
            for (int i = 0; i < 10; i++)
            {
                var length = rand.Next(1, 10 * 1024 * 1024);
                var randText = GetRandomString(length);

                using (var sw = new System.IO.StreamWriter(new FileMappingStream(testFileName,
                    (ulong)length, false)))
                {
                    sw.Write(randText);
                }
                Assert.AreEqual(randText, System.IO.File.ReadAllText(testFileName));
                DeleteTestFile();
            }
        }

        [TestMethod]
        public void TestSeekWrite()
        {
            var rand = new Random();
            for (int i = 0; i < 10; i++)
            {
                var length = rand.Next(1, 10 * 1024 * 1024);
                var randText = GetRandomString(length);

                var skip = rand.Next(length);
                using (var sw = new System.IO.StreamWriter(new FileMappingStream(testFileName,
                    (ulong)length, false)))
                {
                    sw.Write(string.Concat(Enumerable.Repeat(' ', length)));
                    sw.Flush();
                    sw.BaseStream.Seek(skip, System.IO.SeekOrigin.Begin);
                    sw.Write(randText);
                }
                Assert.AreEqual(string.Concat(Enumerable.Repeat(' ', skip)) + randText,
                    System.IO.File.ReadAllText(testFileName));
                DeleteTestFile();
            }
        }

        [TestMethod]
        [TestCategory("BenchMark")]
        public void BenchMarkReadVsFileStream()
        {
            var stopwatch = new Stopwatch();
            TimeSpan time1 = TimeSpan.Zero, time2 = TimeSpan.Zero;

            var length = 100 * 1024 * 1024;
            var buffer = new byte[length];
            System.IO.File.WriteAllBytes(testFileName, buffer);
            var times = 200;

            // 使用 FileStream
            using (var fs = new System.IO.FileStream(testFileName,
                System.IO.FileMode.Open, System.IO.FileAccess.Read))
            {
                stopwatch.Restart();
                for (int i = 0; i < times; i++)
                {
                    fs.Read(buffer, 0, length);
                    fs.Seek(0, System.IO.SeekOrigin.Begin);
                }
                stopwatch.Stop();
            }
            time1 += stopwatch.Elapsed;

            // 使用 FileMappingStream
            using (var fs = new FileMappingStream(testFileName, 0, true))
            {
                stopwatch.Restart();
                for (int i = 0; i < times; i++)
                {
                    fs.Read(buffer, 0, length);
                    fs.Seek(0, System.IO.SeekOrigin.Begin);
                }
                stopwatch.Stop();
            }
            time2 += stopwatch.Elapsed;

            DeleteTestFile();
            Trace.WriteLine(string.Format("FileStream: {0}, FileMappingStream: {1}", time1, time2));
            Assert.IsTrue(time2 < time1, "Performace is not acceptable." );
        }

        [TestMethod]
        [TestCategory("BenchMark")]
        public void BenchMarkWriteVsFileStream()
        {
            var stopwatch = new Stopwatch();
            TimeSpan time1 = TimeSpan.Zero, time2 = TimeSpan.Zero;

            var length = 100 * 1024 * 1024;
            var buffer = new byte[length];
            var times = 10;

            // 使用 FileStream
            using (var fs = new System.IO.FileStream(testFileName,
                System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.Write))
            {
                stopwatch.Restart();
                for (int i = 0; i < times; i++)
                {
                    fs.Write(buffer, 0, length);
                    fs.Seek(0, System.IO.SeekOrigin.Begin);
                }
                stopwatch.Stop();
            }
            time1 += stopwatch.Elapsed;
            DeleteTestFile();

            // 使用 FileMappingStream
            using (var fs = new FileMappingStream(testFileName, (ulong)length, false))
            {
                stopwatch.Restart();
                for (int i = 0; i < times; i++)
                {
                    fs.Write(buffer, 0, length);
                    fs.Seek(0, System.IO.SeekOrigin.Begin);
                }
                stopwatch.Stop();
            }
            time2 += stopwatch.Elapsed;

            DeleteTestFile();
            Trace.WriteLine(string.Format("FileStream: {0}, FileMappingStream: {1}", time1, time2));
            Assert.IsTrue(time2 < time1, "Performace is not acceptable.");
        }
    }
}
