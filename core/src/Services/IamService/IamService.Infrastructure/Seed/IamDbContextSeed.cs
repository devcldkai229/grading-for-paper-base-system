using IamService.Domain.Entities;
using IamService.Domain.Enums;
using IamService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace IamService.Infrastructure.Seed
{
    public class IamDbContextSeed
    {
        public static async Task SeedAsync(IamDbContext context, ILogger<IamDbContextSeed> logger)
        {
            try
            {
                if (context.Database.IsNpgsql())
                {
                    await context.Database.MigrateAsync();
                }

                if (!context.Users.Any())
                {
                    logger.LogInformation("Seeding default users...");

                    // We use simple dummy passwords. In real world, use a PasswordHasher
                    // e.g., BCrypt.Net.BCrypt.HashPassword("Admin@123")

                    var adminUser = new User
                    {
                        Email = "admin@example.com",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                        FullName = "System Admin",
                        Role = UserRole.Admin,
                        Status = UserStatus.Active,
                        LoginProvider = LoginProvider.PasswordAuth
                    };

                    var lecturerUser = new User
                    {
                        Email = "lecture@example.com",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Lecture@123"),
                        FullName = "John Doe Lecture",
                        Role = UserRole.Lecture, // Note: Enum uses "Lecture" instead of "Lecturer"
                        Status = UserStatus.Active,
                        LoginProvider = LoginProvider.PasswordAuth
                    };

                    context.Users.AddRange(adminUser, lecturerUser);
                    await context.SaveChangesAsync();

                    logger.LogInformation("Successfully seeded default users.");
                }
                else
                {
                    // Fix old unhashed passwords for existing seeded data
                    var unhashedAdmin = await context.Users.FirstOrDefaultAsync(u => u.Email == "admin@example.com" && u.PasswordHash == "hashed_admin_password");
                    if (unhashedAdmin != null)
                    {
                        unhashedAdmin.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123");
                    }

                    var unhashedLecture = await context.Users.FirstOrDefaultAsync(u => u.Email == "lecture@example.com" && u.PasswordHash == "hashed_lecture_password");
                    if (unhashedLecture != null)
                    {
                        unhashedLecture.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Lecture@123");
                    }

                    if (unhashedAdmin != null || unhashedLecture != null)
                    {
                        await context.SaveChangesAsync();
                        logger.LogInformation("Updated old unhashed passwords to BCrypt.");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while seeding the database.");
            }
        }
    }
}
