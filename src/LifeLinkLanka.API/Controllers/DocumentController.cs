using LifeLinkLanka.Application.Interfaces;
using LifeLinkLanka.Domain.Entities;
using LifeLinkLanka.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeLinkLanka.API.Controllers;

[ApiController]
[Route("api/v1/documents")]
[Authorize]
public class DocumentController : ControllerBase
{
    private readonly IFileStorageService _storage;
    private readonly ApplicationDbContext _db;

    public DocumentController(IFileStorageService storage, ApplicationDbContext db)
    {
        _storage = storage;
        _db = db;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(10_000_000)] // 10 MB
    public async Task<IActionResult> Upload(IFormFile file, [FromForm] string documentType)
    {
        if (file.Length == 0) return BadRequest("Empty file.");

        var allowedTypes = new[] { "application/pdf", "image/jpeg", "image/png" };
        if (!allowedTypes.Contains(file.ContentType)) return BadRequest("Unsupported file type.");

        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var bucket = documentType switch
        {
            "NIC" => "identity-documents",
            "MedicalCertificate" => "medical-documents",
            "HospitalLicense" => "hospital-documents",
            _ => "misc-documents"
        };

        await using var stream = file.OpenReadStream();
        var (path, publicUrl) = await _storage.UploadAsync(stream, file.FileName, file.ContentType, bucket);

        var doc = new UploadedDocument
        {
            OwnerUserId = userId,
            DocumentType = documentType,
            SupabaseBucket = bucket,
            StoragePath = path,
            PublicUrl = publicUrl,
            FileName = file.FileName,
            SizeBytes = file.Length
        };
        _db.UploadedDocuments.Add(doc);
        await _db.SaveChangesAsync();

        return Ok(new { doc.Id, doc.PublicUrl });
    }
}