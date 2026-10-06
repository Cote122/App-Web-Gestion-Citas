using GestionCitas.Api.Data;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GestionCitas.Api.Controllers;

/// <summary>HU-02: administración de profesionales y su especialidad (solo Administrador).</summary>
[ApiController]
[Route("api/admin/professionals")]
[Authorize(Roles = "Administrador")]
public class AdminProfessionalsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var data = await db.Professionals
            .AsNoTracking()
            .OrderBy(x => x.FullName)
            .Select(x => new AdminProfessionalDto(x.Id, x.FullName, x.Office, x.SpecialtyId, x.Specialty.Name, x.Specialty.IsActive, x.IsActive))
            .ToListAsync();

        return Ok(data);
    }

    [HttpPost]
    public async Task<IActionResult> Create(SaveProfessionalRequest request)
    {
        var (specialty, error) = await ValidateAsync(request, null);
        if (error is not null) return BadRequest(new { message = error });

        var professional = new Professional
        {
            FullName = request.FullName!.Trim(),
            Office = request.Office?.Trim() ?? string.Empty,
            SpecialtyId = specialty!.Id
        };

        db.Professionals.Add(professional);
        await db.SaveChangesAsync();
        return Ok(ToDto(professional, specialty));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, SaveProfessionalRequest request)
    {
        var professional = await db.Professionals.FirstOrDefaultAsync(x => x.Id == id);
        if (professional is null) return NotFound(new { message = "Profesional no encontrado." });

        var (specialty, error) = await ValidateAsync(request, professional);
        if (error is not null) return BadRequest(new { message = error });

        professional.FullName = request.FullName!.Trim();
        professional.Office = request.Office?.Trim() ?? string.Empty;
        professional.SpecialtyId = specialty!.Id;
        await db.SaveChangesAsync();

        return Ok(ToDto(professional, specialty));
    }

    // Desactivar no elimina el registro ni sus citas: solo deja de ofrecerse a los pacientes.
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, SetActiveRequest request)
    {
        var professional = await db.Professionals
            .Include(x => x.Specialty)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (professional is null) return NotFound(new { message = "Profesional no encontrado." });

        professional.IsActive = request.IsActive;
        await db.SaveChangesAsync();

        return Ok(ToDto(professional, professional.Specialty));
    }

    private async Task<(Specialty? Specialty, string? Error)> ValidateAsync(SaveProfessionalRequest request, Professional? current)
    {
        var fullName = request.FullName?.Trim() ?? string.Empty;
        if (fullName.Length < 3) return (null, "El nombre del profesional es obligatorio (mínimo 3 caracteres).");
        if (fullName.Length > 200) return (null, "El nombre no puede superar los 200 caracteres.");
        if ((request.Office?.Trim().Length ?? 0) > 100) return (null, "El consultorio no puede superar los 100 caracteres.");

        var specialty = await db.Specialties.FirstOrDefaultAsync(x => x.Id == request.SpecialtyId);
        if (specialty is null) return (null, "Selecciona una especialidad válida.");

        // Solo se puede asociar a una especialidad activa; si ya pertenecía a una inactiva, se permite editar sus otros datos.
        var changesSpecialty = current is null || current.SpecialtyId != specialty.Id;
        if (changesSpecialty && !specialty.IsActive)
            return (null, "No se puede asociar un profesional a una especialidad inactiva.");

        return (specialty, null);
    }

    private static AdminProfessionalDto ToDto(Professional x, Specialty specialty) =>
        new(x.Id, x.FullName, x.Office, specialty.Id, specialty.Name, specialty.IsActive, x.IsActive);
}
