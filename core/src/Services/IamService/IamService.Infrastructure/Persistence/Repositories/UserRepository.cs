using IamService.Application.Interfaces;
using IamService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace IamService.Infrastructure.Persistence.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly IamDbContext _context;

        public UserRepository(IamDbContext context) => _context = context;

        public async Task<User?> FindByEmailAsync(string email)
            => await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

        public async Task<User?> FindByGoogleIdAsync(string googleId)
            => await _context.Users.FirstOrDefaultAsync(u => u.GoogleId == googleId);

        public async Task<User?> FindByIdAsync(Guid id)
            => await _context.Users.FindAsync(id);

        public async Task AddAsync(User user)
        {
            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(User user)
        {
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> HasAssociatedDataAsync(Guid userId)
        {
            var baseConnectionString = _context.Database.GetDbConnection().ConnectionString;
            var builder = new Npgsql.NpgsqlConnectionStringBuilder(baseConnectionString);

            // 1. Check gradepaper_grading database
            try
            {
                builder.Database = "gradepaper_grading";
                using (var conn = new Npgsql.NpgsqlConnection(builder.ConnectionString))
                {
                    await conn.OpenAsync();

                    var tablesToCheck = new[] 
                    {
                        ("grading_assignments", "teacher_id"),
                        ("marker_assignments", "teacher_id"),
                        ("marker_assignments", "assigned_by"),
                        ("grading_resume_pointers", "teacher_id")
                    };

                    foreach (var (table, column) in tablesToCheck)
                    {
                        var checkTableQuery = $"SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = '{table}')";
                        using (var checkCmd = new Npgsql.NpgsqlCommand(checkTableQuery, conn))
                        {
                            var tableExists = (bool)(await checkCmd.ExecuteScalarAsync() ?? false);
                            if (!tableExists) continue;
                        }

                        var query = $"SELECT COUNT(1) FROM {table} WHERE {column} = @userId";
                        using (var cmd = new Npgsql.NpgsqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("userId", userId);
                            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
                            if (count > 0) return true;
                        }
                    }
                }
            }
            catch
            {
                // Fallback / log if database doesn't exist yet
            }

            // 2. Check gradepaper_reporting database
            try
            {
                builder.Database = "gradepaper_reporting";
                using (var conn = new Npgsql.NpgsqlConnection(builder.ConnectionString))
                {
                    await conn.OpenAsync();

                    var tablesToCheck = new[] 
                    {
                        ("marker_progress", "teacher_id")
                    };

                    foreach (var (table, column) in tablesToCheck)
                    {
                        var checkTableQuery = $"SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = '{table}')";
                        using (var checkCmd = new Npgsql.NpgsqlCommand(checkTableQuery, conn))
                        {
                            var tableExists = (bool)(await checkCmd.ExecuteScalarAsync() ?? false);
                            if (!tableExists) continue;
                        }

                        var query = $"SELECT COUNT(1) FROM {table} WHERE {column} = @userId";
                        using (var cmd = new Npgsql.NpgsqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("userId", userId);
                            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync() ?? 0);
                            if (count > 0) return true;
                        }
                    }
                }
            }
            catch
            {
                // Fallback / log if database doesn't exist yet
            }

            // 3. Check MongoDB gradepaper_submission
            try
            {
                var mongoUrlStr = Environment.GetEnvironmentVariable("SubmissionDatabase__ConnectionString") ?? "mongodb://localhost:27018";
                var mongoDbName = Environment.GetEnvironmentVariable("SubmissionDatabase__DatabaseName") ?? "gradepaper_submission";
                var mongoClient = new MongoDB.Driver.MongoClient(mongoUrlStr);
                var mongoDb = mongoClient.GetDatabase(mongoDbName);
                
                var collection = mongoDb.GetCollection<MongoDB.Bson.BsonDocument>("submission_batches");
                
                var userIdStr = userId.ToString();
                var filterBuilder = MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter;
                
                var filter = filterBuilder.Or(
                    filterBuilder.Eq("uploaded_by", userId),
                    filterBuilder.Eq("uploaded_by", userIdStr)
                );

                var exists = await collection.Find(filter).Limit(1).AnyAsync();
                if (exists) return true;
            }
            catch
            {
                // In case MongoDB is offline or misconfigured, fallback to ignoring
            }

            return false;
        }

        public async Task<(System.Collections.Generic.IEnumerable<User> Items, int TotalCount)> GetUsersPagedAsync(int page, int pageSize, string? search)
        {
            var query = _context.Users.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var lowerSearch = search.ToLower();
                query = query.Where(u => 
                    u.Email.ToLower().Contains(lowerSearch) ||
                    (u.FullName != null && u.FullName.ToLower().Contains(lowerSearch)) ||
                    (u.MarkerCode != null && u.MarkerCode.ToLower().Contains(lowerSearch))
                );
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task AddAuditLogAsync(AuditLog auditLog)
        {
            await _context.AuditLogs.AddAsync(auditLog);
            await _context.SaveChangesAsync();
        }
    }
}
