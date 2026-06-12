using AuthService.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Data.Configurations
{
    public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            builder.HasKey(r =>  r.Id);

            builder.Property(r => r.TokenHash)
                .IsRequired()
                .HasMaxLength(512);
            builder.Property(r => r.CreatedAt)
                .IsRequired();
            builder.Property(r => r.ExpiresAt)
                .IsRequired();
            builder.Property(r => r.Revoked)
                .IsRequired()
                .HasDefaultValue(false);

            builder.HasOne(r => r.Session)
                .WithMany(s => s.RefreshTokens)
                .HasForeignKey(r => r.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cascade soft delete (two levels): tokens of a merged-away user's
            // sessions are invisible.
            builder.HasQueryFilter(r => !r.Session.User.IsDeleted);
        }
    }
}
