using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Kanban")]
public class KanbanAsset : ScriptableObject
{
    [HideInInspector] public List<Column> Columns;

    [System.Serializable]
    public class Column
    {
        public string title;
        public List<Card> cards = new();
    }

    [System.Serializable]
    public class Card
    {
        public string title;
        public string content;
        public List<Object> AssetRefs;
    }
}
