using System.Collections.Generic;
using UnityEngine;

namespace EmissiveClothing
{
    /*
     * LICENSE: Creative Commons with Attribution (CC BY 3.0) https://creativecommons.org/licenses/by/3.0/
     * Originally released by Alazi under CC BY 3.0
     * https://github.com/alazi/CUAController/releases
     */

    // The only thing we absolutely need and can't get normally from
    // DAZSkinWrap is the ComputeBuffers; Material.GetBuffer does not exist
    internal class EmissiveDAZSkinWrap : DAZSkinWrap
    {
        private Dictionary<int, ComputeBuffer> emissiveMaterialVertsBuffers = new Dictionary<int, ComputeBuffer>();

        private JSONStorableBool renderOriginal;
        private bool oldOriginal;
        private Material[] hiddenMats;
        private Material[] emissiveMats;

        public void CopyFrom(DAZSkinWrap wrap, Material[] hiddenMats, Material[] emissiveMats, JSONStorableBool renderOriginal)
        {
            skinTransform = wrap.skinTransform;
            skin = wrap.skin;
            dazMesh = wrap.dazMesh;
            GPUSkinWrapper = wrap.GPUSkinWrapper;
            GPUMeshCompute = wrap.GPUMeshCompute;
            CopyMaterials();
            GPUmaterials = wrap.GPUmaterials;
            this.renderOriginal = renderOriginal;
            this.hiddenMats = hiddenMats;
            this.emissiveMats = emissiveMats;

            wrapName = wrap.wrapName;
            wrapStore = wrap.wrapStore;

            wrap.draw = false;
            draw = true;

            surfaceOffset = wrap.surfaceOffset;
            defaultSurfaceOffset = wrap.defaultSurfaceOffset;
            additionalThicknessMultiplier = wrap.additionalThicknessMultiplier;
            defaultAdditionalThicknessMultiplier = wrap.defaultAdditionalThicknessMultiplier;

            var control = GetComponent<DAZSkinWrapControl>();
            if(control && (control.wrap == null || control.wrap == wrap))
            {
                control.wrap = this;
            }
        }

        private void LateUpdate() // Overwrite base one to possibly not render some mats
        {
            UpdateVertsGPU();
            if(renderOriginal.val != oldOriginal)
            {
                materialVertsBuffers.Clear();
                materialNormalsBuffers.Clear();
                materialTangentsBuffers.Clear();
                oldOriginal = renderOriginal.val;
            }
            if(!renderOriginal.val)
            {
                var temp = GPUmaterials;
                GPUmaterials = hiddenMats;
                DrawMeshGPU();
                GPUmaterials = temp;
            }
            else
            {
                DrawMeshGPU();
            }

            for(int i = 0; i < emissiveMats.Length; i++)
            {
                if(emissiveMats[i] == null)
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
            if(ourB != theirB)
            {
                emissiveMats[i].SetBuffer(buf, theirB);
                ours[i] = theirB;
            }
        }
    }
}
