using LifeLinkLanka.Domain.Enums;

namespace LifeLinkLanka.Application.DTOs.Inventory;

public record BloodInventoryDto(
    Guid Id,
    Guid BloodBankId,
    string BloodBankName,
    string District,
    BloodType BloodType,
    BloodComponentType ComponentType,
    int UnitsAvailable,
    string StorageLocation,
    string BatchNumber,
    DateTime ExpiryDateUtc,
    string Status);

public record AddInventoryDto(
    Guid BloodBankId,
    BloodType BloodType,
    BloodComponentType ComponentType,
    int Units,
    string StorageLocation,
    string BatchNumber,
    DateTime ExpiryDateUtc);

public record UpdateStockDto(
    int UnitsDelta, // positive to add, negative to issue/deduct
    string Reason, // "Donation Intake", "Transfusion Issued", "Expired/Discarded", "Inter-Bank Transfer"
    string? Notes);

public record BloodStockSummaryDto(
    BloodType BloodType,
    int WholeBloodUnits,
    int PackedRbcUnits,
    int PlateletsUnits,
    int PlasmaUnits,
    int TotalUnits,
    string StatusAlert); // "Adequate", "Low", "Critical"
