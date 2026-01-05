using Microsoft.AspNetCore.Http;
using WorthBoards.Business.Services.Interfaces;

namespace WorthBoards.Api.IntegrationTests.Infrastructure;

public class FakeFileService : IFileService
{
    public const string StoredFileName = "fake-upload.png";

    public Task<string> UploadImage(IFormFile image)
    {
        if (image is null)
        {
            throw new ArgumentNullException(nameof(image));
        }

        return Task.FromResult(StoredFileName);
    }
}
