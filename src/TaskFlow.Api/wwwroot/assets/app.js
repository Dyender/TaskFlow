const root = document.querySelector("#app");
const toastRoot = document.querySelector("#toast-root");

const SESSION_KEY = "taskflow.session";
const UI_KEY = "taskflow.ui";

const state = {
    session: loadSession(),
    user: null,
    authMode: "login",
    route: parseRoute(),
    workspaces: [],
    currentWorkspace: null,
    currentBoard: null,
    currentCard: null,
    currentActivity: [],
    notifications: [],
    inviteResults: [],
    selectedInviteUserId: "",
    selectedWorkspaceId: loadUiState().selectedWorkspaceId,
    filters: defaultFilters(),
    ui: {
        showNewColumn: false,
        newCardColumnId: null
    },
    toasts: [],
    loading: {
        boot: true,
        board: false,
        card: false
    }
};

let refreshPromise = null;
let dragState = null;
let toastSeed = 0;

document.addEventListener("click", (event) => {
    void handleClick(event);
});

document.addEventListener("submit", (event) => {
    void handleSubmit(event);
});

document.addEventListener("change", (event) => {
    void handleChange(event);
});

document.addEventListener("dragstart", (event) => {
    handleDragStart(event);
});

document.addEventListener("dragover", (event) => {
    handleDragOver(event);
});

document.addEventListener("dragleave", (event) => {
    handleDragLeave(event);
});

document.addEventListener("drop", (event) => {
    void handleDrop(event);
});

document.addEventListener("dragend", () => {
    handleDragEnd();
});

window.addEventListener("popstate", () => {
    void syncRoute();
});

window.addEventListener("error", (event) => {
    showFatal(event.message || "Client error");
});

window.addEventListener("unhandledrejection", (event) => {
    showFatal(event.reason?.message || String(event.reason || "Unhandled promise rejection"));
});

void init();

async function init() {
    try {
        if (state.session?.accessToken) {
            await hydrateApp();
        }
    } catch (error) {
        clearSession();
        showToast(getErrorMessage(error), "error");
    } finally {
        state.loading.boot = false;
        render();
    }
}

async function hydrateApp() {
    await Promise.all([
        fetchCurrentUser(),
        fetchNotifications(),
        fetchWorkspaces()
    ]);

    await syncRoute({ silentRender: true });
}

async function syncRoute(options = {}) {
    state.route = parseRoute();

    if (!state.session?.accessToken) {
        resetWorkspaceState();
        if (!options.silentRender) {
            render();
        }
        return;
    }

    if (state.route.name === "board") {
        await loadBoard(state.route.boardId, { preserveCard: true });
    } else {
        state.currentBoard = null;
        state.currentCard = null;
        state.currentActivity = [];
        state.filters = defaultFilters();
        state.ui.showNewColumn = false;
        state.ui.newCardColumnId = null;
        await ensureWorkspaceLoaded();
    }

    if (!options.silentRender) {
        render();
    }
}

async function ensureWorkspaceLoaded() {
    if (!state.workspaces.length) {
        state.currentWorkspace = null;
        return;
    }

    if (!state.selectedWorkspaceId || !state.workspaces.some((workspace) => workspace.id === state.selectedWorkspaceId)) {
        state.selectedWorkspaceId = state.workspaces[0].id;
        saveUiState();
    }

    state.currentWorkspace = await api(`/api/workspaces/${state.selectedWorkspaceId}`);
}

async function fetchWorkspaces() {
    state.workspaces = await api("/api/workspaces");
    if (!state.selectedWorkspaceId && state.workspaces.length) {
        state.selectedWorkspaceId = state.workspaces[0].id;
        saveUiState();
    }
}

async function fetchCurrentUser() {
    state.user = await api("/api/users/me");
}

async function fetchNotifications() {
    state.notifications = await api("/api/notifications");
}

async function loadBoard(boardId, options = {}) {
    state.loading.board = true;
    if (!options.silent) {
        render();
    }

    try {
        const board = await api(`/api/boards/${boardId}`);
        const workspace = await api(`/api/workspaces/${board.workspaceId}`);

        state.currentBoard = board;
        state.currentWorkspace = workspace;
        state.selectedWorkspaceId = workspace.id;
        saveUiState();

        if (options.preserveCard && state.currentCard?.id) {
            try {
                await loadCard(state.currentCard.id, { silent: true });
            } catch {
                state.currentCard = null;
                state.currentActivity = [];
            }
        }
    } finally {
        state.loading.board = false;
    }
}

async function loadCard(cardId, options = {}) {
    state.loading.card = true;
    if (!options.silent) {
        render();
    }

    try {
        const [card, activity] = await Promise.all([
            api(`/api/cards/${cardId}`),
            api(`/api/cards/${cardId}/activity`)
        ]);

        state.currentCard = card;
        state.currentActivity = activity;
    } finally {
        state.loading.card = false;
        if (!options.silent) {
            render();
        }
    }
}

async function refreshBoardAndCard() {
    if (!state.currentBoard) {
        return;
    }

    await loadBoard(state.currentBoard.id, { preserveCard: false, silent: true });
    if (state.currentCard?.id) {
        await loadCard(state.currentCard.id, { silent: true });
    }
    render();
}

async function navigate(path, options = {}) {
    const nextPath = path || "/";
    if (window.location.pathname !== nextPath) {
        const method = options.replace ? "replaceState" : "pushState";
        window.history[method]({}, "", nextPath);
    }

    await syncRoute();
}

async function handleClick(event) {
    if (event.target.classList.contains("modal-backdrop")) {
        state.currentCard = null;
        state.currentActivity = [];
        render();
        return;
    }

    const actionEl = event.target.closest("[data-action]");
    if (!actionEl) {
        return;
    }

    const action = actionEl.dataset.action;

    switch (action) {
        case "auth-tab":
            state.authMode = actionEl.dataset.mode || "login";
            render();
            return;
        case "demo-login":
            state.authMode = "login";
            render();
            requestAnimationFrame(() => {
                const emailInput = document.querySelector('[data-form="login"] [name="email"]');
                const passwordInput = document.querySelector('[data-form="login"] [name="password"]');
                if (emailInput) {
                    emailInput.value = actionEl.dataset.email || "";
                }
                if (passwordInput) {
                    passwordInput.value = "Passw0rd!";
                }
            });
            return;
        case "logout":
            clearSession();
            render();
            return;
        case "home":
            await navigate("/");
            return;
        case "select-workspace":
            state.selectedWorkspaceId = actionEl.dataset.workspaceId || null;
            saveUiState();
            await navigate("/");
            return;
        case "open-board":
            await navigate(`/board/${actionEl.dataset.boardId}`);
            return;
        case "open-card":
            await runTask(async () => {
                await loadCard(actionEl.dataset.cardId);
            });
            return;
        case "close-card":
            state.currentCard = null;
            state.currentActivity = [];
            render();
            return;
        case "toggle-new-column":
            state.ui.showNewColumn = !state.ui.showNewColumn;
            render();
            return;
        case "toggle-new-card":
            state.ui.newCardColumnId = state.ui.newCardColumnId === actionEl.dataset.columnId
                ? null
                : actionEl.dataset.columnId;
            render();
            return;
        case "cancel-new-card":
            state.ui.newCardColumnId = null;
            render();
            return;
        case "cancel-new-column":
            state.ui.showNewColumn = false;
            render();
            return;
        case "rename-column":
            await renameColumn(actionEl.dataset.columnId);
            return;
        case "delete-column":
            await deleteColumn(actionEl.dataset.columnId);
            return;
        case "toggle-label-filter":
            toggleLabelFilter(actionEl.dataset.labelId);
            render();
            return;
        case "reset-filters":
            state.filters = defaultFilters();
            render();
            return;
        case "pick-user":
            state.selectedInviteUserId = actionEl.dataset.userId || "";
            render();
            return;
        case "open-notification":
            await openNotification(actionEl.dataset.notificationId);
            return;
        case "mark-read":
            await runTask(async () => {
                await api(`/api/notifications/${actionEl.dataset.notificationId}/read`, { method: "PUT" });
                await fetchNotifications();
            }, "Notification updated.");
            return;
        case "toggle-card-archive":
            await toggleCardArchive();
            return;
        case "delete-card":
            await deleteCurrentCard();
            return;
        case "rename-check-item":
            await renameChecklistItem(actionEl.dataset.itemId, actionEl.dataset.currentTitle || "");
            return;
        case "delete-check-item":
            await deleteChecklistItem(actionEl.dataset.itemId);
            return;
        case "edit-comment":
            await editComment(actionEl.dataset.commentId, actionEl.dataset.currentContent || "");
            return;
        case "delete-comment":
            await deleteComment(actionEl.dataset.commentId);
            return;
        default:
            return;
    }
}

async function handleSubmit(event) {
    const form = event.target.closest("form[data-form]");
    if (!form) {
        return;
    }

    event.preventDefault();
    const formData = new FormData(form);
    const formName = form.dataset.form;

    switch (formName) {
        case "login":
            await runTask(async () => {
                const session = await api("/api/auth/login", {
                    method: "POST",
                    auth: false,
                    body: {
                        email: String(formData.get("email") || ""),
                        password: String(formData.get("password") || "")
                    }
                });
                acceptSession(session);
                await hydrateApp();
            }, "Signed in.");
            return;
        case "register":
            await runTask(async () => {
                const session = await api("/api/auth/register", {
                    method: "POST",
                    auth: false,
                    body: {
                        name: String(formData.get("name") || ""),
                        email: String(formData.get("email") || ""),
                        password: String(formData.get("password") || ""),
                        avatarUrl: normalizeNullable(formData.get("avatarUrl"))
                    }
                });
                acceptSession(session);
                await hydrateApp();
            }, "Account created.");
            return;
        case "create-workspace":
            await runTask(async () => {
                const workspace = await api("/api/workspaces", {
                    method: "POST",
                    body: {
                        name: String(formData.get("name") || ""),
                        description: String(formData.get("description") || "")
                    }
                });
                await fetchWorkspaces();
                state.selectedWorkspaceId = workspace.id;
                saveUiState();
                await navigate("/", { replace: true });
            }, "Workspace created.");
            return;
        case "create-board":
            if (!state.currentWorkspace) {
                showToast("Select a workspace first.", "error");
                return;
            }

            await runTask(async () => {
                const board = await api(`/api/workspaces/${state.currentWorkspace.id}/boards`, {
                    method: "POST",
                    body: {
                        name: String(formData.get("name") || ""),
                        description: String(formData.get("description") || ""),
                        color: normalizeNullable(formData.get("color")) || "#157A6E",
                        visibility: String(formData.get("visibility") || "Workspace"),
                        createDefaultColumns: formData.get("createDefaultColumns") === "on"
                    }
                });
                await fetchWorkspaces();
                await navigate(`/board/${board.id}`);
            }, "Board created.");
            return;
        case "find-user":
            await runTask(async () => {
                state.inviteResults = await api(`/api/users?search=${encodeURIComponent(String(formData.get("query") || "").trim())}`);
            });
            return;
        case "invite-user":
            if (!state.currentWorkspace) {
                return;
            }

            await runTask(async () => {
                await api(`/api/workspaces/${state.currentWorkspace.id}/members`, {
                    method: "POST",
                    body: {
                        userId: String(formData.get("userId") || state.selectedInviteUserId || ""),
                        role: String(formData.get("role") || "Member")
                    }
                });
                state.selectedInviteUserId = "";
                state.inviteResults = [];
                state.currentWorkspace = await api(`/api/workspaces/${state.currentWorkspace.id}`);
            }, "Workspace member added.");
            return;
        case "create-label":
            if (!state.currentWorkspace) {
                return;
            }

            await runTask(async () => {
                await api(`/api/workspaces/${state.currentWorkspace.id}/labels`, {
                    method: "POST",
                    body: {
                        name: String(formData.get("name") || ""),
                        color: normalizeNullable(formData.get("color")) || "#F2855E"
                    }
                });

                if (state.currentBoard?.workspaceId === state.currentWorkspace.id) {
                    await loadBoard(state.currentBoard.id, { preserveCard: true, silent: true });
                }
            }, "Label created.");
            return;
        case "update-board":
            if (!state.currentBoard) {
                return;
            }

            await runTask(async () => {
                await api(`/api/boards/${state.currentBoard.id}`, {
                    method: "PUT",
                    body: {
                        name: normalizeNullable(formData.get("name")),
                        description: String(formData.get("description") || ""),
                        color: normalizeNullable(formData.get("color")),
                        visibility: String(formData.get("visibility") || state.currentBoard.visibility)
                    }
                });
                await loadBoard(state.currentBoard.id, { preserveCard: true, silent: true });
            }, "Board updated.");
            return;
        case "add-board-member":
            if (!state.currentBoard) {
                return;
            }

            await runTask(async () => {
                await api(`/api/boards/${state.currentBoard.id}/members`, {
                    method: "POST",
                    body: {
                        userId: String(formData.get("userId") || ""),
                        role: String(formData.get("role") || "Member")
                    }
                });
                await loadBoard(state.currentBoard.id, { preserveCard: true, silent: true });
            }, "Board member added.");
            return;
        case "apply-filters":
            state.filters = {
                search: String(formData.get("search") || "").trim(),
                assigneeId: String(formData.get("assigneeId") || ""),
                includeArchived: formData.get("includeArchived") === "on",
                labelIds: state.filters.labelIds
            };
            render();
            return;
        case "create-column":
            if (!state.currentBoard) {
                return;
            }

            await runTask(async () => {
                await api(`/api/boards/${state.currentBoard.id}/columns`, {
                    method: "POST",
                    body: {
                        title: String(formData.get("title") || "")
                    }
                });
                state.ui.showNewColumn = false;
                await loadBoard(state.currentBoard.id, { preserveCard: true, silent: true });
            }, "Column created.");
            return;
        case "create-card":
            await runTask(async () => {
                const card = await api(`/api/columns/${formData.get("columnId")}/cards`, {
                    method: "POST",
                    body: {
                        title: String(formData.get("title") || ""),
                        description: "",
                        priority: String(formData.get("priority") || "Medium"),
                        deadlineUtc: null,
                        assigneeId: normalizeNullable(formData.get("assigneeId")),
                        position: null
                    }
                });
                state.ui.newCardColumnId = null;
                await loadBoard(state.currentBoard.id, { preserveCard: false, silent: true });
                await loadCard(card.id, { silent: true });
            }, "Card created.");
            return;
        case "save-card":
            if (!state.currentCard) {
                return;
            }

            await runTask(async () => {
                const deadlineValue = String(formData.get("deadlineUtc") || "").trim();
                await api(`/api/cards/${state.currentCard.id}`, {
                    method: "PUT",
                    body: {
                        title: normalizeNullable(formData.get("title")),
                        description: String(formData.get("description") || ""),
                        priority: String(formData.get("priority") || state.currentCard.priority),
                        deadlineUtc: deadlineValue ? new Date(deadlineValue).toISOString() : undefined,
                        clearDeadline: !deadlineValue && Boolean(state.currentCard.deadlineUtc)
                    }
                });
                await api(`/api/cards/${state.currentCard.id}/assign`, {
                    method: "PUT",
                    body: {
                        assigneeId: normalizeNullable(formData.get("assigneeId"))
                    }
                });
                await api(`/api/cards/${state.currentCard.id}/labels`, {
                    method: "PUT",
                    body: {
                        labelIds: formData.getAll("labelId").map(String)
                    }
                });
                await refreshBoardAndCard();
            }, "Card saved.");
            return;
        case "add-check-item":
            if (!state.currentCard) {
                return;
            }

            await runTask(async () => {
                await api(`/api/cards/${state.currentCard.id}/checklist`, {
                    method: "POST",
                    body: {
                        title: String(formData.get("title") || "")
                    }
                });
                await refreshBoardAndCard();
            }, "Checklist item added.");
            return;
        case "add-comment":
            if (!state.currentCard) {
                return;
            }

            await runTask(async () => {
                await api(`/api/cards/${state.currentCard.id}/comments`, {
                    method: "POST",
                    body: {
                        content: String(formData.get("content") || "")
                    }
                });
                await refreshBoardAndCard();
            }, "Comment added.");
            return;
    }
}

async function handleChange(event) {
    const toggle = event.target.closest("[data-check-item]");
    if (!toggle || !state.currentCard) {
        return;
    }

    await runTask(async () => {
        await api(`/api/checklist-items/${toggle.dataset.itemId}`, {
            method: "PUT",
            body: {
                isCompleted: toggle.checked
            }
        });
        await refreshBoardAndCard();
    });
}

function handleDragStart(event) {
    const card = event.target.closest("[data-drag-card]");
    const column = event.target.closest("[data-drag-column]");

    if (card && canContributeBoard() && !isBoardFiltered()) {
        dragState = {
            type: "card",
            id: card.dataset.cardId
        };
        card.classList.add("is-drag-source");
        document.querySelectorAll(".column-list").forEach((node) => node.classList.add("is-dragging-card"));
        event.dataTransfer.effectAllowed = "move";
        return;
    }

    if (column && canManageBoard() && !isBoardFiltered()) {
        dragState = {
            type: "column",
            id: column.dataset.columnId
        };
        column.classList.add("is-drag-source");
        document.querySelector(".board-columns")?.classList.add("is-dragging-column");
        event.dataTransfer.effectAllowed = "move";
    }
}

function handleDragOver(event) {
    const slot = event.target.closest(".drop-slot");
    if (!slot || !dragState || slot.dataset.dropType !== dragState.type) {
        return;
    }

    event.preventDefault();
    slot.classList.add("is-active");
}

function handleDragLeave(event) {
    const slot = event.target.closest(".drop-slot");
    if (!slot) {
        return;
    }

    if (!slot.contains(event.relatedTarget)) {
        slot.classList.remove("is-active");
    }
}

async function handleDrop(event) {
    const slot = event.target.closest(".drop-slot");
    if (!slot || !dragState || slot.dataset.dropType !== dragState.type) {
        return;
    }

    event.preventDefault();
    slot.classList.remove("is-active");

    if (dragState.type === "card") {
        await moveCard(dragState.id, slot.dataset.columnId, Number(slot.dataset.position || 0));
    } else {
        await moveColumn(dragState.id, Number(slot.dataset.position || 0));
    }

    handleDragEnd();
}

function handleDragEnd() {
    dragState = null;
    document.querySelectorAll(".is-drag-source").forEach((node) => node.classList.remove("is-drag-source"));
    document.querySelectorAll(".column-list").forEach((node) => node.classList.remove("is-dragging-card"));
    document.querySelector(".board-columns")?.classList.remove("is-dragging-column");
    document.querySelectorAll(".drop-slot.is-active").forEach((node) => node.classList.remove("is-active"));
}

async function moveCard(cardId, targetColumnId, targetPosition) {
    await runTask(async () => {
        await api(`/api/cards/${cardId}/move`, {
            method: "PUT",
            body: {
                targetColumnId,
                targetPosition
            }
        });
        await loadBoard(state.currentBoard.id, { preserveCard: true, silent: true });
    }, "Card moved.");
}

async function moveColumn(columnId, targetPosition) {
    const visibleIds = getVisibleColumns().map((column) => column.id);
    const hiddenIds = [...state.currentBoard.columns]
        .sort((left, right) => left.position - right.position)
        .filter((column) => !visibleIds.includes(column.id))
        .map((column) => column.id);

    const reordered = visibleIds.filter((id) => id !== columnId);
    reordered.splice(targetPosition, 0, columnId);

    await runTask(async () => {
        await api("/api/columns/reorder", {
            method: "PUT",
            body: {
                boardId: state.currentBoard.id,
                columns: [...reordered, ...hiddenIds].map((id, index) => ({
                    columnId: id,
                    position: index
                }))
            }
        });
        await loadBoard(state.currentBoard.id, { preserveCard: true, silent: true });
    }, "Columns reordered.");
}

async function renameColumn(columnId) {
    const column = state.currentBoard?.columns.find((item) => item.id === columnId);
    if (!column) {
        return;
    }

    const title = window.prompt("Column title", column.title);
    if (title === null) {
        return;
    }

    await runTask(async () => {
        await api(`/api/columns/${columnId}`, {
            method: "PUT",
            body: {
                title
            }
        });
        await loadBoard(state.currentBoard.id, { preserveCard: true, silent: true });
    }, "Column updated.");
}

async function deleteColumn(columnId) {
    if (!window.confirm("Delete this column? It must be empty.")) {
        return;
    }

    await runTask(async () => {
        await api(`/api/columns/${columnId}`, {
            method: "DELETE"
        });
        await loadBoard(state.currentBoard.id, { preserveCard: true, silent: true });
    }, "Column deleted.");
}

async function openNotification(notificationId) {
    const notification = state.notifications.find((item) => item.id === notificationId);
    if (!notification?.relatedEntityId) {
        return;
    }

    if (notification.type === "AddedToBoard") {
        await navigate(`/board/${notification.relatedEntityId}`);
        return;
    }

    if (notification.type === "AddedToWorkspace") {
        state.selectedWorkspaceId = notification.relatedEntityId;
        saveUiState();
        await navigate("/");
    }
}

async function toggleCardArchive() {
    if (!state.currentCard) {
        return;
    }

    const nextValue = !state.currentCard.isArchived;
    await runTask(async () => {
        await api(`/api/cards/${state.currentCard.id}`, {
            method: "PUT",
            body: {
                isArchived: nextValue
            }
        });
        await refreshBoardAndCard();
    }, nextValue ? "Card archived." : "Card restored.");
}

async function deleteCurrentCard() {
    if (!state.currentCard || !window.confirm("Delete this card?")) {
        return;
    }

    const cardId = state.currentCard.id;
    await runTask(async () => {
        await api(`/api/cards/${cardId}`, { method: "DELETE" });
        state.currentCard = null;
        state.currentActivity = [];
        await loadBoard(state.currentBoard.id, { preserveCard: false, silent: true });
    }, "Card deleted.");
}

async function renameChecklistItem(itemId, currentTitle) {
    const title = window.prompt("Checklist item", currentTitle);
    if (title === null) {
        return;
    }

    await runTask(async () => {
        await api(`/api/checklist-items/${itemId}`, {
            method: "PUT",
            body: {
                title
            }
        });
        await refreshBoardAndCard();
    }, "Checklist item updated.");
}

async function deleteChecklistItem(itemId) {
    if (!window.confirm("Delete this checklist item?")) {
        return;
    }

    await runTask(async () => {
        await api(`/api/checklist-items/${itemId}`, { method: "DELETE" });
        await refreshBoardAndCard();
    }, "Checklist item deleted.");
}

async function editComment(commentId, currentContent) {
    const content = window.prompt("Comment", currentContent);
    if (content === null) {
        return;
    }

    await runTask(async () => {
        await api(`/api/comments/${commentId}`, {
            method: "PUT",
            body: {
                content
            }
        });
        await refreshBoardAndCard();
    }, "Comment updated.");
}

async function deleteComment(commentId) {
    if (!window.confirm("Delete this comment?")) {
        return;
    }

    await runTask(async () => {
        await api(`/api/comments/${commentId}`, { method: "DELETE" });
        await refreshBoardAndCard();
    }, "Comment deleted.");
}

async function runTask(task, successMessage) {
    try {
        await task();
        if (successMessage) {
            showToast(successMessage, "success");
        }
    } catch (error) {
        showToast(getErrorMessage(error), "error");
    } finally {
        render();
    }
}

function render() {
    document.title = state.currentBoard ? `${state.currentBoard.name} - TaskFlow` : "TaskFlow";

    if (state.loading.boot) {
        root.innerHTML = renderLoading();
        renderToasts();
        return;
    }

    if (!state.session?.accessToken) {
        root.innerHTML = renderAuth();
        renderToasts();
        return;
    }

    root.innerHTML = renderShell();
    renderToasts();
}

function renderLoading() {
    return `
        <section class="screen-loader">
            <div class="screen-loader__card stack stack--lg">
                <div class="brand">
                    <span class="brand__mark">TF</span>
                    <strong>TaskFlow</strong>
                </div>
                <div>
                    <p class="eyebrow">Loading</p>
                    <h1 style="margin:0;font-family:Georgia,'Times New Roman',serif;">Starting app...</h1>
                </div>
            </div>
        </section>
    `;
}

function renderAuth() {
    return `
        <section class="auth-shell">
            <section class="auth-hero">
                <div class="stack stack--lg">
                    <div class="brand">
                        <span class="brand__mark">TF</span>
                        <strong>TaskFlow</strong>
                    </div>
                    <div>
                        <p class="eyebrow">Trello-like client for your API</p>
                        <h1>Boards, cards and team flow in one place.</h1>
                        <p>This UI works on top of your existing backend endpoints and turns them into a usable kanban app.</p>
                    </div>
                </div>
                <div class="hero-grid">
                    <div class="hero-grid__card">Auth<strong>Sessions</strong></div>
                    <div class="hero-grid__card">Boards<strong>Columns & cards</strong></div>
                    <div class="hero-grid__card">Details<strong>Comments & checklist</strong></div>
                </div>
            </section>

            <section class="auth-panel">
                <div class="section-head">
                    <div>
                        <h2>Welcome</h2>
                        <p>Use a demo account or create a new user.</p>
                    </div>
                </div>

                <div class="tabs">
                    <button type="button" class="tab ${state.authMode === "login" ? "is-active" : ""}" data-action="auth-tab" data-mode="login">Sign in</button>
                    <button type="button" class="tab ${state.authMode === "register" ? "is-active" : ""}" data-action="auth-tab" data-mode="register">Register</button>
                </div>

                ${state.authMode === "login" ? renderLoginForm() : renderRegisterForm()}
            </section>
        </section>
    `;
}

function renderLoginForm() {
    return `
        <div class="form-section stack stack--lg">
            <form class="stack" data-form="login">
                <div class="field">
                    <label>Email</label>
                    <input class="input" name="email" type="email" required>
                </div>
                <div class="field">
                    <label>Password</label>
                    <input class="input" name="password" type="password" required>
                </div>
                <button class="button button--primary" type="submit">Sign in</button>
            </form>

            <div class="stack">
                <h3 style="margin:0;">Demo users</h3>
                <button type="button" class="button button--ghost" data-action="demo-login" data-email="alice@taskflow.local">Alice</button>
                <button type="button" class="button button--ghost" data-action="demo-login" data-email="bob@taskflow.local">Bob</button>
                <button type="button" class="button button--ghost" data-action="demo-login" data-email="carol@taskflow.local">Carol</button>
            </div>
        </div>
    `;
}

function renderRegisterForm() {
    return `
        <form class="form-section stack" data-form="register">
            <div class="field-inline">
                <div class="field">
                    <label>Name</label>
                    <input class="input" name="name" type="text" required>
                </div>
                <div class="field">
                    <label>Email</label>
                    <input class="input" name="email" type="email" required>
                </div>
            </div>
            <div class="field-inline">
                <div class="field">
                    <label>Password</label>
                    <input class="input" name="password" type="password" minlength="8" required>
                </div>
                <div class="field">
                    <label>Avatar URL</label>
                    <input class="input" name="avatarUrl" type="url">
                </div>
            </div>
            <button class="button button--alt" type="submit">Create account</button>
        </form>
    `;
}

function renderShell() {
    const isBoardRoute = state.route.name === "board";
    return `
        <div class="layout ${isBoardRoute ? "layout--board" : "layout--workspace"}">
            <header class="app-header">
                <div class="header-cluster">
                    <button type="button" class="brand-button" data-action="home" aria-label="Open workspace home">
                        <span class="brand-button__mark" aria-hidden="true">
                            <span></span>
                            <span></span>
                        </span>
                        <span class="brand-button__label">TaskFlow</span>
                    </button>
                    <div class="header-title">
                        <strong>${escapeHtml(state.currentBoard?.name || state.currentWorkspace?.name || "TaskFlow")}</strong>
                        <span>${escapeHtml(renderHeaderSubtitle())}</span>
                    </div>
                </div>
                <div class="toolbar">
                    <span class="status-pill">${state.notifications.filter((n) => !n.isRead).length} unread</span>
                    <span class="status-pill status-pill--accent">${escapeHtml(state.user?.name || "User")}</span>
                    <button type="button" class="button button--danger button--sm" data-action="logout">Logout</button>
                </div>
            </header>

            <div class="main-grid ${isBoardRoute ? "main-grid--board" : ""}">
                ${renderSidebar()}
                <main class="main-stack ${isBoardRoute ? "main-stack--board" : ""}">
                    ${state.route.name === "board" ? renderBoardRoute() : renderWorkspacePage()}
                </main>
            </div>

            ${renderCardModal()}
        </div>
    `;
}

function renderSidebar() {
    return `
        <aside class="sidebar">
            <div class="surface" style="padding:1rem;">
                <div class="section-head">
                    <div>
                        <h3>Workspaces</h3>
                        <p>Switch team areas here.</p>
                    </div>
                </div>
                <div class="sidebar__list">
                    ${state.workspaces.length
                        ? state.workspaces.map((workspace) => `
                            <button type="button" class="workspace-link ${workspace.id === state.selectedWorkspaceId && state.route.name !== "board" ? "is-active" : ""}" data-action="select-workspace" data-workspace-id="${escapeAttribute(workspace.id)}">
                                <strong>${escapeHtml(workspace.name)}</strong>
                                <span>${escapeHtml(workspace.description || "No description")}</span>
                                <em>${workspace.boardCount} boards</em>
                            </button>
                        `).join("")
                        : '<div class="empty-state"><strong>No workspaces yet</strong><p>Create your first workspace below.</p></div>'}
                </div>
            </div>

            <div class="surface" style="padding:1rem;">
                <div class="section-head">
                    <div>
                        <h3>Create workspace</h3>
                        <p>Start a new team space.</p>
                    </div>
                </div>
                <form class="stack" data-form="create-workspace">
                    <div class="field">
                        <label>Name</label>
                        <input class="input" name="name" type="text" required>
                    </div>
                    <div class="field">
                        <label>Description</label>
                        <textarea class="textarea" name="description"></textarea>
                    </div>
                    <button class="button button--primary" type="submit">Create workspace</button>
                </form>
            </div>
        </aside>
    `;
}

function renderWorkspacePage() {
    if (!state.currentWorkspace) {
        return `
            <section class="surface empty-state">
                <strong>No workspace selected</strong>
                <p>Create a workspace to start using the app.</p>
            </section>
        `;
    }

    const boards = state.currentWorkspace.boards.filter((board) => !board.isArchived);
    return `
        <section class="surface hero-surface">
            <div class="hero-content">
                <div class="section-head" style="margin-bottom:0;">
                    <div>
                        <p class="eyebrow">Workspace</p>
                        <h1 class="hero-title">${escapeHtml(state.currentWorkspace.name)}</h1>
                        <p class="hero-subtitle">${escapeHtml(state.currentWorkspace.description || "Boards, members and labels live here.")}</p>
                    </div>
                </div>
                <div class="stats-grid">
                    <div class="stat-card">Boards<strong>${boards.length}</strong></div>
                    <div class="stat-card">Members<strong>${state.currentWorkspace.members.length}</strong></div>
                    <div class="stat-card">Unread<strong>${state.notifications.filter((item) => !item.isRead).length}</strong></div>
                </div>
            </div>
        </section>

        <section class="dashboard-grid">
            <section class="surface">
                <div class="section-head">
                    <div>
                        <h2>Boards</h2>
                        <p>Open a board or create a new one.</p>
                    </div>
                </div>
                <div class="board-gallery">
                    ${boards.map((board) => renderBoardTile(board)).join("")}
                </div>
                <form class="stack" data-form="create-board" style="margin-top:1rem;">
                    <div class="field-inline">
                        <div class="field">
                            <label>Board name</label>
                            <input class="input" name="name" type="text" required>
                        </div>
                        <div class="field">
                            <label>Color</label>
                            <input class="input" name="color" type="text" placeholder="#157A6E">
                        </div>
                    </div>
                    <div class="field">
                        <label>Description</label>
                        <textarea class="textarea" name="description"></textarea>
                    </div>
                    <div class="field">
                        <label>Visibility</label>
                        <select class="select" name="visibility">
                            ${renderOptions([["Workspace", "Workspace"], ["Private", "Private"]], "Workspace")}
                        </select>
                    </div>
                    <label class="toggle-chip">
                        <input type="checkbox" name="createDefaultColumns" checked>
                        Create default columns
                    </label>
                    <button class="button button--alt" type="submit">Create board</button>
                </form>
            </section>

            <div class="main-stack">
                <section class="surface">
                    <div class="section-head">
                        <div>
                            <h3>Members</h3>
                            <p>Invite users by search.</p>
                        </div>
                    </div>
                    <div class="member-list" style="margin-bottom:1rem;">
                        ${state.currentWorkspace.members.map(renderMemberCard).join("")}
                    </div>
                    ${renderInviteSection()}
                </section>

                <section class="surface">
                    <div class="section-head">
                        <div>
                            <h3>Notifications</h3>
                            <p>Latest updates for your account.</p>
                        </div>
                    </div>
                    <div class="notification-list">
                        ${state.notifications.slice(0, 8).map(renderNotificationCard).join("") || '<div class="empty-state"><strong>No notifications</strong><p>You are all caught up.</p></div>'}
                    </div>
                </section>
            </div>
        </section>
    `;
}

function renderInviteSection() {
    return `
        <div class="stack">
            <form class="stack" data-form="find-user">
                <div class="field">
                    <label>Find user</label>
                    <input class="input" name="query" type="text" placeholder="name or email">
                </div>
                <button class="button button--ghost" type="submit">Search</button>
            </form>

            ${state.inviteResults.length ? `
                <div class="stack">
                    ${state.inviteResults.map((user) => `
                        <button type="button" class="search-result" data-action="pick-user" data-user-id="${escapeAttribute(user.id)}">
                            <div class="search-result__head">
                                <div>
                                    <strong>${escapeHtml(user.name)}</strong>
                                    <span>${escapeHtml(user.email)}</span>
                                </div>
                                <span class="badge">${state.selectedInviteUserId === user.id ? "Picked" : "Choose"}</span>
                            </div>
                        </button>
                    `).join("")}
                </div>
            ` : ""}

            <form class="stack" data-form="invite-user">
                <input type="hidden" name="userId" value="${escapeAttribute(state.selectedInviteUserId)}">
                <div class="field">
                    <label>Workspace role</label>
                    <select class="select" name="role">
                        ${renderOptions([["Viewer", "Viewer"], ["Member", "Member"], ["Admin", "Admin"]], "Member")}
                    </select>
                </div>
                <button class="button button--ghost" type="submit" ${state.selectedInviteUserId ? "" : "disabled"}>Add member</button>
            </form>
        </div>
    `;
}

function renderBoardTile(board) {
    return `
        <button type="button" class="board-tile" data-action="open-board" data-board-id="${escapeAttribute(board.id)}" style="--board-color:${escapeAttribute(normalizeColor(board.color, "#157A6E"))};">
            <div class="chip-row">
                <span class="badge">${escapeHtml(board.visibility)}</span>
            </div>
            <h3>${escapeHtml(board.name)}</h3>
            <p>${escapeHtml(board.description || "No description")}</p>
        </button>
    `;
}

function renderBoardPage() {
    if (!state.currentBoard) {
        return `
            <section class="surface empty-state">
                <strong>Board not found</strong>
                <p>Go back to the workspace and open another board.</p>
            </section>
        `;
    }

    return `
        <section class="surface hero-surface" style="background:
            linear-gradient(120deg, rgba(19, 41, 45, 0.88), rgba(21, 122, 110, 0.72)),
            linear-gradient(180deg, ${normalizeColor(state.currentBoard.color, "#157A6E")}66, transparent);">
            <div class="hero-content">
                <div class="section-head" style="margin-bottom:0;">
                    <div>
                        <p class="eyebrow">${escapeHtml(state.currentWorkspace?.name || "Workspace")} · ${escapeHtml(state.currentBoard.visibility)}</p>
                        <h1 class="hero-title">${escapeHtml(state.currentBoard.name)}</h1>
                        <p class="hero-subtitle">${escapeHtml(state.currentBoard.description || "Cards, checklist, comments and history are all connected to the backend.")}</p>
                    </div>
                </div>
            </div>
        </section>

        <section class="surface">
            <div class="section-head">
                <div>
                    <h2>Board settings</h2>
                    <p>Update board details, labels and board membership.</p>
                </div>
            </div>
            <div class="dashboard-grid">
                <form class="stack" data-form="update-board">
                    <div class="field">
                        <label>Name</label>
                        <input class="input" name="name" type="text" value="${escapeAttribute(state.currentBoard.name)}">
                    </div>
                    <div class="field">
                        <label>Description</label>
                        <textarea class="textarea" name="description">${escapeHtml(state.currentBoard.description || "")}</textarea>
                    </div>
                    <div class="field-inline">
                        <div class="field">
                            <label>Color</label>
                            <input class="input" name="color" type="text" value="${escapeAttribute(state.currentBoard.color)}">
                        </div>
                        <div class="field">
                            <label>Visibility</label>
                            <select class="select" name="visibility">
                                ${renderOptions([["Workspace", "Workspace"], ["Private", "Private"]], state.currentBoard.visibility)}
                            </select>
                        </div>
                    </div>
                    <button class="button button--primary" type="submit">Save board</button>
                </form>

                <div class="main-stack">
                    <section class="detail-card">
                        <h3>Board members</h3>
                        <div class="member-list" style="margin-bottom:1rem;">
                            ${state.currentBoard.members.map(renderMemberCard).join("") || '<p class="muted">No explicit board members yet.</p>'}
                        </div>
                        ${renderBoardMemberForm()}
                    </section>

                    <section class="detail-card">
                        <h3>Labels</h3>
                        <div class="label-cloud" style="margin-bottom:1rem;">
                            ${state.currentBoard.labels.map(renderLabelChip).join("") || '<span class="muted">No labels yet.</span>'}
                        </div>
                        <form class="stack" data-form="create-label">
                            <div class="field-inline">
                                <div class="field">
                                    <label>Name</label>
                                    <input class="input" name="name" type="text" required>
                                </div>
                                <div class="field">
                                    <label>Color</label>
                                    <input class="input" name="color" type="text" placeholder="#F2855E">
                                </div>
                            </div>
                            <button class="button button--ghost" type="submit">Create label</button>
                        </form>
                    </section>
                </div>
            </div>
        </section>

        <section class="surface board-toolbar">
            <form class="filter-bar" data-form="apply-filters">
                <div class="field">
                    <label>Search</label>
                    <input class="input" name="search" type="text" value="${escapeAttribute(state.filters.search)}" placeholder="search cards">
                </div>
                <div class="field">
                    <label>Assignee</label>
                    <select class="select" name="assigneeId">
                        ${renderOptions([["", "Anyone"], ...getAssignableMembers().map((member) => [member.userId, member.name])], state.filters.assigneeId)}
                    </select>
                </div>
                <label class="toggle-chip">
                    <input type="checkbox" name="includeArchived" ${state.filters.includeArchived ? "checked" : ""}>
                    Show archived
                </label>
                <div class="inline-actions">
                    <button class="button button--primary" type="submit">Apply</button>
                    <button class="button button--ghost" type="button" data-action="reset-filters">Reset</button>
                </div>
            </form>
            <div class="label-cloud">
                ${state.currentBoard.labels.map(renderFilterLabelChip).join("")}
            </div>
        </section>

        <section class="board-scroll">
            <div class="board-columns">
                ${renderBoardColumns()}
            </div>
        </section>
    `;
}

function renderBoardRoute() {
    if (!state.currentBoard) {
        return `
            <section class="surface empty-state">
                <strong>Board not found</strong>
                <p>Go back to the workspace and open another board.</p>
            </section>
        `;
    }

    const visibleColumns = getVisibleColumns();
    const visibleCardCount = visibleColumns.reduce((total, column) => total + getVisibleCards(column).length, 0);
    const boardAccent = normalizeColor(state.currentBoard.color, "#579DFF");

    return `
        <div class="board-page" style="--board-accent:${escapeAttribute(boardAccent)};">
            <section class="surface board-banner">
                <div class="board-banner__meta">
                    <span class="status-pill">${escapeHtml(state.currentWorkspace?.name || "Workspace")}</span>
                    <span class="status-pill">${escapeHtml(state.currentBoard.visibility)}</span>
                    <span class="status-pill">${visibleColumns.length} lists</span>
                    <span class="status-pill">${visibleCardCount} cards</span>
                </div>
                <div class="board-banner__body">
                    <div>
                        <h1 class="board-banner__title">${escapeHtml(state.currentBoard.name)}</h1>
                        <p class="board-banner__description">${escapeHtml(state.currentBoard.description || "Organize work across lists, cards, checklist items and comments.")}</p>
                    </div>
                </div>
            </section>

            <section class="surface board-admin">
                <div class="section-head">
                    <div>
                        <h2>Board settings</h2>
                        <p>Update board details, labels and board membership.</p>
                    </div>
                </div>
                <div class="dashboard-grid board-admin-grid">
                    <form class="stack" data-form="update-board">
                        <div class="field">
                            <label>Name</label>
                            <input class="input" name="name" type="text" value="${escapeAttribute(state.currentBoard.name)}">
                        </div>
                        <div class="field">
                            <label>Description</label>
                            <textarea class="textarea" name="description">${escapeHtml(state.currentBoard.description || "")}</textarea>
                        </div>
                        <div class="field-inline">
                            <div class="field">
                                <label>Color</label>
                                <input class="input" name="color" type="text" value="${escapeAttribute(state.currentBoard.color)}">
                            </div>
                            <div class="field">
                                <label>Visibility</label>
                                <select class="select" name="visibility">
                                    ${renderOptions([["Workspace", "Workspace"], ["Private", "Private"]], state.currentBoard.visibility)}
                                </select>
                            </div>
                        </div>
                        <button class="button button--primary" type="submit">Save board</button>
                    </form>

                    <div class="main-stack">
                        <section class="detail-card">
                            <h3>Board members</h3>
                            <div class="member-list" style="margin-bottom:1rem;">
                                ${state.currentBoard.members.map(renderMemberCard).join("") || '<p class="muted">No explicit board members yet.</p>'}
                            </div>
                            ${renderBoardMemberFormModern()}
                        </section>

                        <section class="detail-card">
                            <h3>Labels</h3>
                            <div class="label-cloud" style="margin-bottom:1rem;">
                                ${state.currentBoard.labels.map(renderLabelChip).join("") || '<span class="muted">No labels yet.</span>'}
                            </div>
                            <form class="stack" data-form="create-label">
                                <div class="field-inline">
                                    <div class="field">
                                        <label>Name</label>
                                        <input class="input" name="name" type="text" required>
                                    </div>
                                    <div class="field">
                                        <label>Color</label>
                                        <input class="input" name="color" type="text" placeholder="#579DFF">
                                    </div>
                                </div>
                                <button class="button button--ghost" type="submit">Create label</button>
                            </form>
                        </section>
                    </div>
                </div>
            </section>

            <section class="surface board-toolbar">
                <form class="filter-bar" data-form="apply-filters">
                    <div class="field">
                        <label>Search</label>
                        <input class="input" name="search" type="text" value="${escapeAttribute(state.filters.search)}" placeholder="search cards">
                    </div>
                    <div class="field">
                        <label>Assignee</label>
                        <select class="select" name="assigneeId">
                            ${renderOptions([["", "Anyone"], ...getAssignableMembers().map((member) => [member.userId, member.name])], state.filters.assigneeId)}
                        </select>
                    </div>
                    <label class="toggle-chip">
                        <input type="checkbox" name="includeArchived" ${state.filters.includeArchived ? "checked" : ""}>
                        Show archived
                    </label>
                    <div class="inline-actions">
                        <button class="button button--primary" type="submit">Apply</button>
                        <button class="button button--ghost" type="button" data-action="reset-filters">Reset</button>
                    </div>
                </form>
                <div class="label-cloud">
                    ${state.currentBoard.labels.map(renderFilterLabelChip).join("")}
                </div>
            </section>

            <section class="board-scroll">
                <div class="board-columns">
                    ${renderBoardColumns()}
                </div>
            </section>
        </div>
    `;
}

function renderBoardMemberForm() {
    const boardMemberIds = new Set(state.currentBoard.members.map((member) => member.userId));
    const candidates = state.currentWorkspace.members.filter((member) => !boardMemberIds.has(member.userId));

    if (!candidates.length) {
        return '<p class="muted">All workspace members already have explicit board access or inherited access.</p>';
    }

    return `
        <form class="stack" data-form="add-board-member">
            <div class="field">
                <label>User</label>
                <select class="select" name="userId">
                    ${renderOptions(candidates.map((member) => [member.userId, `${member.name} · ${member.role}`]), candidates[0].userId)}
                </select>
            </div>
            <div class="field">
                <label>Role</label>
                <select class="select" name="role">
                    ${renderOptions([["Viewer", "Viewer"], ["Member", "Member"], ["Admin", "Admin"]], "Member")}
                </select>
            </div>
            <button class="button button--ghost" type="submit">Add board member</button>
        </form>
    `;
}

function renderBoardMemberFormModern() {
    const boardMemberIds = new Set(state.currentBoard.members.map((member) => member.userId));
    const candidates = state.currentWorkspace.members.filter((member) => !boardMemberIds.has(member.userId));

    if (!candidates.length) {
        return '<p class="muted">All workspace members already have explicit board access or inherited access.</p>';
    }

    return `
        <form class="stack" data-form="add-board-member">
            <div class="field">
                <label>User</label>
                <select class="select" name="userId">
                    ${renderOptions(candidates.map((member) => [member.userId, `${member.name} - ${member.role}`]), candidates[0].userId)}
                </select>
            </div>
            <div class="field">
                <label>Role</label>
                <select class="select" name="role">
                    ${renderOptions([["Viewer", "Viewer"], ["Member", "Member"], ["Admin", "Admin"]], "Member")}
                </select>
            </div>
            <button class="button button--ghost" type="submit">Add board member</button>
        </form>
    `;
}

function renderBoardColumns() {
    const columns = getVisibleColumns();
    const canDrag = canContributeBoard() && !isBoardFiltered();
    const items = [];

    if (canManageBoard() && !isBoardFiltered()) {
        items.push(renderColumnDropSlot(0));
    }

    columns.forEach((column, index) => {
        items.push(renderColumn(column, canDrag));
        if (canManageBoard() && !isBoardFiltered()) {
            items.push(renderColumnDropSlot(index + 1));
        }
    });

    if (canManageBoard()) {
        items.push(renderNewColumnComposer());
    }

    return items.join("");
}

function renderColumn(column, canDrag) {
    const cards = getVisibleCards(column);
    return `
        <section class="board-column" data-drag-column data-column-id="${escapeAttribute(column.id)}" draggable="${canManageBoard() && !isBoardFiltered() ? "true" : "false"}">
            <div class="column-head">
                <div class="column-title">
                    <span class="badge">${cards.length}</span>
                    <div>
                        <h3>${escapeHtml(column.title)}</h3>
                        ${column.isArchived ? '<span class="meta">Archived column</span>' : ""}
                    </div>
                </div>
                ${canManageBoard() ? `
                    <div class="inline-actions">
                        <button type="button" class="button button--ghost button--sm" data-action="rename-column" data-column-id="${escapeAttribute(column.id)}">Rename</button>
                        <button type="button" class="button button--danger button--sm" data-action="delete-column" data-column-id="${escapeAttribute(column.id)}">Delete</button>
                    </div>
                ` : ""}
            </div>

            <div class="column-list">
                ${canDrag ? renderCardDropSlot(column.id, 0) : ""}
                ${cards.length
                    ? cards.map((card, index) => `${renderCard(card, canDrag)}${canDrag ? renderCardDropSlot(column.id, index + 1) : ""}`).join("")
                    : '<div class="empty-state"><strong>Empty</strong><p>Drop or create a card here.</p></div>'}
            </div>

            ${canContributeBoard()
                ? state.ui.newCardColumnId === column.id
                    ? renderNewCardForm(column.id)
                    : `<button type="button" class="button button--ghost" data-action="toggle-new-card" data-column-id="${escapeAttribute(column.id)}">Add a card</button>`
                : ""}
        </section>
    `;
}

function renderCard(card, canDrag) {
    const labels = card.labelIds
        .map((labelId) => state.currentBoard.labels.find((label) => label.id === labelId))
        .filter(Boolean);

    return `
        <article class="task-card" data-action="open-card" data-card-id="${escapeAttribute(card.id)}" data-drag-card data-card-id="${escapeAttribute(card.id)}" draggable="${canDrag ? "true" : "false"}">
            <div class="chip-row">
                <span class="badge ${priorityBadgeClass(card.priority)}">${escapeHtml(card.priority)}</span>
                ${card.deadlineUtc && isOverdue(card.deadlineUtc) ? '<span class="badge badge--danger">Overdue</span>' : ""}
            </div>
            <h4>${escapeHtml(card.title)}</h4>
            <p>${escapeHtml(truncate(card.description || "Open the card to add description, checklist and comments.", 120))}</p>
            ${labels.length ? `<div class="chip-row">${labels.map(renderLabelChip).join("")}</div>` : ""}
            <div class="task-card__footer">
                <div class="card-metrics">
                    ${card.checklistTotalCount ? `<span class="badge">${card.checklistCompletedCount}/${card.checklistTotalCount}</span>` : ""}
                    ${card.deadlineUtc ? `<span class="badge">${escapeHtml(formatShortDate(card.deadlineUtc))}</span>` : ""}
                </div>
                ${card.assignee ? `<span class="avatar avatar--sm">${escapeHtml(getInitials(card.assignee.name))}</span>` : '<span class="badge">No owner</span>'}
            </div>
        </article>
    `;
}

function renderNewCardForm(columnId) {
    return `
        <form class="composer" data-form="create-card">
            <input type="hidden" name="columnId" value="${escapeAttribute(columnId)}">
            <div class="field">
                <label>Card title</label>
                <input class="input" name="title" type="text" required>
            </div>
            <div class="field-inline">
                <div class="field">
                    <label>Priority</label>
                    <select class="select" name="priority">
                        ${renderOptions([["Low", "Low"], ["Medium", "Medium"], ["High", "High"], ["Critical", "Critical"]], "Medium")}
                    </select>
                </div>
                <div class="field">
                    <label>Assignee</label>
                    <select class="select" name="assigneeId">
                        ${renderOptions([["", "Nobody"], ...getAssignableMembers().map((member) => [member.userId, member.name])], "")}
                    </select>
                </div>
            </div>
            <div class="inline-actions">
                <button class="button button--primary" type="submit">Add card</button>
                <button type="button" class="button button--ghost" data-action="cancel-new-card">Cancel</button>
            </div>
        </form>
    `;
}

function renderNewColumnComposer() {
    return state.ui.showNewColumn
        ? `
            <form class="composer" data-form="create-column" style="width:min(var(--card-width), calc(100vw - 3rem));">
                <div class="field">
                    <label>List title</label>
                    <input class="input" name="title" type="text" required>
                </div>
                <div class="inline-actions">
                    <button class="button button--primary" type="submit">Add list</button>
                    <button type="button" class="button button--ghost" data-action="cancel-new-column">Cancel</button>
                </div>
            </form>
        `
        : `
            <button type="button" class="button button--ghost" style="width:min(var(--card-width), calc(100vw - 3rem));min-height:68px;" data-action="toggle-new-column">
                Add another list
            </button>
        `;
}

function renderCardModal() {
    if (!state.currentCard) {
        return "";
    }

    const card = state.currentCard;
    const canEdit = canEditCard(card);
    return `
        <div class="modal-backdrop"></div>
        <section class="card-modal">
            <div class="modal__head">
                <div>
                    <p class="eyebrow" style="color:var(--muted);margin-bottom:0.3rem;">Card</p>
                    <h2 style="margin:0;font-family:Georgia,'Times New Roman',serif;">${escapeHtml(card.title)}</h2>
                </div>
                <button type="button" class="button button--ghost button--sm" data-action="close-card">Close</button>
            </div>

            <div class="modal__body">
                <div class="modal-panel">
                    <section class="detail-card">
                        <h3>Main</h3>
                        ${canEdit ? renderCardEditForm(card) : renderReadonlyCard(card)}
                    </section>

                    <section class="detail-card">
                        <h3>Checklist</h3>
                        <div class="checklist">
                            ${card.checklistItems.length
                                ? card.checklistItems.map((item) => `
                                    <label class="checklist-item">
                                        <input type="checkbox" data-check-item data-item-id="${escapeAttribute(item.id)}" ${item.isCompleted ? "checked" : ""}>
                                        <span>${escapeHtml(item.title)}</span>
                                        <span class="inline-actions">
                                            <button type="button" class="button button--ghost button--sm" data-action="rename-check-item" data-item-id="${escapeAttribute(item.id)}" data-current-title="${escapeAttribute(item.title)}">Rename</button>
                                            <button type="button" class="button button--danger button--sm" data-action="delete-check-item" data-item-id="${escapeAttribute(item.id)}">Delete</button>
                                        </span>
                                    </label>
                                `).join("")
                                : '<p class="muted">No checklist items yet.</p>'}
                        </div>
                        ${canEdit ? `
                            <form class="stack" data-form="add-check-item" style="margin-top:1rem;">
                                <div class="field">
                                    <label>New checklist item</label>
                                    <input class="input" name="title" type="text" required>
                                </div>
                                <button class="button button--ghost" type="submit">Add checklist item</button>
                            </form>
                        ` : ""}
                    </section>
                </div>

                <div class="modal-panel">
                    <section class="detail-card">
                        <h3>Comments</h3>
                        <div class="comment-list">
                            ${card.comments.length
                                ? card.comments.map((comment) => `
                                    <article class="comment-card">
                                        <div class="comment-card__head">
                                            <div class="comment-card__identity">
                                                <span class="avatar avatar--sm">${escapeHtml(getInitials(comment.author.name))}</span>
                                                <div>
                                                    <strong>${escapeHtml(comment.author.name)}</strong>
                                                    <span>${escapeHtml(formatRelativeTime(comment.updatedAtUtc))}</span>
                                                </div>
                                            </div>
                                            ${canEditComment(comment) ? `
                                                <div class="inline-actions">
                                                    <button type="button" class="button button--ghost button--sm" data-action="edit-comment" data-comment-id="${escapeAttribute(comment.id)}" data-current-content="${escapeAttribute(comment.content)}">Edit</button>
                                                    <button type="button" class="button button--danger button--sm" data-action="delete-comment" data-comment-id="${escapeAttribute(comment.id)}">Delete</button>
                                                </div>
                                            ` : ""}
                                        </div>
                                        <p style="margin:0;">${escapeHtml(comment.content)}</p>
                                    </article>
                                `).join("")
                                : '<p class="muted">No comments yet.</p>'}
                        </div>
                        ${canContributeBoard() ? `
                            <form class="stack" data-form="add-comment" style="margin-top:1rem;">
                                <div class="field">
                                    <label>New comment</label>
                                    <textarea class="textarea" name="content" required></textarea>
                                </div>
                                <button class="button button--ghost" type="submit">Add comment</button>
                            </form>
                        ` : ""}
                    </section>

                    <section class="detail-card">
                        <h3>Activity</h3>
                        <div class="activity-list">
                            ${state.currentActivity.length
                                ? state.currentActivity.map((item) => `
                                    <article class="activity-card">
                                        <strong>${escapeHtml(item.type)}</strong>
                                        <span>${escapeHtml(item.description)}</span>
                                        <span class="meta">${escapeHtml(formatDateTime(item.createdAtUtc))}</span>
                                    </article>
                                `).join("")
                                : '<p class="muted">No activity yet.</p>'}
                        </div>
                    </section>
                </div>
            </div>
        </section>
    `;
}

function renderCardEditForm(card) {
    return `
        <form class="stack" data-form="save-card">
            <div class="field">
                <label>Title</label>
                <input class="input" name="title" type="text" value="${escapeAttribute(card.title)}">
            </div>
            <div class="field">
                <label>Description</label>
                <textarea class="textarea" name="description">${escapeHtml(card.description || "")}</textarea>
            </div>
            <div class="field-inline">
                <div class="field">
                    <label>Priority</label>
                    <select class="select" name="priority">
                        ${renderOptions([["Low", "Low"], ["Medium", "Medium"], ["High", "High"], ["Critical", "Critical"]], card.priority)}
                    </select>
                </div>
                <div class="field">
                    <label>Deadline</label>
                    <input class="input" name="deadlineUtc" type="datetime-local" value="${escapeAttribute(toDateTimeLocalValue(card.deadlineUtc))}">
                </div>
            </div>
            <div class="field">
                <label>Assignee</label>
                <select class="select" name="assigneeId">
                    ${renderOptions([["", "Nobody"], ...getAssignableMembers().map((member) => [member.userId, member.name])], card.assignee?.id || "")}
                </select>
            </div>
            <div class="field">
                <label>Labels</label>
                <div class="label-cloud">
                    ${state.currentBoard.labels.map((label) => `
                        <label class="label-chip">
                            <input type="checkbox" name="labelId" value="${escapeAttribute(label.id)}" ${card.labelIds.includes(label.id) ? "checked" : ""}>
                            <span class="label-chip__swatch" style="background:${escapeAttribute(normalizeColor(label.color, "#F2855E"))};"></span>
                            <span>${escapeHtml(label.name)}</span>
                        </label>
                    `).join("") || '<span class="muted">No labels yet.</span>'}
                </div>
            </div>
            <button class="button button--primary" type="submit">Save card</button>
        </form>
    `;
}

function renderReadonlyCard(card) {
    return `
        <div class="stack">
            <p>${escapeHtml(card.description || "No description yet.")}</p>
            <div class="chip-row">
                <span class="badge ${priorityBadgeClass(card.priority)}">${escapeHtml(card.priority)}</span>
                ${card.deadlineUtc ? `<span class="badge">${escapeHtml(formatDateTime(card.deadlineUtc))}</span>` : ""}
                ${card.assignee ? `<span class="badge">${escapeHtml(card.assignee.name)}</span>` : '<span class="badge">No assignee</span>'}
            </div>
            <p class="muted">Your current role allows read access only.</p>
        </div>
    `;
}

function renderMemberCard(member) {
    return `
        <article class="member-card">
            <div class="member-card__head">
                <div class="member-card__identity">
                    <span class="avatar avatar--sm">${escapeHtml(getInitials(member.name))}</span>
                    <div>
                        <strong>${escapeHtml(member.name)}</strong>
                        <span>${escapeHtml(member.email)}</span>
                    </div>
                </div>
                <span class="badge">${escapeHtml(member.role)}</span>
            </div>
        </article>
    `;
}

function renderNotificationCard(notification) {
    return `
        <article class="notification-card ${notification.isRead ? "" : "is-unread"}">
            <div class="notification-card__head">
                <div>
                    <strong>${escapeHtml(notification.type)}</strong>
                    <span>${escapeHtml(formatRelativeTime(notification.createdAtUtc))}</span>
                </div>
                <span class="badge">${notification.isRead ? "Read" : "New"}</span>
            </div>
            <p style="margin:0;">${escapeHtml(notification.message)}</p>
            <div class="inline-actions">
                ${notification.relatedEntityId && (notification.type === "AddedToBoard" || notification.type === "AddedToWorkspace")
                    ? `<button type="button" class="button button--ghost button--sm" data-action="open-notification" data-notification-id="${escapeAttribute(notification.id)}">Open</button>`
                    : ""}
                ${notification.isRead
                    ? ""
                    : `<button type="button" class="button button--ghost button--sm" data-action="mark-read" data-notification-id="${escapeAttribute(notification.id)}">Mark read</button>`}
            </div>
        </article>
    `;
}

function renderLabelChip(label) {
    return `
        <span class="label-chip">
            <span class="label-chip__swatch" style="background:${escapeAttribute(normalizeColor(label.color, "#F2855E"))};"></span>
            <span>${escapeHtml(label.name)}</span>
        </span>
    `;
}

function renderFilterLabelChip(label) {
    const active = state.filters.labelIds.includes(label.id);
    return `
        <button type="button" class="label-chip" data-action="toggle-label-filter" data-label-id="${escapeAttribute(label.id)}" style="${active ? "box-shadow:0 0 0 2px rgba(21,122,110,0.2);" : ""}">
            <span class="label-chip__swatch" style="background:${escapeAttribute(normalizeColor(label.color, "#F2855E"))};"></span>
            <span>${escapeHtml(label.name)}</span>
        </button>
    `;
}

function renderColumnDropSlot(position) {
    return `<button type="button" class="drop-slot drop-slot--column" data-drop-type="column" data-position="${position}"></button>`;
}

function renderCardDropSlot(columnId, position) {
    return `<button type="button" class="drop-slot drop-slot--card" data-drop-type="card" data-column-id="${escapeAttribute(columnId)}" data-position="${position}"></button>`;
}

function renderToasts() {
    toastRoot.innerHTML = state.toasts.map((toast) => `
        <div class="toast toast--${escapeAttribute(toast.kind)}">${escapeHtml(toast.message)}</div>
    `).join("");
}

function showToast(message, kind = "success") {
    if (!message) {
        return;
    }

    const toast = { id: ++toastSeed, message, kind };
    state.toasts = [...state.toasts, toast];
    renderToasts();

    window.setTimeout(() => {
        state.toasts = state.toasts.filter((item) => item.id !== toast.id);
        renderToasts();
    }, 2800);
}

function showFatal(message) {
    root.innerHTML = `
        <section class="screen-loader">
            <div class="screen-loader__card stack stack--lg">
                <div class="brand">
                    <span class="brand__mark">TF</span>
                    <strong>TaskFlow</strong>
                </div>
                <div class="stack">
                    <strong>Client failed to start</strong>
                    <pre style="white-space:pre-wrap;margin:0;">${escapeHtml(message)}</pre>
                </div>
            </div>
        </section>
    `;
}

async function api(path, options = {}) {
    const headers = new Headers(options.headers || {});
    const authEnabled = options.auth !== false;
    let body = options.body;

    if (authEnabled && state.session?.accessToken) {
        headers.set("Authorization", `Bearer ${state.session.accessToken}`);
    }

    if (body !== undefined && !(body instanceof FormData)) {
        headers.set("Content-Type", "application/json");
        body = JSON.stringify(body);
    }

    const response = await fetch(path, {
        method: options.method || "GET",
        headers,
        body
    });

    if (response.status === 401 && authEnabled && path !== "/api/auth/refresh" && state.session?.refreshToken) {
        await refreshSession();
        return api(path, options);
    }

    const raw = await response.text();
    const data = raw ? safeJson(raw) : null;

    if (!response.ok) {
        throw new Error(data?.error || raw || `HTTP ${response.status}`);
    }

    return data;
}

async function refreshSession() {
    if (refreshPromise) {
        return refreshPromise;
    }

    refreshPromise = (async () => {
        const response = await fetch("/api/auth/refresh", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                refreshToken: state.session?.refreshToken
            })
        });

        const raw = await response.text();
        const data = raw ? safeJson(raw) : null;

        if (!response.ok) {
            clearSession();
            throw new Error(data?.error || "Session refresh failed.");
        }

        acceptSession(data);
    })().finally(() => {
        refreshPromise = null;
    });

    return refreshPromise;
}

function acceptSession(session) {
    state.session = session;
    state.user = {
        id: session.userId,
        name: session.name,
        email: session.email,
        avatarUrl: session.avatarUrl
    };
    saveSession(session);
}

function clearSession() {
    state.session = null;
    state.user = null;
    window.localStorage.removeItem(SESSION_KEY);
    resetWorkspaceState();
}

function resetWorkspaceState() {
    state.workspaces = [];
    state.currentWorkspace = null;
    state.currentBoard = null;
    state.currentCard = null;
    state.currentActivity = [];
    state.notifications = [];
    state.inviteResults = [];
    state.selectedInviteUserId = "";
    state.filters = defaultFilters();
}

function parseRoute(pathname = window.location.pathname) {
    const match = pathname.match(/^\/board\/([0-9a-f-]{36})\/?$/i);
    return match ? { name: "board", boardId: match[1] } : { name: "home" };
}

function defaultFilters() {
    return {
        search: "",
        assigneeId: "",
        includeArchived: false,
        labelIds: []
    };
}

function getVisibleColumns() {
    if (!state.currentBoard) {
        return [];
    }

    return [...state.currentBoard.columns]
        .filter((column) => state.filters.includeArchived || !column.isArchived)
        .sort((left, right) => left.position - right.position);
}

function getVisibleCards(column) {
    return [...column.cards]
        .filter(matchesCardFilters)
        .sort((left, right) => left.position - right.position);
}

function matchesCardFilters(card) {
    if (!state.filters.includeArchived && card.isArchived) {
        return false;
    }

    if (state.filters.search) {
        const haystack = `${card.title} ${card.description}`.toLowerCase();
        if (!haystack.includes(state.filters.search.toLowerCase())) {
            return false;
        }
    }

    if (state.filters.assigneeId && card.assignee?.id !== state.filters.assigneeId) {
        return false;
    }

    if (state.filters.labelIds.length && !state.filters.labelIds.every((id) => card.labelIds.includes(id))) {
        return false;
    }

    return true;
}

function toggleLabelFilter(labelId) {
    if (!labelId) {
        return;
    }

    if (state.filters.labelIds.includes(labelId)) {
        state.filters.labelIds = state.filters.labelIds.filter((id) => id !== labelId);
    } else {
        state.filters.labelIds = [...state.filters.labelIds, labelId];
    }
}

function canManageBoard() {
    const workspaceRole = currentWorkspaceRole();
    if (isAdminRole(workspaceRole)) {
        return true;
    }

    const boardRole = currentBoardRole();
    return isAdminRole(boardRole);
}

function canContributeBoard() {
    if (!state.currentBoard || !state.currentWorkspace) {
        return false;
    }

    const workspaceRole = currentWorkspaceRole();
    if (state.currentBoard.visibility === "Workspace") {
        return Boolean(workspaceRole && workspaceRole !== "Viewer");
    }

    if (isAdminRole(workspaceRole)) {
        return true;
    }

    const boardRole = currentBoardRole();
    return Boolean(boardRole && boardRole !== "Viewer");
}

function canEditCard(card) {
    if (!state.user || !card) {
        return false;
    }

    if (canManageBoard()) {
        return true;
    }

    if (!canContributeBoard()) {
        return false;
    }

    return card.author?.id === state.user.id || card.assignee?.id === state.user.id;
}

function canEditComment(comment) {
    if (!state.user) {
        return false;
    }

    return comment.author.id === state.user.id || canManageBoard();
}

function getAssignableMembers() {
    if (!state.currentBoard || !state.currentWorkspace) {
        return [];
    }

    if (state.currentBoard.visibility === "Workspace") {
        return [...state.currentWorkspace.members];
    }

    const boardIds = new Set(state.currentBoard.members.map((member) => member.userId));
    return state.currentWorkspace.members.filter((member) => isAdminRole(member.role) || boardIds.has(member.userId));
}

function currentWorkspaceRole() {
    return state.currentWorkspace?.members.find((member) => member.userId === state.user?.id)?.role || null;
}

function currentBoardRole() {
    return state.currentBoard?.members.find((member) => member.userId === state.user?.id)?.role || null;
}

function isBoardFiltered() {
    return Boolean(state.filters.search || state.filters.assigneeId || state.filters.includeArchived || state.filters.labelIds.length);
}

function headerSubtitle() {
    if (state.currentBoard && state.currentWorkspace) {
        return `${state.currentWorkspace.name} · ${state.currentBoard.visibility}`;
    }

    if (state.currentWorkspace) {
        return `${state.currentWorkspace.members.length} members · ${state.currentWorkspace.boards.length} boards`;
    }

    return "Frontend for TaskFlow API";
}

function renderHeaderSubtitle() {
    if (state.currentBoard && state.currentWorkspace) {
        return `${state.currentWorkspace.name} | ${state.currentBoard.visibility}`;
    }

    if (state.currentWorkspace) {
        return `${state.currentWorkspace.members.length} members | ${state.currentWorkspace.boards.length} boards`;
    }

    return "Frontend for TaskFlow API";
}

function renderOptions(options, selectedValue) {
    return options.map(([value, label]) => `
        <option value="${escapeAttribute(value)}" ${value === selectedValue ? "selected" : ""}>${escapeHtml(label)}</option>
    `).join("");
}

function priorityBadgeClass(priority) {
    return {
        Low: "badge--low",
        Medium: "badge--medium",
        High: "badge--high",
        Critical: "badge--critical"
    }[priority] || "";
}

function isAdminRole(role) {
    return role === "Admin" || role === "Owner";
}

function isOverdue(value) {
    return Boolean(value && new Date(value).getTime() < Date.now());
}

function formatDateTime(value) {
    if (!value) {
        return "";
    }

    return new Intl.DateTimeFormat("en-US", {
        dateStyle: "medium",
        timeStyle: "short"
    }).format(new Date(value));
}

function formatShortDate(value) {
    if (!value) {
        return "";
    }

    return new Intl.DateTimeFormat("en-US", {
        day: "2-digit",
        month: "short"
    }).format(new Date(value));
}

function formatRelativeTime(value) {
    if (!value) {
        return "";
    }

    const diffMs = new Date(value).getTime() - Date.now();
    const minute = 60 * 1000;
    const hour = 60 * minute;
    const day = 24 * hour;
    const rtf = new Intl.RelativeTimeFormat("en", { numeric: "auto" });

    if (Math.abs(diffMs) < hour) {
        return rtf.format(Math.round(diffMs / minute), "minute");
    }

    if (Math.abs(diffMs) < day) {
        return rtf.format(Math.round(diffMs / hour), "hour");
    }

    return rtf.format(Math.round(diffMs / day), "day");
}

function toDateTimeLocalValue(value) {
    if (!value) {
        return "";
    }

    const date = new Date(value);
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, "0");
    const day = String(date.getDate()).padStart(2, "0");
    const hours = String(date.getHours()).padStart(2, "0");
    const minutes = String(date.getMinutes()).padStart(2, "0");
    return `${year}-${month}-${day}T${hours}:${minutes}`;
}

function normalizeNullable(value) {
    const text = String(value || "").trim();
    return text ? text : null;
}

function normalizeColor(value, fallback) {
    const text = String(value || "").trim();
    return /^#([0-9a-f]{3}|[0-9a-f]{6})$/i.test(text) ? text : fallback;
}

function truncate(value, maxLength) {
    if (!value || value.length <= maxLength) {
        return value;
    }

    return `${value.slice(0, maxLength - 1)}...`;
}

function getInitials(name) {
    return String(name || "TF")
        .split(/\s+/)
        .filter(Boolean)
        .slice(0, 2)
        .map((part) => part[0]?.toUpperCase() || "")
        .join("") || "TF";
}

function escapeHtml(value) {
    return String(value ?? "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

function escapeAttribute(value) {
    return escapeHtml(value);
}

function getErrorMessage(error) {
    return error instanceof Error ? error.message : "Something went wrong.";
}

function safeJson(value) {
    try {
        return JSON.parse(value);
    } catch {
        return null;
    }
}

function loadSession() {
    try {
        const raw = window.localStorage.getItem(SESSION_KEY);
        return raw ? JSON.parse(raw) : null;
    } catch {
        return null;
    }
}

function saveSession(session) {
    window.localStorage.setItem(SESSION_KEY, JSON.stringify(session));
}

function loadUiState() {
    try {
        const raw = window.localStorage.getItem(UI_KEY);
        return raw ? JSON.parse(raw) : { selectedWorkspaceId: null };
    } catch {
        return { selectedWorkspaceId: null };
    }
}

function saveUiState() {
    window.localStorage.setItem(UI_KEY, JSON.stringify({
        selectedWorkspaceId: state.selectedWorkspaceId
    }));
}
