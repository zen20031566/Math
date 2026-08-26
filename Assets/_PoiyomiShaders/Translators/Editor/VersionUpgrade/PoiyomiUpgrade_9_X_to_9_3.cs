using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Thry;
using Thry.ThryEditor;
using Thry.ThryEditor.Helpers;

namespace Poi.Tools.ShaderTranslator.VersionUpgrade
{
    public static class PoiyomiUpgrade_9_X_to_9_3
    {
        const string Nine3Prefix = ".poiyomi/Old Versions/9.3/";

        // Detect and, if it's a removed-version material, route it onto 9.3.
        public static bool UpgradeToNine3(Material material)
        {
            if (material == null) return false;

            if (!LegacyMaterialDetector.TryDetectLegacyNine(material, out LegacyMaterialInfo info))
            {
                ThryLogger.LogWarn($"Material <b>{material.name}</b> is not a removed-version (9.2 or older) Poiyomi material.");
                return false;
            }

            return UpgradeToNine3(material, info);
        }

        public static bool UpgradeToNine3(Material material, LegacyMaterialInfo info)
        {
            Shader target = ResolveNine3Target(info);
            if (target == null)
            {
                ThryLogger.LogErr($"Could not resolve a 9.3 target shader for <b>{material.name}</b> " + $"(edition: {info.Edition}, variant: {info.Variant ?? "unknown"}). Skipped.");
                return false;
            }

            WarnAboutDroppedFeatures(material, info);

            bool ok = info.IsLocked ? UpgradeLocked(material, target) : UpgradeUnlocked(material, target);

            if (ok)
            {
                EditorUtility.SetDirty(material);
                ThryLogger.Log($"Upgraded <b>{material.name}</b> to <b>{target.name}</b> " + $"({(info.IsLocked ? "unlocked onto 9.3" : "reassigned to 9.3")}). You can update it to 10.0 whenever you're ready.");
            }
            return ok;
        }

        // ========== Locked: Spoof Tags, then Unlock ==========

        static bool UpgradeLocked(Material material, Shader target)
        {
            SpoofRecoveryTags(material, target);

            bool unlocked;
            try
            {
                unlocked = ShaderOptimizer.UnlockMaterials(new[] { material });
            }
            catch (Exception ex)
            {
                ThryLogger.LogErr($"Unlock-to-9.3 failed for <b>{material.name}</b>. Report this with the stack trace below.");
                Debug.LogException(ex);
                return false;
            }

            if (!unlocked)
            {
                ThryLogger.LogErr($"ShaderOptimizer could not unlock <b>{material.name}</b> onto 9.3.");
                return false;
            }

            // Unlock lands on 9.3 but leaves the removed DPS/aniso props behind as orphans, so drop them.
            ScriptedShaderTranslator.RemoveOrphanedProperties(material, target);
            return true;
        }

        static void SpoofRecoveryTags(Material material, Shader target)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(target));
            material.SetOverrideTag(ShaderOptimizer.TAG_ORIGINAL_SHADER_GUID, guid);
            material.SetOverrideTag(ShaderOptimizer.TAG_ORIGINAL_SHADER, target.name);
        }

        // ========== Batching ==========

        public static void UpgradeMaterials(IEnumerable<Material>materials)
        {
            var targets = new Dictionary<Material, Shader>();
            var lockedToUnlock = new List<Material>();
            var unlockedDirect = new List<Material>();

            try
            {
                foreach (Material material in materials)
                {
                    if (material == null || !LegacyMaterialDetector.TryDetectLegacyNine(material, out LegacyMaterialInfo info)) continue;

                    Shader target = ResolveNine3Target(info);
                    if (target == null)
                    {
                        ThryLogger.LogErr($"Could not resolve a 9.3 target for <b>{material.name}</b> (variant: {info.Variant ?? "unknown"}). Skipped.");
                        continue;
                    }

                    WarnAboutDroppedFeatures(material, info);
                    targets[material] = target;

                    if (info.IsLocked)
                    {
                        SpoofRecoveryTags(material, target);
                        lockedToUnlock.Add(material);
                    }
                    else
                    {
                        unlockedDirect.Add(material);
                    }
                }

                // One grouped unlock for every locked material - restores each onto its spoofed 9.3 target.
                if (lockedToUnlock.Count > 0) ShaderOptimizer.UnlockMaterials(lockedToUnlock, ShaderOptimizer.ProgressBar.Cancellable);

                foreach (Material material in lockedToUnlock)
                {
                    ScriptedShaderTranslator.RemoveOrphanedProperties(material, targets[material]);
                    EditorUtility.SetDirty(material);
                }

                foreach (Material material in unlockedDirect)
                {
                    if (UpgradeUnlocked(material, targets[material])) EditorUtility.SetDirty(material);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            int total = lockedToUnlock.Count + unlockedDirect.Count;
            ThryLogger.Log($"Routed {total} legacy material(s) onto 9.3. Update them to 10.0 whenever you're ready.");
        }

        // ========== Unlocked: Direct Reassignment ==========

        static bool UpgradeUnlocked(Material material, Shader target)
        {
            // Swapping a shader wipes the render queue override. Make sure it's preserved across the swap.
            int renderQueue = material.renderQueue;
            material.shader = target;
            material.renderQueue = renderQueue;

            // 9.2 == 9.3 in properties, so same-named values are already carried. Just fix keywords and purge orphans.
            ShaderEditor.FixKeywords(new[] { material });
            ScriptedShaderTranslator.RemoveOrphanedProperties(material, target);
            return true;
        }

        // ========== Target Resolution ==========

        static Shader ResolveNine3Target(LegacyMaterialInfo info)
        {
            if (!string.IsNullOrEmpty(info.Variant))
            {
                // Exact variant match - true for every shared 9.2 variant except for "Poiyomi Pro Geom".
                Shader exact = Shader.Find(Nine3Prefix + info.Variant);
                if (exact != null) return exact;

                // Fuzzy same-family fallback (e.g. 9.2 "Poiyomi Pro Geom" -> 9.3 "Poiyomi Pro Geom Wireframe").
                Shader fuzzy = FuzzyMatchNine3(info.Variant);
                if (fuzzy != null)
                {
                    ThryLogger.LogWarn($"No exact 9.3 variant for <b>{info.Variant}</b>; using closest match <b>{fuzzy.name}</b>.");
                    return fuzzy;
                }
            }

            // Variant unknown (unlocked error shader) - fall back to the edition's base shader.
            switch (info.Edition)
            {
                case PoiyomiEdition.Pro: return Shader.Find(Nine3Prefix + "Poiyomi Pro");
                case PoiyomiEdition.Toon: return Shader.Find(Nine3Prefix + "Poiyomi Toon");
                default: return null;
            }
        }

        // Nearest 9.3 variant of the same edition by edit distance, accepted only if reasonably close.
        static Shader FuzzyMatchNine3(string variant)
        {
            bool wantPro = variant.IndexOf(" Pro", StringComparison.OrdinalIgnoreCase) != -1;
            string best = null;
            int bestDist = int.MaxValue;

            foreach (ShaderInfo si in ShaderUtil.GetAllShaderInfo())
            {
                if (si.name == null || !si.name.StartsWith(Nine3Prefix, StringComparison.Ordinal)) continue;

                string candVariant = LegacyMaterialDetector.GetVariantFromName(si.name);
                if (string.IsNullOrEmpty(candVariant)) continue;

                bool candPro = candVariant.IndexOf(" Pro", StringComparison.OrdinalIgnoreCase) != -1;
                if (candPro != wantPro) continue; // Never cross Toon <-> Pro

                int d = Levenshtein(variant, candVariant);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = si.name;
                }
            }

            if (best != null && bestDist < variant.Length * 0.5f) return Shader.Find(best);
            return null;
        }

        // ========== Warnings ==========

        static void WarnAboutDroppedFeatures(Material material, LegacyMaterialInfo info)
        {
            if (info.UsesDps) ThryLogger.LogWarn($"<b>{material.name}</b>: the built-in DPS penetrator config can't carry to 9.3 " + "(Poiyomi Shaders removed DPS in favour of TPS/SPS). All other settings are preserved.");

            if (info.UsesLegacyAnisoNoise) ThryLogger.LogWarn($"<b>{material.name}</b>: the legacy anisotropic-noise map can't carry to 9.3 " + "(anisotropy was reworked). All other settings are preserved.");
        }

        // Small edit-distance helper (kept local to avoid coupling to Thry internals)
        static int Levenshtein(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return string.IsNullOrEmpty(b) ? 0 : b.Length;
			if (string.IsNullOrEmpty(b)) return a.Length;

            int[] prev = new int[b.Length + 1];
			int[] curr = new int[b.Length + 1];
			for (int j = 0; j <= b.Length; j++) prev[j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                curr[0] = i;
				for (int j = 1; j <= b.Length; j++)
				{
					int cost = a[i - 1] == b[j - 1] ? 0 : 1;
					curr[j] = Mathf.Min(Mathf.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
				}
				var tmp = prev; prev = curr; curr = tmp;
            }
            return prev[b.Length];
        }
    }
}