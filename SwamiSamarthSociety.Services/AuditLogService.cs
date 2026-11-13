using SwamiSamarthSociety.Data;
using SwamiSamarthSociety.Data.Entities;

namespace SwamiSamarthSociety.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly ApplicationDbContext _db;

        public AuditLogService(ApplicationDbContext db) => _db = db;

        public async Task LogAsync(int societyId, string tableName, int recordId, string action, string? changedBy, string? oldValue = null, string? newValue = null, CancellationToken ct = default)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                SocietyId = societyId,
                TableName = tableName,
                RecordId = recordId,
                Action = action,
                ChangedBy = changedBy,
                OldValue = oldValue,
                NewValue = newValue,
                ChangedDate = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }
    }
}
