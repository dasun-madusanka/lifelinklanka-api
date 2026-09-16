using LifeLinkLanka.Domain.Enums;

namespace LifeLinkLanka.Application.Interfaces;

public record DonorMatchCandidate(
    Guid DonorUserId,
    string FullName,
    string District,
    BloodType BloodType,
    bool IsExactTypeMatch,
    bool IsSameDistrict,
    double MatchScore);

public interface IBloodMatchingService
{
    IReadOnlyList<BloodType> GetCompatibleDonorBloodTypes(BloodType recipientType, BloodComponentType component);
    bool IsCompatible(BloodType donorType, BloodType recipientType, BloodComponentType component);
    Task<List<DonorMatchCandidate>> FindMatchingDonorsAsync(Guid bloodRequestId);
}
