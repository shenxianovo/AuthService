using AuthService.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Data.Configurations
{
    public class SessionConfiguration : IEntityTypeConfiguration<Session>
    {
        public void Configure(EntityTypeBuilder<Session> builder)
        {
            builder.HasKey(s => s.Id);

            builder.Property(s => s.Device)
                .IsRequired()
                .HasMaxLength(255);
            builder.Property(s => s.IpAddress)
                .IsRequired()
                .HasMaxLength(50);
            builder.Property(s => s.CreatedAt)
                .IsRequired();
            builder.Property(s => s.ExpiresAt)
                .IsRequired();
            builder.Property(s => s.Revoked)
                .IsRequired()
                .HasDefaultValue(false);

            builder.HasOne(s => s.User)
                .WithMany(u => u.Sessions)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cascade soft delete: rows owned by a merged-away user are invisible.
            builder.HasQueryFilter(s => !s.User.IsDeleted);
        }
    }
}
