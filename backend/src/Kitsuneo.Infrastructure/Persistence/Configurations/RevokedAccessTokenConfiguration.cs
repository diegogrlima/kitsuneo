using Kitsuneo.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kitsuneo.Infrastructure.Persistence.Configurations;

public class RevokedAccessTokenConfiguration : IEntityTypeConfiguration<RevokedAccessToken>
{
    public void Configure(EntityTypeBuilder<RevokedAccessToken> builder)
    {
        builder.ToTable("revoked_access_tokens");

        // Looked up by jti on every authenticated request
        builder.HasKey(t => t.Jti);

        builder.Property(t => t.Jti)
            .HasMaxLength(RevokedAccessToken.MaxJtiLength);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(t => t.RevokedAt)
            .IsRequired();

        // Serves purging entries whose token has expired anyway
        builder.Property(t => t.ExpiresAt)
            .IsRequired();

        builder.HasIndex(t => t.ExpiresAt);
    }
}
