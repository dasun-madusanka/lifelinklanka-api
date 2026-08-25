namespace LifeLinkLanka.Domain.Constants;

public static class Roles
{
    public const string Admin = "Admin";
    public const string BloodBank = "BloodBank";
    public const string HospitalStaff = "HospitalStaff";
    public const string Donor = "Donor";
    public const string EmergencyCoordinator = "EmergencyCoordinator";

    public static readonly string[] All =
    {
        Admin, BloodBank, HospitalStaff, Donor, EmergencyCoordinator
    };
}