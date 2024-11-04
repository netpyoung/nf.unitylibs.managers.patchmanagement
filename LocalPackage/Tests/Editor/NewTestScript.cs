using NF.UnityLibs.Managers.Patcher.Common;
using NUnit.Framework;

namespace NF.UnityLibs.Managers.Patcher.EditorTests
{
    public sealed class NewTestScript
    {
        [TestCase("", 0x00000000u)] // 빈 문자열
        [TestCase("hello", 0x3610A686u)] // "hello"에 대한 CRC 값
        [TestCase("123456789", 0xCBF43926u)] // "123456789"에 대한 CRC 값
        public void TestCrc32WithKnownValues(string input, uint expectedCrc)
        {
            uint crcResult = CRC32.Compute(input);
            Assert.AreEqual(expectedCrc, crcResult, $"Failed for input: {input}");
        }

        [TestCase("", "12345")]
        [TestCase("hello", "hellp")]
        public void TestCrc32WithModifiedData(string a, string b)
        {
            uint crcA = CRC32.Compute(a);
            uint crcB = CRC32.Compute(b);

            Assert.AreNotEqual(crcA, crcB, "CRC32 should be different for modified data.");
        }

        [Test]
        public void TestPatchVersionParse()
        {
            string json = @"{ ""A1"": 1, ""B1"": 2 }";
            Assert.True(PatchVersion.TryFromJson(json, out PatchVersion x));

            Assert.True(x.TryGetValue("A1", out int a1));
            Assert.AreEqual(1, a1);

            Assert.True(x.TryGetValue("B1", out int b1));
            Assert.AreEqual(2, b1);
        }
    }
}