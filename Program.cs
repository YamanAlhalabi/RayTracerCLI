using System;

namespace SharpCanvas
{
    class Program
    {
        static void Main(string[] args)
        {
            var samples = 1;
            var scene = SceneGenerator.GenerateTwoSphere();
            var bounces = 50;
            var width = 128;
            var incrementations = 1;
            var aspectRatio = 1f / 1f;
            var segmentSize = 32; 
            var apreture = 0.1f;
            var distToFocus = 10f;

            new Renderer(incrementations, width, samples, segmentSize, aspectRatio, bounces, scene).Begin(apreture, distToFocus);
        }
    }
}
