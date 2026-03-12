using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Entities.Configurations
{
    public class AuthProviderConfiguration : IEntityTypeConfiguration<AuthProvider>
    {
        public void Configure(EntityTypeBuilder<AuthProvider> builder)
        {
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Provider)
                .IsRequired();
            builder.Property(p => p.ProviderUserId)
                .IsRequired()
                .HasMaxLength(255);
            builder.Property(p => p.CreatedAt)
                .IsRequired();

            builder.HasOne(p => p.User)
                .WithMany(u => u.Providers)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(p => new { p.Provider, p.ProviderUserId })
                .IsUnique();
        }
    }
}
