using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OmniMind.Abstractions.Tenant;
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
        private readonly ITenantProvider _tenant;

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


        public OmniMindDbContext(DbContextOptions options, ITenantProvider tenant) : base(options)
        {
            _tenant = tenant;
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // 自动给所有 ITenantEntity 加 QueryFilter
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
                {
                    var method = typeof(OmniMindDbContext)
                        .GetMethod(nameof(SetTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                        .MakeGenericMethod(entityType.ClrType);

                    method.Invoke(null, new object[] { modelBuilder, this });
                }
            }
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
        private static void SetTenantFilter<TEntity>(ModelBuilder builder, OmniMindDbContext db)
       where TEntity : class, ITenantEntity
        {
            // 设置查询过滤：只查询当前租户的数据
            // 注意：这里的过滤是在应用层进行的，对于未登录用户，TenantId 为 0，可能查询不到数据
            // 如果需要允许跨租户查询，应该在具体的服务中禁用查询过滤
            builder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == db._tenant.TenantId);
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            ApplyTenantRules();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }


        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            ApplyTenantRules();
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        private void ApplyTenantRules()
        {

            // 允许 Tenant 表直接写入：Tenant 不要求租户解析
            var isResolved = _tenant?.IsResolved == true;
            var currentTenantId = isResolved ? _tenant!.TenantId : string.Empty;

            foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
            {
                // ✅ 只跳过 Tenant 自己，不影响其他表
                if (entry.Entity is Tenant) continue;

                // 对业务表：必须解析租户
                if (!isResolved)
                    throw new InvalidOperationException("Tenant is not resolved.");
                if (entry.State == EntityState.Added)
                {
                    // 新增实体：自动设置租户ID
                    // 如果租户未解析（未登录用户），使用默认租户0
                    if (string.IsNullOrWhiteSpace(entry.Entity.TenantId))
                    {
                        entry.Entity.TenantId = currentTenantId;
                    }
                }
                else if (entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
                {
                    // 修改或删除实体：只有在租户已解析时才验证
                    if (isResolved)
                    {
                        // 禁止改 TenantId
                        entry.Property(x => x.TenantId).IsModified = false;

                        // 从数据库原值校验（避免实体被提前改掉）
                        var originalTenantId = entry.Property(x => x.TenantId).OriginalValue!;
                        if (originalTenantId != currentTenantId)
                            throw new InvalidOperationException("Cross-tenant update is not allowed.");
                    }
                }
            }
        }
    }
}
