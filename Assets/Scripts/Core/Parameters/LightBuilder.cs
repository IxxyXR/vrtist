﻿using UnityEngine;

namespace VRtist
{
    public class LightBuilder : GameObjectBuilder
    {
        public override GameObject CreateInstance(GameObject source, Transform parent = null, bool isPrefab = false)
        {
            GameObject newLight = GameObject.Instantiate(source, parent);
            LightController lightController = source.GetComponentInChildren<LightController>();
            VRInput.DeepSetLayer(newLight, "CameraHidden");

            if (!isPrefab)
            {
                LightController newController = newLight.GetComponentInChildren<LightController>();
                newController.CopyParameters(lightController);
                GlobalState.castShadowsEvent.AddListener(newController.OnCastShadowsChanged);
            }

            if (!GlobalState.Settings.DisplayGizmos)
                GlobalState.SetGizmoVisible(newLight, false);

            return newLight;
        }
    }
}
