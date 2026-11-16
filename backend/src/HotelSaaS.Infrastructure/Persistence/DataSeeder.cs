using HotelSaaS.Application.Security;
using HotelSaaS.Domain;
using HotelSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HotelSaaS.Infrastructure.Persistence;

/// <summary>
/// First-run seeder. The SQL seed script (database/02_seed.sql) intentionally
/// leaves PasswordHash NULL for demo users; this seeder assigns the hashed
/// default password using the production hashing code so the format is always
/// exactly what PasswordHasher.Verify expects.
/// </summary>
public class DataSeeder(ApplicationDbContext db, ILogger<DataSeeder> logger)
{
    public const string DefaultPassword = "Admin@123";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // Roles are fixed reference data; ensure they exist even on a bare schema.
        var existingRoleIds = await db.Roles.Select(r => r.RoleId).ToListAsync(ct);
        var missingRoles = new (byte Id, string Name)[]
        {
            (1, RoleNames.SuperAdmin), (2, RoleNames.TenantAdmin), (3, RoleNames.Manager),
            (4, RoleNames.FrontDesk), (5, RoleNames.Finance), (6, RoleNames.Hr), (7, RoleNames.Housekeeping)
        }.Where(r => !existingRoleIds.Contains(r.Id)).ToList();
        if (missingRoles.Count > 0)
        {
            db.Roles.AddRange(missingRoles.Select(r => new Role { RoleId = r.Id, Name = r.Name }));
            await db.SaveChangesAsync(ct);
            logger.LogWarning("Inserted missing reference roles: {Roles}", string.Join(", ", missingRoles.Select(r => r.Name)));
        }

        var unhashedUsers = await db.Users.Where(u => u.PasswordHash == null).ToListAsync(ct);
        if (unhashedUsers.Count == 0) return;

        var hash = PasswordHasher.Hash(DefaultPassword);
        foreach (var user in unhashedUsers)
        {
            user.PasswordHash = hash;
            logger.LogWarning(
                "Assigned DEFAULT password to seeded account '{Email}'. Change it immediately after first login.",
                user.Email);
        }
        await db.SaveChangesAsync(ct);
    }
}


