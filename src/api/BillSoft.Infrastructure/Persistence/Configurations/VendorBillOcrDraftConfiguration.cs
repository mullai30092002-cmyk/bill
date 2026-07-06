using BillSoft.Domain.Restaurants;
using BillSoft.Domain.Users;
using BillSoft.Domain.Vendors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BillSoft.Infrastructure.Persistence.Configurations;

public sealed class VendorBillOcrDraftConfiguration : IEntityTypeConfiguration<VendorBillOcrDraft>
{
    public void Configure(EntityTypeBuilder<VendorBillOcrDraft> builder)
    {
        builder.ToTable("VendorBillOcrDrafts");

        builder.HasKey(entity => entity.VendorBillOcrDraftId);

        builder.Property(entity => entity.VendorBillOcrDraftId)
            .ValueGeneratedNever();

        builder.Property(entity => entity.RestaurantId)
            .IsRequired();

        builder.Property(entity => entity.BranchId)
            .IsRequired();

        builder.Property(entity => entity.UploadedByUserId)
            .IsRequired();

        builder.Property(entity => entity.OriginalFileName)
            .IsRequired()
            .HasMaxLength(260);

        builder.Property(entity => entity.StoredFilePath)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(entity => entity.ContentType)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(entity => entity.FileSizeBytes)
            .IsRequired();

        builder.Property(entity => entity.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.ExtractedVendorName)
            .HasMaxLength(160);

        builder.Property(entity => entity.ExtractedBillNumber)
            .HasMaxLength(40);

        builder.Property(entity => entity.ExtractedBillDate)
            .HasColumnType("date");

        builder.Property(entity => entity.ExtractedTotalAmount)
            .HasPrecision(18, 2);

        builder.Property(entity => entity.ExtractedConfidenceScore)
            .HasPrecision(5, 4);

        builder.Property(entity => entity.ProviderWarningsJson)
            .HasMaxLength(2000);

        builder.Property(entity => entity.ReviewedBillNumber)
            .HasMaxLength(40);

        builder.Property(entity => entity.ReviewedBillDate)
            .HasColumnType("date");

        builder.Property(entity => entity.ReviewedTotalAmount)
            .HasPrecision(18, 2);

        builder.Property(entity => entity.SafeErrorMessage)
            .HasMaxLength(500);

        builder.Property(entity => entity.CreatedAtUtc)
            .IsRequired();

        builder.Property(entity => entity.UpdatedAtUtc)
            .IsRequired();

        builder.Property(entity => entity.ConfirmedAtUtc);

        builder.HasIndex(entity => entity.RestaurantId)
            .HasDatabaseName("IX_VendorBillOcrDrafts_RestaurantId");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.BranchId, entity.Status })
            .HasDatabaseName("IX_VendorBillOcrDrafts_RestaurantId_BranchId_Status");

        builder.HasIndex(entity => new { entity.RestaurantId, entity.CreatedAtUtc })
            .HasDatabaseName("IX_VendorBillOcrDrafts_RestaurantId_CreatedAtUtc");

        builder.HasIndex(entity => entity.ConfirmedVendorBillId)
            .IsUnique()
            .HasFilter("[ConfirmedVendorBillId] IS NOT NULL")
            .HasDatabaseName("UX_VendorBillOcrDrafts_ConfirmedVendorBillId");

        builder.HasOne<Restaurant>()
            .WithMany()
            .HasForeignKey(entity => entity.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_VendorBillOcrDrafts_Restaurants_RestaurantId");

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(entity => entity.BranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_VendorBillOcrDrafts_Branches_BranchId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_VendorBillOcrDrafts_Users_UploadedByUserId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.ConfirmedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_VendorBillOcrDrafts_Users_ConfirmedByUserId");

        builder.HasMany(entity => entity.Lines)
            .WithOne()
            .HasForeignKey(entity => entity.VendorBillOcrDraftId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_VendorBillOcrDraftLines_VendorBillOcrDrafts_VendorBillOcrDraftId");
    }
}
