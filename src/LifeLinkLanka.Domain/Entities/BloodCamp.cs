using LifeLinkLanka.Domain.Common;
using LifeLinkLanka.Domain.Enums;

namespace LifeLinkLanka.Domain.Entities;

public class BloodCamp : BaseEntity
{
    public string Title { get; set; } = default!;
    public string OrganizerName { get; set; } = default!;
    public string District { get; set; } = default!;
    public string VenueAddress { get; set; } = default!;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime StartDateUtc { get; set; }
    public DateTime EndDateUtc { get; set; }
    public int TargetUnits { get; set; }
    public int UnitsCollected { get; set; } = 0;
    public string ContactPhone { get; set; } = default!;
    public string? SpecialInstructions { get; set; }
    public CampStatus Status { get; set; } = CampStatus.Upcoming;

    public ICollection<CampRegistration> Registrations { get; set; } = new List<CampRegistration>();
}
