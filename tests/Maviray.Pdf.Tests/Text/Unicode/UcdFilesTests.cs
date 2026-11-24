using System.Security.Cryptography;
using System.Text;

namespace Maviray.Pdf.Tests.Text.Unicode;

public sealed class UcdFilesTests
{
    // The checksums of the UCD 16.0.0 files and the Unicode licence, uncompressed, recorded in the P1.5 plan. A replaced,
    // truncated or re-encoded file fails here before any Unicode test can pass for the wrong reason.
    [Theory]
    [InlineData("UnicodeData.txt", "ff58e5823bd095166564a006e47d111130813dcf8bf234ef79fa51a870edb48f")]
    [InlineData("CompositionExclusions.txt", "89e83cf9cc8bef6c1f8bf77e42cf6f0341dfa42e66261f4dbe9b492e7a23c8ee")]
    [InlineData("DerivedCoreProperties.txt", "39d35161f2954497f69e08bdb9e701493f476a3d30222de20028feda36c1dabd")]
    [InlineData("LineBreak.txt", "e97e4259d0d20fab150b9c7b4b28abfae5cd78ca97e7f4ac6ed20d685d5f4a7c")]
    [InlineData("EastAsianWidth.txt", "43adc76c0686a42cb370764eb8cfe2b2a45b10b855e5572a2db4a0eecce15d5b")]
    [InlineData("emoji-data.txt", "f1365a5173eee18e1f98b240cdc492e84a25f1ce7e0c9d1094eb29c41a22696a")]
    [InlineData("NormalizationTest.txt", "d811971453e7075e1ad56fb1b301eece5aa80757b81f6156e74a1bfb3ae5ceb1")]
    [InlineData("LineBreakTest.txt", "910759a611a479f37df4f535d2e64d7589be3c5ac5f7491cf1fb4fa4cdb211e9")]
    [InlineData("license.txt", "e7a93b009565cfce55919a381437ac4db883e9da2126fa28b91d12732bc53d96")]
    public void FilesAreTheRecordedRelease(string fileName, string sha256)
    {
        var bytes = Encoding.UTF8.GetBytes(UcdFiles.ReadText(fileName));

        Assert.Equal(sha256, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }
}
