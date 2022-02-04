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


            for (int i = 0; i < args.Length; i++)
            {
                switch(args[i])
                {
                    case "--samples":
                        samples = int.Parse(args[i++]);
                        break;
                    case "--scene":
                        switch(args[i++])
                        {
                            case "2spheres":
                                scene = SceneGenerator.GenerateTwoSphere();
                            break;
                            case "marbles":
                                scene = SceneGenerator.GenerateBouncingMarbles();
                            break;
                            
                            default: 
                                throw new ArgumentException();
                        }
                        break;
                    case "--bounces":
                        bounces = int.Parse(args[i++]);
                        break;
                    case "--width":
                        width = int.Parse(args[i++]);
                        break;
                    case "--segment":
                        segmentSize = int.Parse(args[i++]);
                        break;
                    case "--aspectRatio":
                        aspectRatio = float.Parse(args[i++]);
                        break;
                    case "--apreture":
                        apreture = float.Parse(args[i++]);
                        break;
                    case "--distToFocus":
                        distToFocus = float.Parse(args[i++]);
                        break;
                    default: 
                        Console.WriteLine($"argument not recognized {args[i]}");
                        return;
                }
            }
            new Renderer(incrementations, width, samples, segmentSize, aspectRatio, bounces, scene).Begin(apreture, distToFocus);
        }
    }
}
