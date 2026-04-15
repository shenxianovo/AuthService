using AuthService.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Data.Configurations
{
    public class PasswordResetConfiguration : IEntityTypeConfiguration<PasswordReset>
    {
        public void Configure(EntityTypeBuilder<PasswordReset> builder)
        {
            builder.HasKey(p => p.Id);

            builder.Property(p => p.TokenHash)
                .IsRequired()
                .HasMaxLength(512);
            builder.Property(p => p.CreatedAt)
                .IsRequired();
            builder.Property(p => p.ExpiresAt)
                .IsRequired();
            builder.Property(p => p.Used)
                .IsRequired()
                .HasDefaultValue(false);

            builder.HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
