using Microsoft.AspNetCore.Http;
using WorthBoards.Business.Services.Interfaces;

namespace WorthBoards.IntegrationTests.Infrastructure;

public sealed class FakeFileService : IFileService
{
    public Task<string> UploadImage(IFormFile image)
        => Task.FromResult("fake-image.jpg");
}
