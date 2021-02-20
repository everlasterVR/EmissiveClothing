﻿using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MeshVR;
using SimpleJSON;

namespace EmissiveClothing
{
    public class EmissiveClothing : MVRScript
    {
        /*
         * LICENSE: Creative Commons with Attribution (CC BY 3.0) https://creativecommons.org/licenses/by/3.0/
         * Originally released by Alazi under CC BY 3.0
         * https://github.com/alazi/CUAController/releases
         */

        private const string version = "<Version>";

        JSONStorableFloat alpha;
        JSONStorableBool renderOriginal;
        JSONStorableUrl loadedShaderPath = new JSONStorableUrl("shader", "");
        bool loadedAssetBundle;
        bool isLoading;
        int retryAttempts;

        private static readonly string BUNDLE_PATH = "Custom/Assets/Alazi/EmissiveClothing/emissiveshader.assetbundle";

        protected List<Material[]> ourMaterials = new List<Material[]>();
        protected Shader shader;
        protected List<DAZSkinWrap> wraps = new List<DAZSkinWrap>();
        protected JSONStorableColor color;

        JSONStorableBool mode;

        public override void Init()
        {
            try {
                TitleUITextField();

                color = new JSONStorableColor("Color", HSVColorPicker.RGBToHSV(1f, 1f, 1f), _ => SyncMats());
                RegisterColor(color);
                CreateColorPicker(color, true);
                FloatSlider(ref alpha, "Color Alpha", 1,
                    _ => { SyncMats(); }, 0, 2, true);

                renderOriginal = new JSONStorableBool("Render Original Material", true);
                RegisterBool(renderOriginal);
                CreateToggle(renderOriginal, false);

                mode = new JSONStorableBool("Mode", true, (bool _) => SyncMats());
                CreateToggle(mode, false);

                RegisterUrl(loadedShaderPath);

                CreateButton("Rescan active clothes").button.onClick.AddListener(() => {
                    StartCoroutine(Rebuild());
                    });
                
                StartCoroutine(LoadShaderAndInit());
            } catch (Exception e) {
                Log.Error($"{e}");
            }
        }

        private void TitleUITextField()
        {
            JSONStorableString storable = new JSONStorableString("title", "");
            UIDynamicTextField field = CreateTextField(storable);
            field.UItext.fontSize = 36;
            field.height = 100;
            storable.val = $"<b>{nameof(EmissiveClothing)}</b>\n<size=28>v{version}</size>";
        }

        public override void RestoreFromJSON(JSONClass jc, bool restorePhysical = true, bool restoreAppearance = true, JSONArray presetAtoms = null, bool setMissingToDefault = true)
        {
            isLoading = true;
            base.RestoreFromJSON(jc, restorePhysical, restoreAppearance, presetAtoms, setMissingToDefault);
        }

        string GetPluginPath() // basically straight from VAMDeluxe's Dollmaster
        {
            string pluginId = this.storeId.Split('_')[0];
            string pathToScriptFile = manager.GetJSON(true, true)["plugins"][pluginId].Value;
            string pathToScriptFolder = pathToScriptFile.Substring(0, pathToScriptFile.LastIndexOfAny(new char[] { '/', '\\' }));
            return pathToScriptFolder;
        }

        private void SyncMats()
        {
            foreach (var r in ourMaterials) {
                foreach (var m in r) {
                    if (m == null)
                        continue;
                    Color c = color.colorPicker.currentColor;
                    if (mode.val)
                        c.a = alpha.val;
                    else
                        c *= alpha.val;
                    m.SetColor("_Color", c);
                    
                }
            }
        }

        private void FloatSlider(ref JSONStorableFloat output, string name, float start, JSONStorableFloat.SetFloatCallback callback, float min, float max, bool rhs)
        {
            output = new JSONStorableFloat(name, start, callback, min, max, false, true);
            RegisterFloat(output);
            CreateSlider(output, rhs);
        }

        // MeshVR.AssetLoader provides some nice refcounting for us.
        // The downsides are the annoying callback API, that we can't suppress error messages, and that a refcount really shouldn't be created on failure, wat
        class LoadAssetBundle : CustomYieldInstruction {
            AssetLoader.AssetBundleFromFileRequest req;

            private bool done;

            // We can't write the type AssetBundle, but we can call methods on it. lolwut C#
            public bool success => req.assetBundle != null;
            public T GetAsset<T>(string name) where T : UnityEngine.Object
            {
                return req.assetBundle?.LoadAsset<T>(name);
            }

            public LoadAssetBundle(string url)
            {
                req = new AssetLoader.AssetBundleFromFileRequest {
                    path = url,
                    callback = _ => {
                        done = true;
                        if (req.assetBundle == null)
                            AssetLoader.DoneWithAssetBundleFromFile(req.path);
                    }
                };
                AssetLoader.QueueLoadAssetBundleFromFile(req);
            }

            public override bool keepWaiting => !done;
        }

        private IEnumerator AttemptLoadShader(string url)
        {
            if (shader != null)
                yield break;
            var request = new LoadAssetBundle(url);
            yield return StartCoroutine(request);

            if (!request.success)
                yield break;
            shader = request.GetAsset<Shader>("Assets/EmissiveHack.shader");
            if (shader != null) {
                loadedShaderPath.val = url;
                loadedAssetBundle = true;
            } else {
                Log.Error("Bad emissiveshader assetbundle");
                AssetLoader.DoneWithAssetBundleFromFile(url);
            }
        }

        // If there are multiple copies of this script loaded from different paths, avoid failures due to trying to load the same assetbundle. 
        // This is particularly important because sometimes the bundle seems to get leaked and not unload the shader.
        // Stash the url as well so that vac saves work properly even if this happens
        private static readonly string STASH_NAME = "Alazi.EmissiveShaderStash";
        private void StashShaderName()
        {
            PlayerPrefs.SetString(STASH_NAME, loadedShaderPath.val);
        }
        private void LoadStashedShader()
        {
            shader = Resources.FindObjectsOfTypeAll<Shader>().Where(s => s.name == "Custom/Alazi/ExtraEmissiveComputeBuff").FirstOrDefault();
            if (shader != null) {
                loadedShaderPath.val = PlayerPrefs.GetString(STASH_NAME);
            } else {
                PlayerPrefs.DeleteKey(STASH_NAME);
            }
        }

        protected IEnumerator LoadShaderAndInit()
        {
            // Wait until json load is done
            yield return new WaitForEndOfFrame();
            LoadStashedShader();

            if (shader == null) {
                if (loadedShaderPath.val != "")
                    yield return AttemptLoadShader(loadedShaderPath.val);
                yield return AttemptLoadShader($"Custom/Assets/{BUNDLE_NAME}");

                if (shader == null) {
                    Log.Error("Failed to load shader");
                    yield break;
                }
                StashShaderName();
            }

            Build();
        }

        protected IEnumerator Rebuild()
        {
            Unbuild();
            // wait for components to be destroyed
            yield return new WaitForEndOfFrame();
            Build();
        }
        protected void Build()
        {
            var allWraps = containingAtom.gameObject.GetComponentsInChildren<DAZSkinWrap>(false);
            ourMaterials = new List<Material[]>();
            wraps = new List<DAZSkinWrap>();
            if (allWraps.Length == 0) {
                Log.Message("No clothes loaded");
                return;
            }
            bool doRescan = false;
            foreach (var wrap in allWraps) {
                if (wrap.ToString().Contains("Emissive")) {
                    Log.Error($"EmissiveClothing: found dup {wrap}");
                    continue;
                }
                if (wrap.skin.delayDisplayOneFrame) {
                    Log.Error($"EmissiveClothing: {wrap} is delayed, not set up to handle that");
                    continue;
                }
                var ourMats = new Material[wrap.GPUmaterials.Length];
                var theirNewMats = wrap.GPUmaterials.ToArray();
                bool foundAny = false;
                
                foreach (var mo in wrap.GetComponents<DAZSkinWrapMaterialOptions>()) {
                    if (!mo.overrideId.Contains("(em)"))
                        continue;
                    // too lazy to duplicate all the code for slots2 / simpleMaterial
                    if (mo.paramMaterialSlots?.Length == 0) 
                        continue;
                    foundAny = true;
                    
                    foreach (var i in mo.paramMaterialSlots) {
                        var mat = wrap.GPUmaterials[i];
                        var ourMat = new Material(shader);
                        ourMats[i] = ourMat;
                        ourMat.name = mat.name;

                        // Ideally we'd hook all the config stuff in MaterialOptions, but that would 
                        // require too much effort to reimplement all the url/tile/offset->texture code 
                        // or to copy the existing one to override the relevant methods
                        // So require the user to hit rescan manually.
                        var tex = mat.GetTexture("_DecalTex");
                        ourMat.SetTexture("_MainTex", tex);
                        mat.SetTexture("_DecalTex", null);

                        // Particularly during scene load it may take a while for these to be loaded, try a few times
                        if (tex == null)
                            doRescan = true;

                        // could maybe get some tiny extra performance by using a null shader instead
                        theirNewMats[i] = new Material(mat);
                        theirNewMats[i].SetFloat("_AlphaAdjust", -1);
                    }
                }
                if (!foundAny)
                    continue;

                ourMaterials.Add(ourMats);

                wrap.BroadcastMessage("OnApplicationFocus", true);
                wrap.gameObject.AddComponent<EmissiveDAZSkinWrap>().CopyFrom(wrap, theirNewMats, ourMats, renderOriginal);
                wraps.Add(wrap);
            }

            SyncMats();
            if (doRescan)
                StartCoroutine(QueueRescan());
            else
                isLoading = false;
        }

        private IEnumerator QueueRescan()
        {
            // 1s normally or 40s during load
            if (retryAttempts < 5 || (isLoading && retryAttempts < 200)) {
                yield return new WaitForSeconds(0.2f);
                retryAttempts++;
                yield return Rebuild();
            }
        }

        void Unbuild()
        {
            for (int i = 0; i < wraps.Count; i++) {
                var wrap = wraps[i];
                var mats = ourMaterials[i];
                for (int j = 0; j < mats.Length; j++) {
                    if (mats[j] == null)
                        continue;
                    // If it's been changed, don't reset it
                    if (wrap.GPUmaterials[j].GetTexture("_DecalTex") == null)
                        wrap.GPUmaterials[j].SetTexture("_DecalTex", mats[j].GetTexture("_MainTex"));
                }

                GameObject.Destroy(wrap.gameObject?.GetComponent<EmissiveDAZSkinWrap>());
                wrap.draw = true;
                var control = wrap.gameObject?.GetComponent<DAZSkinWrapControl>();
                if (control && (control.wrap == null || control.wrap == this)) {
                    control.wrap = wrap;
                }
            }
        }

        void OnDestroy()
        {
            Unbuild();
            if (loadedAssetBundle) {
                AssetLoader.DoneWithAssetBundleFromFile(loadedShaderPath.val);
            }
        }

        // The only thing we absolutely need and can't get normally from 
        // DAZSkinWrap is the ComputeBuffers; Material.GetBuffer does not exist
        class EmissiveDAZSkinWrap : DAZSkinWrap
        {
            Dictionary<int, ComputeBuffer> emissiveMaterialVertsBuffers = new Dictionary<int, ComputeBuffer>();

            JSONStorableBool renderOriginal;
            bool oldOriginal;
            Material[] hiddenMats;
            Material[] emissiveMats;

            public void CopyFrom(DAZSkinWrap wrap, Material[] hiddenMats, Material[] emissiveMats, JSONStorableBool renderOriginal)
            {
                base.skinTransform = wrap.skinTransform;
                base.skin = wrap.skin;
                base.dazMesh = wrap.dazMesh;
                base.GPUSkinWrapper = wrap.GPUSkinWrapper;
                base.GPUMeshCompute = wrap.GPUMeshCompute;
                base.CopyMaterials();
                base.GPUmaterials = wrap.GPUmaterials;
                this.renderOriginal = renderOriginal;
                this.hiddenMats = hiddenMats;
                this.emissiveMats = emissiveMats;

                base.wrapName = wrap.wrapName;
                base.wrapStore = wrap.wrapStore;

                wrap.draw = false;
                base.draw = true;

                base.surfaceOffset = wrap.surfaceOffset;
                base.defaultSurfaceOffset = wrap.defaultSurfaceOffset;
                base.additionalThicknessMultiplier = wrap.additionalThicknessMultiplier;
                base.defaultAdditionalThicknessMultiplier = wrap.defaultAdditionalThicknessMultiplier;

                var control = GetComponent<DAZSkinWrapControl>();
                if (control && (control.wrap == null || control.wrap == wrap)) {
                    control.wrap = this;
                }
            }


            void LateUpdate() // Overwrite base one to possibly not render some mats
            {
                UpdateVertsGPU();
                if (renderOriginal.val != oldOriginal) {
                    materialVertsBuffers.Clear();
                    materialNormalsBuffers.Clear();
                    materialTangentsBuffers.Clear();
                    oldOriginal = renderOriginal.val;
                }
                if (!renderOriginal.val) {
                    var temp = base.GPUmaterials;
                    base.GPUmaterials = hiddenMats;
                    DrawMeshGPU();
                    base.GPUmaterials = temp;
                } else {
                    DrawMeshGPU();
                }
                
                for (int i = 0; i < emissiveMats.Length; i++) {
                    if (emissiveMats[i] == null)
                        continue;
                    emissiveMats[i].renderQueue = base.GPUmaterials[i].renderQueue; // could probably get away without updating this past initialization
                    UpdateBuffer(emissiveMaterialVertsBuffers, base.materialVertsBuffers, "verts", i);

                    Graphics.DrawMesh(base.mesh, Matrix4x4.identity, emissiveMats[i], 0, null, i, null, false, false);
                }
            }

            private void UpdateBuffer(Dictionary<int, ComputeBuffer> ours, Dictionary<int, ComputeBuffer> theirs, string buf, int i)
            {
                ComputeBuffer ourB, theirB;
                ours.TryGetValue(i, out ourB);
                theirs.TryGetValue(i, out theirB);
                if (ourB != theirB) {
                    emissiveMats[i].SetBuffer(buf, theirB);
                    ours[i] = theirB;
                }
            }
        }
    }

}
