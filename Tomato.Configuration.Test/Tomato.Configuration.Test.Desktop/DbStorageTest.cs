using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tomato.Configuration.Test.Desktop
{
    [TestClass]
    public class DbStorageTest
    {
        [TestMethod]
        public void TestConstructor()
        {
            using (new DbStorage("123.db"))
            {

            }
        }
    }
}
