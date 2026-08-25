using LifeLinkLanka.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Supabase;
using FileOptions = Supabase.Storage.FileOptions;

namespace LifeLinkLanka.Infrastructure.Storage;

public class SupabaseFileStorageService : IFileStorageService
{
    private readonly Client _client;
    private readonly string _supabaseUrl;

    public SupabaseFileStorageService(IConfiguration config)
    {
        _supabaseUrl = config["Supabase:Url"]!;
        _client = new Client(_supabaseUrl, config["Supabase:ServiceRoleKey"],
            new SupabaseOptions { AutoConnectRealtime = false });
    }

    public async Task<(string path, string publicUrl)> UploadAsync(
        Stream fileStream, string fileName, string contentType, string bucket)
    {
        await _client.InitializeAsync();
        var storage = _client.Storage.From(bucket);

        var uniqueName = $"{Guid.NewGuid()}_{fileName}";
        using var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms);

        await storage.Upload(ms.ToArray(), uniqueName,
            new FileOptions { ContentType = contentType, Upsert = false });

        var publicUrl = storage.GetPublicUrl(uniqueName);
        return (uniqueName, publicUrl);
    }

    public async Task DeleteAsync(string bucket, string path)
    {
        await _client.InitializeAsync();
        await _client.Storage.From(bucket).Remove(new List<string> { path });
    }
}