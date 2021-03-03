﻿/* MIT License
 *
 * Copyright (c) 2021 Ubisoft
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using UnityEngine;
using UnityEngine.Assertions;

namespace VRtist
{
    public class CameraFeedback : MonoBehaviour
    {
        public Transform vrCamera;
        public Transform rig;
        private GameObject cameraPlane;
        private GameObject feedbackCamera = null;

        private void Awake()
        {
            CameraManager.Instance.onActiveCameraChanged.AddListener(OnCameraChanged);
            Assert.IsTrue(transform.GetChild(0).name == "CameraFeedbackPlane");
            cameraPlane = transform.GetChild(0).gameObject;
        }

        private void OnEnable()
        {
            SetActiveCamera(CameraManager.Instance.ActiveCamera);
        }

        protected void Update()
        {
            if (!gameObject.activeSelf)
                return;

            if (null == feedbackCamera)
                return;

            float far = Camera.main.farClipPlane * GlobalState.WorldScale * 0.7f;
            float fov = Camera.main.fieldOfView;

            Camera cam = feedbackCamera.GetComponentInChildren<Camera>(true);
            float aspect = cam.aspect;

            float scale = far * Mathf.Tan(Mathf.Deg2Rad * fov * 0.5f) * 0.5f * GlobalState.Settings.cameraFeedbackScaleValue;
            Vector3 direction = GlobalState.Settings.cameraFeedbackDirection;
            transform.localPosition = direction.normalized * far;
            transform.localRotation = Quaternion.LookRotation(-direction) * Quaternion.Euler(0, 180, 0);
            transform.localScale = new Vector3(scale * aspect, scale, scale);
        }

        void OnCameraChanged(GameObject _, GameObject activeCamera)
        {
            SetActiveCamera(activeCamera);
        }

        private void SetActiveCamera(GameObject activeCamera)
        {
            if (feedbackCamera == activeCamera)
                return;
            feedbackCamera = activeCamera;
            if (null != feedbackCamera)
            {
                Camera cam = feedbackCamera.GetComponentInChildren<Camera>(true);
                cameraPlane.SetActive(true);
                cameraPlane.GetComponent<MeshRenderer>().material.SetTexture("_UnlitColorMap", cam.targetTexture);
            }
            else
            {
                cameraPlane.SetActive(false);
            }
        }
    }
}
