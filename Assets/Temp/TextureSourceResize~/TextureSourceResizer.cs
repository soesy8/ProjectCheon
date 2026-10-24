using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

public static class TextureSourceResizer
{
    public static void Resize(string sourcePath, string destinationPath, int maximumSize, bool hasAlpha, bool isNormalMap, bool independentAlpha)
    {
        using (var source = new Bitmap(sourcePath, false))
        {
            double scale = Math.Min(1.0, (double)maximumSize / Math.Max(source.Width, source.Height));
            int width = Math.Max(1, (int)Math.Round(source.Width * scale, MidpointRounding.AwayFromZero));
            int height = Math.Max(1, (int)Math.Round(source.Height * scale, MidpointRounding.AwayFromZero));
            PixelFormat format = hasAlpha ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb;
            int channels = hasAlpha ? 4 : 3;
            byte[] input = ReadPixels(source, format, channels);
            byte[] output = new byte[width * height * channels];
            var columns = CreateContributions(source.Width, width);
            var rows = CreateContributions(source.Height, height);
            double area = (double)source.Width / width * source.Height / height;

            // 면적 평균으로 축소한다. 금속성·매끄러움 데이터는 알파를 독립 계산하고,
            // 투명 이미지는 알파 가중치를 적용해 투명 영역의 색이 테두리에 섞이지 않게 한다.
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    double blue = 0, green = 0, red = 0, alpha = 0;
                    var row = rows[y];
                    var column = columns[x];
                    for (int dy = 0; dy < row.Weights.Length; dy++)
                    {
                        int rowOffset = (row.Start + dy) * source.Width * channels;
                        for (int dx = 0; dx < column.Weights.Length; dx++)
                        {
                            double weight = row.Weights[dy] * column.Weights[dx];
                            int index = rowOffset + (column.Start + dx) * channels;
                            double colorWeight = hasAlpha && !independentAlpha ? weight * input[index + 3] / 255.0 : weight;
                            blue += input[index] * colorWeight;
                            green += input[index + 1] * colorWeight;
                            red += input[index + 2] * colorWeight;
                            if (hasAlpha) alpha += input[index + 3] * weight;
                        }
                    }

                    double colorArea = hasAlpha && !independentAlpha ? alpha / 255.0 : area;
                    blue = colorArea > 0 ? blue / colorArea : 0;
                    green = colorArea > 0 ? green / colorArea : 0;
                    red = colorArea > 0 ? red / colorArea : 0;
                    if (isNormalMap)
                    {
                        // 노멀맵은 평균한 방향 벡터를 정규화해 조명 방향을 유지한다.
                        double nx = red / 127.5 - 1.0;
                        double ny = green / 127.5 - 1.0;
                        double nz = blue / 127.5 - 1.0;
                        double length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                        if (length > 0.000001)
                        {
                            red = (nx / length + 1.0) * 127.5;
                            green = (ny / length + 1.0) * 127.5;
                            blue = (nz / length + 1.0) * 127.5;
                        }
                    }

                    int destination = (y * width + x) * channels;
                    output[destination] = ToByte(blue);
                    output[destination + 1] = ToByte(green);
                    output[destination + 2] = ToByte(red);
                    if (hasAlpha) output[destination + 3] = ToByte(alpha / area);
                }
            }

            using (var result = new Bitmap(width, height, format))
            {
                if (source.HorizontalResolution > 0 && source.VerticalResolution > 0)
                    result.SetResolution(source.HorizontalResolution, source.VerticalResolution);
                WritePixels(result, output, format, channels);
                result.Save(destinationPath, ImageFormat.Png);
            }
        }

        PreserveColorMetadata(sourcePath, destinationPath);
    }

    private struct Contribution
    {
        public int Start;
        public double[] Weights;
    }

    private static Contribution[] CreateContributions(int sourceSize, int destinationSize)
    {
        var contributions = new Contribution[destinationSize];
        double scale = (double)sourceSize / destinationSize;
        for (int i = 0; i < destinationSize; i++)
        {
            double left = i * scale;
            double right = (i + 1) * scale;
            int start = (int)Math.Floor(left);
            int end = Math.Min(sourceSize, (int)Math.Ceiling(right));
            var weights = new double[end - start];
            for (int sample = start; sample < end; sample++)
                weights[sample - start] = Math.Min(right, sample + 1.0) - Math.Max(left, sample);
            contributions[i] = new Contribution { Start = start, Weights = weights };
        }
        return contributions;
    }

    private static byte[] ReadPixels(Bitmap bitmap, PixelFormat format, int channels)
    {
        var pixels = new byte[bitmap.Width * bitmap.Height * channels];
        var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, format);
        try
        {
            int rowBytes = bitmap.Width * channels;
            for (int y = 0; y < bitmap.Height; y++)
                Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), pixels, y * rowBytes, rowBytes);
        }
        finally { bitmap.UnlockBits(data); }
        return pixels;
    }

    private static void WritePixels(Bitmap bitmap, byte[] pixels, PixelFormat format, int channels)
    {
        var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, format);
        try
        {
            int rowBytes = bitmap.Width * channels;
            for (int y = 0; y < bitmap.Height; y++)
                Marshal.Copy(pixels, y * rowBytes, IntPtr.Add(data.Scan0, y * data.Stride), rowBytes);
        }
        finally { bitmap.UnlockBits(data); }
    }

    private static byte ToByte(double value)
    {
        return (byte)Math.Max(0, Math.Min(255, Math.Round(value, MidpointRounding.AwayFromZero)));
    }

    private static readonly HashSet<string> ColorChunks = new HashSet<string>
        { "sRGB", "gAMA", "iCCP", "cHRM", "sBIT" };

    private static void PreserveColorMetadata(string sourcePath, string destinationPath)
    {
        var metadata = new List<byte[]>();
        using (var stream = File.OpenRead(sourcePath))
        using (var reader = new BinaryReader(stream))
        {
            stream.Position = 8;
            while (stream.Position + 12 <= stream.Length)
            {
                byte[] lengthBytes = reader.ReadBytes(4);
                int length = ReadLength(lengthBytes, 0);
                string type = Encoding.ASCII.GetString(reader.ReadBytes(4));
                if (ColorChunks.Contains(type))
                {
                    stream.Position -= 8;
                    metadata.Add(reader.ReadBytes(length + 12));
                }
                else stream.Position += length + 4;
                if (type == "IEND") break;
            }
        }

        byte[] encoded = File.ReadAllBytes(destinationPath);
        using (var output = new MemoryStream())
        {
            output.Write(encoded, 0, 8);
            for (int offset = 8; offset + 12 <= encoded.Length;)
            {
                int length = ReadLength(encoded, offset);
                string type = Encoding.ASCII.GetString(encoded, offset + 4, 4);
                if (!ColorChunks.Contains(type)) output.Write(encoded, offset, length + 12);
                if (type == "IHDR")
                    foreach (byte[] chunk in metadata) output.Write(chunk, 0, chunk.Length);
                offset += length + 12;
            }
            File.WriteAllBytes(destinationPath, output.ToArray());
        }
    }

    private static int ReadLength(byte[] bytes, int offset)
    {
        return (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
    }
}
