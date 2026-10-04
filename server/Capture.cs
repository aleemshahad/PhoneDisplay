using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace PhoneDisplay.Server;

internal sealed class Capture : IDisposable
{
    private readonly int _x;
    private readonly int _y;
    private readonly int _width;
    private readonly int _height;
    private readonly ImageCodecInfo _codec;
    private readonly EncoderParameters _jpegParameters;

    private IntPtr _screenDc;
    private IntPtr _memoryDc;
    private IntPtr _bitmap;
    private IntPtr _bits;
    private IntPtr _previousBitmap;

    private bool _disposed;

    public int Width => _width;
    public int Height => _height;

    public Capture(DisplayInfo display, long quality)
    {
        _x = display.X;
        _y = display.Y;
        _width = display.Width;
        _height = display.Height;

        if (_width <= 0 || _height <= 0)
        {
            throw new InvalidOperationException($"Display {display.DeviceName} has no usable resolution.");
        }

        _screenDc = Interop.GetDC(IntPtr.Zero);
        if (_screenDc == IntPtr.Zero)
        {
            throw new InvalidOperationException("GetDC(NULL) failed.");
        }

        _memoryDc = Interop.CreateCompatibleDC(_screenDc);
        if (_memoryDc == IntPtr.Zero)
        {
            ReleaseScreenDc();
            throw new InvalidOperationException("CreateCompatibleDC failed.");
        }

        var info = new Interop.BITMAPINFO();
        info.bmiHeader.biSize = (uint)Marshal.SizeOf<Interop.BITMAPINFOHEADER>();
        info.bmiHeader.biWidth = _width;
        info.bmiHeader.biHeight = -_height;
        info.bmiHeader.biPlanes = 1;
        info.bmiHeader.biBitCount = 32;
        info.bmiHeader.biCompression = Interop.BI_RGB;

        _bitmap = Interop.CreateDIBSection(_memoryDc, ref info, Interop.DIB_RGB_COLORS, out _bits, IntPtr.Zero, 0);
        if (_bitmap == IntPtr.Zero || _bits == IntPtr.Zero)
        {
            ReleaseMemoryDc();
            ReleaseScreenDc();
            throw new InvalidOperationException("CreateDIBSection failed.");
        }

        _previousBitmap = Interop.SelectObject(_memoryDc, _bitmap);

        var codec = Array.Find(ImageCodecInfo.GetImageEncoders(), c => c.FormatID == ImageFormat.Jpeg.Guid);
        if (codec is null)
        {
            throw new InvalidOperationException("JPEG encoder is not available on this system.");
        }

        _codec = codec;
        _jpegParameters = new EncoderParameters(1);
        _jpegParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
    }

    public byte[]? Grab()
    {
        if (_disposed)
        {
            return null;
        }

        if (!Interop.BitBlt(_memoryDc, 0, 0, _width, _height, _screenDc, _x, _y, Interop.SRCCOPY))
        {
            return null;
        }

        using var bitmap = new Bitmap(_width, _height, _width * 4, PixelFormat.Format32bppArgb, _bits);
        using var buffer = new MemoryStream(96 * 1024);
        bitmap.Save(buffer, _codec, _jpegParameters);
        return buffer.ToArray();
    }

    private void ReleaseScreenDc()
    {
        if (_screenDc != IntPtr.Zero)
        {
            Interop.ReleaseDC(IntPtr.Zero, _screenDc);
            _screenDc = IntPtr.Zero;
        }
    }

    private void ReleaseMemoryDc()
    {
        if (_memoryDc != IntPtr.Zero)
        {
            Interop.DeleteDC(_memoryDc);
            _memoryDc = IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_memoryDc != IntPtr.Zero && _previousBitmap != IntPtr.Zero)
        {
            Interop.SelectObject(_memoryDc, _previousBitmap);
            _previousBitmap = IntPtr.Zero;
        }

        if (_bitmap != IntPtr.Zero)
        {
            Interop.DeleteObject(_bitmap);
            _bitmap = IntPtr.Zero;
        }

        ReleaseMemoryDc();
        ReleaseScreenDc();
        _jpegParameters.Dispose();
    }
}