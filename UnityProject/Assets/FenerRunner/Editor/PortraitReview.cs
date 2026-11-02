using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FenerRunner.Editor
{
    public static class PortraitReview
    {
        [MenuItem("Fener Runner/5 - Open portrait Game view")]
        public static void Open()
        { OpenSize(1920); }
        [MenuItem("Fener Runner/10 - Open tall phone Game view")]
        public static void OpenTall(){OpenSize(2340);}
        public static void OpenSize(int height)
        {
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                if (window.GetType().Name == "PackageManagerWindow") window.Close();
            // Unity's Game view sizing API is internal. These members are verified
            // against the official UnityCsReference 6000.3 GameView source.
            Type sizesType = EditorType("GameViewSizes");
            Type groupType = EditorType("GameViewSizeGroupType");
            Type sizeType = EditorType("GameViewSize");
            Type kindType = EditorType("GameViewSizeType");
            object sizes = sizesType.BaseType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
            MethodInfo getSize = group.GetType().GetMethod("GetGameViewSize");
            int total = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
            int selected = -1;
            string label = "Fener Runner portrait " + height;
            for (int i = 0; i < total; i++)
            {
                object item = getSize.Invoke(group, new object[] { i });
                if ((string)sizeType.GetProperty("baseText").GetValue(item) == label) { selected = i; break; }
            }
            if (selected < 0)
            {
                object size = Activator.CreateInstance(sizeType, new[] { Enum.Parse(kindType, "FixedResolution"), (object)1080, height, label });
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
                selected = total;
                sizesType.GetMethod("SaveToHDD").Invoke(sizes, null);
            }
            Type gameType = EditorType("GameView");
            EditorWindow game = EditorWindow.GetWindow(gameType);
            gameType.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(game, selected);
            game.Show();
            game.Focus();
        }
        private static Type EditorType(string name)
        {
            Type found = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor." + name)).FirstOrDefault(t => t != null);
            if (found == null) throw new InvalidOperationException("Unity Editor Game view API missing: " + name);
            return found;
        }
    }
}
