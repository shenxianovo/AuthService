using AuthService.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Data.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(u => u.Id);

            builder.Property(u => u.Username)
                .IsRequired()
                .HasMaxLength(39);
            builder.HasIndex(u => u.Username)
                .IsUnique();

            builder.Property(u => u.DisplayName)
                .IsRequired()
                .HasMaxLength(128);
            builder.Property(u => u.CreatedAt)
                .IsRequired();
            builder.Property(u => u.UpdatedAt);
            builder.Property(u => u.IsDeleted)
                .HasDefaultValue(false);

            // Soft delete (ADR-006): a merged-away user is invisible everywhere.
            // Existence checks against globally-unique columns (e.g. Username) must
            // use IgnoreQueryFilters — the unique index still sees deleted rows.
            builder.HasQueryFilter(u => !u.IsDeleted);
        }
    }
}
