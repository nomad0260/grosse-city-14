using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.ContentPack;
using Robust.Shared.Graphics;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client.UIpatch;

/// <summary>
/// Full-screen cinematic backdrop. It crossfades between lobby stills and keeps
/// a slow Ken Burns drift, so the scene never reads as a flat photograph.
/// The drift phase is global, so an image swap never produces a visible jump.
/// </summary>
public sealed class LobbyBackdrop : Control
{
    private const float FadeSeconds = 1.8f;
    private const float ZoomBase = 1.025f;
    private const float ZoomSwing = 0.022f;
    private const float PanX = 0.55f;
    private const float PanY = 0.35f;

    private readonly IResourceCache _cache;

    private Texture? _current;
    private Texture? _next;
    private Texture? _currentBlurred;
    private Texture? _nextBlurred;
    private string? _currentPath;
    private string? _nextPath;
    private float _fade = -1f;
    private float _time;

    private Texture[]? _rsiFrames;
    private float[]? _rsiDelays;
    private int _rsiFrameIndex;
    private float _rsiFrameTimer;

    private byte[][]? _seqFrameBytes;
    private string? _sequenceFolder;
    private OwnedTexture? _gifTexCurrent;
    private OwnedTexture? _gifTexNext;
    private OwnedTexture? _gifBlurCurrent;
    private OwnedTexture? _gifBlurNext;
    private float[]? _gifDelays;
    private int _gifFrameIndex;
    private int _gifTexCurrentIndex = -1;
    private int _gifTexNextIndex = -1;
    private float _gifFrameTimer;

    private const int BlurWidth = 128;
    private const int BlurHeight = 72;

    /// <summary>
    /// Current crossfade progress, or -1 when a single image is showing.
    /// </summary>
    public float Fade => _fade;

    public Texture? CurrentTexture => _gifTexCurrent
        ?? (_rsiFrames != null && _rsiFrames.Length > 0 ? _rsiFrames[_rsiFrameIndex] : _current);
    public Texture? NextTexture => _next;
    public Texture? CurrentBlurredTexture => _gifBlurCurrent ?? (_rsiFrames != null ? CurrentTexture : _currentBlurred);
    public Texture? NextBlurredTexture => _gifBlurNext ?? _nextBlurred;

    /// <summary>
    /// Crossfade progress (0..1) for <see cref="NextBlurredTexture"/> when streaming frames, or -1 when inactive.
    /// </summary>
    public float BlurBlend
    {
        get
        {
            if (_gifBlurNext == null || _gifDelays == null || _gifDelays.Length <= 1)
                return _fade;

            var delay = _gifFrameIndex < _gifDelays.Length && _gifDelays[_gifFrameIndex] > 0.005f
                ? _gifDelays[_gifFrameIndex]
                : 0.04f;
            return Math.Clamp(_gifFrameTimer / delay, 0f, 1f);
        }
    }

    public Vector2 ViewSize => new(PixelSize.X, PixelSize.Y);

    /// <summary>
    /// Current Ken Burns zoom. Shared with the frosted panels so their blur
    /// stays aligned with the visible backdrop.
    /// </summary>
    public float ViewZoom => (_seqFrameBytes != null || _rsiFrames != null)
        ? 1f
        : ZoomBase + ZoomSwing * (0.5f + 0.5f * MathF.Sin(_time * 0.055f));

    /// <summary>
    /// Current Ken Burns pan. Shared with the frosted panels.
    /// </summary>
    public Vector2 ViewPan => (_seqFrameBytes != null || _rsiFrames != null)
        ? Vector2.Zero
        : new(PanX * MathF.Sin(_time * 0.031f), PanY * MathF.Sin(_time * 0.023f + 1.2f));

    public LobbyBackdrop()
    {
        _cache = IoCManager.Resolve<IResourceCache>();
        MouseFilter = MouseFilterMode.Ignore;
        RectClipContent = true;
    }

    /// <summary>
    /// Streams an animated frame sequence (e.g. 000.jpg .. NNN.jpg + delays.txt)
    /// using only two reusable GPU textures with bilinear filtering (Filter = true),
    /// staying 100% compatible with the Robust sandbox and ACZ packaging.
    /// </summary>
    public bool SetFrameSequenceBackground(string folderPath)
    {
        try
        {
            var resMan = IoCManager.Resolve<IResourceManager>();
            var delaysPath = new ResPath($"{folderPath}/delays.txt");
            if (!resMan.TryContentFileRead(delaysPath, out var delaysStream))
                return false;

            var delays = new List<float>();
            using (var reader = new StreamReader(delaysStream))
            {
                while (reader.ReadLine() is { } line)
                {
                    if (float.TryParse(line.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var val) && val > 0.005f)
                        delays.Add(val);
                }
            }

            if (delays.Count == 0)
                return false;

            var frameBytes = new byte[delays.Count][];
            var width = 0;
            var height = 0;

            for (var i = 0; i < delays.Count; i++)
            {
                var framePath = new ResPath($"{folderPath}/{i:D3}.jpg");
                if (!resMan.TryContentFileRead(framePath, out var frameStream))
                    return false;

                using (frameStream)
                using (var ms = new MemoryStream())
                {
                    frameStream.CopyTo(ms);
                    frameBytes[i] = ms.ToArray();
                }

                if (i == 0)
                {
                    using var probeMs = new MemoryStream(frameBytes[0]);
                    using var probe = SixLabors.ImageSharp.Image.Load<Rgba32>(probeMs);
                    width = probe.Width;
                    height = probe.Height;
                }
            }

            if (width <= 0 || height <= 0)
                return false;

            DisposeGifResources();

            var clyde = IoCManager.Resolve<IClyde>();
            var loadParams = new TextureLoadParameters
            {
                SampleParameters = new TextureSampleParameters { Filter = true },
            };
            var texSize = new Vector2i(width, height);
            var blurSize = new Vector2i(BlurWidth, BlurHeight);

            _seqFrameBytes = frameBytes;
            _sequenceFolder = folderPath;
            _gifDelays = delays.ToArray();
            _gifTexCurrent = clyde.CreateBlankTexture<Rgba32>(texSize, $"{folderPath}#A", loadParams);
            _gifTexNext = frameBytes.Length > 1 ? clyde.CreateBlankTexture<Rgba32>(texSize, $"{folderPath}#B", loadParams) : null;
            _gifBlurCurrent = clyde.CreateBlankTexture<Rgba32>(blurSize, $"{folderPath}#BlurA", loadParams);
            _gifBlurNext = frameBytes.Length > 1 ? clyde.CreateBlankTexture<Rgba32>(blurSize, $"{folderPath}#BlurB", loadParams) : null;
            _gifFrameIndex = 0;
            _gifFrameTimer = 0f;
            _gifTexCurrentIndex = -1;
            _gifTexNextIndex = -1;
            SyncGifTextures();

            _rsiFrames = null;
            _rsiDelays = null;
            _current = null;
            _next = null;
            _currentBlurred = null;
            _nextBlurred = null;
            _currentPath = folderPath;
            _nextPath = null;
            _fade = -1f;
            return true;
        }
        catch (Exception)
        {
            DisposeGifResources();
            return false;
        }
    }

    private void SyncGifTextures()
    {
        if (_seqFrameBytes == null || _gifTexCurrent == null)
            return;

        var count = _seqFrameBytes.Length;
        if (count <= 0)
            return;

        if (_gifTexCurrentIndex != _gifFrameIndex)
        {
            if (_gifTexNext != null && _gifTexNextIndex == _gifFrameIndex)
            {
                (_gifTexCurrent, _gifTexNext) = (_gifTexNext, _gifTexCurrent);
                (_gifBlurCurrent, _gifBlurNext) = (_gifBlurNext, _gifBlurCurrent);
                (_gifTexCurrentIndex, _gifTexNextIndex) = (_gifTexNextIndex, _gifTexCurrentIndex);
            }
            else
            {
                UploadGifFrame(_gifTexCurrent, _gifBlurCurrent, _gifFrameIndex);
                _gifTexCurrentIndex = _gifFrameIndex;
            }
        }

        if (_gifTexNext != null && count > 1)
        {
            var nextIndex = (_gifFrameIndex + 1) % count;
            if (_gifTexNextIndex != nextIndex)
            {
                UploadGifFrame(_gifTexNext, _gifBlurNext, nextIndex);
                _gifTexNextIndex = nextIndex;
            }
        }
    }

    private void UploadGifFrame(OwnedTexture target, OwnedTexture? blurTarget, int frameIndex)
    {
        if (_seqFrameBytes == null)
            return;

        using var ms = new MemoryStream(_seqFrameBytes[frameIndex]);
        using var img = SixLabors.ImageSharp.Image.Load<Rgba32>(ms);
        target.SetSubImage(Vector2i.Zero, img);

        if (blurTarget != null)
        {
            using var blurImg = img.Clone(ctx => ctx
                .Resize(256, 144)
                .Resize(64, 36)
                .Resize(BlurWidth, BlurHeight));
            blurTarget.SetSubImage(Vector2i.Zero, blurImg);
        }
    }

    private void DisposeGifResources()
    {
        _gifTexCurrent?.Dispose();
        _gifTexNext?.Dispose();
        _gifBlurCurrent?.Dispose();
        _gifBlurNext?.Dispose();
        _gifTexCurrent = null;
        _gifTexNext = null;
        _gifBlurCurrent = null;
        _gifBlurNext = null;
        _seqFrameBytes = null;
        _gifDelays = null;
        _gifTexCurrentIndex = -1;
        _gifTexNextIndex = -1;
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        if (_sequenceFolder != null && _seqFrameBytes == null)
            SetFrameSequenceBackground(_sequenceFolder);
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        DisposeGifResources();
    }

    /// <summary>
    /// Plays an animated RSI state full-screen as the lobby backdrop.
    /// </summary>
    public void SetAnimatedBackground(string rsiPath, string stateName)
    {
        _sequenceFolder = null;
        DisposeGifResources();
        if (!_cache.TryGetResource<RSIResource>(rsiPath, out var rsiRes) ||
            !rsiRes.RSI.TryGetState(stateName, out var state))
            return;

        var frames = state.GetFrames(RsiDirection.South);
        if (frames.Length == 0)
            return;

        _rsiFrames = frames;
        _rsiDelays = state.GetDelays();
        _rsiFrameIndex = 0;
        _rsiFrameTimer = 0f;
        _current = null;
        _next = null;
        _currentBlurred = null;
        _nextBlurred = null;
        _currentPath = rsiPath;
        _nextPath = null;
        _fade = -1f;
    }

    /// <summary>
    /// Shows the given texture path. The first call is immediate, later calls
    /// start a crossfade from whatever is currently on screen.
    /// </summary>
    public void SetBackground(string path)
    {
        _sequenceFolder = null;
        DisposeGifResources();
        _rsiFrames = null;
        _rsiDelays = null;
        if (_currentPath == path && _next == null)
            return;

        var texture = _cache.GetTexture(path);
        var blurred = GetBlurredTexture(path);

        if (_current == null)
        {
            _current = texture;
            _currentBlurred = blurred;
            _currentPath = path;
            _next = null;
            _nextBlurred = null;
            _nextPath = null;
            _fade = -1f;
            return;
        }

        if (_nextPath == path)
            return;

        _next = texture;
        _nextBlurred = blurred;
        _nextPath = path;
        _fade = 0f;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        _time += args.DeltaSeconds;

        if (_seqFrameBytes != null && _gifDelays != null && _gifDelays.Length > 1)
        {
            _gifFrameTimer += args.DeltaSeconds;
            var advanced = false;
            while (true)
            {
                var delay = _gifFrameIndex < _gifDelays.Length && _gifDelays[_gifFrameIndex] > 0.005f
                    ? _gifDelays[_gifFrameIndex]
                    : 0.04f;
                if (_gifFrameTimer < delay)
                    break;

                _gifFrameTimer -= delay;
                _gifFrameIndex = (_gifFrameIndex + 1) % _gifDelays.Length;
                advanced = true;
            }

            if (advanced)
                SyncGifTextures();
        }

        if (_rsiFrames != null && _rsiFrames.Length > 1 && _rsiDelays != null && _rsiDelays.Length > 0)
        {
            _rsiFrameTimer += args.DeltaSeconds;
            while (true)
            {
                var delay = _rsiFrameIndex < _rsiDelays.Length && _rsiDelays[_rsiFrameIndex] > 0.005f
                    ? _rsiDelays[_rsiFrameIndex]
                    : 0.05f;
                if (_rsiFrameTimer < delay)
                    break;

                _rsiFrameTimer -= delay;
                _rsiFrameIndex = (_rsiFrameIndex + 1) % _rsiFrames.Length;
            }
        }

        if (_fade < 0f)
            return;

        _fade = MathF.Min(1f, _fade + args.DeltaSeconds / FadeSeconds);

        if (_fade < 1f)
            return;

        _current = _next;
        _currentBlurred = _nextBlurred;
        _currentPath = _nextPath;
        _next = null;
        _nextBlurred = null;
        _nextPath = null;
        _fade = -1f;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var size = new Vector2(PixelSize.X, PixelSize.Y);
        if (size.X <= 0f || size.Y <= 0f)
            return;

        var zoom = ViewZoom;
        var pan = ViewPan;

        if (_gifTexCurrent != null)
        {
            DrawCover(handle, _gifTexCurrent, zoom, pan, Color.White);

            if (_gifTexNext != null && _gifDelays != null && _gifDelays.Length > 1)
            {
                var delay = _gifFrameIndex < _gifDelays.Length && _gifDelays[_gifFrameIndex] > 0.005f
                    ? _gifDelays[_gifFrameIndex]
                    : 0.04f;
                var blend = Math.Clamp(_gifFrameTimer / delay, 0f, 1f);
                if (blend > 0.004f)
                    DrawCover(handle, _gifTexNext, zoom, pan, Color.White.WithAlpha(blend));
            }

            return;
        }

        if (_rsiFrames != null && _rsiFrames.Length > 0)
        {
            var current = _rsiFrames[_rsiFrameIndex];
            DrawCover(handle, current, zoom, pan, Color.White);

            if (_rsiFrames.Length > 1 && _rsiDelays != null && _rsiDelays.Length > 0)
            {
                var delay = _rsiFrameIndex < _rsiDelays.Length && _rsiDelays[_rsiFrameIndex] > 0.005f
                    ? _rsiDelays[_rsiFrameIndex]
                    : 0.05f;
                var blend = Math.Clamp(_rsiFrameTimer / delay, 0f, 1f);
                if (blend > 0.004f)
                {
                    var nextIndex = (_rsiFrameIndex + 1) % _rsiFrames.Length;
                    DrawCover(handle, _rsiFrames[nextIndex], zoom, pan, Color.White.WithAlpha(blend));
                }
            }

            return;
        }

        if (_current != null)
        {
            var alpha = _next != null && _fade >= 0f
                ? 1f - Easings.InOutQuad(_fade)
                : 1f;

            DrawCover(handle, _current, zoom, pan, Color.White.WithAlpha(alpha));
        }

        if (_next == null || _fade < 0f)
            return;

        DrawCover(handle, _next, zoom, pan, Color.White.WithAlpha(Easings.InOutQuad(_fade)));
    }

    private void DrawCover(DrawingHandleScreen handle, Texture texture, float zoom, Vector2 pan, Color color)
    {
        var size = new Vector2(PixelSize.X, PixelSize.Y);
        var textureSize = texture.Size;
        if (textureSize.X <= 0f || textureSize.Y <= 0f)
            return;

        var scale = MathF.Max(size.X / textureSize.X, size.Y / textureSize.Y) * zoom;
        var drawSize = textureSize * scale;
        var overflow = drawSize - size;
        var offset = -overflow * 0.5f + overflow * 0.5f * pan;

        handle.DrawTextureRect(texture, new UIBox2(offset, offset + drawSize), color);
    }

    /// <summary>
    /// Loads the pre-blurred companion of a lobby still. Missing companions are
    /// tolerated so an old install still renders the glass as a plain tint.
    /// </summary>
    private Texture? GetBlurredTexture(string path)
    {
        if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            return null;

        var blurredPath = path[..^4] + "_blur.png";
        return _cache.TryGetResource<TextureResource>(blurredPath, out var resource)
            ? resource.Texture
            : null;
    }
}
