using AuthService.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Data.Configurations
{
    public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
    {
        public void Configure(EntityTypeBuilder<ApiKey> builder)
        {
            builder.HasKey(k => k.Id);

            builder.Property(k => k.Name).HasMaxLength(100).IsRequired();
            builder.Property(k => k.Prefix).HasMaxLength(16).IsRequired();
            builder.Property(k => k.SecretHash).HasMaxLength(128).IsRequired();

            builder.HasIndex(k => k.Prefix);
            builder.HasIndex(k => new { k.UserId, k.IsRevoked });

            builder.HasOne(k => k.User)
                .WithMany(u => u.ApiKeys)
                .HasForeignKey(k => k.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}