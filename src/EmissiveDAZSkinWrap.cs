using System.Collections.Generic;
using UnityEngine;

namespace EmissiveClothing
{
    // The only thing we absolutely need and can't get normally from
    // DAZSkinWrap is the ComputeBuffers; Material.GetBuffer does not exist
    class EmissiveDAZSkinWrap : DAZSkinWrap
    {
        private Dictionary<int, ComputeBuffer> emissiveMaterialVertsBuffers = new Dictionary<int, ComputeBuffer>();

        private JSONStorableBool renderOriginal;
        private bool oldOriginal;
        private Material[] hiddenMats;
        private Material[] emissiveMats;

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
                var temp = base.GPUmaterials;
                base.GPUmaterials = hiddenMats;
                DrawMeshGPU();
                base.GPUmaterials = temp;
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
