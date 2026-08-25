namespace LifeLinkLanka.Application.Interfaces;

public interface IFileStorageService
{
    Task<(string path, string publicUrl)> UploadAsync(Stream fileStream, string fileName, string contentType, string bucket);
    Task DeleteAsync(string bucket, string path);
}