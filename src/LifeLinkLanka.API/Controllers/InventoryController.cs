using LifeLinkLanka.Application.DTOs.Inventory;
using LifeLinkLanka.Application.Interfaces;
using LifeLinkLanka.Domain.Constants;
using LifeLinkLanka.Domain.Entities;
using LifeLinkLanka.Domain.Enums;
using LifeLinkLanka.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LifeLinkLanka.API.Extensions;

namespace LifeLinkLanka.API.Controllers;

[ApiController]
[Route("api/v1/inventory")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _auditService;

    public InventoryController(ApplicationDbContext db, IAuditService auditService)
    {
        _db = db;
        _auditService = auditService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetInventory(
        [FromQuery] string? district,
        [FromQuery] BloodType? bloodType,
        [FromQuery] BloodComponentType? componentType)
    {
        var query = _db.BloodInventories
            .Include(i => i.BloodBank)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(district))
            query = query.Where(i => i.BloodBank.District == district);

        if (bloodType.HasValue)
            query = query.Where(i => i.BloodType == bloodType.Value);

        if (componentType.HasValue)
            query = query.Where(i => i.ComponentType == componentType.Value);

        var items = await query
            .OrderBy(i => i.BloodType)
            .Select(i => new BloodInventoryDto(
                i.Id,
                i.BloodBankId,
                i.BloodBank.Name,
                i.BloodBank.District,
                i.BloodType,
                i.ComponentType,
                i.UnitsAvailable,
                i.StorageLocation,
                i.BatchNumber,
                i.ExpiryDateUtc,
                i.UnitsAvailable <= 3 ? "Critical" : (i.UnitsAvailable <= 10 ? "Low" : "Adequate")
            ))
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("summary")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStockSummary()
    {
        var inventories = await _db.BloodInventories.ToListAsync();

        var summaries = Enum.GetValues<BloodType>().Select(type =>
        {
            var typeUnits = inventories.Where(i => i.BloodType == type).ToList();
            var wb = typeUnits.Where(i => i.ComponentType == BloodComponentType.WholeBlood).Sum(i => i.UnitsAvailable);
            var rbc = typeUnits.Where(i => i.ComponentType == BloodComponentType.PackedRedBloodCells).Sum(i => i.UnitsAvailable);
            var plt = typeUnits.Where(i => i.ComponentType == BloodComponentType.Platelets).Sum(i => i.UnitsAvailable);
            var ffp = typeUnits.Where(i => i.ComponentType == BloodComponentType.FreshFrozenPlasma).Sum(i => i.UnitsAvailable);
            var total = typeUnits.Sum(i => i.UnitsAvailable);

            var alert = total < 10 ? "Critical" : (total < 25 ? "Low" : "Adequate");

            return new BloodStockSummaryDto(type, wb, rbc, plt, ffp, total, alert);
        }).ToList();

        return Ok(summaries);
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.BloodBank},{Roles.Admin}")]
    public async Task<IActionResult> AddInventory([FromBody] AddInventoryDto dto)
    {
        var bank = await _db.BloodBanks.FindAsync(dto.BloodBankId);
        if (bank is null) return NotFound("Blood bank not found.");

        var item = new BloodInventory
        {
            BloodBankId = dto.BloodBankId,
            BloodType = dto.BloodType,
            ComponentType = dto.ComponentType,
            UnitsAvailable = dto.Units,
            StorageLocation = dto.StorageLocation,
            BatchNumber = dto.BatchNumber,
            ExpiryDateUtc = dto.ExpiryDateUtc,
            Status = "Available"
        };

        _db.BloodInventories.Add(item);
        await _db.SaveChangesAsync();

        var userId = User.GetUserId();
        await _auditService.LogAsync(userId, "INVENTORY_ADDED",
            $"Added {dto.Units} units of {dto.BloodType} ({dto.ComponentType}) to {bank.Name}");

        return Ok(item);
    }

    [HttpPost("{id:guid}/update-stock")]
    [Authorize(Roles = $"{Roles.BloodBank},{Roles.Admin},{Roles.HospitalStaff}")]
    public async Task<IActionResult> UpdateStock(Guid id, [FromBody] UpdateStockDto dto)
    {
        var item = await _db.BloodInventories.Include(i => i.BloodBank).FirstOrDefaultAsync(i => i.Id == id);
        if (item is null) return NotFound("Inventory item not found.");

        if (item.UnitsAvailable + dto.UnitsDelta < 0)
            return BadRequest("Cannot deduct more units than currently available in stock.");

        item.UnitsAvailable += dto.UnitsDelta;
        item.Status = item.UnitsAvailable <= 3 ? "Critical" : (item.UnitsAvailable <= 10 ? "Low" : "Available");

        await _db.SaveChangesAsync();

        var userId = User.GetUserId();
        await _auditService.LogAsync(userId, "STOCK_UPDATED",
            $"Delta: {dto.UnitsDelta} units for {item.BloodType} at {item.BloodBank.Name}. Reason: {dto.Reason}");

        return Ok(item);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = $"{Roles.BloodBank},{Roles.Admin}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var item = await _db.BloodInventories.FindAsync(id);
        if (item is null) return NotFound("Inventory item not found.");

        _db.BloodInventories.Remove(item);
        await _db.SaveChangesAsync();

        var userId = User.GetUserId();
        await _auditService.LogAsync(userId, "INVENTORY_DISCARDED", $"Discarded blood inventory item ID {id}");

        return NoContent();
    }
}
