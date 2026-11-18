using System.ComponentModel.DataAnnotations;
using GestionCitas.Api.Data;
using GestionCitas.Api.Dtos;
using GestionCitas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GestionCitas.Api.Services;

public sealed class AppointmentPolicyOptions
{
    [Range(1, 20)] public int MaxPatientReschedules { get; set; } = 2;
}

public sealed class PatientReschedulingPolicy(AppDbContext db, IOptions<AppointmentPolicyOptions> options)
{
    public const string IncompleteHistoryMessage = "No se pudo validar el historial de reprogramaciones. Contacta al administrador.";
    public async Task<Dictionary<Guid, ReschedulingDto>> BuildAsync(Guid patientId, IReadOnlyList<Appointment>? appointments = null)
    {
        var history = appointments is null
            ? await db.Appointments.AsNoTracking().Where(x => x.PatientId == patientId)
                .Select(x => new Node(x.Id, x.PreviousAppointmentId, x.Status, x.Slot.StartAt)).ToListAsync()
            : appointments.Where(x => x.PatientId == patientId).Select(x => new Node(x.Id, x.PreviousAppointmentId, x.Status, x.Slot.StartAt)).ToList();
        var nodes = history.ToDictionary(x => x.Id);
        // Rescheduled es el evento del paciente. El profesional no genera ese evento.
        var events = (await db.AppointmentAuditLogs.AsNoTracking()
            .Where(x => x.PatientId == patientId && x.EventType == "Rescheduled")
            .Select(x => x.AppointmentId).ToListAsync()).ToHashSet();
        var maximum = options.Value.MaxPatientReschedules;
        var result = new Dictionary<Guid, ReschedulingDto>();
        foreach (var current in history)
        {
            var seen = new HashSet<Guid>();
            Guid? next = current.Id;
            var used = 0;
            var complete = true;
            while (next is Guid id)
            {
                if (!seen.Add(id) || !nodes.TryGetValue(id, out var node)) { complete = false; break; }
                if (events.Contains(id)) used++;
                next = node.PreviousId;
            }
            var remaining = complete ? Math.Max(0, maximum - used) : 0;
            result[current.Id] = new(maximum, used, remaining,
                complete && remaining > 0 && current.Status == AppointmentStatus.Confirmada && current.StartAt > DateTimeOffset.UtcNow, complete);
        }
        return result;
    }
    private sealed record Node(Guid Id, Guid? PreviousId, AppointmentStatus Status, DateTimeOffset StartAt);
}
