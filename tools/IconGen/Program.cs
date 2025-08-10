using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

class IconGen
{
    static void Main(string[] args)
    {
        var outDir = args.Length > 0 ? args[0] : Path.Combine("..", "..", "installer");
        Directory.CreateDirectory(outDir);
        var pngPath = Path.Combine(outDir, "MyCalculator-256.png");
        var icoPath = Path.Combine(outDir, "MyCalculator.ico");

        using (var bmp = new Bitmap(256, 256))
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // Teal background with rounded rect
            var teal = Color.FromArgb(0, 153, 153);
            using (var brush = new SolidBrush(teal))
            using (var path = RoundedRect(new Rectangle(16, 16, 224, 224), 32))
            {
                g.FillPath(brush, path);
            }

            // Calculator screen
            using (var brush = new SolidBrush(Color.White))
            using (var path = RoundedRect(new Rectangle(36, 36, 184, 70), 16))
            {
                g.FillPath(brush, path);
            }

            // Buttons grid 4x4
            var btnColor = Color.FromArgb(240, 248, 248);
            using var btnBrush = new SolidBrush(btnColor);
            int x0 = 36, y0 = 120, w = 40, h = 32, gap = 8;
            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    int x = x0 + c * (w + gap);
                    int y = y0 + r * (h + gap);
                    using var path = RoundedRect(new Rectangle(x, y, w, h), 6);
                    g.FillPath(btnBrush, path);
                }
            }
        
            bmp.Save(pngPath, ImageFormat.Png);
        }

        // Build ICO from multiple sizes
        using var ico = BuildIcon(new[] { 16, 32, 48, 64, 128, 256 }, size => DrawIconBitmap(size));
        using var fs = new FileStream(icoPath, FileMode.Create, FileAccess.Write);
        ico.CopyTo(fs);

        Console.WriteLine($"Generated: {pngPath}\nGenerated: {icoPath}");
    }

    static MemoryStream BuildIcon(int[] sizes, Func<int, Bitmap> make)
    {
        var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, true);
        bw.Write((short)0); // reserved
        bw.Write((short)1); // type ico
        bw.Write((short)sizes.Length); // count
        long dirPos = ms.Position;
        // write placeholders
        for (int i = 0; i < sizes.Length; i++)
        {
            bw.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
            bw.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
            bw.Write((byte)0); // colors
            bw.Write((byte)0); // reserved
            bw.Write((short)1); // planes
            bw.Write((short)32); // bpp
            bw.Write(0); // size
            bw.Write(0); // offset
        }
        var images = new (int size, byte[] data) [sizes.Length];
        for (int i = 0; i < sizes.Length; i++)
        {
            using var bmp = make(sizes[i]);
            using var png = new MemoryStream();
            bmp.Save(png, ImageFormat.Png);
            images[i] = (sizes[i], png.ToArray());
        }
        long dataOffset = ms.Position;
        long[] offsets = new long[sizes.Length];
        for (int i = 0; i < sizes.Length; i++)
        {
            offsets[i] = ms.Position;
            bw.Write(images[i].data);
        }
        long end = ms.Position;
        ms.Position = dirPos;
        using (var bw2 = new BinaryWriter(ms, System.Text.Encoding.UTF8, true))
        {
            for (int i = 0; i < sizes.Length; i++)
            {
                bw2.Write((byte)(images[i].size == 256 ? 0 : images[i].size));
                bw2.Write((byte)(images[i].size == 256 ? 0 : images[i].size));
                bw2.Write((byte)0);
                bw2.Write((byte)0);
                bw2.Write((short)1);
                bw2.Write((short)32);
                bw2.Write(images[i].data.Length);
                bw2.Write((int)(dataOffset + (offsets[i] - dataOffset)));
            }
        }
        ms.Position = 0;
        return ms;
    }

    static Bitmap DrawIconBitmap(int size)
    {
        var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        var pad = Math.Max(2, size / 16);
        var corner = Math.Max(4, size / 8);
        var screenH = Math.Max(10, size / 3);
        using (var tealBrush = new SolidBrush(Color.FromArgb(0, 153, 153)))
        using (var bg = RoundedRect(new Rectangle(pad, pad, size - 2 * pad, size - 2 * pad), corner))
        { g.FillPath(tealBrush, bg); }
        using (var white = new SolidBrush(Color.White))
        using (var scr = RoundedRect(new Rectangle(pad + corner/2, pad + corner/2, size - 2*pad - corner, screenH), corner/2))
        { g.FillPath(white, scr); }
        int rows = 4, cols = 4;
        int gap = Math.Max(1, size / 32);
        int btnW = (size - 2*pad - (cols-1)*gap) / cols;
        int btnH = (size - 2*pad - screenH - gap*3) / rows;
        int y0 = pad + screenH + gap*2;
        using var btnBrush = new SolidBrush(Color.FromArgb(240, 248, 248));
        for (int r = 0; r < rows; r++)
          for (int c = 0; c < cols; c++)
          {
            int x = pad + c * (btnW + gap);
            int y = y0 + r * (btnH + gap);
            using var p = RoundedRect(new Rectangle(x, y, btnW, btnH), Math.Max(2, size/32));
            g.FillPath(btnBrush, p);
          }
        return bmp;
    }

    static GraphicsPath RoundedRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        var arc = new Rectangle(rect.Location, new Size(d, d));
        // TL
        path.AddArc(arc, 180, 90);
        // TR
        arc.X = rect.Right - d;
        path.AddArc(arc, 270, 90);
        // BR
        arc.Y = rect.Bottom - d;
        path.AddArc(arc, 0, 90);
        // BL
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}
