using static System.Convert;

namespace graphicstransform.service
{
    public class GraphicsServer
    {
        public async Task<string> Resize(float wfactor, float hfactor, string data)
        {
            if (hfactor < 0f)
                throw new ArgumentException("hfactor may not be < 0");

            if (wfactor < 0f)
                throw new ArgumentException("wfactor may not be < 0");

            var image = getImage(data);

            if (image.Metadata.DecodedImageFormat == null)
                throw new ArgumentException("image format could net get decoded");

            image.Mutate(x => x.Resize((int)(image.Width * wfactor), (int)(image.Height * hfactor)));

            using MemoryStream ms = new MemoryStream();

            await image.SaveAsync(ms, image.Metadata.DecodedImageFormat);

            return ToBase64String(ms.ToArray());
        }

        public async Task<string> RotateFlip(int rotate, int fliptype, string data)
        {
            if (rotate != 0 && rotate != 90 && rotate != 180 && rotate != 270)
                throw new ArgumentException("invalid rotation type");

            if (fliptype < 0 || fliptype > 2)
                throw new ArgumentException("invalid flip type (0, 1, 2) are valid");

            var image = getImage(data);

            if (image.Metadata.DecodedImageFormat == null)
                throw new ArgumentException("image format could net get decoded");

            image.Mutate(x => x.RotateFlip((RotateMode)rotate, (FlipMode)fliptype));

            using MemoryStream ms = new MemoryStream();

            await image.SaveAsync(ms, image.Metadata.DecodedImageFormat);

            return ToBase64String(ms.ToArray());
        }


        private Image<Rgb24> getImage(string data)
        {
            var span = data.AsSpan();
            var buffer = spanb64(data.AsSpan());

            if (TryFromBase64Chars(span, buffer, out _))
            {
                var image = Image.Load<Rgb24>(buffer);

                if (image.Metadata.DecodedImageFormat == null)
                    throw new ArgumentException("image format could net get decoded");

                return image;
            }
            throw new ArgumentException("image data must be base64 encoded");
        }

        public async Task<string> DrawImageOnImage(string dstData, string srcData, Rectangle pos)
        {
            if (string.IsNullOrEmpty(dstData) || string.IsNullOrEmpty(srcData))
                throw new ArgumentException("empty image data");

            Image<Rgb24> dstImage = getImage(dstData),
                         srcImage = getImage(srcData);

            if (srcImage.Metadata.DecodedImageFormat == null || dstImage.Metadata.DecodedImageFormat == null)
                throw new ArgumentException("image format could net get decoded");

            srcImage.Mutate(ctx => ctx.Resize((int)(srcImage.Width * ((float)pos.Width / srcImage.Width)),
                                              (int)(srcImage.Height * ((float)pos.Height / srcImage.Height))));

            dstImage.Mutate(ctx => ctx.DrawImage(srcImage, new Point { X = pos.X, Y = pos.Y }, 1f));

            using MemoryStream ms = new MemoryStream();

            await dstImage.SaveAsync(ms, dstImage.Metadata.DecodedImageFormat);

            return ToBase64String(ms.ToArray());
        }

        public Rectangle ColorkeyRect(byte r, byte g, byte b, string data)
        {
            if (string.IsNullOrEmpty(data))
                throw new ArgumentException("empty image data");

            ReadOnlySpan<char> span = data.AsSpan();

            var buff = spanb64(data);

            int height = 0, width = 0, startX = -1, startY = -1;

            if (!TryFromBase64Chars(span, buff, out _))
                throw new ArgumentException("image data must be base64 encoded");

            var image = Image.Load<Rgba32>(buff);

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    int count = 0;
                    Span<Rgba32> row = accessor.GetRowSpan(y);
                    // row.Length has the same value as accessor.Width,
                    // but using row.Length allows the JIT to optimize away bounds checks:
                    for (int x = startX == -1 ? 0 : startX; x < row.Length; x++)
                    {
                        // Get a reference to the pixel at position x
                        ref Rgba32 pixel = ref row[x];
                        while (x < row.Length - 1 && pixel.R == r && pixel.G == g && pixel.B == b)
                        {
                            if (height == 0 && startY == -1) startY = y;
                            if (width == 0 && startX == -1) startX = x;

                            count++;
                            x++;
                            pixel = ref row[x];
                        }

                        if (count > 0)
                        {
                            width = count;
                            height++;
                            x = row.Length;
                        }
                        else if (width > 1) { return; }
                    }
                }
            });

            return new Rectangle(width == 0 ? 0 : startX, width == 0 ? 0 : startY, width, height);
        }

        public List<Rectangle> ColorkeyRectAlpha(string data)
        {
            if (string.IsNullOrEmpty(data))
                throw new ArgumentException("empty image data");

            ReadOnlySpan<char> span = data.AsSpan();

            var buff = spanb64(data);

            if (!TryFromBase64Chars(span, buff, out var written))
                throw new ArgumentException("image data must be base64 encoded");

            var image = Image.Load<Rgba32>(buff);

            List<Rectangle> rects = new();

            image.ProcessPixelRows(accessor =>
            {
                int x = 0, y = 0;

                while (y < accessor.Height)
                {
                    x = 0;
                    Span<Rgba32> row = accessor.GetRowSpan(y);

                    // row.Length has the same value as accessor.Width, but using row.Length allows the JIT to optimize away bounds checks:
                    while (x < row.Length)
                    {
                        // Get a reference on stack to rgba at position x
                        ref Rgba32 pixel = ref row[x];
                        if (pixel.A < 255 && !pointInRect(rects, x, y))
                        {
                            Rectangle newrect = new(x, y, 0, 0);

                            newrect.Width = searchRight(row, x) - x;
                            newrect.Height = searchBottom(image, x, y) - y;

                            rects.Add(newrect);

                            x += newrect.Width;
                        }
                        else x++;
                    }
                    y++;
                }
            });

            return rects;
        }

        private bool pointInRect(List<Rectangle> rects, int x, int y)
        {
            foreach (var r in rects)
            {
                if (x >= r.X && x <= r.X + r.Width && y >= r.Y && y <= r.Y + r.Height)
                    return true;
            }
            return false;
        }

        private int searchRight(ReadOnlySpan<Rgba32> row, int X)
        {
            while (++X < row.Length && row[X].A < 255) /* deliberately empty */ ;

            return X;
        }

        private int searchBottom(Image<Rgba32> image, int X, int Y)
        {
            while (Y < image.Height && image[X, ++Y].A < 255)  /* deliberately empty */ ;

            return Y;
        }

        private Span<byte> spanb64(ReadOnlySpan<char> data)
        {
            return new byte[((data.Length * 3) + 3) / 4 - (data[data.Length - 1] == '=' ? data[data.Length - 2] == '=' ? 2 : 1 : 0)];
        }
    }
}