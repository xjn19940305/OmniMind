using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OmniMind.Entities;

namespace OmniMind.Persistence.PostgreSql
{
    public class OmniMindDbContext : IdentityDbContext<User, Role, string, UserClaim, UserRole, UserLogin, RoleClaim, UserToken>
    {
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<KnowledgeBase> KnowledgeBases { get; set; }
        public DbSet<KnowledgeBaseMember> KnowledgeBaseMembers { get; set; }
        public DbSet<KnowledgeBaseInvitation> KnowledgeBaseInvitations { get; set; }
        public DbSet<Folder> Folders { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<DocumentVersion> DocumentVersions { get; set; }
        public DbSet<Chunk> Chunks { get; set; }
        public DbSet<IngestionTask> IngestionTasks { get; set; }
        public DbSet<TokenUsageLog> TokenUsageLogs { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<PushDevice> PushDevices { get; set; }
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }

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

            // User 索引
            modelBuilder.Entity<User>(build =>
            {
                build.HasIndex(u => u.PhoneNumber);
            });

            // KnowledgeBase - Owner 关系
            modelBuilder.Entity<KnowledgeBase>()
                .HasOne(kb => kb.Owner)
                .WithMany(u => u.OwnedKnowledgeBases)
                .HasForeignKey(kb => kb.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // KnowledgeBase - Members 关系
            modelBuilder.Entity<KnowledgeBase>()
                .HasMany(kb => kb.Members)
                .WithOne(m => m.KnowledgeBase)
                .HasForeignKey(m => m.KnowledgeBaseId)
                .OnDelete(DeleteBehavior.Cascade);

            // KnowledgeBaseMember - User 关系
            modelBuilder.Entity<KnowledgeBaseMember>()
                .HasOne(m => m.User)
                .WithMany(u => u.KnowledgeBaseMemberships)
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // KnowledgeBase - Documents 关系
            modelBuilder.Entity<Document>()
                .HasOne(d => d.KnowledgeBase)
                .WithMany(kb => kb.Documents)
                .HasForeignKey(d => d.KnowledgeBaseId)
                .OnDelete(DeleteBehavior.Restrict);

            // KnowledgeBase - Folders 关系
            modelBuilder.Entity<Folder>()
                .HasOne(f => f.KnowledgeBase)
                .WithMany(kb => kb.Folders)
                .HasForeignKey(f => f.KnowledgeBaseId)
                .OnDelete(DeleteBehavior.Restrict);

            // Folder - 自引用（父文件夹）
            modelBuilder.Entity<Folder>()
                .HasOne(f => f.ParentFolder)
                .WithMany(f => f.ChildFolders)
                .HasForeignKey(f => f.ParentFolderId)
                .OnDelete(DeleteBehavior.Restrict);

            // Document - Folder 关系
            modelBuilder.Entity<Document>()
                .HasOne(d => d.Folder)
                .WithMany(f => f.Documents)
                .HasForeignKey(d => d.FolderId)
                .OnDelete(DeleteBehavior.SetNull);

            // Document - IngestionTasks 关系
            modelBuilder.Entity<IngestionTask>()
                .HasOne(t => t.Document)
                .WithMany(d => d.IngestionTasks)
                .HasForeignKey(t => t.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            // User - UserProfile 一对一关系
            modelBuilder.Entity<UserProfile>()
                .HasOne(p => p.User)
                .WithOne(u => u.Profile)
                .HasForeignKey<UserProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // User - PushDevice 一对多关系
            modelBuilder.Entity<PushDevice>()
                .HasOne(d => d.User)
                .WithMany(u => u.PushDevices)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // PushDevice - ClientId 索引（推送时常用）
            modelBuilder.Entity<PushDevice>()
                .HasIndex(d => d.ClientId)
                .IsUnique();

            // Conversation - User 关系
            modelBuilder.Entity<Conversation>()
                .HasOne(c => c.User)
                .WithMany(u => u.Conversations)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Conversation - KnowledgeBase 关系（可选）
            modelBuilder.Entity<Conversation>()
                .HasOne(c => c.KnowledgeBase)
                .WithMany()
                .HasForeignKey(c => c.KnowledgeBaseId)
                .OnDelete(DeleteBehavior.SetNull);

            // Conversation - Document 关系（临时文件，可选）
            modelBuilder.Entity<Conversation>()
                .HasOne(c => c.Document)
                .WithMany()
                .HasForeignKey(c => c.DocumentId)
                .OnDelete(DeleteBehavior.SetNull);

            // ChatMessage - Conversation 关系
            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
