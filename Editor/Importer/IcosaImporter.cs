// Copyright 2017 Google Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;
using System;
using System.Linq;
using IcosaApiClient;
using IcosaClientInternal;

namespace IcosaClientEditor
{
    /// <summary>
    /// Responsible for importing Icosa assets.
    ///
    /// This class is a custom Unity AssetPostprocessor which detects when new GLTF files are imported into
    /// the project and processes them to convert them into the appropriate Icosa Client objects (PtAsset
    /// and prefab).
    /// </summary>
    public class IcosaImporter : AssetPostprocessor
    {
        private const string PROGRESS_BAR_TITLE = "Importing...";
        private const string PROGRESS_BAR_TEXT = "Importing glTF...";

        /// <summary>
        /// Pending import requests, keyed by local gltf path.
        /// </summary>
        static Dictionary<string, ImportRequest> importRequests = new Dictionary<string, ImportRequest>();

        /// <summary>
        /// Adds an import request. The request will be executed when the indicated file gets imported
        /// into the project.
        /// </summary>
        /// <param name="request">The request to add.</param>
        public static void AddImportRequest(ImportRequest request)
        {
            importRequests[request.gltfLocalPath] = request;
        }

        /// <summary>
        /// Called by Unity to inform us that new assets were imported.
        /// </summary>
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (string localAssetPath in importedAssets)
            {
                ImportRequest request;
                if (importRequests.TryGetValue(localAssetPath, out request))
                {
                    importRequests.Remove(localAssetPath);
                    // ExecuteImportRequest needs to create and get pointers to prefabs.
                    // Starting in 2018.3, prefabs require asset processing before they
                    // are fully created. Since Unity doesn't do recursive asset processing,
                    // this means ExecuteImportRequest can't happen in OnPostprocessAllAssets.
                    CoroutineRunner.StartCoroutine(ExecuteImportRequestCoroutine(request));
                }
            }
        }

        private static IEnumerator<object> ExecuteImportRequestCoroutine(ImportRequest request)
        {
            try
            {
                ExecuteImportRequest(request);
            }
            catch (Exception ex)
            {
                Debug.LogErrorFormat("Import error: {0}", ex);
                EditorUtility.DisplayDialog("Error",
                    "There was an error importing the asset. Please check the logs for more information.", "OK");
            }

            yield break;
        }

        /// <summary>
        /// Executes the given import request, producing a PtAsset and a prefab.
        /// </summary>
        /// <param name="request">The request to perform.</param>
        private static void ExecuteImportRequest(ImportRequest request)
        {
            PtDebug.LogFormat("Executing import request: {0}", request);

            string gltfFullPath = PtUtils.ToAbsolutePath(request.gltfLocalPath);
            string assetLocalPath = request.ptAssetLocalPath;
            string assetFullPath = PtUtils.ToAbsolutePath(assetLocalPath);

            PtAsset assetToReplace = AssetDatabase.LoadAssetAtPath<PtAsset>(assetLocalPath);

            GameObject prefabToReplace = null;
            if (assetToReplace != null)
            {
                if (assetToReplace.assetPrefab == null)
                {
                    Debug.LogErrorFormat("Couldn't find prefab for asset {0}.", assetToReplace);
                    return;
                }

                prefabToReplace = assetToReplace.assetPrefab;
            }

            // Load the GLTF GameObject that Unity already imported via GLTFImporter.
            EditorUtility.DisplayProgressBar(PROGRESS_BAR_TITLE, PROGRESS_BAR_TEXT, 0.5f);

            GameObject importedGltfObject = null;
            try
            {
                // Unity's GLTFImporter has already imported the GLTF file.
                // Load the main GameObject from the asset database.
                importedGltfObject = AssetDatabase.LoadMainAssetAtPath(request.gltfLocalPath) as GameObject;

                if (importedGltfObject == null)
                {
                    Debug.LogErrorFormat("Could not load imported GLTF GameObject from {0}. " +
                        "Make sure Unity's GLTFImporter processed the file.", request.gltfLocalPath);
                    return;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            string baseName = PtUtils.GetPtAssetBaseName(request.IcosaAsset);

            // Create the asset (delete it first if it exists).
            if (File.Exists(assetFullPath))
            {
                AssetDatabase.DeleteAsset(assetLocalPath);

                // If we are replacing an existing asset, we should rename the replacement to the new name,
                // since the name reflects the identity of the asset. So if the user is importing the asset
                // dog_a381b3g to replace what was previously cat_v81938.asset, the replacement file should
                // be named dog_a381b3g.asset, not cat_v81938.asset.
                assetLocalPath = PtUtils.GetDefaultPtAssetPath(request.IcosaAsset);
                assetFullPath = PtUtils.ToAbsolutePath(assetLocalPath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(assetFullPath));

            // Create the new PtAsset with metadata only (no embedded meshes/materials/textures).
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<PtAsset>(), assetLocalPath);
            PtAsset newAsset = AssetDatabase.LoadAssetAtPath<PtAsset>(assetLocalPath);
            newAsset.name = baseName;
            newAsset.title = request.IcosaAsset.displayName ?? "";
            newAsset.author = request.IcosaAsset.authorName ?? "";
            newAsset.license = request.IcosaAsset.license;
            newAsset.url = request.IcosaAsset.Url;

            // Create a prefab variant from the imported GLTF GameObject.
            // The prefab will reference the GLTF's meshes/materials/textures, not embed them.
            GameObject newPrefab;
            string prefabLocalPath = Regex.Replace(assetLocalPath, "\\.asset$", ".prefab");
            if (!prefabLocalPath.EndsWith(".prefab"))
            {
                Debug.LogErrorFormat("Error: failed to compute prefab path for {0}", assetLocalPath);
                return;
            }

            // Instantiate the imported GLTF GameObject in the scene temporarily.
            GameObject tempInstance = PrefabUtility.InstantiatePrefab(importedGltfObject) as GameObject;
            if (tempInstance == null)
            {
                Debug.LogErrorFormat("Failed to instantiate GLTF GameObject for prefab creation: {0}", request.gltfLocalPath);
                return;
            }

            tempInstance.name = baseName;

            // Add PtAssetObject component to link back to the PtAsset.
            tempInstance.AddComponent<PtAssetObject>().asset = newAsset;

            if (prefabToReplace)
            {
#if UNITY_2018_3_OR_NEWER
                // Replace the existing prefab (Unity 2018.3+).
                newPrefab = PrefabUtility.SaveAsPrefabAsset(tempInstance, AssetDatabase.GetAssetPath(prefabToReplace));
                AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(newPrefab), baseName);
#else
                // Replace the existing prefab (legacy API).
                newPrefab = PrefabUtility.ReplacePrefab(tempInstance, prefabToReplace,
                    ReplacePrefabOptions.ReplaceNameBased);
                AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(newPrefab), baseName);
#endif
            }
            else
            {
#if UNITY_2018_3_OR_NEWER
                // Create a new prefab (Unity 2018.3+).
                newPrefab = PrefabUtility.SaveAsPrefabAsset(tempInstance, prefabLocalPath);
#else
                // Create a new prefab (legacy API).
                newPrefab = PrefabUtility.CreatePrefab(prefabLocalPath, tempInstance);
#endif
            }

            // Clean up the temporary instance.
            GameObject.DestroyImmediate(tempInstance);

            // Link the PtAsset to the prefab.
            newAsset.assetPrefab = newPrefab;
            if (newAsset.assetPrefab == null)
            {
                Debug.LogErrorFormat("Could not create prefab for asset {0}", newAsset);
            }

            AssetDatabase.Refresh();

            if (request.options.alsoInstantiate)
            {
                PrefabUtility.InstantiatePrefab(newPrefab);
            }

            PtDebug.LogFormat("GLTF import complete: {0}", request);


            // If this is a third-party asset, we need to update the attributions file.
            AttributionFileGenerator.Generate( /* showUi */ false);

            EditorWindow.GetWindow<AssetBrowserWindow>().HandleAssetImported(request.IcosaAsset.assetId);

            // Select the prefab in the editor so the user knows where it is.
            AssetDatabase.Refresh();
            Selection.activeObject = newPrefab;
            EditorGUIUtility.PingObject(newPrefab);
        }

        private static void SaveMeshes(List<Mesh> meshes, PtAsset asset)
        {
            for (int i = 0; i < meshes.Count; ++i)
            {
                AssetDatabase.AddObjectToAsset(meshes[i], asset);
            }
        }

        private static void SaveMaterials(List<Material> materials, PtAsset asset)
        {
            for (int i = 0; i < materials.Count; ++i)
            {
                AssetDatabase.AddObjectToAsset(materials[i], asset);
            }
        }

        private static void SaveTextures(List<Texture2D> textures, PtAsset asset)
        {
            for (int i = 0; i < textures.Count; ++i)
            {
                AssetDatabase.AddObjectToAsset(textures[i], asset);
            }
        }

        /// <summary>
        /// Represents a request to import an asset, with parameters specifying how to do so.
        /// </summary>
        public class ImportRequest
        {
            /// <summary>
            /// Local path to the GLTF to import ("Assets/.../something.gltf").
            /// </summary>
            public string gltfLocalPath;

            /// <summary>
            /// The path to the PtAsset to write. If the asset already exists, it will be replaced
            /// smartly (references will be preserved, etc).
            /// </summary>
            public string ptAssetLocalPath;

            /// <summary>
            /// Import options.
            /// </summary>
            public EditTimeImportOptions options;

            /// <summary>
            /// The icosaAsset that we are importing. This contains the metadata for the imported asset,
            /// such as the title and author name.
            /// </summary>
            public IcosaAsset IcosaAsset;

            public ImportRequest(string gltfLocalPath, string ptAssetLocalPath, EditTimeImportOptions options,
                IcosaAsset icosaAsset)
            {
                this.gltfLocalPath = gltfLocalPath;
                this.ptAssetLocalPath = ptAssetLocalPath;
                this.options = options;
                this.IcosaAsset = icosaAsset;
            }

            public override string ToString()
            {
                return string.Format("ImportRequest: {0}, {1} -> {2}", IcosaAsset, gltfLocalPath,
                    ptAssetLocalPath);
            }
        }

    }
}