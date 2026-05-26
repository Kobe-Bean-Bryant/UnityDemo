using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TaskCanvas.Editor
{
    public static class BoardSerializer
    {
        private const string BOARD_FILE = "board.json";
        private const string COLUMNS_FOLDER = "columns";
        private const string CARDS_FOLDER = "cards";
        private const string CONTENT_SUFFIX = "_content.json";
        private const string POSITION_SUFFIX = "_position.json";

        public static KanbanBoard LoadBoard(string folderPath)
        {
            var boardFile = Path.Combine(folderPath, BOARD_FILE);
            if (!File.Exists(boardFile))
            {
                Debug.LogError($"Board file not found: {boardFile}");
                return null;
            }

            try
            {
                var json = File.ReadAllText(boardFile);
                var board = JsonUtility.FromJson<KanbanBoard>(json);
                board.boardFolderPath = folderPath;
                board.columns = LoadColumns(folderPath);
                return board;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load board from {folderPath}: {e.Message}");
                return null;
            }
        }

        public static void SaveBoard(KanbanBoard board)
        {
            if (string.IsNullOrEmpty(board.boardFolderPath))
            {
                Debug.LogError("Board has no folder path set");
                return;
            }

            EnsureFolderStructure(board.boardFolderPath);

            try
            {
                var boardData = new BoardFileData
                {
                    boardName = board.boardName,
                    assignees = board.assignees,
                    allTags = board.allTags
                };

                var json = JsonUtility.ToJson(boardData, true);
                File.WriteAllText(Path.Combine(board.boardFolderPath, BOARD_FILE), json);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save board: {e.Message}");
            }
        }

        public static KanbanBoard CreateNewBoard(string folderPath, string boardName)
        {
            return CreateNewBoard(folderPath, boardName, BoardTemplate.Kanban);
        }

        public static KanbanBoard CreateNewBoard(string folderPath, string boardName, BoardTemplate template)
        {
            if (Directory.Exists(folderPath))
            {
                Debug.LogError($"Board folder already exists: {folderPath}");
                return null;
            }

            try
            {
                EnsureFolderStructure(folderPath);

                var board = new KanbanBoard(boardName);
                board.boardFolderPath = folderPath;

                var columns = GetTemplateColumns(template);
                board.columns = columns;

                SaveBoard(board);
                foreach (var column in columns)
                {
                    SaveColumn(folderPath, column);
                }

                RefreshAssets();
                return board;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to create board: {e.Message}");
                return null;
            }
        }

        public static List<KanbanColumn> GetTemplateColumns(BoardTemplate template)
        {
            switch (template)
            {
                case BoardTemplate.Blank:
                    return new List<KanbanColumn>();

                case BoardTemplate.Kanban:
                    return new List<KanbanColumn>
                    {
                        new KanbanColumn("📋 To Do", new Color(0.4f, 0.6f, 0.9f)) { sortKey = "a" },
                        new KanbanColumn("🔄 In Progress", new Color(0.9f, 0.7f, 0.3f)) { sortKey = "n" },
                        new KanbanColumn("✅ Done", new Color(0.4f, 0.8f, 0.5f)) { sortKey = "z" }
                    };

                case BoardTemplate.Sprint:
                    return new List<KanbanColumn>
                    {
                        new KanbanColumn("📋 Backlog", new Color(0.5f, 0.5f, 0.6f)) { sortKey = "a" },
                        new KanbanColumn("🎯 Sprint", new Color(0.4f, 0.6f, 0.9f)) { sortKey = "d" },
                        new KanbanColumn("🔄 In Progress", new Color(0.9f, 0.7f, 0.3f)) { sortKey = "h" },
                        new KanbanColumn("📝 Review", new Color(0.8f, 0.5f, 0.8f)) { sortKey = "m" },
                        new KanbanColumn("✅ Done", new Color(0.4f, 0.8f, 0.5f)) { sortKey = "z" }
                    };

                case BoardTemplate.BugTracking:
                    return new List<KanbanColumn>
                    {
                        new KanbanColumn("🐛 New", new Color(0.9f, 0.4f, 0.4f)) { sortKey = "a" },
                        new KanbanColumn("🔍 Investigating", new Color(0.9f, 0.7f, 0.3f)) { sortKey = "d" },
                        new KanbanColumn("🔧 Fixing", new Color(0.4f, 0.6f, 0.9f)) { sortKey = "h" },
                        new KanbanColumn("🧪 Testing", new Color(0.8f, 0.5f, 0.8f)) { sortKey = "m" },
                        new KanbanColumn("✅ Resolved", new Color(0.4f, 0.8f, 0.5f)) { sortKey = "z" }
                    };

                case BoardTemplate.FeatureDevelopment:
                    return new List<KanbanColumn>
                    {
                        new KanbanColumn("💡 Ideas", new Color(0.9f, 0.8f, 0.3f)) { sortKey = "a" },
                        new KanbanColumn("📐 Design", new Color(0.8f, 0.5f, 0.8f)) { sortKey = "d" },
                        new KanbanColumn("🔧 Development", new Color(0.4f, 0.6f, 0.9f)) { sortKey = "h" },
                        new KanbanColumn("🧪 QA", new Color(0.9f, 0.7f, 0.3f)) { sortKey = "m" },
                        new KanbanColumn("🚀 Released", new Color(0.4f, 0.8f, 0.5f)) { sortKey = "z" }
                    };

                default:
                    return new List<KanbanColumn>();
            }
        }

        private static List<KanbanColumn> LoadColumns(string folderPath)
        {
            var columnsFolder = Path.Combine(folderPath, COLUMNS_FOLDER);
            var columns = new List<KanbanColumn>();

            if (!Directory.Exists(columnsFolder))
                return columns;

            foreach (var file in Directory.GetFiles(columnsFolder, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var column = JsonUtility.FromJson<KanbanColumn>(json);
                    if (column != null && !string.IsNullOrEmpty(column.id))
                    {
                        columns.Add(column);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to load column from {file}: {e.Message}");
                }
            }

            columns.Sort((a, b) => string.Compare(a.sortKey ?? "", b.sortKey ?? "", StringComparison.Ordinal));
            return columns;
        }

        public static void SaveColumn(string folderPath, KanbanColumn column)
        {
            var columnsFolder = Path.Combine(folderPath, COLUMNS_FOLDER);
            EnsureDirectoryExists(columnsFolder);

            var columnFile = Path.Combine(columnsFolder, $"{column.id}.json");
            var json = JsonUtility.ToJson(column, true);
            File.WriteAllText(columnFile, json);
        }

        public static void DeleteColumn(string folderPath, string columnId)
        {
            var columnFile = Path.Combine(folderPath, COLUMNS_FOLDER, $"{columnId}.json");
            if (File.Exists(columnFile))
            {
                File.Delete(columnFile);
                var metaFile = columnFile + ".meta";
                if (File.Exists(metaFile))
                    File.Delete(metaFile);
            }
            RefreshAssets();
        }

        public static CardContent LoadCardContent(string folderPath, string cardId)
        {
            var contentFile = Path.Combine(folderPath, CARDS_FOLDER, $"{cardId}{CONTENT_SUFFIX}");
            if (!File.Exists(contentFile))
                return null;

            try
            {
                var json = File.ReadAllText(contentFile);
                return JsonUtility.FromJson<CardContent>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load card content {cardId}: {e.Message}");
                return null;
            }
        }

        public static CardPosition LoadCardPosition(string folderPath, string cardId)
        {
            var positionFile = Path.Combine(folderPath, CARDS_FOLDER, $"{cardId}{POSITION_SUFFIX}");
            if (!File.Exists(positionFile))
                return null;

            try
            {
                var json = File.ReadAllText(positionFile);
                return JsonUtility.FromJson<CardPosition>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load card position {cardId}: {e.Message}");
                return null;
            }
        }

        public static KanbanCard LoadCard(string folderPath, string cardId)
        {
            var content = LoadCardContent(folderPath, cardId);
            if (content == null) return null;

            var position = LoadCardPosition(folderPath, cardId);
            return KanbanCard.FromContentAndPosition(content, position);
        }

        public static void SaveCardContent(string folderPath, CardContent content)
        {
            var cardsFolder = Path.Combine(folderPath, CARDS_FOLDER);
            EnsureDirectoryExists(cardsFolder);

            var contentFile = Path.Combine(cardsFolder, $"{content.id}{CONTENT_SUFFIX}");
            var json = JsonUtility.ToJson(content, true);
            File.WriteAllText(contentFile, json);
        }

        public static void SaveCardPosition(string folderPath, string cardId, CardPosition position)
        {
            var cardsFolder = Path.Combine(folderPath, CARDS_FOLDER);
            EnsureDirectoryExists(cardsFolder);

            var positionFile = Path.Combine(cardsFolder, $"{cardId}{POSITION_SUFFIX}");
            var json = JsonUtility.ToJson(position, true);
            File.WriteAllText(positionFile, json);
        }

        public static void SaveCard(string folderPath, KanbanCard card)
        {
            SaveCardContent(folderPath, card.ToContent());
            SaveCardPosition(folderPath, card.id, card.ToPosition());
        }

        public static void DeleteCard(string folderPath, string cardId)
        {
            var cardsFolder = Path.Combine(folderPath, CARDS_FOLDER);

            var contentFile = Path.Combine(cardsFolder, $"{cardId}{CONTENT_SUFFIX}");
            if (File.Exists(contentFile))
            {
                File.Delete(contentFile);
                var metaFile = contentFile + ".meta";
                if (File.Exists(metaFile))
                    File.Delete(metaFile);
            }

            var positionFile = Path.Combine(cardsFolder, $"{cardId}{POSITION_SUFFIX}");
            if (File.Exists(positionFile))
            {
                File.Delete(positionFile);
                var metaFile = positionFile + ".meta";
                if (File.Exists(metaFile))
                    File.Delete(metaFile);
            }

            RefreshAssets();
        }

        public static List<string> GetAllCardIds(string folderPath)
        {
            var cardsFolder = Path.Combine(folderPath, CARDS_FOLDER);
            var cardIds = new HashSet<string>();

            if (!Directory.Exists(cardsFolder))
                return new List<string>();

            foreach (var file in Directory.GetFiles(cardsFolder, $"*{CONTENT_SUFFIX}"))
            {
                var fileName = Path.GetFileName(file);
                var cardId = fileName.Substring(0, fileName.Length - CONTENT_SUFFIX.Length);
                cardIds.Add(cardId);
            }

            return cardIds.ToList();
        }

        public static List<KanbanCard> GetCardsInColumn(string folderPath, string columnId)
        {
            var cardIds = GetAllCardIds(folderPath);
            var cardsInColumn = new List<KanbanCard>();

            foreach (var cardId in cardIds)
            {
                var position = LoadCardPosition(folderPath, cardId);
                if (position != null && position.columnId == columnId)
                {
                    var content = LoadCardContent(folderPath, cardId);
                    if (content != null)
                    {
                        var card = KanbanCard.FromContentAndPosition(content, position);
                        cardsInColumn.Add(card);
                    }
                }
            }

            cardsInColumn.Sort((a, b) => string.Compare(a.sortKey ?? "", b.sortKey ?? "", StringComparison.Ordinal));
            return cardsInColumn;
        }

        public static List<string> FindBoardFolders(string searchPath)
        {
            var boards = new List<string>();

            if (!Directory.Exists(searchPath))
                return boards;

            foreach (var dir in Directory.GetDirectories(searchPath))
            {
                var boardFile = Path.Combine(dir, BOARD_FILE);
                if (File.Exists(boardFile))
                {
                    boards.Add(dir);
                }
            }

            return boards;
        }

        public static string GetBoardName(string folderPath)
        {
            var boardFile = Path.Combine(folderPath, BOARD_FILE);
            if (!File.Exists(boardFile))
                return Path.GetFileName(folderPath);

            try
            {
                var json = File.ReadAllText(boardFile);
                var data = JsonUtility.FromJson<BoardFileData>(json);
                return data.boardName;
            }
            catch
            {
                return Path.GetFileName(folderPath);
            }
        }

        private static void EnsureFolderStructure(string folderPath)
        {
            EnsureDirectoryExists(folderPath);
            EnsureDirectoryExists(Path.Combine(folderPath, COLUMNS_FOLDER));
            EnsureDirectoryExists(Path.Combine(folderPath, CARDS_FOLDER));
        }

        private static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private static void RefreshAssets()
        {
            EditorApplication.delayCall += () => AssetDatabase.Refresh();
        }

        [Serializable]
        private class BoardFileData
        {
            public string boardName;
            public List<Assignee> assignees;
            public List<string> allTags;
        }
    }
}
