using UnityEngine;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;

public class MazeBuilder : MonoBehaviour
{
    public string fileName = "maze.txt";
    public Material wallMaterial;
    
    [Header("Adjustment")]
    [Tooltip("Labirenti yerden yükseltmek için (0.5 küpün yarısıdır)")]
    public float yOffset = 0.5f; 
    [Tooltip("Python'daki eksenleri Unity'e göre değiştir (X, Z, Y gibi)")]
    public bool swapYZ = false; 

    void Start()
    {
        BuildMaze();
    }

  void BuildMaze()
    {

        string filePath = Path.Combine(Application.dataPath, fileName);

        if (!File.Exists(filePath))
        {
            filePath = Path.Combine(Application.dataPath, "Data", fileName);
            
            if(!File.Exists(filePath))
            {
                Debug.LogError($"[MazeBuilder] File NOT found at: {filePath}. Make sure 'maze.txt' is inside the Assets folder!");
                return;
            }
        }

        string[] lines = File.ReadAllLines(filePath);
        GameObject root = new GameObject("Maze_Root");

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            MatchCollection matches = Regex.Matches(line, @"\(([^)]+)\)");
            if (matches.Count >= 2)
            {
                Vector3 pos = ParseVector3(matches[0].Groups[1].Value);
                Vector3 scale = ParseVector3(matches[1].Groups[1].Value);

                if (swapYZ) {
                    pos = new Vector3(pos.x, pos.z, pos.y);
                }

                pos.y += yOffset;

                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.position = pos;
                cube.transform.localScale = scale;
                cube.transform.parent = root.transform;

                if (wallMaterial != null)
                    cube.GetComponent<Renderer>().material = wallMaterial;
            }
        }
        Debug.Log("Maze Generation Complete!");
    }

    Vector3 ParseVector3(string data)
    {
        string[] p = data.Split(',');
        return new Vector3(
            float.Parse(p[0], CultureInfo.InvariantCulture),
            float.Parse(p[1], CultureInfo.InvariantCulture),
            float.Parse(p[2], CultureInfo.InvariantCulture)
        );
    }
}