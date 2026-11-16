using System.Data;
using HotelSaaS.Application.Interfaces;
using HotelSaaS.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelSaaS.Infrastructure.Persistence;

public partial class ApplicationDbContext : IAppDbContext
{
    private IDbContextTransaction? _currentTransaction;

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        _currentTransaction ??= await Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    }

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_currentTransaction == null) return;
        await SaveChangesAsync(ct);
        await _currentTransaction.CommitAsync(ct);
        await _currentTransaction.DisposeAsync();
        _currentTransaction = null;
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_currentTransaction == null) return;
        await _currentTransaction.RollbackAsync(ct);
        await _currentTransaction.DisposeAsync();
        _currentTransaction = null;
    }

    /// <summary>
    /// Atomic per-tenant sequence for booking/invoice numbers (PRD rules 8 + 43):
    /// single UPDATE ... OUTPUT statement is safe under concurrency.
    /// </summary>
    public async Task<int> IncrementCounterAsync(long tenantId, bool bookingCounter, CancellationToken ct)
    {
        var column = bookingCounter ? "BookingCounter" : "InvoiceCounter";
        await using var cmd = Database.GetDbConnection().CreateCommand();
        if (Database.GetDbConnection().State != ConnectionState.Open)
            await Database.OpenConnectionAsync(ct);

        cmd.CommandText =
            $"UPDATE dbo.PropertySettings SET {column} = {column} + 1 " +
            "OUTPUT inserted." + column + " WHERE TenantId = @tenantId";
        var p = cmd.CreateParameter();
        p.ParameterName = "@tenantId";
        p.Value = tenantId;
        cmd.Parameters.Add(p);

        // Enlist in the ambient EF transaction (booking/invoice numbering must
        // commit or roll back atomically with the business operation).
        if (Database.CurrentTransaction is not null)
            cmd.Transaction = Database.CurrentTransaction.GetDbTransaction();

        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }
}
