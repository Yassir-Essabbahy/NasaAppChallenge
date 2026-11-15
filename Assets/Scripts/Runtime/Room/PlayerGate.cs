using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Outpost.Runtime.Room
{
    /// <summary>
    /// Wraps the project's Modular FirstPersonController without modifying its feel or coupling Outpost.Runtime
    /// directly to Assembly-CSharp types. Handles EnableControl(bool), velocity zeroing, cursor state,
    /// accessibility head-bob binding, teleportation, and footstep audio hooks.
    /// </summary>
    public class PlayerGate : MonoBehaviour
    {
        [Header("Target Modular FPS Controller")]
        [SerializeField] private MonoBehaviour fpsController;
        [SerializeField] private Rigidbody playerRigidbody;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private Transform jointTransform;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private GameObject controllerHudCanvas;

        [Header("Audio & Accessibility")]
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private AccessibilitySettings accessibility;
        [SerializeField] private AudioClip[] footstepClips;
        [SerializeField] private AudioClip[] exteriorFootstepClips;
        [SerializeField] private float footstepInterval = 0.48f;

        private FieldInfo _fiPlayerCanMove;
        private FieldInfo _fiCameraCanMove;
        private FieldInfo _fiLockCursor;
        private FieldInfo _fiIsWalking;
        private FieldInfo _fiIsSprinting;
        private FieldInfo _fiIsZoomed;
        private FieldInfo _fiEnableHeadBob;
        private FieldInfo _fiYaw;
        private FieldInfo _fiPitch;
        private bool _fieldsCached;
        private Vector3 _defaultJointLocalPos = new Vector3(0f, 0.75f, 0f);
        private float _stepTimer;
        private Canvas _controllerCanvasComponent;
        private readonly RaycastHit[] _surfaceHitBuffer = new RaycastHit[4];

        public bool IsControlEnabled { get; private set; }
        public Camera PlayerCamera => playerCamera;
        public Transform PlayerTransform => playerTransform != null ? playerTransform : (fpsController != null ? fpsController.transform : transform);
        public Transform JointTransform => jointTransform;

        private void Awake()
        {
            CacheReferencesAndFields();
            HideBuiltInControllerHud();
        }

        private void Start()
        {
            HideBuiltInControllerHud();
            if (accessibility != null)
            {
                accessibility.OnSettingsChanged += HandleAccessibilityChanged;
                ApplyAccessibility();
            }
        }

        private void OnDestroy()
        {
            if (accessibility != null)
                accessibility.OnSettingsChanged -= HandleAccessibilityChanged;
        }

        public void ConfigureReferences(
            MonoBehaviour controller,
            Rigidbody rb,
            Transform playerRoot,
            Transform joint,
            Camera cam,
            GameObject builtInHud,
            AudioManager audio,
            AccessibilitySettings acc)
        {
            fpsController = controller;
            playerRigidbody = rb;
            playerTransform = playerRoot;
            jointTransform = joint;
            playerCamera = cam;
            controllerHudCanvas = builtInHud;
            audioManager = audio;
            accessibility = acc;
            _fieldsCached = false;
            CacheReferencesAndFields();
            HideBuiltInControllerHud();
        }

        public void EnableControl(bool enabled)
        {
            CacheReferencesAndFields();
            HideBuiltInControllerHud();
            IsControlEnabled = enabled;

            if (fpsController != null)
            {
                SetField(_fiPlayerCanMove, enabled);
                SetField(_fiCameraCanMove, enabled);
                SetField(_fiLockCursor, enabled);
                if (!enabled)
                {
                    SetField(_fiIsWalking, false);
                    SetField(_fiIsSprinting, false);
                    SetField(_fiIsZoomed, false);
                }
                else
                {
                    SetField(_fiYaw, PlayerTransform.localEulerAngles.y);
                }
                fpsController.enabled = enabled;
            }

            if (playerRigidbody != null)
            {
                if (!enabled)
                {
                    if (!playerRigidbody.isKinematic)
                    {
                        playerRigidbody.linearVelocity = Vector3.zero;
                        playerRigidbody.angularVelocity = Vector3.zero;
                    }
                    playerRigidbody.isKinematic = true;
                }
                else
                {
                    playerRigidbody.isKinematic = false;
                    playerRigidbody.linearVelocity = Vector3.zero;
                    playerRigidbody.angularVelocity = Vector3.zero;
                }
            }

            ApplyAccessibility();

            Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !enabled;
            _stepTimer = 0f;
        }

        public void TeleportTo(Vector3 worldPosition, Quaternion worldRotation)
        {
            CacheReferencesAndFields();
            bool wasKinematic = playerRigidbody != null && playerRigidbody.isKinematic;
            if (playerRigidbody != null && !wasKinematic)
            {
                playerRigidbody.linearVelocity = Vector3.zero;
                playerRigidbody.angularVelocity = Vector3.zero;
            }

            PlayerTransform.SetPositionAndRotation(worldPosition, worldRotation);

            if (jointTransform != null)
            {
                jointTransform.localPosition = _defaultJointLocalPos;
                jointTransform.localRotation = Quaternion.identity;
            }

            if (playerCamera != null)
            {
                playerCamera.transform.localPosition = Vector3.zero;
                playerCamera.transform.localRotation = Quaternion.identity;
            }

            if (fpsController != null)
            {
                SetField(_fiYaw, worldRotation.eulerAngles.y);
                SetField(_fiPitch, 0f);
                SetField(_fiIsWalking, false);
                SetField(_fiIsSprinting, false);
            }
        }

        private void Update()
        {
            if (!IsControlEnabled) return;

            // Keep cursor locked in RoomView if user clicked back into Game View / WebGL canvas
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                audioManager?.EnsureLoopsPlaying();
            }

            // Footstep hook driven by Rigidbody horizontal speed
            if (playerRigidbody != null && !playerRigidbody.isKinematic && audioManager != null)
            {
                Vector3 vel = playerRigidbody.linearVelocity;
                vel.y = 0f;
                float speed = vel.magnitude;
                if (speed > 0.45f)
                {
                    float rate = speed > 5.5f ? 0.68f : 1f;
                    _stepTimer += Time.deltaTime;
                    if (_stepTimer >= footstepInterval * rate)
                    {
                        _stepTimer = 0f;
                        bool onExterior = IsStandingOnExteriorSurface();
                        AudioClip[] clips = onExterior ? exteriorFootstepClips : footstepClips;
                        audioManager.PlayFootstep(clips, onExterior);
                    }
                }
                else
                {
                    _stepTimer = footstepInterval * 0.7f;
                }
            }
        }

        private bool IsStandingOnExteriorSurface()
        {
            Transform p = PlayerTransform;
            if (p != null)
            {
                Vector3 rayOrigin = p.position + Vector3.up * 0.25f;
                int hitCount = Physics.RaycastNonAlloc(rayOrigin, Vector3.down, _surfaceHitBuffer, 2.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                float bestDist = float.MaxValue;
                Collider bestCollider = null;

                for (int i = 0; i < hitCount; i++)
                {
                    Collider col = _surfaceHitBuffer[i].collider;
                    if (col == null) continue;
                    if (col.transform == p || col.transform.IsChildOf(p)) continue;
                    if (_surfaceHitBuffer[i].distance < bestDist)
                    {
                        bestDist = _surfaceHitBuffer[i].distance;
                        bestCollider = col;
                    }
                }

                if (bestCollider != null)
                {
                    string colName = bestCollider.name;
                    if (colName.IndexOf("Mars", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        colName.IndexOf("Terrain", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        colName.IndexOf("Rock", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;

                    Transform cur = bestCollider.transform;
                    while (cur != null)
                    {
                        if (cur.name == "MarsSurfaceWorld")
                            return true;
                        if (cur.name == "CommandModuleRoom" || cur.name == "HubCorridor" ||
                            cur.name == "Room_Medbay" || cur.name == "Room_Greenhouse" ||
                            cur.name == "Room_Storage" || cur.name == "Room_Airlock")
                            return false;
                        cur = cur.parent;
                    }
                }

                if (p.position.z < -17.45f)
                    return true;
            }

            return audioManager != null && audioManager.IsExterior;
        }

        private void HandleAccessibilityChanged()
        {
            ApplyAccessibility();
        }

        private void ApplyAccessibility()
        {
            if (fpsController == null) return;
            CacheReferencesAndFields();
            bool reduceMotion = accessibility != null && accessibility.ReduceMotion;
            SetField(_fiEnableHeadBob, !reduceMotion);
        }

        private void HideBuiltInControllerHud()
        {
            if (controllerHudCanvas == null) return;
            if (_controllerCanvasComponent == null)
                _controllerCanvasComponent = controllerHudCanvas.GetComponent<Canvas>();

            // Disable only the Canvas renderer component so FirstPersonController.Start() never hits null children.
            if (_controllerCanvasComponent != null)
                _controllerCanvasComponent.enabled = false;
        }

        private void CacheReferencesAndFields()
        {
            if (_fieldsCached) return;

            if (playerTransform == null && fpsController != null)
                playerTransform = fpsController.transform;
            if (playerRigidbody == null && PlayerTransform != null)
                playerRigidbody = PlayerTransform.GetComponent<Rigidbody>();
            if (jointTransform != null)
                _defaultJointLocalPos = jointTransform.localPosition;

            if (fpsController != null)
            {
                Type t = fpsController.GetType();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                _fiPlayerCanMove = t.GetField("playerCanMove", flags);
                _fiCameraCanMove = t.GetField("cameraCanMove", flags);
                _fiLockCursor = t.GetField("lockCursor", flags);
                _fiIsWalking = t.GetField("isWalking", flags);
                _fiIsSprinting = t.GetField("isSprinting", flags);
                _fiIsZoomed = t.GetField("isZoomed", flags);
                _fiEnableHeadBob = t.GetField("enableHeadBob", flags);
                _fiYaw = t.GetField("yaw", flags);
                _fiPitch = t.GetField("pitch", flags);
            }

            _fieldsCached = true;
        }

        private void SetField(FieldInfo fi, object val)
        {
            if (fi != null && fpsController != null)
                fi.SetValue(fpsController, val);
        }
    }
}
