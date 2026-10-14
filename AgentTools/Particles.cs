using System.Text;
using UnityEngine;

// Lists every particle system in the generated world with its state and particle count.
public static class ParticlesDiag
{
    public static string Run()
    {
        var sb = new StringBuilder();
        foreach (var ps in Resources.FindObjectsOfTypeAll<ParticleSystem>())
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            sb.AppendLine($"{ps.transform.parent?.name}/{ps.name} active={ps.gameObject.activeInHierarchy} playing={ps.isPlaying} count={ps.particleCount} rate={ps.emission.rateOverTime.constant} " +
                          $"pos={ps.transform.position} bounds={r.bounds.center}/{r.bounds.size} mat={(r.sharedMaterial != null ? r.sharedMaterial.shader.name : "null")}");
        }
        return sb.ToString();
    }
}
