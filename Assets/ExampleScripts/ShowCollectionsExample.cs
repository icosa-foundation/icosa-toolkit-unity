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

using UnityEngine;
using IcosaApiClient;

/// <summary>
/// Example that shows how to list and retrieve collections from Icosa Gallery.
///
/// This example demonstrates:
/// 1. Listing public collections
/// 2. Listing the authenticated user's own collections
/// 3. Fetching a specific collection by URL
/// 4. Fetching collection thumbnails
/// </summary>
public class ShowCollectionsExample : MonoBehaviour
{
    private void Start()
    {
        // Example 1: Request a list of public collections from Icosa Gallery.
        Debug.Log("=== Getting public collections ===");

        IcosaListCollectionsRequest request = IcosaListCollectionsRequest.Newest();
        request.pageSize = 10;
        IcosaApi.ListCollections(request, ListCollectionsCallback);

        // Example 2: Request the authenticated user's own collections.
        // Note: This requires authentication. Make sure you have authenticated before calling this.
        if (IcosaApi.IsAuthenticated)
        {
            Debug.Log("=== Getting user's own collections ===");
            IcosaListUserCollectionsRequest userRequest = IcosaListUserCollectionsRequest.MyNewest();
            userRequest.pageSize = 10;
            // You can filter by visibility: PRIVATE, PUBLISHED, or UNSPECIFIED (all)
            userRequest.visibility = IcosaVisibilityFilter.UNSPECIFIED;
            IcosaApi.ListUserCollections(userRequest, ListUserCollectionsCallback);
        }
        else
        {
            Debug.Log("User is not authenticated. Skipping user collections example.");
        }
    }

    // Callback invoked when the collections results are returned.
    private void ListCollectionsCallback(IcosaStatusOr<IcosaListCollectionsResult> result)
    {
        if (!result.Ok)
        {
            Debug.LogError("Failed to get collections. Reason: " + result.Status);
            return;
        }

        Debug.Log("Successfully got collections!");
        Debug.Log(string.Format("Found {0} collections (total: {1})",
            result.Value.collections.Count, result.Value.totalSize));

        // Display information about each collection
        foreach (IcosaCollection collection in result.Value.collections)
        {
            Debug.Log(string.Format("Collection: {0}", collection.name));
            Debug.Log(string.Format("  URL: {0}", collection.Url));
            Debug.Log(string.Format("  Description: {0}", collection.description));
            Debug.Log(string.Format("  Visibility: {0}", collection.visibility));
            Debug.Log(string.Format("  Assets: {0}", collection.assets.Count));
            Debug.Log(string.Format("  Created: {0}", collection.createTime));

            // Optionally fetch the collection's thumbnail
            if (!string.IsNullOrEmpty(collection.imageUrl))
            {
                IcosaApi.FetchCollectionThumbnail(collection, (IcosaCollection col, IcosaStatus status) =>
                {
                    if (status.ok)
                    {
                        Debug.Log(string.Format("Successfully fetched thumbnail for collection: {0}", col.name));
                    }
                    else
                    {
                        Debug.LogWarning(string.Format("Failed to fetch thumbnail for collection {0}: {1}",
                            col.name, status));
                    }
                });
            }
        }

        // Example: Get a specific collection by URL
        if (result.Value.collections.Count > 0)
        {
            string firstCollectionUrl = result.Value.collections[0].url;
            Debug.Log(string.Format("Fetching specific collection by URL: {0}", firstCollectionUrl));
            IcosaApi.GetCollection(firstCollectionUrl, GetCollectionCallback);
        }
    }

    // Callback invoked when the user's collections results are returned.
    private void ListUserCollectionsCallback(IcosaStatusOr<IcosaListCollectionsResult> result)
    {
        if (!result.Ok)
        {
            Debug.LogError("Failed to get user collections. Reason: " + result.Status);
            return;
        }

        Debug.Log("Successfully got user's collections!");
        Debug.Log(string.Format("Found {0} user collections (total: {1})",
            result.Value.collections.Count, result.Value.totalSize));

        // Display information about each user collection
        foreach (IcosaCollection collection in result.Value.collections)
        {
            Debug.Log(string.Format("User Collection: {0}", collection.name));
            Debug.Log(string.Format("  URL: {0}", collection.Url));
            Debug.Log(string.Format("  Visibility: {0}", collection.visibility));
            Debug.Log(string.Format("  Assets: {0}", collection.assets.Count));
        }
    }

    // Callback invoked when a specific collection is retrieved.
    private void GetCollectionCallback(IcosaStatusOr<IcosaCollection> result)
    {
        if (!result.Ok)
        {
            Debug.LogError("Failed to get collection. Reason: " + result.Status);
            return;
        }

        Debug.Log(string.Format("Successfully retrieved collection: {0}", result.Value.name));
        Debug.Log(string.Format("  Contains {0} assets", result.Value.assets.Count));

        // List assets in the collection
        foreach (IcosaAsset asset in result.Value.assets)
        {
            Debug.Log(string.Format("  - Asset: {0} by {1}", asset.displayName, asset.authorName));
        }
    }
}
