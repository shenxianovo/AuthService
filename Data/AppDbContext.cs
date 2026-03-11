using AuthService.Entities;
using AuthService.Entities.Configurations;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<UserEmail> UserEmails => Set<UserEmail>();
        public DbSet<PasswordCredential> PasswordCredentials => Set<PasswordCredential>();
        public DbSet<AuthProvider> AuthProviders => Set<AuthProvider>();
        public DbSet<Session> Sessions => Set<Session>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<EmailVerification> EmailVerifications => Set<EmailVerification>();
        public DbSet<PasswordReset> PasswordResets => Set<PasswordReset>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfiguration(new UserConfiguration());
            modelBuilder.ApplyConfiguration(new UserEmailConfiguration());
            modelBuilder.ApplyConfiguration(new PasswordCredentialConfiguration());
            modelBuilder.ApplyConfiguration(new AuthProviderConfiguration());
            modelBuilder.ApplyConfiguration(new SessionConfiguration());
            modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());
            modelBuilder.ApplyConfiguration(new EmailVerificationConfiguration());
            modelBuilder.ApplyConfiguration(new PasswordResetConfiguration());
        }
    }
}