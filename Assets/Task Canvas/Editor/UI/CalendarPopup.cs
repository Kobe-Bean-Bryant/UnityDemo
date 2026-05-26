using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaskCanvas.Editor
{
    public class CalendarPopup : VisualElement
    {
        private const int CELL_SIZE = 32;
        private const int GRID_COLUMNS = 7;
        private const int GRID_ROWS = 6;
        
        private DateTime _displayedMonth;
        private DateTime? _selectedDate;
        private Action<DateTime?> _onDateSelected;
        private VisualElement _calendarGrid;
        private Label _monthYearLabel;
        
        public CalendarPopup(DateTime? initialDate, Action<DateTime?> onDateSelected)
        {
            _selectedDate = initialDate;
            _displayedMonth = initialDate ?? DateTime.Today;
            _displayedMonth = new DateTime(_displayedMonth.Year, _displayedMonth.Month, 1);
            _onDateSelected = onDateSelected;
            
            AddToClassList("calendar-popup-overlay");
            BuildUI();
        }
        
        private void BuildUI()
        {
            var content = new VisualElement();
            content.AddToClassList("calendar-popup-content");
            content.style.backgroundColor = new Color(0.18f, 0.18f, 0.20f);
            content.style.borderTopLeftRadius = 8;
            content.style.borderTopRightRadius = 8;
            content.style.borderBottomLeftRadius = 8;
            content.style.borderBottomRightRadius = 8;
            content.style.paddingTop = 12;
            content.style.paddingBottom = 12;
            content.style.paddingLeft = 12;
            content.style.paddingRight = 12;
            content.style.width = CELL_SIZE * GRID_COLUMNS + 24;
            Add(content);
            
            BuildHeader(content);
            
            BuildDayLabels(content);
            
            _calendarGrid = new VisualElement();
            _calendarGrid.style.flexDirection = FlexDirection.Row;
            _calendarGrid.style.flexWrap = Wrap.Wrap;
            _calendarGrid.style.width = CELL_SIZE * GRID_COLUMNS;
            content.Add(_calendarGrid);
            
            RefreshCalendar();
            
            BuildShortcuts(content);
            
            RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == this)
                    Close();
            });
        }
        
        private void BuildHeader(VisualElement parent)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8;
            parent.Add(header);
            
            var prevBtn = new Button(() => NavigateMonth(-1)) { text = "<" };
            prevBtn.AddToClassList("calendar-nav-btn");
            prevBtn.style.width = 28;
            prevBtn.style.height = 28;
            header.Add(prevBtn);
            
            _monthYearLabel = new Label();
            _monthYearLabel.style.flexGrow = 1;
            _monthYearLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _monthYearLabel.style.fontSize = 13;
            _monthYearLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _monthYearLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            header.Add(_monthYearLabel);
            
            var nextBtn = new Button(() => NavigateMonth(1)) { text = ">" };
            nextBtn.AddToClassList("calendar-nav-btn");
            nextBtn.style.width = 28;
            nextBtn.style.height = 28;
            header.Add(nextBtn);
            
            UpdateMonthYearLabel();
        }
        
        private void BuildDayLabels(VisualElement parent)
        {
            var dayLabels = new VisualElement();
            dayLabels.style.flexDirection = FlexDirection.Row;
            dayLabels.style.marginBottom = 4;
            parent.Add(dayLabels);
            
            string[] days = { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };
            foreach (var day in days)
            {
                var label = new Label(day);
                label.style.width = CELL_SIZE;
                label.style.height = 20;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.fontSize = 10;
                label.style.color = new Color(0.6f, 0.6f, 0.6f);
                dayLabels.Add(label);
            }
        }
        
        private void BuildShortcuts(VisualElement parent)
        {
            var shortcuts = new VisualElement();
            shortcuts.style.flexDirection = FlexDirection.Row;
            shortcuts.style.flexWrap = Wrap.Wrap;
            shortcuts.style.marginTop = 8;
            shortcuts.style.justifyContent = Justify.Center;
            parent.Add(shortcuts);
            
            AddShortcut(shortcuts, "Today", 0);
            AddShortcut(shortcuts, "+1d", 1);
            AddShortcut(shortcuts, "+3d", 3);
            AddShortcut(shortcuts, "+1w", 7);
            AddShortcut(shortcuts, "+2w", 14);
            
            var clearBtn = new Button(() =>
            {
                _onDateSelected?.Invoke(null);
                Close();
            }) { text = "Clear" };
            clearBtn.AddToClassList("calendar-shortcut-btn");
            clearBtn.style.color = new Color(0.8f, 0.5f, 0.5f);
            shortcuts.Add(clearBtn);
        }
        
        private void AddShortcut(VisualElement parent, string label, int daysFromToday)
        {
            var btn = new Button(() =>
            {
                var date = DateTime.Today.AddDays(daysFromToday);
                _onDateSelected?.Invoke(date);
                Close();
            }) { text = label };
            btn.AddToClassList("calendar-shortcut-btn");
            parent.Add(btn);
        }
        
        private void NavigateMonth(int direction)
        {
            _displayedMonth = _displayedMonth.AddMonths(direction);
            UpdateMonthYearLabel();
            RefreshCalendar();
        }
        
        private void UpdateMonthYearLabel()
        {
            _monthYearLabel.text = _displayedMonth.ToString("MMMM yyyy");
        }
        
        private void RefreshCalendar()
        {
            _calendarGrid.Clear();
            
            var firstDayOfMonth = new DateTime(_displayedMonth.Year, _displayedMonth.Month, 1);
            int daysInMonth = DateTime.DaysInMonth(_displayedMonth.Year, _displayedMonth.Month);
            int startDayOfWeek = (int)firstDayOfMonth.DayOfWeek;
            
            var prevMonth = firstDayOfMonth.AddMonths(-1);
            int prevMonthDays = DateTime.DaysInMonth(prevMonth.Year, prevMonth.Month);
            for (int i = 0; i < startDayOfWeek; i++)
            {
                int day = prevMonthDays - startDayOfWeek + i + 1;
                var date = new DateTime(prevMonth.Year, prevMonth.Month, day);
                AddDayCell(date, isCurrentMonth: false);
            }
            
            for (int day = 1; day <= daysInMonth; day++)
            {
                var date = new DateTime(_displayedMonth.Year, _displayedMonth.Month, day);
                AddDayCell(date, isCurrentMonth: true);
            }
            
            int totalCells = startDayOfWeek + daysInMonth;
            int remainingCells = (GRID_COLUMNS * GRID_ROWS) - totalCells;
            var nextMonth = firstDayOfMonth.AddMonths(1);
            for (int day = 1; day <= remainingCells; day++)
            {
                var date = new DateTime(nextMonth.Year, nextMonth.Month, day);
                AddDayCell(date, isCurrentMonth: false);
            }
        }
        
        private void AddDayCell(DateTime date, bool isCurrentMonth)
        {
            var cell = new Button(() =>
            {
                _onDateSelected?.Invoke(date);
                Close();
            });
            cell.text = date.Day.ToString();
            cell.AddToClassList("calendar-day-cell");
            cell.style.width = CELL_SIZE;
            cell.style.height = CELL_SIZE;
            cell.style.marginTop = 0;
            cell.style.marginBottom = 0;
            cell.style.marginLeft = 0;
            cell.style.marginRight = 0;
            cell.style.paddingTop = 0;
            cell.style.paddingBottom = 0;
            cell.style.paddingLeft = 0;
            cell.style.paddingRight = 0;
            cell.style.borderTopWidth = 0;
            cell.style.borderBottomWidth = 0;
            cell.style.borderLeftWidth = 0;
            cell.style.borderRightWidth = 0;
            cell.style.borderTopLeftRadius = 4;
            cell.style.borderTopRightRadius = 4;
            cell.style.borderBottomLeftRadius = 4;
            cell.style.borderBottomRightRadius = 4;
            cell.style.backgroundColor = new Color(0.22f, 0.22f, 0.24f);
            cell.style.color = isCurrentMonth ? new Color(0.85f, 0.85f, 0.85f) : new Color(0.45f, 0.45f, 0.45f);
            cell.style.fontSize = 11;
            cell.style.unityTextAlign = TextAnchor.MiddleCenter;
            
            if (date.Date == DateTime.Today)
            {
                cell.style.borderTopWidth = 1;
                cell.style.borderBottomWidth = 1;
                cell.style.borderLeftWidth = 1;
                cell.style.borderRightWidth = 1;
                cell.style.borderTopColor = new Color(0.3f, 0.5f, 0.8f);
                cell.style.borderBottomColor = new Color(0.3f, 0.5f, 0.8f);
                cell.style.borderLeftColor = new Color(0.3f, 0.5f, 0.8f);
                cell.style.borderRightColor = new Color(0.3f, 0.5f, 0.8f);
            }
            
            if (_selectedDate.HasValue && date.Date == _selectedDate.Value.Date)
            {
                cell.style.backgroundColor = new Color(0.15f, 0.4f, 0.7f);
                cell.style.color = Color.white;
            }
            
            _calendarGrid.Add(cell);
        }
        
        private void Close()
        {
            RemoveFromHierarchy();
        }
    }
}
