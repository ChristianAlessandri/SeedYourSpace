using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

/// <summary>
/// A modern URP Custom Renderer Feature using the Render Graph API.
/// Downsamples the 3D scene to create a crisp pixel-art aesthetic,
/// while leaving the Overlay UI rendered at full resolution.
/// </summary>
public class PixelizeRenderFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class PixelizeSettings
    {
        [Tooltip("Higher value means chunkier pixels.")]
        [Range(1, 10)]
        public int pixelScale = 4;
        
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

    // A simple struct to hold our texture handles during the Render Graph execution
    private class PassData
    {
        public TextureHandle source;
    }

    /// <summary>
    /// Record the render graph commands for this pass. This is where we define the downsampling and upsampling steps.
    /// </summary>
    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        // Get the current frame's camera and resource data
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

        TextureHandle activeColor = resourceData.activeColorTexture;

        if (!activeColor.IsValid()) return;

        RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
        int downscaledWidth = Mathf.Max(1, desc.width / settings.pixelScale);
        int downscaledHeight = Mathf.Max(1, desc.height / settings.pixelScale);

        // Define our temporary low-res texture
        TextureDesc tempDesc = new TextureDesc(downscaledWidth, downscaledHeight);
        tempDesc.colorFormat = desc.graphicsFormat;
        tempDesc.depthBufferBits = 0;
        tempDesc.filterMode = FilterMode.Point; // Keep pixels sharp
        tempDesc.name = "_PixelizeTempTexture";

        // Let the Render Graph allocate the texture memory
        TextureHandle tempTexture = renderGraph.CreateTexture(tempDesc);

        // PASS 1: Downsample (Copy from the Main Camera to the Tiny Texture)
        using (var builder = renderGraph.AddRasterRenderPass<PassData>("Pixelize Downsample", out var passData))
        {
            passData.source = activeColor;
            
            builder.UseTexture(passData.source, AccessFlags.Read);
            builder.SetRenderAttachment(tempTexture, 0, AccessFlags.Write);

            builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
            {
                // The 'false' at the end ensures point filtering is used
                Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0.0f, false);
            });
        }

        // PASS 2: Upsample (Copy from the Tiny Texture back to the Main Camera)
        using (var builder = renderGraph.AddRasterRenderPass<PassData>("Pixelize Upsample", out var passData))
        {
            passData.source = tempTexture;

            builder.UseTexture(passData.source, AccessFlags.Read);
            builder.SetRenderAttachment(activeColor, 0, AccessFlags.Write);

            builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
            {
                Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0.0f, false);
            });
        }
    }
}