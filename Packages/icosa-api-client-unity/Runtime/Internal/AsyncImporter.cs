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

using IcosaApiClient;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityGLTF;
using UnityGLTF.Loader;

namespace IcosaClientInternal
{
    /// <summary>
    /// Handles asynchronous importing of assets.
    /// </summary>
    [ExecuteInEditMode]
    public class AsyncImporter : MonoBehaviour
    {
        /// <summary>
        /// Callback called when an async import operation is finished.
        /// </summary>
        /// <param name="meshCreator">The mesh creator enumerable. The caller must fully enumerate this
        /// enumerable in order to create all meshes.</param>
        public delegate void AsyncImportCallback(IcosaStatus status, GameObject root, IEnumerable meshCreator);

        /// <summary>
        /// Queue of operations whose background part has finished, and are awaiting to be picked up by
        /// the main thread for callback dispatching. To operate on this queue, hold the
        /// finishedOperationsLock lock.
        /// </summary>
        private Queue<ImportOperation> finishedOperations = new Queue<ImportOperation>();

        private object finishedOperationsLock = new byte[0];

        /// <summary>
        /// Adapter to bridge IUriLoader to UnityGLTF's IDataLoader interface.
        /// This allows UnityGLTF to load external resources from the in-memory format data.
        /// </summary>
        private class FormatDataLoader : IDataLoader
        {
            private IUriLoader _uriLoader;

            public FormatDataLoader(IUriLoader uriLoader)
            {
                _uriLoader = uriLoader;
            }

            public Task<Stream> LoadStreamAsync(string relativeFilePath)
            {
                try
                {
                    var bufferReader = _uriLoader.Load(relativeFilePath);
                    if (bufferReader == null)
                    {
                        throw new FileNotFoundException($"Resource not found: {relativeFilePath}");
                    }

                    // Get the content length if available
                    long contentLength = bufferReader.GetContentLength();

                    // Read all data from IBufferReader into a MemoryStream
                    MemoryStream memoryStream;
                    if (contentLength > 0)
                    {
                        // If we know the length, allocate the exact size
                        byte[] data = new byte[contentLength];
                        bufferReader.Read(data, destinationOffset: 0, readStart: 0, readSize: (int)contentLength);
                        memoryStream = new MemoryStream(data, writable: false);
                    }
                    else
                    {
                        // Unknown length, read in chunks
                        memoryStream = new MemoryStream();
                        byte[] buffer = new byte[4096];
                        int position = 0;
                        int chunkSize = buffer.Length;

                        // Keep reading chunks until we've read everything
                        // Note: IBufferReader doesn't tell us when we're at EOF, so we read the max possible
                        try
                        {
                            while (true)
                            {
                                bufferReader.Read(buffer, destinationOffset: 0, readStart: position, readSize: chunkSize);
                                memoryStream.Write(buffer, 0, chunkSize);
                                position += chunkSize;
                            }
                        }
                        catch
                        {
                            // End of data reached
                        }

                        memoryStream.Position = 0;
                    }

                    return Task.FromResult<Stream>(memoryStream);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error loading resource '{relativeFilePath}': {ex.Message}");
                    return Task.FromException<Stream>(ex);
                }
            }
        }

        /// <summary>
        /// Must be called to set up this object before use.
        /// </summary>
        public void Setup()
        {
            // No init needed for now. But leave this hook in place for consistency with other classes,
            // and it's a good habit.
        }

        /// <summary>
        /// Imports the given format of the given asset, asynchronously in a background thread.
        /// Calls the supplied callback when done.
        /// </summary>
        public void ImportAsync(IcosaAsset asset, IcosaFormat format, IcosaImportOptions options,
            AsyncImportCallback callback = null)
        {
            ImportOperation operation = new ImportOperation();
            operation.instance = this;
            operation.asset = asset;
            operation.format = format;
            operation.options = options;
            operation.callback = callback;
            operation.status = IcosaStatus.Success();
            operation.loader = new FormatLoader(format);
            if (Application.isPlaying)
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback(BackgroundThreadProc), operation);
            }
            else
            {
                // If we are in the editor, don't do this in a background thread. Do it directly
                // here on the main thread.
                BackgroundThreadProc(operation);
                Update();
            }
        }

        private static void BackgroundThreadProc(object userData)
        {
            ImportOperation operation = (ImportOperation)userData;
            try
            {
                // Create a stream from the in-memory GLTF data
                var gltfStream = new MemoryStream(operation.format.root.contents);

                // Create import options with our data loader adapter
                var importOptions = new ImportOptions
                {
                    DataLoader = new FormatDataLoader(operation.loader)
                };

                // Create the GLTFSceneImporter. The constructor parses the GLTF data (thread-safe).
                operation.gltfImporter = new GLTFSceneImporter(gltfStream, importOptions);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                operation.status = IcosaStatus.Error("Error importing asset.", ex);
            }

            // Done with background thread part, let's queue it so we can finish up on the main thread.
            operation.instance.EnqueueFinishedOperation(operation);
        }

        private void EnqueueFinishedOperation(ImportOperation operation)
        {
            lock (finishedOperationsLock)
            {
                finishedOperations.Enqueue(operation);
            }
        }

        public void Update()
        {
            // We process at most one import result per frame, to avoid doing too much work
            // in the main thread.
            ImportOperation operation;
            lock (finishedOperationsLock)
            {
                if (finishedOperations.Count == 0) return;
                operation = finishedOperations.Dequeue();
            }

            if (!operation.status.ok)
            {
                // Import failed.
                operation.callback(operation.status, root: null, meshCreator: null);
                return;
            }

            try
            {
                // Use UnityGLTF's LoadScene to create the GameObject hierarchy.
                // This must run on the main thread.
                var sceneEnumerator = operation.gltfImporter.LoadScene();

                if (!operation.options.clientThrottledMainThread)
                {
                    // If we're not in throttled mode, create everything immediately by exhausting
                    // the LoadScene enumerator.
                    while (sceneEnumerator.MoveNext())
                    {
                        /* Process all import steps immediately */
                    }

                    // All done, no meshCreator needed for callback
                    GameObject root = operation.gltfImporter.CreatedObject;
                    operation.callback(IcosaStatus.Success(), root, meshCreator: null);
                }
                else
                {
                    // In throttled mode, wrap the enumerator so the caller can exhaust it incrementally.
                    // We need to track when it's complete to get the root object.
                    IEnumerable meshCreator = WrapSceneEnumerator(sceneEnumerator, operation.gltfImporter);
                    operation.callback(IcosaStatus.Success(), root: null, meshCreator);
                }
            }
            catch (Exception ex)
            {
                // Import failed.
                Debug.LogException(ex);
                operation.callback(IcosaStatus.Error("Failed to convert import to Unity objects.", ex),
                    root: null, meshCreator: null);
            }
        }

        /// <summary>
        /// Wraps the UnityGLTF scene enumerator for throttled mode.
        /// This allows the caller to exhaust the enumerator incrementally (e.g., one step per frame).
        /// </summary>
        private static IEnumerable WrapSceneEnumerator(IEnumerator sceneEnumerator, GLTFSceneImporter importer)
        {
            while (sceneEnumerator.MoveNext())
            {
                yield return null;
            }
            // When complete, yield the created root object
            yield return importer.CreatedObject;
        }

        /// <summary>
        /// Represents in import operation that's in progress.
        /// </summary>
        private class ImportOperation
        {
            /// <summary>
            /// Instance of the AsyncImporter that this operation belongs go.
            /// We need this because the thread main method has to be static.
            /// </summary>
            public AsyncImporter instance;

            /// <summary>
            /// The asset we are importing.
            /// </summary>
            public IcosaAsset asset;

            /// <summary>
            /// The format of the asset we are importing.
            /// </summary>
            public IcosaFormat format;

            /// <summary>
            /// Import options.
            /// </summary>
            public IcosaImportOptions options;

            /// <summary>
            /// The callback we are supposed to call at the end of the operation.
            /// </summary>
            public AsyncImportCallback callback;

            /// <summary>
            /// The GLTFSceneImporter instance used for importing the GLTF data.
            /// </summary>
            public GLTFSceneImporter gltfImporter;

            /// <summary>
            /// The loader used to load resources for the import.
            /// </summary>
            public IUriLoader loader;

            /// <summary>
            /// Status of the import operation.
            /// </summary>
            public IcosaStatus status;
        }
    }
}