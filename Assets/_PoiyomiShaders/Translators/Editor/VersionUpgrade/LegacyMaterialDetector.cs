// Designed by BluWizard LABS - https://github.com/BluWizard10
//
// Reads the version from a PRESENT shader's `shader_master_label`.
// Fails for 9.2-and-older because those shaders were deleted from the package:
//   - Unlocked materials fall back to Hidden/InternalErrorShader (no label to read),
//   - Locked materials can silently mis-resolve to 10.0 via the OriginalShader NAME tag.
//
// This detector recovers the removed state from signals that survive the shader removal:
//   1. OriginalShaderGUID tag - if the GUID no longer resolves to an asset, the original was DELETED.
//      This is collision-proof, unlike the name tag which can resolve to the 10.0 shader.
//   2. Property fingerprint - names that existed in 9.0-9.3 and were RENAMED/REMOVED in 10.0
//      (_ALUVPosition, _FlipbookScaleOffset, _RimSharpness, _ContinuousDissolve). Read straight from
//      the serialized property sheet, so it works even with no shader assigned.
//
// The empirical delta (see research): Toon 9.2 == 9.3 exactly; Pro 9.2 -> 9.3 only drops the DPS
// penetrator system + legacy anisotropic-noise map, both removed upstream. So the target is always 9.3
// and there are zero property remaps.

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Thry.ThryEditor;

namespace Poi.Tools.ShaderTranslator.VersionUpgrade
{
    public enum PoiyomiEdition
    {
        Unknown, Toon, Pro
    }

    // Info grabber used to reroute a material using a removed version onto 9.3.
    public struct LegacyMaterialInfo
    {
        public bool IsLocked;
        public bool LockedShaderBroken;
        public PoiyomiEdition Edition;
        public string Variant;
        public System.Version DetectedVersion;
        public bool UsesDps;
        public bool UsesLegacyAnisoNoise;
    }

    public static class LegacyMaterialDetector
    {
        public static readonly System.Version Nine3 = new System.Version(9, 3);

        static readonly string[] Pre10FingerprintMarkers =
		{
			"_ALUVPosition", "_ALUVScale", "_ALUVRotation",
			"_FlipbookScaleOffset", "_RimSharpness", "_Rim2Sharpness", "_ContinuousDissolve"
		};
        static readonly string[] DpsMarkers =
		{
			"_PenetratorEnabled",
            "_OrifaceEnabled",
            "_OrificeData",
			"_Squeeze",
            "_Wriggle",
            "_Curvature",
            "_Length",
            "_Shape1Depth",
            "_BlendshapePower"
		};
        static readonly string[] DpsEnableToggles =
        {
            "_PenetratorEnabled",
            "_OrifaceEnabled"
        };
        const string LegacyAnisoNoiseTex = "_AnisoNoiseMap";

        const string LockedShaderPrefix = "Hidden/Locked/";

        // Sets to true if the Material is stranded on 9.2 or older and should be rerouted to 9.3.
        public static bool TryDetectLegacyNine(Material material, out LegacyMaterialInfo info)
        {
            info = default;
            if (material == null || material.shader == null) return false;

            var reader = MaterialSerializedReader.Read(material);

            bool hasPre10Fingerprint = reader.HasAny(Pre10FingerprintMarkers);
            string originalTag = material.GetTag(ShaderOptimizer.TAG_ORIGINAL_SHADER, false, string.Empty);
            bool looksPoiyomi = ShaderNameIsPoiyomi(material.shader.name) || originalTag.IndexOf("poiyomi", StringComparison.OrdinalIgnoreCase) != -1 || hasPre10Fingerprint;

            if (!looksPoiyomi) return false;

            bool locked = IsLocked(material, reader);

            if (locked)
            {
                if (!TryDetectLocked(material, originalTag, hasPre10Fingerprint, ref info)) return false;
            }
            else
            {
                if (!TryDetectUnlocked(material, hasPre10Fingerprint, ref info)) return false;
            }

            FinalizeInfo(reader, originalTag, ref info);
            return true;
        }

        // Convenience wrapper for menu validation
        public static bool NeedsLegacyUpgrade(Material material) => TryDetectLegacyNine(material, out _);

        // ========== LOCKED PATH ==========

        // The OriginalShaderGUID tag is the signal we're using. If it no longer resolves to an asset, the
		// original shader was deleted (a removed version). If it DOES resolve, read that shader's version.
        static bool TryDetectLocked(Material material, string originalTag, bool hasPre10Fingerprint, ref LegacyMaterialInfo info)
        {
            info.IsLocked = true;
            info.LockedShaderBroken = material.shader.IsBroken();

            string guid = material.GetTag(ShaderOptimizer.TAG_ORIGINAL_SHADER_GUID, false, string.Empty);
            if (!string.IsNullOrEmpty(guid))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                {
                    // Only resolve if it's version is pre-9.3
                    Shader original = AssetDatabase.LoadAssetAtPath<Shader>(path);
                    if (original != null && PoiyomiVersionDetector.TryGetVersionFromShader(original, out System.Version v))
                    {
                        info.DetectedVersion = v;
                        info.Variant = PoiyomiVersionDetector.GetShaderVariant(original);
                        return v < Nine3;
                    }
                }
            }

            // If GUID is dead or unreadable, then confirm if it's really Poiyomi 9.x
            if (originalTag.IndexOf("poiyomi", StringComparison.OrdinalIgnoreCase) == -1 && !hasPre10Fingerprint) return false;

            info.Variant = VariantFromLockedName(material.shader.name, originalTag);
            info.DetectedVersion = ParseVersionFromTag(originalTag);
            return true;
        }

        // ========== UNLOCKED PATH ==========

        static bool TryDetectUnlocked(Material material, bool hasPre10Fingerprint, ref LegacyMaterialInfo info)
        {
            // Present, healthy. Only use if it reads as a pre-9.3 in a rare post-removal.
            if (!material.shader.IsBroken())
            {
                if (PoiyomiVersionDetector.TryGetVersionFromShader(material.shader, out System.Version v))
                {
                    if (v >= Nine3) return false;
                    info.DetectedVersion = v;
                    info.Variant = PoiyomiVersionDetector.GetShaderVariant(material.shader);
                    return true;
                }
                return false;
            }

            // Fingerprint decides if it's broken or missing (hopefully an unlocked <= 9.2 material).
            if (!hasPre10Fingerprint) return false;

            info.Variant = null;
            info.DetectedVersion = null;
            return true;
        }

        // ========== SHARED FINALIZATION ==========

        static void FinalizeInfo(MaterialSerializedReader reader, string originalTag, ref LegacyMaterialInfo info)
        {
            info.Edition = ResolveEdition(info.Variant, originalTag, reader);

            foreach (string toggle in DpsEnableToggles)
            {
                if (reader.GetFloat(toggle, 0f) > 0.5f)
                {
                    info.UsesDps = true;
                    break;
                }
            }

            info.UsesLegacyAnisoNoise = reader.HasTextureAssigned(LegacyAnisoNoiseTex);
        }

        static PoiyomiEdition ResolveEdition(string variant, string originalTag, MaterialSerializedReader reader)
        {
            if (!string.IsNullOrEmpty(variant)) return variant.IndexOf(" Pro", StringComparison.OrdinalIgnoreCase) != -1 ? PoiyomiEdition.Pro : PoiyomiEdition.Toon;

            if (originalTag.IndexOf("Poiyomi Pro", StringComparison.OrdinalIgnoreCase) != -1) return PoiyomiEdition.Pro;
            if (originalTag.IndexOf("Poiyomi Toon", StringComparison.OrdinalIgnoreCase) != -1) return PoiyomiEdition.Toon;

            return reader.HasAny(DpsMarkers) ? PoiyomiEdition.Pro : PoiyomiEdition.Toon;
        }

        // ========== HELPERS ==========

        static bool ShaderNameIsPoiyomi(string name) => !string.IsNullOrEmpty(name) && name.IndexOf("poiyomi", StringComparison.OrdinalIgnoreCase) != -1;

        static bool IsLocked(Material material, MaterialSerializedReader reader)
        {
            if (material.shader.name.StartsWith(LockedShaderPrefix, StringComparison.OrdinalIgnoreCase)) return true;

            return reader.GetFloat("_ShaderOptimizerEnabled", 0f) > 0.5f;
        }

        static string VariantFromLockedName(string lockedOrErrorName, string originalTag)
        {
            // Prefer the locked shader name; fall back to the OriginalShader tag.
            string fromShader = GetVariantFromName(lockedOrErrorName);
			if (!string.IsNullOrEmpty(fromShader)) return fromShader;
			return GetVariantFromName(originalTag);
        }

        // Extracts the "Poiyomi ..." variant from a raw shader-name string (a locked shader name or an
        // OriginalShader tag). Mirrors the VersionDetector but works on a string, since removed versions
        // have no live Shader to read. Self-contained and needs no edits to the original version detector.
        public static string GetVariantFromName(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName)) return null;

            // Find last "Poiyomi" - the variant always starts with it (not .poiyomi in the path).
            int poiIndex = shaderName.LastIndexOf("Poiyomi", StringComparison.OrdinalIgnoreCase);
            if (poiIndex < 0) return null;

            string name = shaderName.Substring(poiIndex);

            // For locked shaders, strip the guid suffix (everything after /).
            int slashIndex = name.IndexOf('/');
            if (slashIndex > 0) name = name.Substring(0, slashIndex);

            return name;
        }

        static System.Version ParseVersionFromTag(string originalTag)
        {
            // e.g. ".poiyomi/Old Versions/9.2/Poiyomi Toon" -> 9.2. Locked-as-current tags (".poiyomi/Poiyomi Toon")
			// carry no version, so this returns null and the caller treats it as generic pre-10.0.
            return PoiyomiVersionDetector.TryParseVersionFromLabel(originalTag, out System.Version v) ? v : null;
        }

        // Reads a material's serialized property sheet directly, so property names/values remain visible even
        // when the current shader declares none of them.
        public sealed class MaterialSerializedReader
        {
            readonly HashSet<string> _names = new HashSet<string>(StringComparer.Ordinal);
			readonly Dictionary<string, float> _floats = new Dictionary<string, float>(StringComparer.Ordinal);
			readonly HashSet<string> _assignedTextures = new HashSet<string>(StringComparer.Ordinal);

			public bool Has(string name) => _names.Contains(name);
			public bool HasAny(string[] names) { foreach (var n in names) if (_names.Contains(n)) return true; return false; }
			public bool HasTextureAssigned(string name) => _assignedTextures.Contains(name);
			public float GetFloat(string name, float fallback) => _floats.TryGetValue(name, out float v) ? v : fallback;

            public static MaterialSerializedReader Read(Material material)
            {
                var r = new MaterialSerializedReader();
				if (material == null) return r;

				var so = new SerializedObject(material);
				SerializedProperty saved = so.FindProperty("m_SavedProperties");
				if (saved == null) return r;

				ReadFloats(saved.FindPropertyRelative("m_Floats"), r);
				ReadFloats(saved.FindPropertyRelative("m_Ints"), r);   // null-safe; older Unity keeps ints here
				ReadNames(saved.FindPropertyRelative("m_Colors"), r);
				ReadTextures(saved.FindPropertyRelative("m_TexEnvs"), r);
				return r;
            }

            static void ReadFloats(SerializedProperty array, MaterialSerializedReader r)
            {
                if (array == null || !array.isArray) return;
				for (int i = 0; i < array.arraySize; i++)
				{
					SerializedProperty e = array.GetArrayElementAtIndex(i);
					string name = EntryName(e);
					if (string.IsNullOrEmpty(name)) continue;
					r._names.Add(name);
					SerializedProperty val = e.FindPropertyRelative("second");
					if (val != null) r._floats[name] = val.propertyType == SerializedPropertyType.Integer ? val.intValue : val.floatValue;
				}
            }

            static void ReadNames(SerializedProperty array, MaterialSerializedReader r)
            {
                if (array == null || !array.isArray) return;
				for (int i = 0; i < array.arraySize; i++)
				{
					string name = EntryName(array.GetArrayElementAtIndex(i));
					if (!string.IsNullOrEmpty(name)) r._names.Add(name);
				}
            }

            static void ReadTextures(SerializedProperty array, MaterialSerializedReader r)
            {
                if (array == null || !array.isArray) return;
				for (int i = 0; i < array.arraySize; i++)
				{
					SerializedProperty e = array.GetArrayElementAtIndex(i);
					string name = EntryName(e);
					if (string.IsNullOrEmpty(name)) continue;
					r._names.Add(name);
					var tex = e.FindPropertyRelative("second")?.FindPropertyRelative("m_Texture")?.objectReferenceValue;
					if (tex != null) r._assignedTextures.Add(name);
				}
            }

            static string EntryName(SerializedProperty entry)
            {
                SerializedProperty first = entry.FindPropertyRelative("first");
				if (first == null) return null;
				if (first.propertyType == SerializedPropertyType.String) return first.stringValue;
				return first.FindPropertyRelative("name")?.stringValue;
            }
        }
    }
}
