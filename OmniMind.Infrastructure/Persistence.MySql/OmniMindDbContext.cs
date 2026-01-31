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
        private readonly ITenantProvider _tenant;

        public DbSet<RefreshToken> RefreshTokens { get; set; }


        public OmniMindDbContext(DbContextOptions options, ITenantProvider tenant) : base(options)
        {
            _tenant = tenant;
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
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
                build.HasIndex(u => new { u.TenantId, u.PhoneNumber });

            });

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
            // 获取当前租户ID，如果未解析则使用默认租户（0）
            var currentTenantId = _tenant.TenantId;
            var isResolved = _tenant.IsResolved;

            foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
            {
                if (entry.State == EntityState.Added)
                {
                    // 新增实体：自动设置租户ID
                    // 如果租户未解析（未登录用户），使用默认租户0
                    if (entry.Entity.TenantId == 0)
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
                        var originalTenantId = (long)entry.Property(x => x.TenantId).OriginalValue!;
                        if (originalTenantId != currentTenantId)
                            throw new InvalidOperationException("Cross-tenant update is not allowed.");
                    }
                }
            }
        }
    }
}
