using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using OpenTK.Mathematics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO;

namespace SharpCanvas
{
    public class Renderer
    {
        public float AspectRatio = 16f / 9f;
        public int Width, Height;
        public Vector2i Size;
        public Renderer(int incrementations, int width, int sample, int segmentSize, float aspectRatio, int bounces, HittableList world) 
        {
            _world = world;

            _samplesPerPixel = sample;
            _maxDepth = bounces;
            _samplesDone = _samplesPerPixel;
            _incrementations = incrementations;
            _segmentationSize = segmentSize;
            AspectRatio = aspectRatio;
            
            Width = width;
            Height = (int)(Width / AspectRatio);

            Size = new Vector2i(Width, Height);
        }
        
        private Stack<(int, int)> _segmentations = new Stack<(int, int)>();
        private Vector3[] _pixels;
        private Vector3i[] _output;
        HittableList _world;
        Camera _camera;

        private float _timeSpent = 0.0f;
        private int _segmentsDone = 0;
        private int _samplesDone;

        int _segmentationSize;
        int _samplesPerPixel;
        int _maxDepth;
        private int _incrementations;

        private Color4 RayColor(Ray ray, ref HittableList world, int depth)
        {
            if (depth <= 0)
            {
                return new Color4(0f, 0f, 0f, 1f);
            }

            HitRecord record = new HitRecord();

            if(world.Hit(ray, 0.001f, float.PositiveInfinity, ref record))
            {
                Ray scattered = new Ray(Vector3.Zero, Vector3.Zero);
                Color4 attenuation = new Color4();

                var scatter = record.Material.Scatter(ray, record, ref attenuation, ref scattered);

                if(scatter)
                {
                    var color = RayColor(scattered, ref world, depth - 1);
                    return new Color4(attenuation.R * color.R, attenuation.G * color.G, attenuation.B * color.B, 1f);
                }

                return Color4.Black;
            }

            var unit_direction = ray.Direction.Normalized();
            var t = 0.5f * (unit_direction.Y  + 1.0f);
            return new Color4((1f - t) + (t * 0.5f), (1f - t) + (t * 0.7f), (1f - t) + (t * 1f), 1f);
        }

        public void Begin(float apreture, float distToFocus)
        {        
            var lookFrom = new Vector3(13f, 2f, 3f);
            var lookAt = new Vector3(0f, 0f, 0f);

            _camera = new Camera(lookFrom, lookAt, new Vector3(0f, 1f, 0f), 20, AspectRatio, apreture, distToFocus, 0, 1f);

            _pixels = new Vector3[Width * Height];
            _output = new Vector3i[Width * Height];

            var tracerThread = new Thread(() => {
                Console.WriteLine($"Creating {(Width * Height)/(_segmentationSize * _segmentationSize)} segmentation for rendering {_samplesPerPixel} samples.");
                for (int j = 0; j < _incrementations; j++)
                {
                    for (int i = 0; i < Width * Height; i += _segmentationSize * _segmentationSize)
                    {
                        _segmentations.Push((i, i + _segmentationSize * _segmentationSize));
                    }
                    Cast();
                    using (Image<Rgba32> image = new Image<Rgba32>(Width, Height))
                    {
                        for (int y = 0; y < image.Height; y++)
                        {
                            Span<Rgba32> row = image.GetPixelRowSpan(y);

                            for (int x = 0; x < row.Length; x++)
                            {
                                ref Rgba32 pixel =  ref row[x];
                                pixel.R = (byte)(255 - _output[ConvertIndex(x, y)].X * 255);
                                pixel.G = (byte)(255 - _output[ConvertIndex(x, y)].Y * 255);
                                pixel.B = (byte)(255 - _output[ConvertIndex(x, y)].Z * 255);
                                pixel.A = 255;
                            }
                        }
                        Directory.CreateDirectory("./samples/");
                        image.SaveAsPngAsync($"./samples/sample_{_samplesDone}.png");
                    }

                    _samplesDone += _samplesPerPixel;
                }
            });
            tracerThread.Start();
        }

        private void PrintColor(int j, int i, Vector3 color)
        {
            var index = ConvertInvertedIndex(j, i);

            var r = color.X + _pixels[index].X;
            var g = color.Y + _pixels[index].Y;
            var b = color.Z + _pixels[index].Z;

            var scale = 1.0f / _samplesDone;
            r = MathF.Sqrt(scale * r);
            g = MathF.Sqrt(scale * g);
            b = MathF.Sqrt(scale * b);

            _pixels[index] += color;
            _output[index] = new Vector3i((int)(r*255), (int)(g*255), (int)(b*255));
        }

        private int ConvertInvertedIndex(int x, int y)
        {
            var index = x + Width * ((y) - 1);
            
            if(index < 0 || index > Width * Height)
            return 0;

            return index;
        }
        private int ConvertIndex(int x, int y)
        {
            var index = x + Width * ((Height - y) - 1);

            if(index < 0 || index > Width * Height)
            return 0;

            return index;
        }

        private void Cast()
        {
            var watch = new Stopwatch();
            watch.Start();

            var threadCount = _segmentations.Count;

            while(_segmentations.TryPop(out var segment))
            {
                ThreadPool.QueueUserWorkItem((callback) => {
                    var localSegment = segment;
                    SampleSegment(localSegment);
                    _segmentsDone++;
                });
            }
            
            while(threadCount > _segmentsDone);
            
            watch.Stop();

            _timeSpent += watch.ElapsedMilliseconds;

            Console.WriteLine($"Rendered {_samplesDone} samples after {(watch.ElapsedMilliseconds) / 1000f}s AVG: {(_timeSpent / (_samplesDone / _samplesPerPixel)) / 1000f}s TOT: {_timeSpent / 1000f}s APT: {(((_timeSpent / (_samplesDone / _samplesPerPixel)) / (double)(Width * Height)))}ms AST: {((((_timeSpent / (_samplesDone / _samplesPerPixel)) / (double)(Width * Height)))) / (double)_samplesPerPixel}ms.");
            _segmentsDone = 0;
        }

        private void SampleSegment((int, int) segment)
        {
            for (int i = segment.Item2; i >= segment.Item1; i--)
            {
                var x = i % Width;
                var y = i / Width;

                var r = 0.0f;
                var g = 0.0f;
                var b = 0.0f;

                for (int s = 0; s < _samplesPerPixel; s++)
                {
                    var u = (float)x / (Width - 1);
                    var v = (float)y / (Height - 1);

                    Ray ray = _camera.GetRay(u, v);
                                
                    var rayColor = RayColor(ray, ref _world, _maxDepth);
                    
                    r += rayColor.R;
                    g += rayColor.G;
                    b += rayColor.B;
                }

                PrintColor(x, y, new Vector3(r, g, b));
            }
        }

        private void Sample()
        {
            for (int j = Height - 1; j >= 0; --j)
            {
                for (int i = 0; i < Width; ++i)
                {
                    var r = 0.0f;
                    var g = 0.0f;
                    var b = 0.0f;

                    for (int s = 0; s < _samplesPerPixel; s++)
                    {
                        var u = (float)i / (Width - 1);
                        var v = (float)j / (Height - 1);

                        Ray ray = _camera.GetRay(u, v);
                        
                        var rayColor = RayColor(ray, ref _world, _maxDepth);
                        
                        r += rayColor.R;
                        g += rayColor.G;
                        b += rayColor.B;

                    }
                    PrintColor(i, j, new Vector3(r, g, b));
                }
            }
        }
    }
}