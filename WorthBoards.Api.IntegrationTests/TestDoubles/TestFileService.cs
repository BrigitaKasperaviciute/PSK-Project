using Microsoft.AspNetCore.Http;
using WorthBoards.Business.Services.Interfaces;

namespace WorthBoards.Api.IntegrationTests.TestDoubles;

public class TestFileService : IFileService
{
    public Task<string> UploadImage(IFormFile image)
    {
        return Task.FromResult($"images/{image.FileName}");
    }
}
