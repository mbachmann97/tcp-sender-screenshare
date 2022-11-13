using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Net.Sockets;
using System.IO;
using System.Diagnostics;
using System.Net;

namespace ScreenShareMB
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        struct CURSORINFO
        {
            public Int32 cbSize;
            public Int32 flags;
            public IntPtr hCursor;
            public POINTAPI ptScreenPos;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct POINTAPI
        {
            public int x;
            public int y;
        }

        [DllImport("user32.dll")]
        static extern bool GetCursorInfo(out CURSORINFO pci);

        [DllImport("user32.dll")]
        static extern bool DrawIcon(IntPtr hDC, int X, int Y, IntPtr hIcon);

        const Int32 CURSOR_SHOWING = 0x00000001;

        private System.Threading.Timer timer;
        public MainWindow()
        {
            // var uri = new Uri("C:\\Projekte\\imgcombiner-rust\\images\\img1.bmp", UriKind.Absolute);
            // var bmp = new BitmapImage(uri);
            // ImageContent.Source = bmp;

            timer = new System.Threading.Timer(timer_Tick, new object(), 0, 50);

            InitializeComponent();

            BitmapSource source = CopyScreen();

            JpegBitmapEncoder encoder = new JpegBitmapEncoder();
            encoder.QualityLevel = 100;
            byte[] imageData;
            using (MemoryStream mstream = new MemoryStream())
            {
                encoder.Frames.Add(BitmapFrame.Create(source));
                encoder.Save(mstream);
                imageData = mstream.ToArray();
                mstream.Close();
            }

            var size = imageData.Length;
            var chunkCount = (int)Math.Ceiling(size / 1000F);
            var bufferArray = new byte[chunkCount][];
            for (var i = 0; i < chunkCount; i++)
            {
                bufferArray[i] = new byte[1000];
                for (var j = 0; j < 1000 && i * chunkCount + j < size; j++)
                {
                    bufferArray[i][j] = imageData[i * chunkCount + j];
                }
            }
        }

        


        private void timer_Tick(object sender)
        {
            Dispatcher.Invoke(new Action(() =>  // Call a special portion of your code from the WPF thread (called dispatcher)
            {
                BitmapSource source = CopyScreen();

                JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                encoder.QualityLevel = 100;
                byte[] imageData;
                using (MemoryStream ms = new MemoryStream())
                {
                    encoder.Frames.Add(BitmapFrame.Create(source));
                    encoder.Save(ms);
                    imageData = ms.ToArray();
                    ms.Close();
                }

                IPEndPoint RemoteIpEndPoint = new IPEndPoint(new IPAddress(new byte[] { 127, 0, 0, 1 }), 9724);
                TcpClient tcpClient = new TcpClient();
                tcpClient.Connect(RemoteIpEndPoint);
                NetworkStream stream = tcpClient.GetStream();
                stream.Write(imageData, 0, imageData.Length);
                stream.Close();
            }));
        }

        private static BitmapSource CopyScreen()
        {
            using (var screenBmp = new Bitmap(
               (int)SystemParameters.PrimaryScreenWidth,
                (int)SystemParameters.PrimaryScreenHeight,
               PixelFormat.Format32bppArgb))
            {
                using (var bmpGraphics = Graphics.FromImage(screenBmp))
                {
                    bmpGraphics.CopyFromScreen(0, 0, 0, 0, screenBmp.Size);

                    CURSORINFO pci;
                    pci.cbSize = Marshal.SizeOf(typeof(CURSORINFO));

                    if (GetCursorInfo(out pci))
                    {
                        if (pci.flags == CURSOR_SHOWING)
                        {
                            DrawIcon(bmpGraphics.GetHdc(), pci.ptScreenPos.x, pci.ptScreenPos.y, pci.hCursor);
                            bmpGraphics.ReleaseHdc();
                        }
                    }

                    IntPtr hBitmap = screenBmp.GetHbitmap();

                    BitmapSource source;

                    try
                    {
                        source = Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    }
                    finally
                    {
                        DeleteObject(hBitmap);
                    }

                    return source;
                }
            }
        }
    }
}
