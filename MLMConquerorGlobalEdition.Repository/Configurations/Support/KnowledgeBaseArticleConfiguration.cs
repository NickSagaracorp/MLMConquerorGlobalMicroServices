using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MLMConquerorGlobalEdition.Domain.Entities.Support;

namespace MLMConquerorGlobalEdition.Repository.Configurations.Support;

public class KnowledgeBaseArticleConfiguration : IEntityTypeConfiguration<KnowledgeBaseArticle>
{
    public void Configure(EntityTypeBuilder<KnowledgeBaseArticle> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.Property(x => x.Title).IsRequired().HasMaxLength(300);
        builder.Property(x => x.Slug).IsRequired().HasMaxLength(300);
        builder.HasIndex(x => x.Slug).IsUnique();

        builder.Property(x => x.Body).IsRequired();
        builder.Property(x => x.TagsJson).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.AuthorAgentId).IsRequired();
        builder.Property(x => x.SourceTicketId).HasMaxLength(36);

        builder.HasIndex(x => new { x.Visibility, x.CategoryId });
        builder.HasIndex(x => x.HelpfulCount);

        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Versions)
            .WithOne(v => v.Article)
            .HasForeignKey(v => v.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
