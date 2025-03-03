using UnityEngine;
using Unity.Mathematics;

/*
 * Unity Noise-based Terrain Generator
 * 
 * Based on original code by Brackeys (https://www.youtube.com/watch?v=vFvwyu_ZKfU)
 */
public class Generator : MonoBehaviour
{
    public Terrain terrain;

    public MapType mapType = MapType.Flat;

    [Range(1, 10000)]
    public int randomSeed = 10; // Seed for RNG

    [Header("Terrain Size")]
    public int width = 256;
    public int depth = 256;
    [Range(0, 100)]
    public int height = 20;

    public enum MapType
    {
        Flat, Slope, Random, Perlin, Simplex, PerlinOctave
    };

    [Header("Perlin Noise")]
    [Range(0f, 100f)]
    public float frequency = 20f;
    [Range(0f, 10000f)]
    public float offsetX = 100f;
    [Range(0f, 10000f)]
    public float offsetY = 100f;

    public bool animateOffset = false;

    [Header("Octaves")]
    [Range(1, 8)]
    public int octaves = 3;
    [Range(0, 4)]
    public float amplitudeModifier;
    [Range(0, 4)]
    public float frequencyModifier;

    public void Start()
    {
        // Get a reference to the terrain component
        terrain = GetComponent<Terrain>();
    }

    // Update is called every frame
    public void Update()
    {
        // Generate the terrain according to current parameters
        terrain.terrainData = GenerateTerrain(terrain.terrainData);

        // Move along the X axis
        if (animateOffset)
            offsetX += Time.deltaTime * 5f;
    }

    // Update the terrain height values
    public TerrainData GenerateTerrain(TerrainData data)
    {
        // Set size and resolution for the terrain data
        data.heightmapResolution = width + 1;
        data.size = new Vector3(width, height, depth);

        float[,] heightMap;


        // Generate a height map
        switch (mapType)
        {
            case (MapType.Slope):
                heightMap = SlopingMap();
                break;
            case (MapType.Random):
                heightMap = RandomMap();
                break;
            case (MapType.Perlin):
                heightMap = NoiseMap(false);  // 使用 Perlin 噪声
                break;
            case (MapType.Simplex):
                heightMap = NoiseMap(true);  // 使用 Simplex 噪声
                break;
            case (MapType.PerlinOctave):
                heightMap = PerlinOctaveMap();
                break;

            default:
                heightMap = FlatMap();
                break;
        }

        // Set the terrain data to the new height map
        data.SetHeights(0, 0, heightMap);

        return data;
    }

    // Generate a flat height map (all zero)
    public float[,] FlatMap()
    {
        float[,] heights = new float[width, depth];

        return heights;
    }

    // Generate a sloping height map - you need to fix this!
    public float[,] SlopingMap()
    {
        float[,] heights = new float[width, depth]; // width：列数（x 方向的大小）depth：行数（y 方向的大小）

        // Iterate over map positions
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < depth; y++)
            {
                // Set height at this position

                heights[x, y] = ((float)x / (float)width + (float)y / (float)depth) / 2f;
            }
        }

        return heights;
    }

    public float[,] RandomMap()
    {
        float[,] heights = new float[width, depth];
        System.Random rng = new System.Random();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < depth; y++)
            { 
                heights[x, y] = (float)rng.NextDouble(); // NextDouble()返回值：一个介于 [0, 1) 的双精度浮点数（double） 
            }
        }
        return heights;
    }

    public float[,] NoiseMap(bool useSimplex)
    {
        float[,] heights = new float[width, depth];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < depth; y++)
            {
                float sampleX = frequency * (float)x / width; // frequency 控制噪声的密集程度（值越大，变化越剧烈）
                float sampleY = frequency * (float)y / depth;
                float noiseValue;
                if (useSimplex)
                {
                    /* Simplex 噪声函数 */
                    noiseValue = noise.snoise(new float2(sampleX, sampleY)); // 接受一个 float2参数(二维向量)返回一个 float 值，范围是 [-1, 1]
                    noiseValue = (noiseValue + 1f) / 2f;  // 归一化到 [0, 1]
                }else
                {
                    noiseValue = Mathf.PerlinNoise(sampleX, sampleY); // 返回一个 [0,1] 之间的浮点数，表示该点的地形高度
                }
                heights[x, y] = noiseValue;
            }
        }
        return heights;
    }
    /*
        Octave 是指将多种不同频率和振幅的噪声组合在一起，生成更自然的地形。
        高频的噪声捕捉细节，低频的噪声生成大地形轮廓。
        每个 octave 都有独立的频率、振幅和偏移量。

        算法流程：
        1.初始频率和振幅分别为 frequency 和 1。
        2.每个新的 octave，频率乘以 frequencyModifier，振幅乘以 amplitudeModifier。
        3.将多个 octave 的值相加，最后归一化到 [0, 1]。
     */
    public float[,] PerlinOctaveMap()
    {
        float[,] heights = new float[width, depth];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < depth; y++)
            {
                float sampleX = (float)x / width;
                float sampleY = (float)y / depth;
                float amplitude = 1f;
                float frequency = this.frequency;
                float noiseSum = 0f;
                float maxPossibleHeight = 0f;

                for (int i = 0; i < octaves; i++)
                {
                    float offsetX = this.offsetX + i * 100;  // 确保每个 octave 偏移不同
                    float offsetY = this.offsetY + i * 100;
                    float noiseValue = Mathf.PerlinNoise(frequency * sampleX + offsetX, frequency * sampleY + offsetY);

                    noiseSum += noiseValue * amplitude;
                    maxPossibleHeight += amplitude;

                    amplitude *= amplitudeModifier;
                    frequency *= frequencyModifier;
                }

                // 归一化到 [0, 1]
                heights[x, y] = noiseSum / maxPossibleHeight;
            }
        }
        return heights;
    }
}
