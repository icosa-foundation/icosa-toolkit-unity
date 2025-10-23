using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

[ScriptedImporter(1, new[] { "icosa-obj" })]
public class IcosaObjScriptedImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    { string assetFullPath = Path.GetFullPath(ctx.assetPath);
        string assetName = Path.GetFileNameWithoutExtension(assetFullPath);

        GameObject root = new GameObject(string.IsNullOrEmpty(assetName) ? "ImportedOBJ" : assetName);
        OBJ loader = root.AddComponent<OBJ>();

        try
        {
            loader.LoadFromFileSync(assetFullPath);

            RegisterDependencies(ctx, loader.LoadedResourcePaths);
            RemoveLoaderComponent(loader);
            RegisterMeshes(ctx, root);
            RegisterMaterialsAndTextures(ctx, root);

            ctx.AddObjectToAsset("root", root);
            ctx.SetMainObject(root);
        }
        catch (Exception ex)
        {
            UnityEngine.Object.DestroyImmediate(root);
            throw new InvalidOperationException($"Failed to import OBJ asset at '{ctx.assetPath}'.", ex);
        }
    }

    private static void RegisterMeshes(AssetImportContext ctx, GameObject root)
    {
        var meshes = new HashSet<Mesh>();
        var meshFilters = root.GetComponentsInChildren<MeshFilter>(includeInactive: true);
        int meshIndex = 0;
        foreach (MeshFilter filter in meshFilters)
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null || !meshes.Add(mesh))
            {
                continue;
            }

            if (string.IsNullOrEmpty(mesh.name))
            {
                mesh.name = $"{root.name}_Mesh_{meshIndex}";
            }
            ctx.AddObjectToAsset($"mesh_{meshIndex}", mesh);
            meshIndex++;
        }
    }

    private static void RegisterMaterialsAndTextures(AssetImportContext ctx, GameObject root)
    {
        var materialSet = new HashSet<Material>();
        var textureSet = new HashSet<Texture2D>();
        int materialIndex = 0;
        int textureIndex = 0;

        var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
        foreach (Renderer renderer in renderers)
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null || !materialSet.Add(material))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(material.name))
                {
                    material.name = $"{root.name}_Mat_{materialIndex}";
                }
                ctx.AddObjectToAsset($"mat_{materialIndex}", material);
                materialIndex++;

                // Register all textures from this material
                Shader shader = material.shader;
                for (int propIdx = 0; propIdx < shader.GetPropertyCount(); propIdx++)
                {
                    if (shader.GetPropertyType(propIdx) == UnityEngine.Rendering.ShaderPropertyType.Texture)
                    {
                        string propName = shader.GetPropertyName(propIdx);
                        Texture texture = material.GetTexture(propName);
                        if (texture is Texture2D tex && textureSet.Add(tex))
                        {
                            if (string.IsNullOrEmpty(tex.name))
                            {
                                tex.name = $"{material.name}_{propName}";
                            }
                            ctx.AddObjectToAsset($"tex_{textureIndex}", tex);
                            textureIndex++;
                        }
                    }
                }
            }
        }
    }

    private static void RemoveLoaderComponent(OBJ loader)
    {
        if (loader != null)
        {
            UnityEngine.Object.DestroyImmediate(loader, allowDestroyingAssets: true);
        }
    }

    private static void RegisterDependencies(AssetImportContext ctx, IReadOnlyList<string> resourcePaths)
    {
        if (resourcePaths == null)
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string fullPath in resourcePaths)
        {
            string assetPath = ToAssetPath(fullPath);
            if (!string.IsNullOrEmpty(assetPath) && seen.Add(assetPath))
            {
                ctx.DependsOnSourceAsset(assetPath);
            }
        }
    }

    private static string ToAssetPath(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath))
        {
            return null;
        }

        string normalizedFull = Path.GetFullPath(fullPath).Replace('\\', '/');
        string projectDataPath = Path.GetFullPath(Application.dataPath).Replace('\\', '/');

        if (normalizedFull.StartsWith(projectDataPath))
        {
            return "Assets" + normalizedFull.Substring(projectDataPath.Length);
        }

        return null;
    }
}
