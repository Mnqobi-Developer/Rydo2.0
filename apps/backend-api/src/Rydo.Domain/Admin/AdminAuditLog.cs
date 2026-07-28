namespace Rydo.Domain.Admin;

public sealed class AdminAuditLog
{
    private AdminAuditLog()
    {
    }

    private AdminAuditLog(
        Guid id,
        Guid adminUserId,
        string action,
        string entityType,
        Guid entityId,
        string details,
        DateTimeOffset createdAt)
    {
        Id = id;
        AdminUserId = adminUserId;
        Action = Normalize(action, 64, "Admin audit actions");
        EntityType = Normalize(entityType, 64, "Admin audit entity types");
        EntityId = entityId;
        Details = Normalize(details, 2000, "Admin audit details");
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid AdminUserId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public Guid EntityId { get; private set; }

    public string Details { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static AdminAuditLog Create(
        Guid adminUserId,
        string action,
        string entityType,
        Guid entityId,
        string details,
        DateTimeOffset createdAt)
    {
        if (adminUserId == Guid.Empty || entityId == Guid.Empty)
        {
            throw new ArgumentException("Admin audit identifiers are required.");
        }

        return new AdminAuditLog(
            Guid.NewGuid(),
            adminUserId,
            action,
            entityType,
            entityId,
            details,
            createdAt);
    }

    private static string Normalize(string value, int maximumLength, string fieldName)
    {
        var normalized = value.Trim();
        return normalized.Length is > 0 && normalized.Length <= maximumLength
            ? normalized
            : throw new ArgumentException($"{fieldName} must contain between 1 and {maximumLength} characters.");
    }
}
