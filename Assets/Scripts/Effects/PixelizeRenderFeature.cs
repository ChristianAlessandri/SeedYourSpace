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

    // A struct to hold our texture handles during the Render Graph execution
    private class PassData
    {
        public TextureHandle source;
    }

    /// <summary>
    /// Record the render graph commands for this pass. This is where we define the downsampling and upsampling steps.
    /// </summary>
    /// <param name="renderGraph">The render graph instance.</param>
    /// <param name="frameData">The context container holding frame-specific data.</param>
    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        // Get the current frame's camera and resource data
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

        TextureHandle activeColor = resourceData.activeColorTexture;
        if (!activeColor.IsValid()) return;

        // Define and allocate our temporary low-res texture
        TextureDesc tempDesc = CreateTextureDescriptor(cameraData.cameraTargetDescriptor);
        TextureHandle tempTexture = renderGraph.CreateTexture(tempDesc);

        // Orchestrate the rendering passes
        AddDownsamplePass(renderGraph, activeColor, tempTexture);
        AddUpsamplePass(renderGraph, tempTexture, activeColor);
    }

    /// <summary>
    /// Calculates the scaled-down dimensions and creates the texture descriptor.
    /// </summary>
    /// <param name="cameraDescriptor">The descriptor of the main camera's render target.</param>
    /// <returns>A TextureDesc for the downscaled temporary texture.</returns>
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

    /// <summary>
    /// Adds a raster pass to copy and scale down the main camera output to the tiny texture.
    /// </summary>
    /// <param name="renderGraph">The render graph instance.</param>
    /// <param name="sourceTexture">The source texture (main camera output).</param>
    /// <param name="targetTexture">The target texture (downscaled temporary texture).</param>
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

    /// <summary>
    /// Adds a raster pass to copy and scale up the tiny texture back to the main camera output.
    /// </summary>
    /// <param name="renderGraph">The render graph instance.</param>
    /// <param name="sourceTexture">The low-resolution texture to upsample.</param>
    /// <param name="targetTexture">The main camera's render target to write the up
    /// sampled result to.</param>
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