using System.ComponentModel.DataAnnotations;

namespace GestionCitas.Api.Models;

public class NotificationOutbox
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RecipientUserId { get; set; }
    public User RecipientUser { get; set; } = null!;

    [MaxLength(200)]
    public string RecipientEmail { get; set; } = string.Empty;

    [MaxLength(80)]
    public string Type { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Body { get; set; } = string.Empty;

    [MaxLength(30)]
    public string Status { get; set; } = "Pendiente";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; set; }

    [MaxLength(1000)]
    public string? LastError { get; set; }
}
