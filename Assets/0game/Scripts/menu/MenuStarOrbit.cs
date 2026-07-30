using System;
using System.Collections.Generic;
using UnityEngine;

public class MenuStarOrbit : MonoBehaviour
{
    [Serializable]
    public class OrbitStar
    {
        public Transform star;

        [Min(0.01f)]
        public float radius = 2f;

        [Tooltip("Degrees per second. Negative value rotates clockwise.")]
        public float angularSpeed = 35f;

        [Range(0f, 360f)]
        public float startAngle;

        [Tooltip("Adds a small vertical ellipse instead of a perfect circle.")]
        [Range(0.1f, 1f)]
        public float verticalRadiusMultiplier = 0.72f;
    }

    [Header("Centre")]
    [SerializeField] private Transform blackHole;

    [Header("Orbiting stars")]
    [SerializeField] private List<OrbitStar> stars = new List<OrbitStar>();

    [Header("Optional motion")]
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool rotateStarsAlongOrbit = false;

    private void Update()
    {
        if (blackHole == null) return;

        float time = useUnscaledTime ? Time.unscaledTime : Time.time;
        Vector3 centre = blackHole.position;

        foreach (OrbitStar orbitStar in stars)
        {
            if (orbitStar == null || orbitStar.star == null) continue;

            float angle = (orbitStar.startAngle + orbitStar.angularSpeed * time) * Mathf.Deg2Rad;

            Vector3 offset = new Vector3(
                Mathf.Cos(angle) * orbitStar.radius,
                Mathf.Sin(angle) * orbitStar.radius * orbitStar.verticalRadiusMultiplier,
                0f
            );

            Vector3 position = centre + offset;
            position.z = orbitStar.star.position.z;
            orbitStar.star.position = position;

            if (rotateStarsAlongOrbit)
            {
                float tangentAngle = angle * Mathf.Rad2Deg
                    + (orbitStar.angularSpeed >= 0f ? 90f : -90f);

                orbitStar.star.rotation = Quaternion.Euler(0f, 0f, tangentAngle);
            }
        }
    }
}