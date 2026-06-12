using AuthService.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Data.Configurations
{
    public class UserEmailConfiguration : IEntityTypeConfiguration<UserEmail>
    {
        public void Configure(EntityTypeBuilder<UserEmail> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(255);
            builder.Property(e => e.IsPrimary)
                .HasDefaultValue(false);
            builder.Property(e => e.CreatedAt)
                .IsRequired();
            builder.Property(e => e.VerifiedAt);

            builder.HasOne(e => e.User)
                .WithMany(u => u.Emails)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(e => e.Email)
                .IsUnique();

            // Cascade soft delete: rows owned by a merged-away user are invisible.
            builder.HasQueryFilter(e => !e.User.IsDeleted);
        }
    }
}
