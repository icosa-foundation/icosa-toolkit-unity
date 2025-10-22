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

using Newtonsoft.Json.Linq;
using IcosaApiClient;
using System;
using System.Collections.Generic;
using System.Text;
using IcosaClientInternal.model.util;
using UnityEngine;
using UnityEngine.Networking;
using IcosaClientInternal.client.model.util;

namespace IcosaClientInternal.api_clients.icosa_client
{
    /// <summary>
    ///   Parses the response of a List Assets request from Icosa into a IcosaListResult.
    /// </summary>
    public class ParseAssetsBackgroundWork : BackgroundWork
    {
        private byte[] response;
        private IcosaStatus status;
        private Action<IcosaStatus, IcosaListAssetsResult> callback;
        private IcosaListAssetsResult icosaListAssetsResult;

        public ParseAssetsBackgroundWork(byte[] response, Action<IcosaStatus, IcosaListAssetsResult> callback)
        {
            this.response = response;
            this.callback = callback;
        }

        public void BackgroundWork()
        {
            JObject result;
            status = IcosaClient.ParseResponse(response, out result);
            if (status.ok)
            {
                status = IcosaClient.ParseReturnedAssets(Encoding.UTF8.GetString(response), out icosaListAssetsResult);
            }
        }

        public void PostWork()
        {
            callback(status, icosaListAssetsResult);
        }
    }

    /// <summary>
    ///   Parses an asset from Icosa into a IcosaAsset.
    /// </summary>
    public class ParseAssetBackgroundWork : BackgroundWork
    {
        private byte[] response;
        private Action<IcosaStatus, IcosaAsset> callback;
        private IcosaStatus status;
        private IcosaAsset icosaAsset;

        public ParseAssetBackgroundWork(byte[] response, Action<IcosaStatus, IcosaAsset> callback)
        {
            this.response = response;
            this.callback = callback;
        }

        public void BackgroundWork()
        {
            JObject result;
            status = IcosaClient.ParseResponse(response, out result);
            if (status.ok)
            {
                status = IcosaClient.ParseAsset(result, out icosaAsset);
            }
        }

        public void PostWork()
        {
            callback(status, icosaAsset);
        }
    }

    [ExecuteInEditMode]
    public class IcosaClient : MonoBehaviour
    {
        /// <summary>
        /// Default cache expiration time (millis) for queries of public assets.
        /// Only applies for queries of PUBLIC assets (featured, categories, etc), not the user's private
        /// assets.
        /// </summary>
        private const long DEFAULT_QUERY_CACHE_MAX_AGE_MILLIS = 60 * 60 * 1000; // 60 minutes.

        private static readonly Dictionary<IcosaCategory, string> CATEGORIES = new Dictionary<IcosaCategory, string>()
        {
            { IcosaCategory.ANIMALS, "ANIMALS" },
            { IcosaCategory.ARCHITECTURE, "ARCHITECTURE" },
            { IcosaCategory.ART, "ART" },
            { IcosaCategory.FOOD, "FOOD" },
            { IcosaCategory.NATURE, "NATURE" },
            { IcosaCategory.OBJECTS, "OBJECTS" },
            { IcosaCategory.PEOPLE, "PEOPLE" },
            { IcosaCategory.PLACES, "SCENES" },
            { IcosaCategory.TECH, "TECH" },
            { IcosaCategory.TRANSPORT, "TRANSPORT" },
        };

        private static readonly Dictionary<IcosaOrderBy, string> ORDER_BY = new Dictionary<IcosaOrderBy, string>()
        {
            { IcosaOrderBy.BEST, "BEST" },
            { IcosaOrderBy.NEWEST, "NEWEST" },
            { IcosaOrderBy.OLDEST, "OLDEST" },
            { IcosaOrderBy.LIKED_TIME, "LIKED_TIME" },
        };

        private static readonly Dictionary<IcosaFormatFilter, string> FORMAT_FILTER =
            new Dictionary<IcosaFormatFilter, string>()
            {
                { IcosaFormatFilter.BLOCKS, "BLOCKS" },
                { IcosaFormatFilter.FBX, "FBX" },
                { IcosaFormatFilter.GLTF, "GLTF" },
                { IcosaFormatFilter.GLTF_2, "GLTF2" },
                { IcosaFormatFilter.OBJ, "TILT" },
                { IcosaFormatFilter.TILT, "TILT" },
            };

        private static readonly Dictionary<IcosaVisibilityFilter, string> VISIBILITY =
            new Dictionary<IcosaVisibilityFilter, string>()
            {
                { IcosaVisibilityFilter.PRIVATE, "PRIVATE" },
                { IcosaVisibilityFilter.PUBLISHED, "PUBLISHED" },
            };

        private static readonly Dictionary<IcosaMaxComplexityFilter, string> MAX_COMPLEXITY =
            new Dictionary<IcosaMaxComplexityFilter, string>()
            {
                { IcosaMaxComplexityFilter.SIMPLE, "SIMPLE" },
                { IcosaMaxComplexityFilter.MEDIUM, "MEDIUM" },
                { IcosaMaxComplexityFilter.COMPLEX, "COMPLEX" },
            };

        public static readonly Dictionary<IcosaAssetLicense, string> LICENCE =
            new Dictionary<IcosaAssetLicense, string>()
            {
                { IcosaAssetLicense.UNKNOWN, "Unknown License" },
                { IcosaAssetLicense.CREATIVE_COMMONS_BY, "Creative Commons Attribution" },
                { IcosaAssetLicense.ALL_RIGHTS_RESERVED, "All Rights Reserved, No permissions granted beyond viewing" },
                { IcosaAssetLicense.CREATIVE_COMMONS_BY_ND, "Creative Commons Attribution, No Derivatives" },
                { IcosaAssetLicense.CREATIVE_COMMONS_BY_SA, "Creative Commons Attribution, Share-alike" },
                { IcosaAssetLicense.CREATIVE_COMMONS_BY_NC, "Creative Commons Attribution, Non-Commercial" },
                {
                    IcosaAssetLicense.CREATIVE_COMMONS_BY_NC_ND,
                    "Creative Commons Attribution, Non-Commercial, No Derivatives"
                },
                {
                    IcosaAssetLicense.CREATIVE_COMMONS_BY_NC_SA,
                    "Creative Commons Attribution, Non-Commercial, Share-alike"
                },
                { IcosaAssetLicense.CC0, "Creative Commons Zero" },
            };

        /// <summary>
        /// Return a Icosa search URL representing a ListAssetsRequest.
        /// </summary>
        private static string MakeSearchUrl(IcosaListAssetsRequest listAssetsRequest)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(GetBaseUrl())
                .Append("/v1/assets");

            sb.AppendFormat("?order_by={0}", UnityWebRequest.EscapeURL(ORDER_BY[listAssetsRequest.orderBy]));
            sb.AppendFormat("&page_size={0}", listAssetsRequest.pageSize.ToString());

            if (listAssetsRequest.formatFilter != null)
            {
                sb.AppendFormat("&format={0}", UnityWebRequest.EscapeURL(FORMAT_FILTER[listAssetsRequest.formatFilter.Value]));
            }

            if (listAssetsRequest.keywords != null)
            {
                sb.AppendFormat("&keywords={0}", UnityWebRequest.EscapeURL(listAssetsRequest.keywords));
            }

            if (listAssetsRequest.category != IcosaCategory.UNSPECIFIED)
            {
                sb.AppendFormat("&category={0}", UnityWebRequest.EscapeURL(CATEGORIES[listAssetsRequest.category]));
            }

            if (listAssetsRequest.curated)
            {
                sb.Append("&curated=true");
            }

            if (listAssetsRequest.maxComplexity != IcosaMaxComplexityFilter.UNSPECIFIED)
            {
                sb.AppendFormat("&max_complexity={0}", UnityWebRequest.EscapeURL(MAX_COMPLEXITY[listAssetsRequest.maxComplexity]));
            }

            if (listAssetsRequest.pageToken != null)
            {
                sb.AppendFormat("&page_token={0}", UnityWebRequest.EscapeURL(listAssetsRequest.pageToken));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Return a Icosa search URL representing a ListUserAssetsRequest.
        /// </summary>
        private static string MakeSearchUrl(IcosaListUserAssetsRequest listUserAssetsRequest)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(GetBaseUrl())
                .Append("/v1/users/me/assets")
                .AppendFormat("?key={0}", IcosaMainInternal.Instance.apiKey);

            if (listUserAssetsRequest.formatFilter != null)
            {
                sb.AppendFormat("&format={0}", UnityWebRequest.EscapeURL(FORMAT_FILTER[listUserAssetsRequest.formatFilter.Value]));
            }

            if (listUserAssetsRequest.visibility != IcosaVisibilityFilter.UNSPECIFIED)
            {
                sb.AppendFormat("&visibility={0}", UnityWebRequest.EscapeURL(VISIBILITY[listUserAssetsRequest.visibility]));
            }

            sb.AppendFormat("&order_by={0}", UnityWebRequest.EscapeURL(ORDER_BY[listUserAssetsRequest.orderBy]));
            sb.AppendFormat("&page_size={0}", listUserAssetsRequest.pageSize);
            if (listUserAssetsRequest.pageToken != null)
            {
                sb.AppendFormat("&page_token={0}", UnityWebRequest.EscapeURL(listUserAssetsRequest.pageToken));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Return a Icosa search URL representing a ListLikedAssetsRequest.
        /// </summary>
        private static string MakeSearchUrl(IcosaListLikedAssetsRequest listLikedAssetsRequest)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(GetBaseUrl())
                .Append("/v1/users/me/likedassets")
                .AppendFormat("?key={0}", IcosaMainInternal.Instance.apiKey);

            sb.AppendFormat("&order_by={0}", UnityWebRequest.EscapeURL(ORDER_BY[listLikedAssetsRequest.orderBy]));
            sb.AppendFormat("&page_size={0}", listLikedAssetsRequest.pageSize);
            if (listLikedAssetsRequest.pageToken != null)
            {
                sb.AppendFormat("&page_token={0}", UnityWebRequest.EscapeURL(listLikedAssetsRequest.pageToken));
            }

            return sb.ToString();
        }

        private static string MakeSearchUrl(IcosaRequest request)
        {
            if (request is IcosaListAssetsRequest)
            {
                return MakeSearchUrl(request as IcosaListAssetsRequest);
            }
            else if (request is IcosaListUserAssetsRequest)
            {
                return MakeSearchUrl(request as IcosaListUserAssetsRequest);
            }
            else if (request is IcosaListLikedAssetsRequest)
            {
                return MakeSearchUrl(request as IcosaListLikedAssetsRequest);
            }
            else
            {
                throw new Exception("Must be a valid request type.");
            }
        }

        public void Setup()
        {
        }

        /// <summary>
        ///   Takes a string, representing either a ListAssetsResponse or ListUserAssetsResponse proto, and
        ///   fills icosaListResult with relevant fields from the response and returns a success status
        ///   if the response is of the expected format, or a failure status if it's not.
        /// </summary>
        public static IcosaStatus ParseReturnedAssets(string response, out IcosaListAssetsResult icosaListAssetsResult)
        {
            // Try and actually parse the string.
            JObject results = JObject.Parse(response);
            IJEnumerable<JToken> assets = results["assets"].AsJEnumerable();
            // If assets is null, check for a userAssets object, which would be present if the response was
            // a ListUserAssets response.
            if (assets == null) assets = results["userAssets"].AsJEnumerable();
            if (assets == null)
            {
                // Empty response means there were no assets that matched the request parameters.
                icosaListAssetsResult = new IcosaListAssetsResult(IcosaStatus.Success(), /*totalSize*/ 0);
                return IcosaStatus.Success();
            }

            List<IcosaAsset> icosaAssets = new List<IcosaAsset>();
            foreach (JToken asset in assets)
            {
                IcosaAsset icosaAsset;
                if (!(asset is JObject))
                {
                    Debug.LogWarningFormat("Ignoring asset since it's not a JSON object: " + asset);
                    continue;
                }

                JObject jObjectAsset = (JObject)asset;
                if (asset["asset"] != null)
                {
                    // If this isn't null, means we are parsing a ListUserAssets response, which has an added
                    // layer of nesting.
                    jObjectAsset = (JObject)asset["asset"];
                }

                IcosaStatus parseStatus = ParseAsset(jObjectAsset, out icosaAsset);
                if (parseStatus.ok)
                {
                    icosaAssets.Add(icosaAsset);
                }
                else
                {
                    Debug.LogWarningFormat("Failed to parse a returned asset: {0}", parseStatus);
                }
            }

            var totalSize = results["totalSize"] != null ? int.Parse(results["totalSize"].ToString()) : 0;
            var nextPageToken = results["nextPageToken"] != null ? results["nextPageToken"].ToString() : null;
            icosaListAssetsResult =
                new IcosaListAssetsResult(IcosaStatus.Success(), totalSize, icosaAssets, nextPageToken);
            return IcosaStatus.Success();
        }

        /// <summary>
        /// Parses a single asset.
        /// </summary>
        public static IcosaStatus ParseAsset(JObject asset, out IcosaAsset icosaAsset)
        {
            icosaAsset = new IcosaAsset();

            if (asset["visibility"] == null)
            {
                return IcosaStatus.Error("Asset has no visibility set.");
            }

            icosaAsset.assetId = asset["assetId"].ToString();
            icosaAsset.displayName = asset["name"].ToString();
            icosaAsset.authorName = asset["authorName"].ToString();
            icosaAsset.authorId = asset["authorId"].ToString();

            if (asset["thumbnail"] != null)
            {
                IJEnumerable<JToken> thumbnailElements = asset["thumbnail"].AsJEnumerable();
                icosaAsset.thumbnail = new IcosaFile(
                    thumbnailElements["relativePath"]?.ToString(),
                    thumbnailElements["url"]?.ToString(),
                    thumbnailElements["contentType"]?.ToString()
                );
            }

            if (asset["formats"] == null)
            {
                Debug.LogError("No formats found");
            }
            else
            {
                foreach (JToken format in asset["formats"])
                {
                    IcosaFormat newFormat = ParseAssetsPackage(format);
                    newFormat.formatType = ParseIcosaFormatType(format["formatType"]);
                    if (newFormat.formatType == IcosaFormatType.UNKNOWN)
                    {
                        PtDebug.Log("Did not recognize format type: " + format["formatType"].ToString());
                    }

                    icosaAsset.formats.Add(newFormat);
                }
            }

            icosaAsset.displayName = asset["displayName"].ToString();
            icosaAsset.createTime = DateTime.Parse(asset["createTime"].ToString());
            icosaAsset.updateTime = DateTime.Parse(asset["updateTime"].ToString());
            icosaAsset.visibility = ParseIcosaVisibility(asset["visibility"]);
            icosaAsset.license = ParseIcosaAssetLicense(asset["license"]);
            if (asset["isCurated"] != null)
            {
                icosaAsset.isCurated = bool.Parse(asset["isCurated"].ToString());
            }

            return IcosaStatus.Success();
        }

        private static IcosaFormatType ParseIcosaFormatType(JToken token)
        {
            if (token == null) return IcosaFormatType.UNKNOWN;
            string tokenValue = token.ToString();
            return tokenValue == "OBJ" ? IcosaFormatType.OBJ :
                tokenValue == "GLTF2" ? IcosaFormatType.GLTF_2 :
                tokenValue == "GLTF" ? IcosaFormatType.GLTF :
                tokenValue == "TILT" ? IcosaFormatType.TILT :
                IcosaFormatType.UNKNOWN;
        }

        private static IcosaVisibility ParseIcosaVisibility(JToken token)
        {
            if (token == null) return IcosaVisibility.UNSPECIFIED;
            string tokenValue = token.ToString();
            return tokenValue == "PRIVATE" ? IcosaVisibility.PRIVATE :
                tokenValue == "UNLISTED" ? IcosaVisibility.UNLISTED :
                tokenValue == "PUBLIC" ? IcosaVisibility.PUBLISHED :
                IcosaVisibility.UNSPECIFIED;
        }

        private static IcosaAssetLicense ParseIcosaAssetLicense(JToken token)
        {
            if (token == null) return IcosaAssetLicense.UNKNOWN;
            var tokenValue = token.ToString();
            Enum.TryParse(tokenValue, out IcosaAssetLicense license);
            return license;
        }

        // As above, accepting a string response (such that we can parse on a background thread).
        public static IcosaStatus ParseAsset(string response, out IcosaAsset objectStoreEntry,
            bool hackUrls)
        {
            return ParseAsset(JObject.Parse(response), out objectStoreEntry);
        }

        private static IcosaFormat ParseAssetsPackage(JToken token)
        {
            IcosaFormat package = new IcosaFormat();
            package.root = new IcosaFile(token["root"]["relativePath"].ToString(),
                token["root"]["url"].ToString(), token["root"]["contentType"].ToString());
            // Get the supporting files (resources).
            // Supporting files (including MTL files) are listed under /resource:
            package.resources = new List<IcosaFile>();
            if (token["resources"] != null)
            {
                IJEnumerable<JToken> resourceTags = token["resources"].AsJEnumerable();
                if (resourceTags != null)
                {
                    foreach (JToken resourceTag in resourceTags)
                    {
                        if (resourceTag["url"] != null)
                        {
                            package.resources.Add(new IcosaFile(
                                resourceTag["relativePath"].ToString(),
                                resourceTag["url"].ToString(),
                                resourceTag["contentType"].ToString()));
                        }
                    }
                }
            }

            // Get the format complexity
            if (token["formatComplexity"] != null)
            {
                package.formatComplexity = new IcosaFormatComplexity();
                if (token["formatComplexity"]["triangleCount"] != null)
                {
                    package.formatComplexity.triangleCount =
                        int.Parse(token["formatComplexity"]["triangleCount"].ToString());
                }

                if (token["formatComplexity"]["lodHint"] != null)
                {
                    package.formatComplexity.lodHint = int.Parse(token["formatComplexity"]["lodHint"].ToString());
                }
            }

            return package;
        }

        /// <summary>
        /// Fetches a list of Icosa assets together with metadata, using the given request params.
        /// </summary>
        /// <param name="request">The request to send; can be either a ListAssetsRequest, a ListUserAssetsRequest, or
        /// a ListLikedAssetsRequest.</param>
        /// <param name="callback">The callback to call when the request is complete.</param>
        /// <param name="maxCacheAge">The maximum cache age to use.</param>
        /// <param name="isRecursion"> If true, this is a recursive call to this function, and no
        /// further retries should be attempted.</param>
        public void SendRequest(IcosaRequest request, Action<IcosaStatus, IcosaListAssetsResult> callback,
            long maxCacheAge = DEFAULT_QUERY_CACHE_MAX_AGE_MILLIS, bool isRecursion = false)
        {
            IcosaMainInternal.Instance.webRequestManager.EnqueueRequest(
                () => { return GetRequest(MakeSearchUrl(request)); },
                (IcosaStatus status, int responseCode, byte[] response) =>
                {
                    // Retry the request if this was the first failure. The failure may be a server blip, or may indicate
                    // an authentication token has become stale and must be refreshed.
                    if (responseCode == 401 || !status.ok)
                    {
                        if (isRecursion || !Authenticator.IsInitialized || !Authenticator.Instance.IsAuthenticated)
                        {
                            callback(IcosaStatus.Error(status, "Query error ({0})", responseCode), null);
                            return;
                        }
                        else
                        {
                            Authenticator.Instance.Reauthorize((IcosaStatus reauthStatus) =>
                            {
                                if (reauthStatus.ok)
                                {
                                    SendRequest(request, callback, maxCacheAge: maxCacheAge, isRecursion: true);
                                }
                                else
                                {
                                    callback(IcosaStatus.Error(reauthStatus, "Failed to reauthorize."), null);
                                }
                            });
                        }
                    }
                    else
                    {
                        IcosaMainInternal.Instance.DoBackgroundWork(new ParseAssetsBackgroundWork(
                            response, callback));
                    }
                }, maxCacheAge);
        }

        /// <summary>
        ///   Fetch a specific asset.
        /// </summary>
        /// <param name="assetId">The asset to be fetched.</param>
        /// <param name="callback">A callback to call with the result of the operation.</param>
        /// <param name="isRecursion">
        ///   If true, this is a recursive call to this function, and no further retries should be attempted.
        /// </param>
        public void GetAsset(string assetId, Action<IcosaStatus, IcosaAsset> callback, bool isRecursion = false)
        {
            IcosaMainInternal.Instance.webRequestManager.EnqueueRequest(
                () =>
                {
                    string url = String.Format("{0}/v1/{1}?{2}", GetBaseUrl(), assetId,
                        IcosaMainInternal.Instance.apiKeyUrlParam);
                    return GetRequest(url);
                },
                (IcosaStatus status, int responseCode, byte[] response) =>
                {
                    if (responseCode < 200 || responseCode > 299 || !status.ok)
                    {
                        if (isRecursion || !Authenticator.IsInitialized)
                        {
                            callback(IcosaStatus.Error("Get asset error ({0})", responseCode), null);
                            return;
                        }
                        else
                        {
                            Authenticator.Instance.Reauthorize((IcosaStatus reauthStatus) =>
                            {
                                if (reauthStatus.ok)
                                {
                                    GetAsset(assetId, callback, isRecursion: true);
                                }
                                else
                                {
                                    callback(
                                        IcosaStatus.Error(reauthStatus, "Failed to reauthenticate to get asset {0}",
                                            assetId), null);
                                }
                            });
                        }
                    }
                    else
                    {
                        IcosaMainInternal.Instance.DoBackgroundWork(new ParseAssetBackgroundWork(response,
                            callback));
                    }
                }, DEFAULT_QUERY_CACHE_MAX_AGE_MILLIS);
        }

        private static string GetBaseUrl()
        {
            return PtSettings.Instance.authConfig.baseUrl;
        }

        /// <summary>
        ///   Forms a GET request from a HTTP path.
        /// </summary>
        public UnityWebRequest GetRequest(string path, string contentType = null, bool requiresAuth = false)
        {
            // The default constructor for a UnityWebRequest gives a GET request.
            UnityWebRequest request = new UnityWebRequest(path);
            if (contentType != null)
            {
                request.SetRequestHeader("Content-type", contentType);
            }

            if (requiresAuth)
            {
                string token = IcosaMainInternal.Instance.GetAccessToken();
                if (token != null)
                {
                    request.SetRequestHeader("Authorization", string.Format("Bearer {0}", token));
                }
            }

            return request;
        }

        public static IcosaStatus ParseResponse(byte[] response, out JObject result)
        {
            try
            {
                result = JObject.Parse(Encoding.UTF8.GetString(response));
                JToken errorToken = result["error"];
                if (errorToken != null)
                {
                    IJEnumerable<JToken> error = errorToken.AsJEnumerable();
                    return IcosaStatus.Error("{0}: {1}",
                        error["code"] != null ? error["code"].ToString() : "(no error code)",
                        error["message"] != null ? error["message"].ToString() : "(no error message)");
                }
                else
                {
                    return IcosaStatus.Success();
                }
            }
            catch (Exception ex)
            {
                result = null;
                return IcosaStatus.Error("Failed to parse Icosa API response, encountered exception: {0}", ex.Message);
            }
        }
    }
}