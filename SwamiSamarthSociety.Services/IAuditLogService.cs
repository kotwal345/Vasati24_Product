namespace SwamiSamarthSociety.Services
{
    // Records who did what, to which record, and when -- for the SuperAdmin's platform-wide
    // Activity view. SocietyId is taken explicitly rather than from the ambient tenant context,
    // since half the call sites (society creation/deactivation) happen as SuperAdmin, who has
    // no ambient SocietyId to auto-stamp from.
    public interface IAuditLogService
    {
        Task LogAsync(int societyId, string tableName, int recordId, string action, string? changedBy, string? oldValue = null, string? newValue = null, CancellationToken ct = default);
    }
}
