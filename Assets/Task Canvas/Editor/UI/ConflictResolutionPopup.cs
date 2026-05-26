using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    public class ConflictResolutionPopup : VisualElement
    {
        public enum Resolution
        {
            KeepLocal,
            UseExternal
        }

        private Action<Resolution> _onResolved;
        private List<ConflictInfo> _conflicts;

        public class ConflictInfo
        {
            public string CardId;
            public string CardTitle;
            public string Description;
        }

        public ConflictResolutionPopup(List<ConflictInfo> conflicts, Action<Resolution> onResolved)
        {
            _conflicts = conflicts ?? new List<ConflictInfo>();
            _onResolved = onResolved;
            AddToClassList("modal-overlay");
            BuildUI();
        }

        private void BuildUI()
        {
            var content = new VisualElement();
            content.AddToClassList("modal-content");
            content.style.minWidth = 400;
            content.style.maxWidth = 600;
            Add(content);

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 16;
            content.Add(header);

            var warningIcon = new Label("⚠️");
            warningIcon.style.fontSize = 24;
            warningIcon.style.marginRight = 8;
            header.Add(warningIcon);

            var title = new Label("Conflict Detected");
            title.AddToClassList("modal-title");
            title.style.color = new Color(1f, 0.8f, 0.3f);
            header.Add(title);

            var description = new Label(
                "External changes have been detected (possibly from Git), but you have unsaved local changes. " +
                "Please choose which version to keep:");
            description.style.whiteSpace = WhiteSpace.Normal;
            description.style.marginBottom = 16;
            content.Add(description);

            if (_conflicts.Count > 0)
            {
                var conflictSection = new VisualElement();
                conflictSection.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
                conflictSection.style.borderTopLeftRadius = 4;
                conflictSection.style.borderTopRightRadius = 4;
                conflictSection.style.borderBottomLeftRadius = 4;
                conflictSection.style.borderBottomRightRadius = 4;
                conflictSection.style.paddingTop = 8;
                conflictSection.style.paddingBottom = 8;
                conflictSection.style.paddingLeft = 12;
                conflictSection.style.paddingRight = 12;
                conflictSection.style.marginBottom = 16;
                conflictSection.style.maxHeight = 150;
                content.Add(conflictSection);

                var conflictLabel = new Label("Affected items:");
                conflictLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                conflictLabel.style.marginBottom = 8;
                conflictSection.Add(conflictLabel);

                var scrollView = new ScrollView(ScrollViewMode.Vertical);
                scrollView.style.maxHeight = 100;
                conflictSection.Add(scrollView);

                foreach (var conflict in _conflicts)
                {
                    var itemLabel = new Label($"• {conflict.CardTitle ?? conflict.CardId}");
                    itemLabel.style.marginLeft = 8;
                    scrollView.Add(itemLabel);
                }
            }

            var optionsContainer = new VisualElement();
            optionsContainer.style.marginBottom = 16;
            content.Add(optionsContainer);

            var optionA = CreateOptionButton(
                "🖥️ Keep My Changes",
                "Discard external changes and keep your unsaved local modifications.",
                new Color(0.3f, 0.5f, 0.8f),
                () => Resolve(Resolution.KeepLocal)
            );
            optionsContainer.Add(optionA);

            var optionB = CreateOptionButton(
                "📥 Use External Changes",
                "Discard your local changes and load the new version from disk (Git).",
                new Color(0.4f, 0.7f, 0.4f),
                () => Resolve(Resolution.UseExternal)
            );
            optionsContainer.Add(optionB);

            RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == this)
                {
                    Resolve(Resolution.KeepLocal);
                }
            });
        }

        private VisualElement CreateOptionButton(string title, string description, Color accentColor, Action onClick)
        {
            var container = new Button(onClick);
            container.style.flexDirection = FlexDirection.Column;
            container.style.alignItems = Align.FlexStart;
            container.style.paddingTop = 12;
            container.style.paddingBottom = 12;
            container.style.paddingLeft = 16;
            container.style.paddingRight = 16;
            container.style.marginBottom = 8;
            container.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            container.style.borderTopLeftRadius = 6;
            container.style.borderTopRightRadius = 6;
            container.style.borderBottomLeftRadius = 6;
            container.style.borderBottomRightRadius = 6;
            container.style.borderLeftColor = accentColor;
            container.style.borderLeftWidth = 4;

            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = Color.white;
            titleLabel.style.marginBottom = 4;
            container.Add(titleLabel);

            var descLabel = new Label(description);
            descLabel.style.fontSize = 11;
            descLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            container.Add(descLabel);

            return container;
        }

        private void Resolve(Resolution resolution)
        {
            _onResolved?.Invoke(resolution);
            RemoveFromHierarchy();
        }
    }
}
