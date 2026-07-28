using System;
using System.Runtime.InteropServices;
using SharpGen.Runtime;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using static Vortice.Direct3D11.D3D11;
using static Vortice.DXGI.DXGI;

namespace WinGamma
{
    internal sealed class DesktopCaptureAccessLostException : Exception
    {
        public DesktopCaptureAccessLostException()
            : base("Desktop capture access was lost.")
        {
        }
    }

    internal sealed class DesktopCapture : IDisposable
    {
        private static readonly FeatureLevel[] FeatureLevels = {
            FeatureLevel.Level_11_1,
            FeatureLevel.Level_11_0,
            FeatureLevel.Level_10_1,
            FeatureLevel.Level_10_0
        };

        private IDXGIFactory2 _factory;
        private IDXGIAdapter1 _adapter;
        private IDXGIOutput _output;
        private IDXGIOutput1 _output1;
        private IDXGIOutputDuplication _duplication;
        private bool _disposed;

        public ID3D11Device Device { get; private set; }
        public ID3D11DeviceContext Context { get; private set; }
        public OutputDescription OutputDescription { get; private set; }

        public DesktopCapture(DisplayMonitor monitor)
        {
            if (monitor == null)
                throw new ArgumentNullException("monitor");
            if (monitor.IsHdr)
                throw new InvalidOperationException(
                    "HSL Overlay is unavailable while HDR is active.");

            try
            {
                _factory = CreateDXGIFactory1<IDXGIFactory2>();
                FindOutput(monitor.DeviceName);

                FeatureLevel featureLevel;
                Result result = D3D11CreateDevice(_adapter, DriverType.Unknown,
                    DeviceCreationFlags.BgraSupport, FeatureLevels,
                    out ID3D11Device device, out featureLevel,
                    out ID3D11DeviceContext context);
                result.CheckError();
                Device = device;
                Context = context;
                _duplication = _output1.DuplicateOutput(Device);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public bool CopyNextFrame(ID3D11Texture2D destination,
            uint timeoutMilliseconds)
        {
            if (_disposed)
                throw new ObjectDisposedException("DesktopCapture");
            OutduplFrameInfo frameInfo;
            IDXGIResource resource;
            Result result = _duplication.AcquireNextFrame(
                timeoutMilliseconds, out frameInfo, out resource);
            if (result == Vortice.DXGI.ResultCode.WaitTimeout)
                return false;
            if (result == Vortice.DXGI.ResultCode.AccessLost)
                throw new DesktopCaptureAccessLostException();
            result.CheckError();

            try
            {
                using (ID3D11Texture2D texture =
                    resource.QueryInterface<ID3D11Texture2D>())
                {
                    Context.CopyResource(destination, texture);
                }
            }
            finally
            {
                resource.Dispose();
                Result release = _duplication.ReleaseFrame();
                if (release == Vortice.DXGI.ResultCode.AccessLost)
                    throw new DesktopCaptureAccessLostException();
                release.CheckError();
            }
            return true;
        }

        public Texture2DDescription GetDesktopTextureDescription()
        {
            OutduplDescription description = _duplication.Description;
            Texture2DDescription texture = new Texture2DDescription();
            texture.Width = description.ModeDescription.Width;
            texture.Height = description.ModeDescription.Height;
            texture.MipLevels = 1;
            texture.ArraySize = 1;
            texture.Format = description.ModeDescription.Format;
            texture.SampleDescription = SampleDescription.Default;
            texture.Usage = ResourceUsage.Default;
            texture.BindFlags = BindFlags.ShaderResource;
            texture.CPUAccessFlags = CpuAccessFlags.None;
            texture.MiscFlags = ResourceOptionFlags.None;
            return texture;
        }

        public System.Drawing.Color? CaptureAverageColor(
            uint timeoutMilliseconds)
        {
            Texture2DDescription description = GetDesktopTextureDescription();
            if (description.Format != Format.B8G8R8A8_UNorm)
                throw new NotSupportedException(
                    "The DDA diagnostic requires an SDR BGRA8 desktop.");
            description.Usage = ResourceUsage.Staging;
            description.BindFlags = BindFlags.None;
            description.CPUAccessFlags = CpuAccessFlags.Read;
            using (ID3D11Texture2D staging =
                Device.CreateTexture2D(description))
            {
                if (!CopyNextFrame(staging, timeoutMilliseconds))
                    return null;
                MappedSubresource mapped = Context.Map(staging, 0,
                    MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                try
                {
                    long red = 0;
                    long green = 0;
                    long blue = 0;
                    long count = 0;
                    int stepX = Math.Max(1, (int)description.Width / 64);
                    int stepY = Math.Max(1, (int)description.Height / 64);
                    for (int y = stepY; y < description.Height - stepY;
                        y += stepY)
                    {
                        for (int x = stepX; x < description.Width - stepX;
                            x += stepX)
                        {
                            int offset = checked((int)(y * mapped.RowPitch
                                + x * 4));
                            blue += Marshal.ReadByte(mapped.DataPointer,
                                offset);
                            green += Marshal.ReadByte(mapped.DataPointer,
                                offset + 1);
                            red += Marshal.ReadByte(mapped.DataPointer,
                                offset + 2);
                            count++;
                        }
                    }
                    return System.Drawing.Color.FromArgb(
                        (int)(red / count), (int)(green / count),
                        (int)(blue / count));
                }
                finally
                {
                    Context.Unmap(staging, 0);
                }
            }
        }

        private void FindOutput(string deviceName)
        {
            for (uint adapterIndex = 0; ; adapterIndex++)
            {
                IDXGIAdapter1 adapter;
                if (_factory.EnumAdapters1(adapterIndex, out adapter).Failure)
                    break;
                bool keepAdapter = false;
                try
                {
                    for (uint outputIndex = 0; ; outputIndex++)
                    {
                        IDXGIOutput output;
                        if (adapter.EnumOutputs(outputIndex, out output).Failure)
                            break;
                        OutputDescription description = output.Description;
                        if (String.Equals(description.DeviceName, deviceName,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            _adapter = adapter;
                            keepAdapter = true;
                            _output = output;
                            _output1 = output.QueryInterface<IDXGIOutput1>();
                            OutputDescription = description;
                            return;
                        }
                        output.Dispose();
                    }
                }
                finally
                {
                    if (!keepAdapter)
                        adapter.Dispose();
                }
            }
            throw new InvalidOperationException(
                "DXGI output was not found for " + deviceName + ".");
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_duplication != null)
                _duplication.Dispose();
            if (_output1 != null)
                _output1.Dispose();
            if (_output != null)
                _output.Dispose();
            if (Context != null)
                Context.Dispose();
            if (Device != null)
                Device.Dispose();
            if (_adapter != null)
                _adapter.Dispose();
            if (_factory != null)
                _factory.Dispose();
        }
    }
}
