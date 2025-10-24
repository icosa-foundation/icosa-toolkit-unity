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
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using IcosaApiClient;

namespace IcosaClientEditor
{
    class WarnIfManagerNotInScene : IProcessSceneWithReport, IPostprocessBuildWithReport
    {
        bool sawManager;

        public int callbackOrder
        {
            get { return 0; }
        }

        public void OnProcessScene(UnityEngine.SceneManagement.Scene scene, BuildReport report)
        {
            if (!sawManager)
            {
                if (scene.isLoaded)
                {
                    foreach (var gameObject in scene.GetRootGameObjects())
                    {
                        if (gameObject.GetComponentInChildren<IcosaToolkitManager>() != null)
                        {
                            sawManager = true;
                            break;
                        }
                    }
                }
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (!sawManager)
            {
                Debug.LogWarning("Please add a IcosaToolkitManager component to your scene.");
            }
        }
    }
}