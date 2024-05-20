using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class KanbanWindow : EditorWindow
{
    private KanbanAsset m_KanbanAsset;
    public KanbanAsset KanbanAsset { get => m_KanbanAsset; set { m_KanbanAsset = value; OnKanbanAssetChanged(); } }

    [UnityEditor.Callbacks.OnOpenAsset(UnityEditor.Callbacks.OnOpenAssetAttributeMode.Execute)]
    public static bool OnOpenAsset(int instanceID, int line)
    {
        string assetPath = AssetDatabase.GetAssetPath(instanceID);
        KanbanAsset scriptableObject = AssetDatabase.LoadAssetAtPath<KanbanAsset>(assetPath);
        if (scriptableObject != null)
        {
            KanbanWindow window = (KanbanWindow)GetWindow(typeof(KanbanWindow));
            window.KanbanAsset = scriptableObject;
            window.Show();
            return true;
        }
        return false;
    }

    void OnKanbanAssetChanged()
    {
        this.titleContent = new($"{KanbanAsset.name} - Kanban");
    }

    const float COLUMN_SPACING = 20f;
    const float COLUMN_WIDTH = 250f;
    const float CARD_WIDTH = COLUMN_WIDTH - CARD_H_MARGIN * 2;
    const float CARD_H_MARGIN = 10f;
    const float CARD_SPACING = 5f;

    private int editedColumn;
    private int editedCard;

    private bool dragging;
    private int draggedColumnIndex;
    private int draggedCardIndex;

    private int dropCardIndex;
    private int dropColumnIndex;

    private bool DraggingColumn => dragging && draggedColumnIndex >= 0 && draggedCardIndex == -1;
    private bool DraggingCard => dragging && draggedColumnIndex >= 0 && draggedCardIndex >= 0;
    private float DraggedCardHeight => DraggingCard ? textHeightWrapped(KanbanAsset.Columns[draggedColumnIndex].cards[draggedCardIndex].title, CARD_WIDTH, GUI.skin.box) : 0;

    public void OnGUI()
    {
        if (KanbanAsset == null) return;
        GUI.color = Color.white;
        GUI.backgroundColor = Color.white;
        Event e = Event.current;
        dropCardIndex = -1;
        dropColumnIndex = -1;
        float x = 0;
        float y = 0;
        Undo.RecordObject(KanbanAsset, "Kanban Window - Rename");
        for (int columnIndex = 0; columnIndex < KanbanAsset.Columns.Count; columnIndex++)
        {
            DrawColumn(e, ref x, ref y, columnIndex);
            y = 0;
        }
        x += COLUMN_SPACING;
        Undo.FlushUndoRecordObjects();

        Undo.RecordObject(KanbanAsset, "Kanban Window - Add Column");
        DrawAddColumnButton(e, ref x, ref y);
        Undo.FlushUndoRecordObjects();

        if (e.type == EventType.MouseDrag && e.button == 0) dragging = true;
        if (e.type == EventType.MouseUp && e.button == 0 && dragging)
        {
            Undo.RecordObject(KanbanAsset, "Kanban Window - Drop");
            if (DraggingCard)
            {
                dropColumnIndex = Mathf.Max(0, dropColumnIndex);
                var card = KanbanAsset.Columns[draggedColumnIndex].cards[draggedCardIndex];
                var dropColumn = KanbanAsset.Columns[dropColumnIndex];
                var dropCard = dropColumn.cards.Count > 0 && dropColumn.cards.Count > dropCardIndex ? dropColumn.cards[dropCardIndex] : null;

                KanbanAsset.Columns[draggedColumnIndex].cards.Remove(card);
                if (dropCard != null) dropColumn.cards.Insert(dropColumn.cards.IndexOf(dropCard), card);
                else dropColumn.cards.Add(card);
                EditorUtility.SetDirty(KanbanAsset);
            }
            else if (DraggingColumn)
            {
                var draggedColumn = KanbanAsset.Columns[draggedColumnIndex];
                var droppedColumn = KanbanAsset.Columns.Count > 0 && KanbanAsset.Columns.Count > dropColumnIndex && dropColumnIndex >= 0 ? KanbanAsset.Columns[dropColumnIndex] : null;

                KanbanAsset.Columns.Remove(draggedColumn);
                if (droppedColumn != null) KanbanAsset.Columns.Insert(KanbanAsset.Columns.IndexOf(droppedColumn), draggedColumn);
                else KanbanAsset.Columns.Add(draggedColumn);
                EditorUtility.SetDirty(KanbanAsset);
            }
            Undo.FlushUndoRecordObjects();

            dragging = false;
            draggedCardIndex = -1;
            draggedColumnIndex = -1;
        }
        Repaint();
    }

    private Rect DrawAddColumnButton(Event e, ref float x, ref float y)
    {
        if (DraggingColumn) return new();
        y += CARD_SPACING;
        var buttonHeight = textHeightWrapped("+", COLUMN_WIDTH, GUI.skin.box);
        var buttonRect = new Rect(x, y, COLUMN_WIDTH, buttonHeight);
        GUI.backgroundColor = Color.white * (buttonRect.Contains(e.mousePosition) && !dragging ? 2.5f : 2);
        if (GUI.Button(buttonRect, "+", GUI.skin.box) && e.button == 0)
        {
            KanbanAsset.Columns.Add(new());
            editedColumn = KanbanAsset.Columns.Count - 1;
            editedCard = -1;
            EditorUtility.SetDirty(KanbanAsset);
        }
        GUI.backgroundColor = Color.white;
        y += buttonHeight;
        return buttonRect;
    }

    private Rect DrawColumn(Event e, ref float x, ref float y, int columnIndex)
    {
        y += CARD_SPACING;
        x += COLUMN_SPACING;
        var column = KanbanAsset.Columns[columnIndex];
        var draggingThisColumn = dragging && draggedCardIndex == -1 && draggedColumnIndex == columnIndex;
        var mp = e.mousePosition;
        if (DraggingColumn && !(draggedColumnIndex == columnIndex))
        {
            var mouseInColumn = mp.x >= x && mp.x <= x + COLUMN_WIDTH + COLUMN_SPACING;
            if (mouseInColumn)
            {
                x += DraggedCardHeight + COLUMN_WIDTH + COLUMN_SPACING;
                dropColumnIndex = columnIndex;
                dropCardIndex = -1;
            }
        }

        var draggedX = e.mousePosition.x - COLUMN_WIDTH / 2f;
        var draggedY = e.mousePosition.y - textHeightWrapped(column.title, COLUMN_WIDTH, GUI.skin.box);

        if (!draggingThisColumn)
        {
            DrawColumnTitle(e, column, ref x, ref y, columnIndex);
            y += CARD_SPACING;
            for (int cardIndex = 0; cardIndex < column.cards.Count; cardIndex++)
            {
                var card = column.cards[cardIndex];
                DrawCardTitle(e, card, ref x, ref y, columnIndex, cardIndex);
                y += CARD_SPACING;
            }
            DrawAddCardButton(e, column, ref x, ref y, columnIndex);
            x += COLUMN_WIDTH;
            return new(x - COLUMN_WIDTH, CARD_SPACING, COLUMN_WIDTH, y - CARD_SPACING);
        }
        else
        {
            DrawColumnTitle(e, column, ref draggedX, ref draggedY, columnIndex);
            draggedY += CARD_SPACING;
            for (int cardIndex = 0; cardIndex < column.cards.Count; cardIndex++)
            {
                var card = column.cards[cardIndex];
                DrawCardTitle(e, card, ref draggedX, ref draggedY, columnIndex, cardIndex);
                draggedY += CARD_SPACING;
            }
            DrawAddCardButton(e, column, ref draggedX, ref draggedY, columnIndex);
            draggedX += COLUMN_WIDTH;
            x -= COLUMN_SPACING;
            return new(e.mousePosition.x - COLUMN_WIDTH / 2f, e.mousePosition.y, COLUMN_WIDTH, draggedY - e.mousePosition.y);
        }
    }

    private Rect DrawAddCardButton(Event e, KanbanAsset.Column column, ref float x, ref float y, int columnIndex)
    {
        var mp = e.mousePosition;
        if (DraggingCard)
        {
            var mouseInColumn = mp.x > x && mp.x < x + COLUMN_WIDTH;
            if (mouseInColumn)
            {
                var draggedCardCenter = mp.y - DraggedCardHeight / 2f;
                if (column.cards.Count == 0 || (column.cards.Count == 1 && columnIndex == draggedColumnIndex) || draggedCardCenter >= y)
                {
                    y += DraggedCardHeight + CARD_SPACING;
                    dropColumnIndex = columnIndex;
                    dropCardIndex = column.cards.Count;
                }
            }
        }

        var buttonHeight = textHeightWrapped("+", CARD_WIDTH, GUI.skin.box);
        var buttonRect = new Rect(x + CARD_H_MARGIN, y, CARD_WIDTH, buttonHeight);
        GUI.backgroundColor = Color.white * (buttonRect.Contains(mp) && !dragging ? 2.5f : 2);
        if (GUI.Button(buttonRect, "+", GUI.skin.box) && e.button == 0)
        {
            column.cards.Add(new());
            editedColumn = columnIndex;
            editedCard = column.cards.Count - 1;
            EditorUtility.SetDirty(KanbanAsset);
        }
        GUI.backgroundColor = Color.white;
        y += buttonHeight;
        return buttonRect;
    }

    private Rect DrawCardTitle(Event e, KanbanAsset.Card card, ref float x, ref float y, int columnIndex, int cardIndex)
    {
        var titleHeight = textHeightWrapped(card.title, CARD_WIDTH, GUI.skin.box);

        var mp = e.mousePosition;
        if (DraggingCard && !(draggedCardIndex == cardIndex && draggedColumnIndex == columnIndex))
        {
            var mouseInColumn = mp.x > x && mp.x < x + COLUMN_WIDTH;
            if (mouseInColumn)
            {
                var draggedCardCenter = mp.y - DraggedCardHeight / 2f;
                dropColumnIndex = columnIndex;
                if ((cardIndex == 0 || (cardIndex == 1 && draggedColumnIndex == columnIndex && draggedCardIndex == 0) || y <= draggedCardCenter) && (y + titleHeight + CARD_SPACING) >= draggedCardCenter)
                {
                    y += DraggedCardHeight + CARD_SPACING;
                    dropCardIndex = cardIndex;
                }
            }
        }

        var boxRect = new Rect(x + CARD_H_MARGIN, y, CARD_WIDTH, titleHeight);
        if (e.type == EventType.ContextClick && !dragging)
        {
            if (boxRect.Contains(e.mousePosition)) OpenCardContextMenu(columnIndex, cardIndex);
        }

        var editingThisCardTitle = editedCard == cardIndex && editedColumn == columnIndex && !dragging;
        if (e.type == EventType.MouseDrag && !dragging && e.button == 0 && !editingThisCardTitle && boxRect.Contains(e.mousePosition))
        {
            draggedCardIndex = cardIndex;
            draggedColumnIndex = columnIndex;
            editedCard = -1;
            editedColumn = -1;
            dragging = true;
        }

        var draggingThisCard = dragging && draggedCardIndex == cardIndex && draggedColumnIndex == columnIndex;
        if (e.type == EventType.MouseUp && e.button == 0 && !dragging)
        {
            if (boxRect.Contains(e.mousePosition))
            {
                {
                    editedCard = cardIndex;
                    editedColumn = columnIndex;
                }
            }
            else if (editingThisCardTitle) editedCard = editedColumn = -1;
        }
        if (e.keyCode == KeyCode.Return) editedCard = editedColumn = -1;

        if (draggingThisCard) boxRect = new(e.mousePosition.x - boxRect.width / 2, e.mousePosition.y - boxRect.height, boxRect.width, boxRect.height);
        if (editingThisCardTitle)
        {
            GUI.SetNextControlName("Card Title Text Area");
            GUI.backgroundColor = Color.gray;
            EditorGUI.BeginChangeCheck();
            card.title = GUI.TextArea(boxRect, card.title, GUI.skin.box);
            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(KanbanAsset);
            GUI.backgroundColor = Color.white;
            GUI.FocusControl("Card Title Text Area");
        }
        else GUI.Box(boxRect, card.title, GUI.skin.box);

        if (!DraggingCard || !(draggedCardIndex == cardIndex && draggedColumnIndex == columnIndex)) y += titleHeight;
        else y -= CARD_SPACING;
        return boxRect;
    }

    private Rect DrawColumnTitle(Event e, KanbanAsset.Column column, ref float x, ref float y, int columnIndex)
    {
        var titleHeight = textHeightWrapped(column.title, COLUMN_WIDTH, GUI.skin.box);
        var editingThisColumnTitle = editedCard == -1 && editedColumn == columnIndex;

        var boxRect = new Rect(x, y, COLUMN_WIDTH, titleHeight);
        if (e.type == EventType.ContextClick && !dragging)
        {
            if (boxRect.Contains(e.mousePosition)) OpenColumnContextMenu(columnIndex);
        }
        if (e.type == EventType.MouseDrag && !dragging && e.button == 0 && !editingThisColumnTitle && boxRect.Contains(e.mousePosition))
        {
            draggedCardIndex = -1;
            draggedColumnIndex = columnIndex;
            editedCard = -1;
            editedColumn = -1;
            dragging = true;
        }

        if (e.type == EventType.MouseUp && e.button == 0 && !dragging)
        {
            if (boxRect.Contains(e.mousePosition))
            {
                {
                    editedCard = -1;
                    editedColumn = columnIndex;
                }
            }
            else if (editingThisColumnTitle) editedCard = editedColumn = -1;
        }
        if (e.keyCode == KeyCode.Return) editedCard = editedColumn = -1;

        if (editingThisColumnTitle)
        {
            GUI.SetNextControlName("Column Title Text Area");
            GUI.backgroundColor = Color.gray;
            EditorGUI.BeginChangeCheck();
            column.title = GUI.TextArea(boxRect, column.title, GUI.skin.box);
            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(KanbanAsset);
            GUI.backgroundColor = Color.white;
            GUI.FocusControl("Column Title Text Area");
        }
        else GUI.Box(boxRect, column.title, GUI.skin.box);
        y += titleHeight;
        return boxRect;
    }

    private void OpenColumnContextMenu(int columnIndex)
    {
        var menu = new GenericMenu();
        var column = KanbanAsset.Columns[columnIndex];
        menu.AddItem(new("Rename List"), false, () => UndoableAction(() => { editedColumn = columnIndex; editedCard = -1; }));
        menu.AddItem(new("Add Card"), false, () => UndoableAction(() => { column.cards.Add(new()); editedColumn = columnIndex; editedCard = column.cards.Count - 1; }));
        menu.AddItem(new("Delete List"), false, () => UndoableAction(() => { KanbanAsset.Columns.RemoveAt(columnIndex); }));
        menu.ShowAsContext();
    }

    private void OpenCardContextMenu(int columnIndex, int cardIndex)
    {
        var menu = new GenericMenu();
        var column = KanbanAsset.Columns[columnIndex];
        var card = column.cards[cardIndex];

        menu.AddItem(new("Rename Card"), false, () => UndoableAction(() => { editedColumn = columnIndex; editedCard = cardIndex; }));
        menu.AddItem(new("Delete Card"), false, () => UndoableAction(() => { column.cards.RemoveAt(cardIndex); }));
        foreach (var c in KanbanAsset.Columns)
            menu.AddItem(new($"Move to.../{c.title}"), false, () => UndoableAction(() => { column.cards.Remove(card); c.cards.Add(card); }));
        menu.ShowAsContext();
    }

    private void UndoableAction(Action action)
    {
        Undo.RecordObject(KanbanAsset, "Kanban Window - ");
        action();
        EditorUtility.SetDirty(KanbanAsset);
        Undo.FlushUndoRecordObjects();
    }

    private float textHeightWrapped(string text, float width, GUIStyle style)
    {
        var height = style.CalcHeight(new(text), width);
        return height;
    }

}