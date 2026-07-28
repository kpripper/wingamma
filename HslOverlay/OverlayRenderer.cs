using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace WinGamma
{
    internal sealed class OverlayRenderer : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct GpuConstants
        {
            public Vector4 Band0;
            public Vector4 Band1;
            public Vector4 Band2;
            public Vector4 Band3;
            public Vector4 Band4;
            public Vector4 Band5;
            public Vector4 Band6;
            public Vector4 Band7;
            public Vector4 Lum0;
            public Vector4 Lum1;
            public Vector4 Lum2;
            public Vector4 Lum3;
            public Vector4 Lum4;
            public Vector4 Lum5;
            public Vector4 Lum6;
            public Vector4 Lum7;
            public Vector4 RenderParams;
        }

        private DesktopCapture _capture;
        private IDXGISwapChain1 _swapChain;
        private ID3D11RenderTargetView _renderTarget;
        private ID3D11Texture2D _frameTexture;
        private ID3D11ShaderResourceView _frameView;
        private ID3D11VertexShader _vertexShader;
        private ID3D11PixelShader _pixelShader;
        private ID3D11SamplerState _sampler;
        private ID3D11Buffer _constantBuffer;
        private readonly int _width;
        private readonly int _height;
        private bool _hasFrame;
        private bool _disposed;

        public OverlayRenderer(IntPtr window, DisplayMonitor monitor)
        {
            _width = monitor.Bounds.Width;
            _height = monitor.Bounds.Height;
            try
            {
                _capture = new DesktopCapture(monitor);

                using (IDXGIDevice dxgiDevice =
                    _capture.Device.QueryInterface<IDXGIDevice>())
                using (IDXGIAdapter adapter = dxgiDevice.GetAdapter())
                using (IDXGIFactory2 factory =
                    adapter.GetParent<IDXGIFactory2>())
                {
                    SwapChainDescription1 description =
                        new SwapChainDescription1();
                    description.Width = (uint)_width;
                    description.Height = (uint)_height;
                    description.Format = Format.B8G8R8A8_UNorm;
                    description.SampleDescription = SampleDescription.Default;
                    description.BufferUsage = Usage.RenderTargetOutput;
                    description.BufferCount = 2;
                    description.Scaling = Scaling.Stretch;
                    description.SwapEffect = SwapEffect.FlipDiscard;
                    description.AlphaMode = AlphaMode.Ignore;
                    SwapChainFullscreenDescription fullscreen =
                        new SwapChainFullscreenDescription();
                    fullscreen.Windowed = true;
                    _swapChain = factory.CreateSwapChainForHwnd(
                        _capture.Device, window, description, fullscreen);
                    factory.MakeWindowAssociation(window,
                        WindowAssociationFlags.IgnoreAltEnter);
                }

                using (ID3D11Texture2D backBuffer =
                    _swapChain.GetBuffer<ID3D11Texture2D>(0))
                    _renderTarget =
                        _capture.Device.CreateRenderTargetView(backBuffer);

                Texture2DDescription frameDescription =
                    _capture.GetDesktopTextureDescription();
                _frameTexture =
                    _capture.Device.CreateTexture2D(frameDescription);
                _frameView =
                    _capture.Device.CreateShaderResourceView(_frameTexture);

                string shaderPath = Path.Combine(AppContext.BaseDirectory,
                    "HslOverlay", "Shaders", "HslAdjust.hlsl");
                if (!File.Exists(shaderPath))
                    throw new FileNotFoundException(
                        "HSL shader source is missing.", shaderPath);
                ReadOnlyMemory<byte> vertexBytecode =
                    ShaderCache.LoadOrCompile(
                        shaderPath, "VSMain", "vs_5_0");
                ReadOnlyMemory<byte> pixelBytecode =
                    ShaderCache.LoadOrCompile(
                        shaderPath, "PSMain", "ps_5_0");
                _vertexShader =
                    _capture.Device.CreateVertexShader(vertexBytecode.Span);
                _pixelShader =
                    _capture.Device.CreatePixelShader(pixelBytecode.Span);
                _sampler = _capture.Device.CreateSamplerState(
                    SamplerDescription.LinearClamp);
                _constantBuffer = _capture.Device.CreateBuffer(
                    (uint)Marshal.SizeOf(typeof(GpuConstants)),
                    BindFlags.ConstantBuffer, ResourceUsage.Default,
                    CpuAccessFlags.None);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Render(HslBandSettings settings)
        {
            if (_disposed)
                return;
            if (_capture.CopyNextFrame(_frameTexture, 16))
                _hasFrame = true;
            if (!_hasFrame)
                return;

            GpuConstants constants = CreateConstants(settings,
                _capture.OutputDescription.Rotation);
            _capture.Context.UpdateSubresource(in constants, _constantBuffer);
            _capture.Context.OMSetRenderTargets(_renderTarget);
            _capture.Context.RSSetViewport(0, 0, _width, _height);
            _capture.Context.IASetPrimitiveTopology(
                PrimitiveTopology.TriangleList);
            _capture.Context.VSSetShader(_vertexShader);
            _capture.Context.PSSetShader(_pixelShader);
            _capture.Context.PSSetShaderResource(0, _frameView);
            _capture.Context.PSSetSampler(0, _sampler);
            _capture.Context.PSSetConstantBuffer(0, _constantBuffer);
            _capture.Context.Draw(3, 0);
            _capture.Context.PSSetShaderResource(0, null);
            _swapChain.Present(1, PresentFlags.None);
        }

        private static GpuConstants CreateConstants(HslBandSettings settings,
            ModeRotation rotation)
        {
            settings = (settings ?? HslBandSettings.CreateDefault()).Clone();
            settings.EnsureValid();
            Vector4[] bands = new Vector4[8];
            Vector4[] lum = new Vector4[8];
            for (int i = 0; i < 8; i++)
            {
                HslBand band = settings.Bands[i];
                bands[i] = new Vector4(band.CenterHueDeg, band.WidthDeg,
                    band.HueShiftDeg, band.SaturationScale);
                lum[i] = new Vector4(band.LuminanceShift, 0, 0, 0);
            }
            GpuConstants value = new GpuConstants();
            value.Band0 = bands[0]; value.Band1 = bands[1];
            value.Band2 = bands[2]; value.Band3 = bands[3];
            value.Band4 = bands[4]; value.Band5 = bands[5];
            value.Band6 = bands[6]; value.Band7 = bands[7];
            value.Lum0 = lum[0]; value.Lum1 = lum[1];
            value.Lum2 = lum[2]; value.Lum3 = lum[3];
            value.Lum4 = lum[4]; value.Lum5 = lum[5];
            value.Lum6 = lum[6]; value.Lum7 = lum[7];
            float turns = rotation == ModeRotation.Rotate90 ? 1
                : rotation == ModeRotation.Rotate180 ? 2
                : rotation == ModeRotation.Rotate270 ? 3 : 0;
            value.RenderParams = new Vector4(turns, 0, 0, 0);
            return value;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_capture != null && _capture.Context != null)
                _capture.Context.ClearState();
            if (_constantBuffer != null) _constantBuffer.Dispose();
            if (_sampler != null) _sampler.Dispose();
            if (_pixelShader != null) _pixelShader.Dispose();
            if (_vertexShader != null) _vertexShader.Dispose();
            if (_frameView != null) _frameView.Dispose();
            if (_frameTexture != null) _frameTexture.Dispose();
            if (_renderTarget != null) _renderTarget.Dispose();
            if (_swapChain != null) _swapChain.Dispose();
            if (_capture != null) _capture.Dispose();
        }
    }
}
