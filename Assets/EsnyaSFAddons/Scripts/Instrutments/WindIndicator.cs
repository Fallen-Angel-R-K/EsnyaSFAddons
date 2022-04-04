﻿
using TMPro;
using UdonSharp;
using UnityEngine;

namespace EsnyaSFAddons
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class WindIndicator : UdonSharpBehaviour
    {
        public float updateInterval = 0.1f;
        public bool showGust = true;
        public Transform directionIndicator;
        public TextMeshProUGUI speedText;

        private SaccAirVehicle airVehicle;
        private Transform vehicleTransform;

        private void _Awake()
        {
            initialized = true;

            var entity = GetComponentInParent<SaccEntity>();
            vehicleTransform = entity.transform;
            airVehicle = (SaccAirVehicle)entity.GetExtention(GetUdonTypeName<SaccAirVehicle>());
        }

        private bool initialized;
        private void OnEnable()
        {
            if (!initialized) _Awake();
            SendCustomEventDelayedSeconds(nameof(_ThinUpdate), Random.Range(0, updateInterval));
        }

        public void _ThinUpdate()
        {
            if (!gameObject.activeInHierarchy) return;

            var wind = airVehicle.Wind;
            if (showGust) wind += GetGust();

            var windAngle = Vector3.SignedAngle(wind.normalized, vehicleTransform.forward, Vector3.up);
            var windSpeed = wind.magnitude * 1.944f;

            if (directionIndicator)
            {
                directionIndicator.localRotation = Quaternion.AngleAxis(windAngle, Vector3.forward);
            }

            if (speedText)
            {
                speedText.text = windSpeed.ToString("#0");
            }

            SendCustomEventDelayedSeconds(nameof(_ThinUpdate), updateInterval);
        }

        private Vector3 GetGust()
        {
            var t = Time.time * airVehicle.WindGustiness;
            var turbulanceScale = airVehicle.WindTurbulanceScale;
            var gustx = t + (vehicleTransform.position.x * turbulanceScale);
            var gustz = t + (vehicleTransform.position.z * turbulanceScale);
            return Vector3.Normalize(new Vector3(Mathf.PerlinNoise(gustx + 9000, gustz) - .5f, 0, Mathf.PerlinNoise(gustx, gustz + 9999) - .5f)) * airVehicle.WindGustStrength;
        }
    }
}
