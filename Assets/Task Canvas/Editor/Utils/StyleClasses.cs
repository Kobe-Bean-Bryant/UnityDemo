namespace TaskCanvas.Editor
{
    /// <summary>
    /// CSS class constants for Task Canvas UI components.
    /// Using constants prevents typos and enables refactoring.
    /// </summary>
    public static class StyleClasses
    {
        // Modal
        public const string ModalOverlay = "modal-overlay";
        public const string ModalContent = "modal-content";
        public const string ModalTitle = "modal-title";
        public const string ModalField = "modal-field";
        public const string ModalFieldLabel = "modal-field-label";
        public const string ModalTextField = "modal-text-field";
        public const string ModalButtons = "modal-buttons";
        public const string ModalButton = "modal-button";
        public const string ModalButtonPrimary = "modal-button-primary";
        public const string ModalButtonSecondary = "modal-button-secondary";
        public const string ModalButtonDanger = "modal-button-danger";

        // Cards
        public const string KanbanCard = "kanban-card";
        public const string CardContent = "card-content";
        public const string CardTitle = "card-title";
        public const string CardDescription = "card-description";
        public const string CardFooter = "card-footer";
        public const string CardTag = "card-tag";
        public const string CardTagMore = "card-tag-more";
        public const string CardAssignee = "card-assignee";
        public const string CardActionButton = "card-action-button";
        public const string CardButtonsContainer = "card-buttons-container";
        public const string CardHover = "card-hover";
        public const string Completed = "completed";
        public const string CompletedText = "completed-text";
        public const string CheckContainer = "check-container";
        public const string CheckLabel = "check-label";

        // Columns
        public const string KanbanColumn = "kanban-column";
        public const string ColumnHeader = "column-header";
        public const string ColumnTitle = "column-title";
        public const string ColumnTitleField = "column-title-field";
        public const string ColumnCount = "column-count";
        public const string ColumnEditButton = "column-edit-button";
        public const string ColumnCardsScroll = "column-cards-scroll";
        public const string ColumnCards = "column-cards";
        public const string ColumnAddButton = "column-add-button";
        public const string ColumnsContainer = "columns-container";
        public const string AddColumnButton = "add-column-button";

        // Inline Card Creation
        public const string InlineCardContainer = "inline-card-container";
        public const string InlineCardField = "inline-card-field";
        public const string InlineCardPlaceholder = "inline-card-placeholder";
        public const string InlineCardButtons = "inline-card-buttons";
        public const string InlineCardAddBtn = "inline-card-add-btn";
        public const string InlineCardCancelBtn = "inline-card-cancel-btn";

        // Filters
        public const string FilterBar = "filter-bar";
        public const string FilterLabel = "filter-label";
        public const string FilterChip = "filter-chip";
        public const string FilterChipAdd = "filter-chip-add";
        public const string FilterChipMore = "filter-chip-more";
        public const string FilterChipClear = "filter-chip-clear";
        public const string AssigneeChip = "assignee-chip";
        public const string Active = "active";

        // Priority
        public const string PriorityNone = "priority-none";
        public const string PriorityLow = "priority-low";
        public const string PriorityMedium = "priority-medium";
        public const string PriorityHigh = "priority-high";

        // Drag and Drop
        public const string DragGhostCard = "drag-ghost-card";
        public const string DragGhostColumn = "drag-ghost-column";
        public const string DragPlaceholder = "drag-placeholder";
        public const string DragPlaceholderColumn = "drag-placeholder-column";

        // Quick Create
        public const string QuickCreatePanel = "quick-create-panel";
        public const string QuickCreateHeader = "quick-create-header";
        public const string QuickCreateTitle = "quick-create-title";
        public const string QuickCreateInput = "quick-create-input";
        public const string QuickCreatePlaceholder = "quick-create-placeholder";
        public const string QuickCreateClose = "quick-create-close";
        public const string QuickCreateSelectors = "quick-create-selectors";
        public const string QuickCreateDropdown = "quick-create-dropdown";
        public const string QuickCreateOptions = "quick-create-options";
        public const string QuickCreateActions = "quick-create-actions";
        public const string QuickCreateBtnPrimary = "quick-create-btn-primary";
        public const string QuickCreateBtnSecondary = "quick-create-btn-secondary";
        public const string QuickCreateDivider = "quick-create-divider";
        public const string QuickCreateOptionRow = "quick-create-option-row";
        public const string QuickCreateOptionLabel = "quick-create-option-label";
        public const string QuickCreatePriorityBtn = "quick-create-priority-btn";
        public const string QuickCreateTag = "quick-create-tag";
        public const string QuickCreateAssignee = "quick-create-assignee";
        public const string QuickCreateDateBtn = "quick-create-date-btn";
    }
}
