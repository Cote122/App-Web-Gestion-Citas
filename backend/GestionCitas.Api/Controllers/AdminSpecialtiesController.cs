using GestionCitas.Api.Data;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionCitas.Api.Controllers;

/// <summary>HU-02: administración de especialidades (solo Administrador).</summary>
[ApiController]
[Route("api/admin/specialties")]
[Authorize(Roles = "Administrador")]
public class AdminSpecialtiesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var data = await db.Specialties
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new AdminSpecialtyDto(x.Id, x.Name, x.Description, x.Price, x.IsActive, x.Professionals.Count))
            .ToListAsync();

        return Ok(data);
    }

    [HttpPost]
    public async Task<IActionResult> Create(SaveSpecialtyRequest request)
    {
        var error = await ValidateAsync(request, null);
        if (error is not null) return BadRequest(new { message = error });

        var specialty = new Specialty
        {
            Name = request.Name!.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Price = request.Price
        };

        db.Specialties.Add(specialty);
        await db.SaveChangesAsync();
        return Ok(ToDto(specialty, 0));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, SaveSpecialtyRequest request)
    {
        var specialty = await db.Specialties.FirstOrDefaultAsync(x => x.Id == id);
        if (specialty is null) return NotFound(new { message = "Especialidad no encontrada." });

        var error = await ValidateAsync(request, id);
        if (error is not null) return BadRequest(new { message = error });

        specialty.Name = request.Name!.Trim();
        specialty.Description = request.Description?.Trim() ?? string.Empty;
        specialty.Price = request.Price;
        await db.SaveChangesAsync();

        return Ok(ToDto(specialty, await db.Professionals.CountAsync(x => x.SpecialtyId == id)));
    }

    // Desactivar no elimina el registro: se conserva para trazabilidad.
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, SetActiveRequest request)
    {
        var specialty = await db.Specialties.FirstOrDefaultAsync(x => x.Id == id);
        if (specialty is null) return NotFound(new { message = "Especialidad no encontrada." });

        specialty.IsActive = request.IsActive;
        await db.SaveChangesAsync();

        return Ok(ToDto(specialty, await db.Professionals.CountAsync(x => x.SpecialtyId == id)));
    }

    private async Task<string?> ValidateAsync(SaveSpecialtyRequest request, Guid? currentId)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length < 3) return "El nombre de la especialidad es obligatorio (mínimo 3 caracteres).";
        if (name.Length > 100) return "El nombre no puede superar los 100 caracteres.";
        if ((request.Description?.Trim().Length ?? 0) > 300) return "La descripción no puede superar los 300 caracteres.";
        if (request.Price < 0 || request.Price > 99999) return "El precio debe estar entre 0 y 99 999.";

        var duplicated = await db.Specialties.AnyAsync(x => x.Name == name && x.Id != currentId);
        return duplicated ? "Ya existe una especialidad con ese nombre." : null;
    }

    private static AdminSpecialtyDto ToDto(Specialty x, int professionalCount) =>
        new(x.Id, x.Name, x.Description, x.Price, x.IsActive, professionalCount);
}
