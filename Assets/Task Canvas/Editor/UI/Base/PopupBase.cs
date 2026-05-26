using System;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    /// <summary>
    /// Base class for popup dialogs in Task Canvas.
    /// Provides common functionality like modal overlay and close button.
    /// </summary>
    public abstract class PopupBase : VisualElement
    {
        protected VisualElement ContentContainer { get; private set; }

        protected PopupBase()
        {
            AddToClassList(StyleClasses.ModalOverlay);
            
            ContentContainer = new VisualElement();
            ContentContainer.AddToClassList(StyleClasses.ModalContent);
            Add(ContentContainer);

            RegisterCallback<ClickEvent>(OnOverlayClick);
        }

        private void OnOverlayClick(ClickEvent evt)
        {
            // Close when clicking on the overlay (not the content)
            if (evt.target == this)
            {
                Close();
            }
        }

        /// <summary>
        /// Adds a title label to the popup.
        /// </summary>
        protected Label AddTitle(string text)
        {
            var title = new Label(text);
            title.AddToClassList(StyleClasses.ModalTitle);
            ContentContainer.Add(title);
            return title;
        }

        /// <summary>
        /// Creates a standard buttons row with Close button.
        /// </summary>
        protected VisualElement CreateButtonsRow()
        {
            var buttons = new VisualElement();
            buttons.AddToClassList(StyleClasses.ModalButtons);
            return buttons;
        }

        /// <summary>
        /// Creates a primary action button.
        /// </summary>
        protected Button CreatePrimaryButton(string text, Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.AddToClassList(StyleClasses.ModalButton);
            btn.AddToClassList(StyleClasses.ModalButtonPrimary);
            return btn;
        }

        /// <summary>
        /// Creates a secondary action button.
        /// </summary>
        protected Button CreateSecondaryButton(string text, Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.AddToClassList(StyleClasses.ModalButton);
            btn.AddToClassList(StyleClasses.ModalButtonSecondary);
            return btn;
        }

        /// <summary>
        /// Creates a danger action button (for delete operations).
        /// </summary>
        protected Button CreateDangerButton(string text, Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.AddToClassList(StyleClasses.ModalButton);
            btn.AddToClassList(StyleClasses.ModalButtonDanger);
            return btn;
        }

        /// <summary>
        /// Creates a labeled field group.
        /// </summary>
        protected VisualElement CreateFieldGroup(string labelText, out Label label)
        {
            var group = new VisualElement();
            group.AddToClassList(StyleClasses.ModalField);

            label = new Label(labelText);
            label.AddToClassList(StyleClasses.ModalFieldLabel);
            group.Add(label);

            return group;
        }

        /// <summary>
        /// Closes and removes the popup from the visual tree.
        /// </summary>
        protected virtual void Close()
        {
            RemoveFromHierarchy();
        }
    }
}
