using System.Collections.Generic;
using UnityEngine;

// Tag component on star GameObjects only (excluded: planets, comets, asteroids, debris).
// Maintains a static list of active stars used by StarCollisionManager.
public class StarMarker : MonoBehaviour
{
    private static readonly List<StarMarker> s_active = new List<StarMarker>();
    public static IReadOnlyList<StarMarker> AllStars => s_active;

    void OnEnable() { s_active.Add(this); }
    void OnDisable() { s_active.Remove(this); }
}
