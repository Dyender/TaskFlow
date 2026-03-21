using TaskFlow.Application.Contracts;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Infrastructure.Seeding;

public static class TaskFlowSeeder
{
    public static void Seed(ITaskFlowRepository repository, IPasswordHasher passwordHasher, TimeProvider timeProvider)
    {
        if (repository.GetUsers().Count > 0)
        {
            return;
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var owner = new User
        {
            Name = "Alice Owner",
            Email = "alice@taskflow.local",
            PasswordHash = passwordHasher.Hash("Passw0rd!"),
            AvatarUrl = "https://api.dicebear.com/9.x/initials/svg?seed=Alice",
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };
        var admin = new User
        {
            Name = "Bob Admin",
            Email = "bob@taskflow.local",
            PasswordHash = passwordHasher.Hash("Passw0rd!"),
            AvatarUrl = "https://api.dicebear.com/9.x/initials/svg?seed=Bob",
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };
        var member = new User
        {
            Name = "Carol Member",
            Email = "carol@taskflow.local",
            PasswordHash = passwordHasher.Hash("Passw0rd!"),
            AvatarUrl = "https://api.dicebear.com/9.x/initials/svg?seed=Carol",
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

        repository.AddUser(owner);
        repository.AddUser(admin);
        repository.AddUser(member);

        var workspace = new Workspace
        {
            Name = "TaskFlow Demo Workspace",
            Description = "Demo workspace with kanban data for portfolio showcase.",
            OwnerId = owner.Id,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
            Members =
            {
                new WorkspaceMember { UserId = owner.Id, Role = WorkspaceRole.Owner, JoinedAtUtc = utcNow },
                new WorkspaceMember { UserId = admin.Id, Role = WorkspaceRole.Admin, JoinedAtUtc = utcNow },
                new WorkspaceMember { UserId = member.Id, Role = WorkspaceRole.Member, JoinedAtUtc = utcNow }
            }
        };
        repository.AddWorkspace(workspace);

        var board = new Board
        {
            WorkspaceId = workspace.Id,
            Name = "Backend Development",
            Description = "Main board for API, auth, realtime and notifications.",
            Color = "#0F766E",
            Visibility = BoardVisibility.Workspace,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
            Members =
            {
                new BoardMember { UserId = owner.Id, Role = WorkspaceRole.Admin, JoinedAtUtc = utcNow },
                new BoardMember { UserId = admin.Id, Role = WorkspaceRole.Admin, JoinedAtUtc = utcNow },
                new BoardMember { UserId = member.Id, Role = WorkspaceRole.Member, JoinedAtUtc = utcNow }
            }
        };
        repository.AddBoard(board);

        var backlog = new BoardColumn { BoardId = board.Id, Title = "Backlog", Position = 0, CreatedAtUtc = utcNow, UpdatedAtUtc = utcNow };
        var todo = new BoardColumn { BoardId = board.Id, Title = "To Do", Position = 1, CreatedAtUtc = utcNow, UpdatedAtUtc = utcNow };
        var progress = new BoardColumn { BoardId = board.Id, Title = "In Progress", Position = 2, CreatedAtUtc = utcNow, UpdatedAtUtc = utcNow };
        var review = new BoardColumn { BoardId = board.Id, Title = "Review", Position = 3, CreatedAtUtc = utcNow, UpdatedAtUtc = utcNow };
        var done = new BoardColumn { BoardId = board.Id, Title = "Done", Position = 4, CreatedAtUtc = utcNow, UpdatedAtUtc = utcNow };
        repository.AddColumn(backlog);
        repository.AddColumn(todo);
        repository.AddColumn(progress);
        repository.AddColumn(review);
        repository.AddColumn(done);

        var bugLabel = new Label { WorkspaceId = workspace.Id, Name = "Bug", Color = "#DC2626", CreatedAtUtc = utcNow, UpdatedAtUtc = utcNow };
        var backendLabel = new Label { WorkspaceId = workspace.Id, Name = "Backend", Color = "#2563EB", CreatedAtUtc = utcNow, UpdatedAtUtc = utcNow };
        var urgentLabel = new Label { WorkspaceId = workspace.Id, Name = "Urgent", Color = "#F59E0B", CreatedAtUtc = utcNow, UpdatedAtUtc = utcNow };
        repository.AddLabel(bugLabel);
        repository.AddLabel(backendLabel);
        repository.AddLabel(urgentLabel);

        var card = new TaskCard
        {
            BoardId = board.Id,
            ColumnId = progress.Id,
            Title = "Implement JWT auth and refresh flow",
            Description = "Build secure login, access token validation, and refresh sessions.",
            Priority = TaskPriority.High,
            DeadlineUtc = utcNow.AddDays(2),
            AssigneeId = admin.Id,
            AuthorId = owner.Id,
            Position = 0,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };
        card.LabelIds.AddRange([bugLabel.Id, backendLabel.Id, urgentLabel.Id]);
        card.ChecklistItems.AddRange(
        [
            new ChecklistItem { Title = "Create token service", Position = 0, IsCompleted = true, CreatedAtUtc = utcNow, UpdatedAtUtc = utcNow },
            new ChecklistItem { Title = "Protect endpoints", Position = 1, IsCompleted = true, CreatedAtUtc = utcNow, UpdatedAtUtc = utcNow },
            new ChecklistItem { Title = "Add refresh rotation", Position = 2, IsCompleted = false, CreatedAtUtc = utcNow, UpdatedAtUtc = utcNow }
        ]);
        repository.AddCard(card);

        var comment = new Comment
        {
            CardId = card.Id,
            AuthorId = member.Id,
            Content = "I can take the API integration tests after auth is stable.",
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };
        repository.AddComment(comment);

        repository.AddNotification(new Notification
        {
            UserId = admin.Id,
            Type = NotificationType.Assignment,
            Message = $"You were assigned to card '{card.Title}'.",
            RelatedEntityId = card.Id,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        });

        repository.AddActivity(new ActivityLog
        {
            WorkspaceId = workspace.Id,
            BoardId = board.Id,
            CardId = card.Id,
            ActorId = owner.Id,
            Type = ActivityType.CardCreated,
            Description = $"Created card '{card.Title}'.",
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        });

        repository.SaveChanges();
    }
}
