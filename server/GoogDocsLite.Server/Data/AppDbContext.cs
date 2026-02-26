using GoogDocsLite.Server.Data.Entities;
using GoogDocsLite.Shared;
using Microsoft.EntityFrameworkCore;

namespace GoogDocsLite.Server.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // Tabela principala de documente.
    public DbSet<DocumentEntity> Documents => Set<DocumentEntity>();

    // Tabela care retine drepturile acordate explicit utilizatorilor.
    public DbSet<DocumentPermissionEntity> DocumentPermissions => Set<DocumentPermissionEntity>();

    // Tabela de invitatii (inclusiv pending pentru email-uri neinregistrate inca).
    public DbSet<DocumentInviteEntity> DocumentInvites => Set<DocumentInviteEntity>();

    // Tabela pentru lock-ul activ de editare (un singur editor activ per document).
    public DbSet<DocumentEditLockEntity> DocumentEditLocks => Set<DocumentEditLockEntity>();

    // Tabela cu operatii realtime (OT), folosita pentru replay/resync.
    public DbSet<DocumentOperationEntity> DocumentOperations => Set<DocumentOperationEntity>();

    // Configureaza schema SQL: chei, indexuri, lungimi maxime si relatii.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DocumentEntity>(entity =>
        {
            entity.ToTable("Documents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(AppDefaults.DocumentTitleMaxLength).IsRequired();
            entity.Property(x => x.OwnerUserId).IsRequired();
            entity.Property(x => x.Content).HasDefaultValue(string.Empty);
            entity.Property(x => x.ContentDeltaJson).IsRequired(false);
            entity.Property(x => x.LiveRevision).HasDefaultValue(0L);
            entity.HasIndex(x => new { x.OwnerUserId, x.UpdatedAt });
        });

        modelBuilder.Entity<DocumentPermissionEntity>(entity =>
        {
            entity.ToTable("DocumentPermissions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).IsRequired();
            entity.Property(x => x.GrantedByUserId).IsRequired();
            entity.Property(x => x.Role).HasConversion<string>().IsRequired();
            entity.HasIndex(x => new { x.DocumentId, x.UserId }).IsUnique();

            entity.HasOne(x => x.Document)
                .WithMany(x => x.Permissions)
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DocumentInviteEntity>(entity =>
        {
            entity.ToTable("DocumentInvites");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.InviteeEmailNormalized).IsRequired();
            entity.Property(x => x.CreatedByUserId).IsRequired();
            entity.Property(x => x.Role).HasConversion<string>().IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().IsRequired();
            entity.HasIndex(x => new { x.InviteeEmailNormalized, x.Status });

            entity.HasOne(x => x.Document)
                .WithMany(x => x.Invites)
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DocumentEditLockEntity>(entity =>
        {
            entity.ToTable("DocumentEditLocks");
            entity.HasKey(x => x.DocumentId);
            entity.Property(x => x.LockOwnerUserId).IsRequired();
            entity.Property(x => x.LockOwnerDisplayName).IsRequired();

            entity.HasOne(x => x.Document)
                .WithOne(x => x.EditLock)
                .HasForeignKey<DocumentEditLockEntity>(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DocumentOperationEntity>(entity =>
        {
            entity.ToTable("DocumentOperations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).IsRequired();
            entity.Property(x => x.ClientOpId).IsRequired();
            entity.Property(x => x.DeltaJson).IsRequired();
            entity.HasIndex(x => new { x.DocumentId, x.Revision }).IsUnique();
            entity.HasIndex(x => new { x.DocumentId, x.UserId, x.ClientOpId }).IsUnique();
            entity.HasIndex(x => new { x.DocumentId, x.CreatedAtUtc });

            entity.HasOne(x => x.Document)
                .WithMany(x => x.Operations)
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
