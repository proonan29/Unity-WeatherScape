using UnityEngine;
using WeatherScape;

// Point the camera rig at the palace main hall (tourist scene) for close-up checks.
public static class CloseUpHall
{
    public static string Run()
    {
        var rig = Object.FindAnyObjectByType<CameraRig>();
        rig.focus = new Vector3(0, 10, 125); rig.distance = 70f; rig.height = 22f; rig.yaw = 20f; rig.autoOrbit = false;
        rig.Place();
        return "camera " + rig.transform.position;
    }
}

// Count vertices with zero / NaN normals in the generated meshes.
public static class BadNormals
{
    public static string Run()
    {
        int bad = 0, meshes = 0; string where = "";
        foreach (var mf in Resources.FindObjectsOfTypeAll<MeshFilter>())
        {
            var m = mf.sharedMesh; if (m == null || !m.isReadable) continue;
            meshes++;
            int b = 0;
            foreach (var n in m.normals) if (float.IsNaN(n.x) || n.sqrMagnitude < 0.5f) b++;
            if (b > 0) { bad += b; where += $"{mf.name}:{b} "; }
        }
        return $"meshes={meshes} bad_normals={bad} {where}";
    }
}
