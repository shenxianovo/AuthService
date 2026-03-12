using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Entities.Configurations
{
    public class EmailVerificationConfiguration : IEntityTypeConfiguration<EmailVerification>
    {
        public void Configure(EntityTypeBuilder<EmailVerification> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.TokenHash)
                .IsRequired()
                .HasMaxLength(512);
            builder.Property(e => e.CreatedAt)
                .IsRequired();
            builder.Property(e => e.ExpiresAt)
                .IsRequired();
            builder.Property(e => e.Used)
                .IsRequired()
                .HasDefaultValue(false);

            builder.HasOne(e => e.UserEmail)
                .WithMany()
                .HasForeignKey(e => e.UserEmailId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
