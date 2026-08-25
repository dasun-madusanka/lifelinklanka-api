using LifeLinkLanka.Domain.Common;

namespace LifeLinkLanka.Domain.Entities;

public class UploadedDocument : BaseEntity
{
    public Guid OwnerUserId { get; set; }
    public string DocumentType { get; set; } = default!; // "NIC", "MedicalCertificate", "HospitalLicense"
    public string SupabaseBucket { get; set; } = default!;
    public string StoragePath { get; set; } = default!;
    public string PublicUrl { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public long SizeBytes { get; set; }
}