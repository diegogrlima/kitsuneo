using Kitsuneo.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kitsuneo.Infrastructure.Persistence.Configurations;

public class ReviewScheduleConfiguration : IEntityTypeConfiguration<ReviewSchedule>
{
    public void Configure(EntityTypeBuilder<ReviewSchedule> builder)
    {
        builder.ToTable("review_schedules");

        // One schedule per phrase, keyed by the phrase itself
        builder.HasKey(s => s.PhraseId);

        // Stored as text through the enum convention in AppDbContext
        builder.Property(s => s.State)
            .IsRequired();

        builder.Property(s => s.DueAt)
            .IsRequired();
    }
}
