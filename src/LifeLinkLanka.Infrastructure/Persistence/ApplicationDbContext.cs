namespace LifeLinkLanka.Infrastructure.Persistence;

using LifeLinkLanka.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;



public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<DonorProfile> DonorProfiles => Set<DonorProfile>();
    public DbSet<Hospital> Hospitals => Set<Hospital>();
    public DbSet<BloodBank> BloodBanks => Set<BloodBank>();
    public DbSet<BloodRequest> BloodRequests => Set<BloodRequest>();
    public DbSet<DonorMatch> DonorMatches => Set<DonorMatch>();
    public DbSet<DonationRecord> DonationRecords => Set<DonationRecord>();
    public DbSet<UploadedDocument> UploadedDocuments => Set<UploadedDocument>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<DonorProfile>()
            .HasOne(d => d.User)
            .WithOne(u => u.DonorProfile)
            .HasForeignKey<DonorProfile>(d => d.UserId);

        builder.Entity<BloodRequest>()
            .HasOne(r => r.Hospital)
            .WithMany(h => h.BloodRequests)
            .HasForeignKey(r => r.HospitalId);

        builder.Entity<DonorMatch>()
            .HasOne(m => m.BloodRequest)
            .WithMany(r => r.Matches)
            .HasForeignKey(m => m.BloodRequestId);

        // Global soft-delete filter
        builder.Entity<DonorProfile>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Hospital>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<BloodRequest>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<DonationRecord>().HasQueryFilter(d => !d.DonorProfile.IsDeleted);
        builder.Entity<DonorMatch>().HasQueryFilter(m => !m.BloodRequest.IsDeleted);

        builder.Entity<ApplicationUser>().HasIndex(u => u.NicNumber).IsUnique();
    }
}