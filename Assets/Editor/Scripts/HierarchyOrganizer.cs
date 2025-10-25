using UnityEditor;
using UnityEngine;

namespace Editor.Scripts
{
    public static class HierarchyOrganizer
    {
        [MenuItem("Tools/Organize Hierarchy ")]
        public static void CreateOrganizer()
        {
            CreateGameobject("ENVIRONMENT");
            CreateEmptySpace();
            CreateGameobject("GAMEPLAY");
            CreateEmptySpace();
            CreateGameobject("UI");
            CreateEmptySpace();
            CreateGameobject("MANAGERS");
            CreateEmptySpace();
        } 
        static void CreateEmptySpace(string name = " ")
        {
            GameObject emptySpace = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(emptySpace, "Empty Space GameObject");
        }

        static void CreateGameobject(string name)
        {
            GameObject gameobject = new GameObject($"=== {name} ===");
            Undo.RegisterCreatedObjectUndo(gameobject, "Gameobject");
        }
    }
}
