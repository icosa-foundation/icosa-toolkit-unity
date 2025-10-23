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

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IcosaClientInternal;
using System.Collections;

namespace IcosaApiClient
{
    /// <summary>
    /// Represents a Icosa asset (the information about a 3D object in Icosa).
    /// </summary>
    /// <remarks>
    /// This is not the actual object that is added to the scene. This is just a container for
    /// the object's data, from which a GameObject can eventually be constructed.
    /// </remarks>
    [AutoStringifiable]
    public class IcosaAsset
    {

        public static string API_BASE_URL => "https://api.icosa.gallery";
        public static string WEBSITE_BASE_URL => "https://icosa.gallery";
        public static string USER_PROFILE_BASE_URL => $"{WEBSITE_BASE_URL}/user";
        public static string ASSET_BASE_URL => $"{WEBSITE_BASE_URL}/view";

        public string Url => $"{ASSET_BASE_URL}/{assetId}";
        public string AuthorUrl => $"{USER_PROFILE_BASE_URL}/{authorId}";



        /// <summary>
        /// For backwards compatibility with Poly API - same as assetId but with "assets/" prefix.
        /// For example, "assets/L1o2e3m4I5p6s7u8m".
        /// Not to be confused with displayName which is the human readable name
        /// </summary>
        public string name;

        /// <summary>
        /// Identifier for the asset. This is an alphanumeric string that identifies the asset,
        /// but is not meant for display. For example, "L1o2e3m4I5p6s7u8m".
        /// </summary>
        public string assetId;

        /// <summary>
        /// Human-readable name of the asset.
        /// </summary>
        public string displayName;

        /// <summary>
        /// Name of the asset's author.
        /// </summary>
        public string authorName;

        /// <summary>
        /// ID of the asset's author.
        /// </summary>
        public string authorId;

        /// <summary>
        /// Human-readable description of the asset.
        /// </summary>
        public string description;

        /// <summary>
        /// Date and time when the asset was created.
        /// </summary>
        public DateTime createTime;

        /// <summary>
        /// Date and time when the asset was last updated.
        /// </summary>
        public DateTime updateTime;

        /// <summary>
        /// A list of the available formats for this asset. Each format describes a content-type of a
        /// representation of the asset, and specifies where the underlying data files can be found.
        /// </summary>
        public List<IcosaFormat> formats = new List<IcosaFormat>();

        /// <summary>
        /// Thumbnail image information for this asset.
        /// </summary>
        public IcosaFile thumbnail;

        /// <summary>
        /// The license under which the author has made this asset available for use, if any.
        /// </summary>
        public IcosaAssetLicense license;

        /// <summary>
        /// Visibility of this asset (who can access it).
        /// </summary>
        public IcosaVisibility visibility;

        /// <summary>
        /// If true, the asset was manually curated by the Icosa team.
        /// </summary>
        public bool isCurated;

        /// <summary>
        /// The texture with the asset's thumbnail image. Only available after successfully fetched.
        /// </summary>
        public Texture2D thumbnailTexture;

        /// <summary>
        /// Returns a IcosaFormat of the given type, if it exists.
        /// If the asset has more than one format of the given type, returns the first one seen.
        /// If the asset does not have a format of the given type, returns null.
        /// </summary>
        public IcosaFormat GetFormatIfExists(IcosaFormatType type)
        {
            foreach (IcosaFormat format in formats)
            {
                if (format == null)
                {
                    continue;
                }

                if (format.formatType == type) return format;
            }

            return null;
        }

        /// <summary>
        /// Gets the best format for download, filtering by isPreferredForDownload.
        /// Priority: GLTF2 > GLTF > OBJ (among preferred formats only).
        /// Returns null if no preferred format is available.
        /// </summary>
        public IcosaFormat GetBestFormat()
        {
            // Filter to only preferred formats
            var preferredFormats = formats.Where(f => f != null && f.isPreferredForDownload).ToList();


            if (preferredFormats.Count == 0)
            {
                // Fallback: no preferred formats, return null
                return null;
            }

            // Prioritize: GLTF2 > GLTF > OBJ > any other format
            var gltf2 = preferredFormats.FirstOrDefault(f => f.formatType == IcosaFormatType.GLTF_2);
            if (gltf2 != null) return gltf2;

            var gltf = preferredFormats.FirstOrDefault(f => f.formatType == IcosaFormatType.GLTF);
            if (gltf != null) return gltf;

            var obj = preferredFormats.FirstOrDefault(f => f.formatType == IcosaFormatType.OBJ);
            if (obj != null) return obj;

            // Return first preferred format as fallback
            return preferredFormats[0];
        }

        /// <summary>
        /// Returns whether the asset is known to be mutable, due to its visibility.
        /// Public and unlisted assets are immutable. Private assets are mutable.
        /// </summary>
        /// <remarks>
        /// Immutable assets can be cached indefinitely, since they can't be modified.
        /// Depending on your use-case, you may wish to frequently re-download mutable assets, if you expect them to be
        /// changed while your app is running.
        /// </remarks>
        public bool IsMutable => visibility == IcosaVisibility.PRIVATE || visibility == IcosaVisibility.UNSPECIFIED;

        public string FriendlyShortLicenseName
        {
            get
            {
                switch (license)
                {
                    case IcosaAssetLicense.UNKNOWN:
                        return "Unknown";
                    case IcosaAssetLicense.CREATIVE_COMMONS_BY:
                        return "CC-BY";
                    case IcosaAssetLicense.ALL_RIGHTS_RESERVED:
                        return "All Rights Reserved";
                    case IcosaAssetLicense.CREATIVE_COMMONS_BY_ND:
                        return "CC-BY-ND";
                    case IcosaAssetLicense.CREATIVE_COMMONS_BY_SA:
                        return "CC-BY-SA";
                    case IcosaAssetLicense.CREATIVE_COMMONS_BY_NC:
                        return "CC-BY-NC";
                    case IcosaAssetLicense.CREATIVE_COMMONS_BY_NC_ND:
                        return "CC-BY-NC-ND";
                    case IcosaAssetLicense.CREATIVE_COMMONS_BY_NC_SA:
                        return "CC-BY-NC-SA";
                    case IcosaAssetLicense.CC0:
                        return "CC0";
                }

                return "Unrecognized License";
            }
        }

        public string FriendlyLongLicenseName
        {
            get
            {
                switch (license)
                {
                    case IcosaAssetLicense.UNKNOWN:
                        return "Unknown";
                    case IcosaAssetLicense.CREATIVE_COMMONS_BY:
                        return "Creative Commons Attribution";
                    case IcosaAssetLicense.ALL_RIGHTS_RESERVED:
                        return "All Rights Reserved";
                    case IcosaAssetLicense.CREATIVE_COMMONS_BY_ND:
                        return "Creative Commons Attribution No Derivatives";
                    case IcosaAssetLicense.CREATIVE_COMMONS_BY_SA:
                        return "Creative Commons Attribution Share Alike";
                    case IcosaAssetLicense.CREATIVE_COMMONS_BY_NC:
                        return "Creative Commons Attribution Non Commercial";
                    case IcosaAssetLicense.CREATIVE_COMMONS_BY_NC_ND:
                        return "Creative Commons Attribution Non Commercial No Derivatives";
                    case IcosaAssetLicense.CREATIVE_COMMONS_BY_NC_SA:
                        return "Creative Commons Attribution Non Commercial Share Alike";
                    case IcosaAssetLicense.CC0:
                        return "CC0";
                }

                return "Unrecognized License";
            }
        }

        public string LicenseUrl
        {
            get
            {
                switch (license)
                {
                    case IcosaAssetLicense.UNKNOWN:
                        return "Unknown";
                    case IcosaAssetLicense.CREATIVE_COMMONS_BY:
                        return "https://creativecommons.org/licenses/by/3.0/legalcode";
                    case IcosaAssetLicense.ALL_RIGHTS_RESERVED:
                        return "https://en.wikipedia.org/wiki/All_rights_reserved";
                    case IcosaAssetLicense.CREATIVE_COMMONS_BY_ND:
                        return "https://creativecommons.org/licenses/by-nd/3.0/legalcode";
                    case IcosaAssetLicense.CREATIVE_COMMONS_BY_SA:
                        return "https://creativecommons.org/licenses/by-sa/3.0/legalcode";
                    case IcosaAssetLicense.CREATIVE_COMMONS_BY_NC:
                        return "https://creativecommons.org/licenses/by-nc/3.0/legalcode";
                    case IcosaAssetLicense.CREATIVE_COMMONS_BY_NC_ND:
                        return "https://creativecommons.org/licenses/by-nc-nd/3.0/legalcode";
                    case IcosaAssetLicense.CREATIVE_COMMONS_BY_NC_SA:
                        return "https://creativecommons.org/licenses/by-nc-sa/3.0/legalcode";
                    case IcosaAssetLicense.CC0:
                        return "https://creativecommons.org/publicdomain/zero/1.0/legalcode";
                }

                return "";
            }
        }

        /// <summary>
        /// Returns attribution information about the asset.
        /// </summary>
        public string AttributionInfo
        {
            get
            {
                string licenceText;
                switch (license)
                {
                    case IcosaAssetLicense.UNKNOWN:
                        licenceText = "Unknown";
                        break;
                    case IcosaAssetLicense.CREATIVE_COMMONS_BY:
                        licenceText = $"{FriendlyShortLicenseName}\n{LicenseUrl}";
                        break;
                    case IcosaAssetLicense.ALL_RIGHTS_RESERVED:
                        licenceText = $"{FriendlyShortLicenseName}\n{LicenseUrl}";
                        break;
                    case IcosaAssetLicense.CREATIVE_COMMONS_BY_ND:
                        licenceText = $"{FriendlyShortLicenseName}\n{LicenseUrl}";
                        break;
                    case IcosaAssetLicense.CREATIVE_COMMONS_BY_SA:
                        licenceText = $"{FriendlyShortLicenseName}\n{LicenseUrl}";
                        break;
                    case IcosaAssetLicense.CREATIVE_COMMONS_BY_NC:
                        licenceText = $"{FriendlyShortLicenseName}\n{LicenseUrl}";
                        break;
                    case IcosaAssetLicense.CREATIVE_COMMONS_BY_NC_ND:
                        licenceText = $"{FriendlyShortLicenseName}\n{LicenseUrl}";
                        break;
                    case IcosaAssetLicense.CREATIVE_COMMONS_BY_NC_SA:
                        licenceText = $"{FriendlyShortLicenseName}\n{LicenseUrl}";
                        break;
                    case IcosaAssetLicense.CC0:
                        licenceText = $"{FriendlyShortLicenseName}\n{LicenseUrl}";
                        break;
                    default:
                        licenceText = $"All Rights Reserved\n{LicenseUrl}";
                        break;
                }
                return AttributionGeneration.GenerateAttributionString(displayName, authorName, Url, licenceText);
            }
        }

        public override string ToString()
        {
            return AutoStringify.Stringify(this);
        }
    }

    /// <summary>
    /// A specific representation of an asset, containing all the information needed to retrieve and
    /// describe this representation.
    /// </summary>
    /// <remarks>
    /// Each format is a "package" of files, with one root file and any number of resource files that accompany
    /// it. For example, for the OBJ format, the root file is the OBJ file that contains the asset's geometry
    /// and the corresponding MTL files are resource files.
    /// </remarks>
    [AutoStringifiable]
    public class IcosaFormat
    {
        /// <summary>
        /// Format type (OBJ, GLTF, etc).
        /// </summary>
        public IcosaFormatType formatType;

        /// <summary>
        /// The root (main) file for this format.
        /// </summary>
        public IcosaFile root;

        /// <summary>
        /// The list of resource (auxiliary) files for this format.
        /// </summary>
        public List<IcosaFile> resources = new List<IcosaFile>();

        /// <summary>
        /// Complexity of this format.
        /// </summary>
        public IcosaFormatComplexity formatComplexity;

        /// <summary>
        /// Whether this format is preferred for download according to the API.
        /// </summary>
        public bool isPreferredForDownload;

        public override string ToString()
        {
            return AutoStringify.Stringify(this);
        }
    }

    /// <summary>
    /// Represents an Icosa file.
    /// </summary>
    [AutoStringifiable]
    public class IcosaFile
    {
        /// <summary>
        /// The relative path of the file in the local filesystem when it was uploaded.
        /// For resource files, the path is relative to the root file. This always includes the name fo the
        /// file, and may or may not include a directory path.
        /// </summary>
        public string relativePath;

        /// <summary>
        /// The URL at which the contents of this file can be retrieved.
        /// </summary>
        public string url;

        /// <summary>
        /// The content type of this file. For example, "text/plain".
        /// </summary>
        public string contentType;

        /// <summary>
        /// Binary contents of this file. Only available after fetched.
        /// </summary>
        [AutoStringifyAbridged] public byte[] contents;

        /// <summary>
        /// Cached text contents of this file (lazily decoded from binary).
        /// </summary>
        [AutoStringifyAbridged] private string text;

        public IcosaFile(string relativePath, string url, string contentType)
        {
            this.relativePath = relativePath;
            this.url = url;
            this.contentType = contentType;
        }

        /// <summary>
        /// Returns the contents of this file as text.
        /// </summary>
        public string Text
        {
            get
            {
                if (text == null) text = System.Text.Encoding.UTF8.GetString(contents);
                return text;
            }
        }

        public override string ToString()
        {
            return AutoStringify.Stringify(this);
        }
    }

    /// <summary>
    /// Information on the complexity of a format.
    /// </summary>
    [AutoStringifiable]
    public class IcosaFormatComplexity
    {
        /// <summary>
        /// Approximate number of triangles in the asset's geometry.
        /// </summary>
        public long triangleCount;

        /// <summary>
        /// Hint for the level of detail (LOD) of this format relative to the other formats in this
        /// same asset. 0 is the most detailed version.
        /// </summary>
        public int lodHint;

        public override string ToString()
        {
            return AutoStringify.Stringify(this);
        }
    }

    /// <summary>
    /// Possible format types that can be returned from the Icosa REST API.
    /// </summary>
    public enum IcosaFormatType
    {
        UNKNOWN = 0,
        OBJ = 1,
        GLTF = 2,
        GLTF_2 = 3,
        TILT = 4,
    }

    /// <summary>
    /// Possible asset licenses.
    /// </summary>
    public enum IcosaAssetLicense
    {
        UNKNOWN = 0,
        CREATIVE_COMMONS_BY = 1,
        ALL_RIGHTS_RESERVED = 2,
        CREATIVE_COMMONS_BY_ND = 3,
        CREATIVE_COMMONS_BY_SA = 4,
        CREATIVE_COMMONS_BY_NC = 5,
        CREATIVE_COMMONS_BY_NC_ND = 6,
        CREATIVE_COMMONS_BY_NC_SA = 7,
        CC0 = 8
    }

    /// <summary>
    /// Visibility filters for a IcosaListUserAssets request.
    /// </summary>
    public enum IcosaVisibilityFilter
    {
        /// <summary>
        /// No visibility specified. Returns all assets.
        /// </summary>
        UNSPECIFIED = 0,

        /// <summary>
        /// Return only private assets.
        /// </summary>
        PRIVATE = 1,

        /// <summary>
        /// Return only published assets, including unlisted assets.
        /// </summary>
        PUBLISHED = 2,
    }

    /// <summary>
    /// Visibility of a Icosa asset.
    /// </summary>
    public enum IcosaVisibility
    {
        /// <summary>
        /// Unknown (and invalid) visibility.
        /// </summary>
        UNSPECIFIED = 0,

        /// <summary>
        /// Only the owner of the asset can access it.
        /// </summary>
        PRIVATE = 1,

        /// <summary>
        /// Read access to anyone who knows the asset ID (link to the asset), but the
        /// logged-in user's unlisted assets are returned in IcosaListUserAssets.
        /// </summary>
        UNLISTED = 2,

        /// <summary>
        /// Read access for everyone.
        /// </summary>
        PUBLISHED = 3,
    }

    /// <summary>
    /// Category of a Icosa asset.
    /// </summary>
    public enum IcosaCategory
    {
        UNSPECIFIED = 0,
        ANIMALS = 1,
        ARCHITECTURE = 2,
        ART = 3,
        FOOD = 4,
        NATURE = 5,
        OBJECTS = 6,
        PEOPLE = 7,
        PLACES = 8,
        TECH = 9,
        TRANSPORT = 10,
    }

    /// <summary>
    /// How the requested assets should be ordered in the response.
    /// </summary>
    public enum IcosaOrderBy
    {
        BEST,
        NEWEST,
        OLDEST,

        // Liked time is only a valid in a IcosaListLikedAssetsRequest.
        LIKED_TIME
    }

    /// <summary>
    /// Options for filtering to return only assets that contain the given format.
    /// </summary>
    public enum IcosaFormatFilter
    {
        BLOCKS = 1,
        FBX = 2,
        GLTF = 3,
        GLTF_2 = 4,
        OBJ = 5,
        TILT = 6,
    }

    /// <summary>
    /// Options for filtering on the maximum complexity of the asset.
    /// </summary>
    public enum IcosaMaxComplexityFilter
    {
        UNSPECIFIED = 0,
        SIMPLE = 1,
        MEDIUM = 2,
        COMPLEX = 3,
    }

    /// <summary>
    /// Base class that all request types derive from.
    /// </summary>
    public abstract class IcosaRequest
    {
        /// <summary>
        /// How to sort the results.
        /// </summary>
        public IcosaOrderBy orderBy = IcosaOrderBy.NEWEST;

        /// <summary>
        /// Size of each returned page.
        /// </summary>
        public int pageSize = 45;

        /// <summary>
        /// Page continuation token for pagination.
        /// </summary>
        public string pageToken = null;
    }

    /// <summary>
    /// Represents a set of Icosa request parameters determining which assets should be returned.
    /// null values mean "don't filter by this parameter".
    /// </summary>
    [AutoStringifiable]
    public class IcosaListAssetsRequest : IcosaRequest
    {
        public string keywords = "";
        public bool curated = false;

        /// <summary>
        /// Category can be any of the IcosaCategory object categories (e.g. "IcosaCategory.ANIMALS").
        /// </summary>
        public IcosaCategory category = IcosaCategory.UNSPECIFIED;

        public IcosaMaxComplexityFilter maxComplexity = IcosaMaxComplexityFilter.UNSPECIFIED;
        public IcosaFormatFilter? formatFilter = null;

        public IcosaListAssetsRequest()
        {
        }

        /// <summary>
        /// Returns a ListAssetsRequest that requests the featured assets. This approximates what the
        /// user would see in the Icosa main page, but the ordering might be different.
        /// </summary>
        public static IcosaListAssetsRequest Featured()
        {
            IcosaListAssetsRequest featured = new IcosaListAssetsRequest();
            featured.curated = true;
            featured.orderBy = IcosaOrderBy.BEST;
            return featured;
        }

        /// <summary>
        /// Returns a ListAssetsRequest that requests the latest assets. This query is not curated,
        /// so it will return the latest assets regardless of whether they have been reviewed.
        /// If you wish to enable curation, set curated=true on the returned object.
        /// </summary>
        public static IcosaListAssetsRequest Latest()
        {
            IcosaListAssetsRequest latest = new IcosaListAssetsRequest();
            latest.orderBy = IcosaOrderBy.NEWEST;
            return latest;
        }

        public override string ToString()
        {
            return AutoStringify.Stringify(this);
        }
    }

    /// <summary>
    /// Represents a set of Icosa request parameters determining which of the user's assets should be returned.
    /// null values mean "don't filter by this parameter".
    /// </summary>
    [AutoStringifiable]
    public class IcosaListUserAssetsRequest : IcosaRequest
    {
        public IcosaFormatType format = IcosaFormatType.UNKNOWN;
        public IcosaVisibilityFilter visibility = IcosaVisibilityFilter.UNSPECIFIED;
        public IcosaFormatFilter? formatFilter = null;

        public IcosaListUserAssetsRequest()
        {
        }

        /// <summary>
        /// Returns a ListUserAssetsRequest that requests the user's latest assets.
        /// </summary>
        public static IcosaListUserAssetsRequest MyNewest()
        {
            IcosaListUserAssetsRequest myNewest = new IcosaListUserAssetsRequest();
            myNewest.orderBy = IcosaOrderBy.NEWEST;
            return myNewest;
        }

        public override string ToString()
        {
            return AutoStringify.Stringify(this);
        }
    }

    /// <summary>
    /// Represents a set of Icosa request parameters determining which liked assets should be returned.
    /// Currently, only requests for the liked assets of the logged in user are supported.
    /// null values mean "don't filter by this parameter".
    /// </summary>
    [AutoStringifiable]
    public class IcosaListLikedAssetsRequest : IcosaRequest
    {
        /// <summary>
        // A valid user id. Currently, only the special value 'me', representing the
        // currently-authenticated user is supported. To use 'me', you must pass
        // an OAuth token with the request.
        /// </summary>
        public string name = "me";

        public IcosaListLikedAssetsRequest()
        {
        }

        /// <summary>
        /// Returns a ListUserAssetsRequest that requests the user's most recently liked assets.
        /// </summary>
        public static IcosaListLikedAssetsRequest MyLiked()
        {
            IcosaListLikedAssetsRequest myLiked = new IcosaListLikedAssetsRequest();
            myLiked.orderBy = IcosaOrderBy.LIKED_TIME;
            return myLiked;
        }

        public override string ToString()
        {
            return AutoStringify.Stringify(this);
        }
    }

    /// <summary>
    /// Represents the status of an operation: success or failure + error message.
    ///
    /// A typical pattern is to return a IcosaStatus to indicate the success of an operation, instead of just a bool.
    /// So your code would do something like:
    ///
    /// @{
    /// IcosaStatus MyMethod() {
    ///   if (somethingWentWrong) {
    ///     return IcosaStatus.Error("Failed to reticulate spline.");
    ///   }
    ///   ...
    ///   return IcosaStatus.Success();
    /// }
    /// @}
    ///
    /// You can also chain IcosaStatus failures, using one IcosaStatus as the cause of another:
    ///
    /// @{
    /// IcosaStatus MyMethod() {
    ///   IcosaStatus status = TesselateParabolicNonUniformDarkMatterQuantumSuperManifoldWithCheese();
    ///   if (!status.ok) {
    ///     return IcosaStatus.Error(status, "Tesselation failure.");
    ///   }
    ///   ...
    ///   return IcosaStatus.Success();
    /// }
    /// @}
    ///
    /// Using IcosaStatus vs. throwing exceptions: IcosaStatus typically represents an "expected" failure, that is,
    /// an operation where failure is common and acceptable. For example, validating user input, consuming some
    /// external file which might or might not be well formatted, sending a web request, etc. For unexpected
    /// failures (logic errors, assumption violations, etc), it's best to use exceptions.
    /// </summary>
    public struct IcosaStatus
    {
        /// <summary>
        /// Indicates whether the operation succeeded.
        /// </summary>
        public bool ok;

        /// <summary>
        /// If the operation failed, this is the error message. This is an error message suitable for
        /// logging, not necessarily a user-friendly message.
        /// </summary>
        public string errorMessage;

        /// <summary>
        /// Creates a new IcosaStatus with the given success status and error message.
        /// </summary>
        /// <param name="ok">Whether the operation succeeded.</param>
        /// <param name="errorMessage">The error message (only relevant if ok == false).</param>
        public IcosaStatus(bool ok, string errorMessage = "")
        {
            this.ok = ok;
            this.errorMessage = errorMessage;
        }

        /// <summary>
        /// Creates a new success status.
        /// </summary>
        public static IcosaStatus Success()
        {
            return new IcosaStatus(true);
        }

        /// <summary>
        /// Creates a new error status with the given error message.
        /// </summary>
        public static IcosaStatus Error(string errorMessage)
        {
            return new IcosaStatus(false, errorMessage);
        }

        /// <summary>
        /// Creates a new error status with the given error message.
        /// </summary>
        public static IcosaStatus Error(string format, params object[] args)
        {
            return new IcosaStatus(false, string.Format(format, args));
        }

        /// <summary>
        /// Creates a new error status with the given error message and cause.
        /// The error message will automatically include all error messages in the causal chain.
        /// </summary>
        public static IcosaStatus Error(IcosaStatus cause, string errorMessage)
        {
            return new IcosaStatus(false, errorMessage + "\nCaused by: " + cause.errorMessage);
        }

        /// <summary>
        /// Creates a new error status with the given error message and cause.
        /// The error message will automatically include all error messages in the causal chain.
        /// </summary>
        public static IcosaStatus Error(IcosaStatus cause, string format, params object[] args)
        {
            return new IcosaStatus(false, string.Format(format, args) + "\nCaused by: " + cause.errorMessage);
        }

        public override string ToString()
        {
            return ok ? "OK" : string.Format("ERROR: {0}", errorMessage);
        }
    }

    /// <summary>
    /// A union of a IcosaStatus and a type. Used to represent the result of an operation, which can either
    /// be an error (represented as a IcosaStatus), or a result object (the parameter type T).
    /// </summary>
    /// <typeparam name="T">The result object.</typeparam>
    public class IcosaStatusOr<T>
    {
        private IcosaStatus status;
        private T value;

        /// <summary>
        /// Creates a IcosaStatusOr with the given error status.
        /// </summary>
        /// <param name="status">The error status with which to create it.</param>
        public IcosaStatusOr(IcosaStatus status)
        {
            if (status.ok)
            {
                throw new Exception("IcosaStatusOr(IcosaStatus) can only be used with an error status.");
            }

            this.status = status;
            this.value = default(T);
        }

        /// <summary>
        /// Creates a IcosaStatusOr with the given value.
        /// The status will be set to success.
        /// </summary>
        /// <param name="value">The value with which to create it.</param>
        public IcosaStatusOr(T value)
        {
            this.status = IcosaStatus.Success();
            this.value = value;
        }

        /// <summary>
        /// Returns the status.
        /// </summary>
        public IcosaStatus Status
        {
            get { return status; }
        }

        /// <summary>
        /// Shortcut to Status.ok.
        /// </summary>
        public bool Ok
        {
            get { return status.ok; }
        }

        /// <summary>
        /// Returns the value. The value can only be obtained if the status is successful. If the status
        /// is an error, reading this property will throw an exception.
        /// </summary>
        public T Value
        {
            get
            {
                if (!status.ok)
                {
                    throw new Exception("Can't get value from an unsuccessful IcosaStatusOr: " + this);
                }

                return value;
            }
        }

        public override string ToString()
        {
            return string.Format("IcosaStatusOr<{0}>: {1}{2}", typeof(T).Name, status,
                status.ok ? (value == null ? "(null)" : value.ToString()) : "");
        }
    }

    /// <summary>
    /// Base class for all result types.
    /// </summary>
    public abstract class IcosaBaseResult
    {
        /// <summary>
        /// The status of the operation (success or failure).
        /// </summary>
        public IcosaStatus status;
    }

    /// <summary>
    /// Represents the result of a IcosaListAssetsRequest or IcosaListUserAssetsRequest.
    /// </summary>
    [AutoStringifiable]
    public class IcosaListAssetsResult : IcosaBaseResult
    {
        /// <summary>
        /// A list of assets that match the criteria specified in the request.
        /// </summary>
        public List<IcosaAsset> assets;

        /// <summary>
        /// The total number of assets in the list, without pagination.
        /// </summary>
        public int totalSize;

        /// <summary>
        /// The token to retrieve the next page of results, if any.
        /// If there is no next page, this will be null.
        /// </summary>
        public string nextPageToken;

        public IcosaListAssetsResult(IcosaStatus status, int totalSize = 0, List<IcosaAsset> assets = null,
            string nextPageToken = null)
        {
            this.status = status;
            this.assets = assets;
            this.totalSize = totalSize;
            this.nextPageToken = nextPageToken;
        }

        public override string ToString()
        {
            return AutoStringify.Stringify(this);
        }
    }

    /// <summary>
    /// Represents the result of importing an asset.
    /// </summary>
    public class IcosaImportResult
    {
        /// <summary>
        /// The GameObject representing the imported asset.
        /// </summary>
        public GameObject gameObject;

        /// <summary>
        /// The main thread throttler object, if importing in "throttled" mode. This will be null if not
        /// in throttled mode. Enumerate this on the main thread to gradually perform necessary main
        /// thread operations like creating meshes, textures, etc (see documentation for IcosaImportOptions for
        /// more details).
        ///
        /// IMPORTANT: this enumerator is not designed to be used across scene (level) loads. Always finish
        /// enumerating it before loading a new scene.
        /// </summary>
        public IEnumerable mainThreadThrottler;

        public IcosaImportResult(GameObject gameObject)
        {
            this.gameObject = gameObject;
        }
    }

    /// <summary>
    /// Represents the result of fetching files for an asset.
    /// </summary>
    [AutoStringifiable]
    public class IcosaFormatTypeFetchResult : IcosaBaseResult
    {
        public IcosaAsset asset;

        public IcosaFormatTypeFetchResult(IcosaStatus status, IcosaAsset asset)
        {
            this.status = status;
            this.asset = asset;
        }

        public override string ToString()
        {
            return AutoStringify.Stringify(this);
        }
    }

    /// <summary>
    /// Options for fetching a thumbnail.
    /// </summary>
    public class IcosaFetchThumbnailOptions
    {
        /// <summary>
        /// If nonzero, this is the requested thumbnail image size, in pixels. This is the size
        /// of the image's largest dimension (width, for most thumbnails).
        /// This is just a hint that the implementation will try (but is not guaranteed) to honor.
        /// </summary>
        public int requestedImageSize { get; private set; }

        public IcosaFetchThumbnailOptions()
        {
        }

        public void SetRequestedImageSize(int requestedImageSize)
        {
            this.requestedImageSize = requestedImageSize;
        }
    }
}