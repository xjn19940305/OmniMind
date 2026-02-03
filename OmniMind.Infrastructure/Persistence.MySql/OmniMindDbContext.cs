using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OmniMind.Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection.Emit;
using System.Text;

namespace OmniMind.Persistence.MySql
{
    public class OmniMindDbContext : IdentityDbContext<User, Role, string, UserClaim, UserRole, UserLogin, RoleClaim, UserToken>
    {
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<KnowledgeBase> KnowledgeBases { get; set; }
        public DbSet<Workspace> Workspaces { get; set; }
        public DbSet<KnowledgeBaseWorkspace> KnowledgeBaseWorkspaces { get; set; }
        public DbSet<WorkspaceMember> WorkspaceMembers { get; set; }
        public DbSet<Folder> Folders { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<DocumentVersion> DocumentVersions { get; set; }
        public DbSet<Chunk> Chunks { get; set; }
        public DbSet<IngestionTask> IngestionTasks { get; set; }


        public OmniMindDbContext(DbContextOptions options) : base(options)
        {
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<User>(build =>
            {
                build.HasIndex(u => new { u.PhoneNumber });

            });
            //// KnowledgeBaseWorkspace：级联删除（删除 KB 或 Workspace 时清理关联）
            //modelBuilder.Entity<KnowledgeBaseWorkspace>()
            //    .HasOne(x => x.KnowledgeBase)
            //    .WithMany(x => x.WorkspaceLinks)
            //    .HasForeignKey(x => x.KnowledgeBaseId)
            //    .OnDelete(DeleteBehavior.Cascade);

            //modelBuilder.Entity<KnowledgeBaseWorkspace>()
            //    .HasOne(x => x.Workspace)
            //    .WithMany(x => x.KnowledgeBaseLinks)
            //    .HasForeignKey(x => x.WorkspaceId)
            //    .OnDelete(DeleteBehavior.Cascade);

            //// Document：建议 Restrict，避免误删 KB/Workspace 导致文档全灭（看你业务）
            //modelBuilder.Entity<Document>()
            //    .HasOne(x => x.KnowledgeBase)
            //    .WithMany(x => x.Documents)
            //    .HasForeignKey(x => x.KnowledgeBaseId)
            //    .OnDelete(DeleteBehavior.Restrict);

            //modelBuilder.Entity<Document>()
            //    .HasOne(x => x.Workspace)
            //    .WithMany(x => x.Documents)
            //    .HasForeignKey(x => x.WorkspaceId)
            //    .OnDelete(DeleteBehavior.Restrict);

        }
    }
}
