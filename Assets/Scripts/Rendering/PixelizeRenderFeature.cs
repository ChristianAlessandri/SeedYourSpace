using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class PixelizeRenderFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class PixelizeSettings
    {
        [Tooltip("Higher value means chunkier pixels.")]
        [Range(1, 10)]
        public int pixelScale = 0;
        
        [Tooltip("Toggle the effect on or off.")]
        public bool isEnabled = true;
    }

    public PixelizeSettings settings = new PixelizeSettings();
    private PixelizePass pixelizePass;

    public override void Create()
    {
        pixelizePass = new PixelizePass(settings);
        // Injecting the pass AFTER transparent objects but BEFORE Post Processing and UI
        pixelizePass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!settings.isEnabled) return;

        #if UNITY_EDITOR
        if (renderingData.cameraData.isSceneViewCamera) return;
        #endif

        renderer.EnqueuePass(pixelizePass);
    }
}

public class PixelizePass : ScriptableRenderPass
{
    private PixelizeRenderFeature.PixelizeSettings settings;

    public PixelizePass(PixelizeRenderFeature.PixelizeSettings settings)
    {
        this.settings = settings;
    }

    private class PassData
    {
        public TextureHandle source;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

        TextureHandle activeColor = resourceData.activeColorTexture;
        if (!activeColor.IsValid()) return;

        // Define and allocate our temporary low-res texture
        TextureDesc tempDesc = CreateTextureDescriptor(cameraData.cameraTargetDescriptor);
        TextureHandle tempTexture = renderGraph.CreateTexture(tempDesc);

        AddDownsamplePass(renderGraph, activeColor, tempTexture);
        AddUpsamplePass(renderGraph, tempTexture, activeColor);
    }

    private TextureDesc CreateTextureDescriptor(RenderTextureDescriptor cameraDescriptor)
    {
        int downscaledWidth = Mathf.Max(1, cameraDescriptor.width / settings.pixelScale);
        int downscaledHeight = Mathf.Max(1, cameraDescriptor.height / settings.pixelScale);

        TextureDesc tempDesc = new TextureDesc(downscaledWidth, downscaledHeight);
        tempDesc.colorFormat = cameraDescriptor.graphicsFormat;
        tempDesc.depthBufferBits = 0;
        tempDesc.filterMode = FilterMode.Point; // Keep pixels sharp
        tempDesc.name = "_PixelizeTempTexture";

        return tempDesc;
    }

    private void AddDownsamplePass(RenderGraph renderGraph, TextureHandle sourceTexture, TextureHandle targetTexture)
    {
        using (var builder = renderGraph.AddRasterRenderPass<PassData>("Pixelize Downsample", out var passData))
        {
            passData.source = sourceTexture;
            
            builder.UseTexture(passData.source, AccessFlags.Read);
            builder.SetRenderAttachment(targetTexture, 0, AccessFlags.Write);

            builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
            {
                // The 'false' at the end ensures point filtering is used
                Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0.0f, false);
            });
        }
    }

    private void AddUpsamplePass(RenderGraph renderGraph, TextureHandle sourceTexture, TextureHandle targetTexture)
    {
        using (var builder = renderGraph.AddRasterRenderPass<PassData>("Pixelize Upsample", out var passData))
        {
            passData.source = sourceTexture;

            builder.UseTexture(passData.source, AccessFlags.Read);
            builder.SetRenderAttachment(targetTexture, 0, AccessFlags.Write);

            builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
            {
                Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0.0f, false);
            });
        }
    }
}