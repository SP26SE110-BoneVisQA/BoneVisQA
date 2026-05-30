using Microsoft.AspNetCore.Http;

namespace BoneVisQA.API.Tests.Support;

internal static class TestFormFileFactory
{
    public static IFormFile Zip(string fileName, byte[] content) =>
        new FormFile(new MemoryStream(content), 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/zip",
        };
}
