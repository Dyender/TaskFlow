using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Infrastructure.Persistence;

public sealed class TaskFlowDbContext(DbContextOptions<TaskFlowDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<BoardColumn> Columns => Set<BoardColumn>();
    public DbSet<TaskCard> Cards => Set<TaskCard>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ActivityLog> Activities => Set<ActivityLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var labelIdsConverter = new ValueConverter<List<Guid>, Guid[]>(
            value => value.ToArray(),
            value => value.ToList());
        var labelIdsComparer = new ValueComparer<List<Guid>>(
            (left, right) => left != null && right != null
                ? left.SequenceEqual(right)
                : left == right,
            value => HashLabelIds(value),
            value => CloneLabelIds(value));

        modelBuilder.Entity<User>(builder =>
        {
            builder.ToTable("Users");
            builder.HasKey(user => user.Id);
            builder.Property(user => user.Name).HasMaxLength(200).IsRequired();
            builder.Property(user => user.Email).HasMaxLength(320).IsRequired();
            builder.Property(user => user.PasswordHash).HasMaxLength(512).IsRequired();
            builder.Property(user => user.AvatarUrl).HasMaxLength(2000);
            builder.HasIndex(user => user.Email).IsUnique();
        });

        modelBuilder.Entity<RefreshSession>(builder =>
        {
            builder.ToTable("RefreshSessions");
            builder.HasKey(session => session.Id);
            builder.Property(session => session.TokenHash).HasMaxLength(256).IsRequired();
            builder.HasIndex(session => session.TokenHash).IsUnique();
            builder.HasIndex(session => session.UserId);
            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(session => session.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Workspace>(builder =>
        {
            builder.ToTable("Workspaces");
            builder.HasKey(workspace => workspace.Id);
            builder.Property(workspace => workspace.Name).HasMaxLength(200).IsRequired();
            builder.Property(workspace => workspace.Description).HasMaxLength(4000);
            builder.HasIndex(workspace => workspace.OwnerId);
            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(workspace => workspace.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.OwnsMany(workspace => workspace.Members, members =>
            {
                members.ToTable("WorkspaceMembers");
                members.WithOwner().HasForeignKey("WorkspaceId");
                members.HasKey("WorkspaceId", nameof(WorkspaceMember.UserId));
                members.Property(member => member.UserId).ValueGeneratedNever();
                members.Property(member => member.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
                members.Property(member => member.JoinedAtUtc).IsRequired();
            });

            builder.Navigation(workspace => workspace.Members).AutoInclude();
        });

        modelBuilder.Entity<Board>(builder =>
        {
            builder.ToTable("Boards");
            builder.HasKey(board => board.Id);
            builder.Property(board => board.Name).HasMaxLength(200).IsRequired();
            builder.Property(board => board.Description).HasMaxLength(4000);
            builder.Property(board => board.Color).HasMaxLength(32).IsRequired();
            builder.Property(board => board.Visibility).HasConversion<string>().HasMaxLength(32).IsRequired();
            builder.HasIndex(board => board.WorkspaceId);
            builder.HasOne<Workspace>()
                .WithMany()
                .HasForeignKey(board => board.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.OwnsMany(board => board.Members, members =>
            {
                members.ToTable("BoardMembers");
                members.WithOwner().HasForeignKey("BoardId");
                members.HasKey("BoardId", nameof(BoardMember.UserId));
                members.Property(member => member.UserId).ValueGeneratedNever();
                members.Property(member => member.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
                members.Property(member => member.JoinedAtUtc).IsRequired();
            });

            builder.Navigation(board => board.Members).AutoInclude();
        });

        modelBuilder.Entity<BoardColumn>(builder =>
        {
            builder.ToTable("BoardColumns");
            builder.HasKey(column => column.Id);
            builder.Property(column => column.Title).HasMaxLength(200).IsRequired();
            builder.HasIndex(column => new { column.BoardId, column.Position }).IsUnique();
            builder.HasOne<Board>()
                .WithMany()
                .HasForeignKey(column => column.BoardId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaskCard>(builder =>
        {
            builder.ToTable("TaskCards");
            builder.HasKey(card => card.Id);
            builder.Property(card => card.Title).HasMaxLength(250).IsRequired();
            builder.Property(card => card.Description).HasMaxLength(8000);
            builder.Property(card => card.Priority).HasConversion<string>().HasMaxLength(32).IsRequired();
            builder.Property(card => card.LabelIds)
                .HasColumnType("uuid[]")
                .HasConversion(labelIdsConverter);
            builder.Property(card => card.LabelIds).Metadata.SetValueComparer(labelIdsComparer);
            builder.HasIndex(card => card.BoardId);
            builder.HasIndex(card => new { card.ColumnId, card.Position });
            builder.HasIndex(card => card.AssigneeId);
            builder.HasOne<Board>()
                .WithMany()
                .HasForeignKey(card => card.BoardId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<BoardColumn>()
                .WithMany()
                .HasForeignKey(card => card.ColumnId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(card => card.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(card => card.AssigneeId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.OwnsMany(card => card.ChecklistItems, checklistItems =>
            {
                checklistItems.ToTable("ChecklistItems");
                checklistItems.WithOwner().HasForeignKey("TaskCardId");
                checklistItems.HasKey(item => item.Id);
                checklistItems.Property(item => item.Id).ValueGeneratedNever();
                checklistItems.Property(item => item.Title).HasMaxLength(250).IsRequired();
                checklistItems.HasIndex("TaskCardId", nameof(ChecklistItem.Position));
            });

            builder.Navigation(card => card.ChecklistItems).AutoInclude();
        });

        modelBuilder.Entity<Comment>(builder =>
        {
            builder.ToTable("Comments");
            builder.HasKey(comment => comment.Id);
            builder.Property(comment => comment.Content).HasMaxLength(4000).IsRequired();
            builder.HasIndex(comment => comment.CardId);
            builder.HasIndex(comment => comment.AuthorId);
            builder.HasOne<TaskCard>()
                .WithMany()
                .HasForeignKey(comment => comment.CardId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(comment => comment.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Label>(builder =>
        {
            builder.ToTable("Labels");
            builder.HasKey(label => label.Id);
            builder.Property(label => label.Name).HasMaxLength(120).IsRequired();
            builder.Property(label => label.Color).HasMaxLength(32).IsRequired();
            builder.HasIndex(label => new { label.WorkspaceId, label.Name }).IsUnique();
            builder.HasOne<Workspace>()
                .WithMany()
                .HasForeignKey(label => label.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Notification>(builder =>
        {
            builder.ToTable("Notifications");
            builder.HasKey(notification => notification.Id);
            builder.Property(notification => notification.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
            builder.Property(notification => notification.Message).HasMaxLength(1000).IsRequired();
            builder.HasIndex(notification => notification.UserId);
            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(notification => notification.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ActivityLog>(builder =>
        {
            builder.ToTable("ActivityLogs");
            builder.HasKey(activity => activity.Id);
            builder.Property(activity => activity.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
            builder.Property(activity => activity.Description).HasMaxLength(1000).IsRequired();
            builder.HasIndex(activity => activity.WorkspaceId);
            builder.HasIndex(activity => activity.BoardId);
            builder.HasIndex(activity => activity.CardId);
            builder.HasIndex(activity => activity.ActorId);
            builder.HasOne<Workspace>()
                .WithMany()
                .HasForeignKey(activity => activity.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Board>()
                .WithMany()
                .HasForeignKey(activity => activity.BoardId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne<TaskCard>()
                .WithMany()
                .HasForeignKey(activity => activity.CardId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(activity => activity.ActorId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static int HashLabelIds(List<Guid> value)
    {
        var hash = new HashCode();
        foreach (var item in value)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }

    private static List<Guid> CloneLabelIds(List<Guid> value) => value.ToList();
}
