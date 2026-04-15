using AuthService.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Data.Configurations
{
    public class PasswordCredentialConfiguration : IEntityTypeConfiguration<PasswordCredential>
    {
        public void Configure(EntityTypeBuilder<PasswordCredential> builder)
        {
            builder.HasKey(p => p.UserId);

            builder.Property(p => p.PasswordHash)
                .IsRequired()
                .HasMaxLength(512);
            builder.Property(p => p.CreatedAt)
                .IsRequired();
            builder.Property(p => p.UpdatedAt);

            builder.HasOne(p => p.User)
                .WithOne(u => u.PasswordCredential)
                .HasForeignKey<PasswordCredential>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
