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
using UnityEngine;
using UnityEngine.Networking;

namespace IcosaClientInternal
{
    /// <summary>
    /// Fetches and converts a thumbnail for a particular given collection.
    /// </summary>
    public class CollectionThumbnailFetcher
    {
        /// <summary>
        /// Maximum cache age for thumbnails of collections.
        /// </summary>
        private const long CACHE_MAX_AGE_MILLIS = 14 * 24 * 60 * 60 * 1000L; // A fortnight.

        private const int MIN_REQUESTED_SIZE = 32;
        private const int MAX_REQUESTED_SIZE = 512;

        private IcosaCollection collection;
        private IcosaFetchThumbnailOptions options;
        private IcosaApi.FetchCollectionThumbnailCallback callback;

        /// <summary>
        /// Builds a CollectionThumbnailFetcher that will fetch the thumbnail for the given collection
        /// and call the given callback when done. Building this object doesn't immediately
        /// start the fetch. To start, call Fetch().
        /// </summary>
        /// <param name="collection">The collection to fetch the thumbnail for.</param>
        /// <param name="callback">The callback to call when done. Can be null.</param>
        public CollectionThumbnailFetcher(IcosaCollection collection, IcosaFetchThumbnailOptions options,
            IcosaApi.FetchCollectionThumbnailCallback callback)
        {
            this.collection = collection;
            this.options = options ?? new IcosaFetchThumbnailOptions();
            this.callback = callback;
        }

        /// <summary>
        /// Starts fetching the thumbnail (in the background).
        /// </summary>
        public void Fetch()
        {
            if (string.IsNullOrEmpty(collection.imageUrl))
            {
                // If there's no thumbnail URL, fail early with a clear error message.
                if (callback != null)
                {
                    callback(collection, IcosaStatus.Error("Thumbnail URL not available for collection: {0}", collection));
                }

                return;
            }

            // Use cache for collection thumbnails
            long cacheAgeMaxMillis = CACHE_MAX_AGE_MILLIS;
            IcosaMainInternal.Instance.webRequestManager.EnqueueRequest(MakeRequest, ProcessResponse,
                cacheAgeMaxMillis);
        }

        private UnityWebRequest MakeRequest()
        {
            string url = collection.imageUrl;
            // If an image size hint was provided, forward it to the server if the server supports it.
            if (options.requestedImageSize > 0 && url.Contains(".googleusercontent.com/"))
            {
                url += "=s" + Mathf.Clamp(options.requestedImageSize, MIN_REQUESTED_SIZE, MAX_REQUESTED_SIZE);
            }

            return IcosaMainInternal.Instance.IcosaClient.GetRequest(url);
        }

        private void ProcessResponse(IcosaStatus status, int responseCode, byte[] data)
        {
            if (data == null || data.Length <= 0)
            {
                status = IcosaStatus.Error("Thumbnail data was null or empty.");
            }

            if (status.ok)
            {
                collection.thumbnailTexture = new Texture2D(1, 1);
                collection.thumbnailTexture.LoadImage(data);
            }
            else
            {
                Debug.LogWarningFormat("Failed to fetch thumbnail for collection {0} ({1}): {2}",
                    collection.url, collection.name, status);
            }

            if (callback != null)
            {
                callback(collection, status);
            }
        }
    }
}
