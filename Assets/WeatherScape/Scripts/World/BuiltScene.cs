using System.Collections.Generic;
using UnityEngine;

namespace WeatherScape.World
{
    /// <summary>What a scene builder produced: the object root plus anchors the weather/hazard effects and camera need.</summary>
    public class BuiltScene
    {
        public SceneKind kind;
        public Transform root;
        public Mats mats;

        // camera orbit
        public Vector3 focus;
        public float camDistance = 260f, camHeight = 90f, camYaw = 25f;
        public float extent = 300f;              // half size of the detailed area

        // animated things
        public struct Sway { public Transform t; public Quaternion baseRot; public float phase, flex; }
        public struct Bob { public Transform t; public Vector3 basePos; public Quaternion baseRot; public float phase; }
        public class Lane { public List<Vector3> pts = new List<Vector3>(); public bool loop; public float length; }
        public struct CarOnLane { public Transform t; public Lane lane; public float s, speed; }

        public readonly List<Sway> sway = new List<Sway>();
        public readonly List<Bob> boats = new List<Bob>();
        public readonly List<Lane> lanes = new List<Lane>();
        public readonly List<CarOnLane> cars = new List<CarOnLane>();
        public readonly List<Vector3> lamps = new List<Vector3>();
        public readonly List<Transform> spinning = new List<Transform>();   // lighthouse beam, wind turbines …

        // water
        public Material seaMat, lakeMat;
        public float seaLevel = 0f;
        public Vector3 seaDir;                   // horizontal direction from focus towards the open sea (zero = no sea)
        public float shoreDistance;              // distance from focus to the shore along seaDir

        // hazard anchors
        public Vector3 volcanoPos = new Vector3(0, 0, 1700f);
        public Vector3 fireCenter = new Vector3(-260f, 0, 380f);
        public float floodLevel = 1.1f;
        public float groundLevel = 0f;
        public float floodExtent = 700f;
        public Transform lighthouseBeam;

        /// <summary>Terrain height (for placing effects); flat ground returns groundLevel.</summary>
        public System.Func<float, float, float> height;
        public float H(float x, float z) => height != null ? height(x, z) : groundLevel;
    }
}
