#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;
using miniRAID;
using miniRAID.UIElements;

public static class BossStatsControllerChecks
{
    public static string Run()
    {
        var go = new GameObject("Isolated Boss HUD check") { hideFlags = HideFlags.HideAndDontSave };
        go.SetActive(false);
        var descriptor = ScriptableObject.CreateInstance<EnemyMobDescriptorSO>();
        try
        {
            var renderer = go.AddComponent<MobRenderer>();
            var root = new VisualElement();
            root.Add(new VisualElement { name = "HPContent" });
            foreach (var name in new[] { "Name", "Buffs", "Incoming", "Debuffs", "HPNum", "HPPercentage" })
                root.Add(new Label { name = name });
            renderer.data = new MobData { baseDescriptor = descriptor, nickname = "Before", health = 75, maxHealth = 100 };
            var hud = new BossStatsController(root);
            hud.Register(renderer);
            if (root.Q<Label>("HPNum").text != "75 / 100") throw new Exception("Initial HP binding");

            // Deserialization replaces MobRenderer.data without selecting the boss again.
            var restored = new MobData { baseDescriptor = descriptor, nickname = "Restored", health = 33, maxHealth = 120 };
            renderer.data = restored;
            hud.Update();
            if (root.Q<Label>("HPNum").text != "33 / 120") throw new Exception("Restored HP binding");
            if (!root.Q<Label>("Name").text.Contains("Restored")) throw new Exception("Restored identity binding");

            restored.health = 0;
            UnityEngine.Object.DestroyImmediate(go);
            hud.Update();
            if (root.Q<Label>("HPNum").text != "0 / 120") throw new Exception("Destroyed renderer fallback");
            return "4 Boss HUD checks passed";
        }
        finally
        {
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(descriptor);
        }
    }
}
#endif
