namespace LifeLinkLanka.Application.Interfaces;

public interface IDonorEligibilityJob
{
    Task RecalculateAllDonorsAsync();
}