using Outpost.Sim;
using UnityEngine;

namespace Outpost.Runtime.Room
{
    public enum DoorMotionType
    {
        SlideLocalOffset,
        HingeLocalY
    }

    /// <summary>
    /// Modular habitat door with smooth ~0.6s open/close motion, collider sync so open doorways never snag,
    /// optional lock state with localized reason, and swappable audio/status indicator slots.
    /// </summary>
    public class DoorInteractable : MonoBehaviour
    {
        [Header("Identity & Motion")]
        [SerializeField] private string doorNameKey = "door.name.corridor";
        [SerializeField] private DoorMotionType motionType = DoorMotionType.SlideLocalOffset;
        [SerializeField] private Transform movingLeaf;
        [SerializeField] private Collider blockingCollider;
        [SerializeField] private Vector3 openLocalOffset = new Vector3(1.18f, 0f, 0f);
        [SerializeField] private Transform secondaryLeaf;
        [SerializeField] private Collider secondaryCollider;
        [SerializeField] private Vector3 secondaryOpenLocalOffset = new Vector3(-0.94f, 0f, 0f);
        [SerializeField] private float openHingeAngle = 88f;
        [SerializeField] private float animDuration = 0.6f;

        [Header("State & Status Indicator")]
        [SerializeField] private bool startsOpen;
        [SerializeField] private bool isLocked;
        [SerializeField] private string lockedReasonKey = "airlock.hatch_locked";
        [SerializeField] private Renderer statusIndicatorRenderer;

        [Header("Swappable Audio Slots (Optional)")]
        [SerializeField] private AudioClip overrideOpenClip;
        [SerializeField] private AudioClip overrideCloseClip;
        [SerializeField] private AudioClip overrideLockedClip;

        private Vector3 _closedLocalPos;
        private Quaternion _closedLocalRot;
        private Vector3 _secondaryClosedLocalPos;
        private Quaternion _secondaryClosedLocalRot;
        private bool _poseCaptured;
        private bool _targetOpen;
        private float _openAmount;
        private MaterialPropertyBlock _mpb;

        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProp = Shader.PropertyToID("_Color");
        private static readonly int EmissionProp = Shader.PropertyToID("_EmissionColor");

        public bool IsOpen => _targetOpen;
        public bool IsLocked => isLocked;
        public bool IsMoving => (_targetOpen && _openAmount < 0.99f) || (!_targetOpen && _openAmount > 0.01f);
        public string DoorDisplayName => Localization.Get(doorNameKey);

        private void Awake()
        {
            CaptureClosedPose();
            _targetOpen = startsOpen;
            _openAmount = startsOpen ? 1f : 0f;
            ApplyDoorTransformAndCollider(_openAmount, null);
            RefreshIndicatorColor();
        }

        private void Update()
        {
            float target = _targetOpen ? 1f : 0f;
            if (Mathf.Approximately(_openAmount, target))
            {
                // If closing paused while player stood inside threshold, finish making collider solid once clear
                SyncColliderState(_openAmount, null);
                return;
            }

            float speed = animDuration > 0.02f ? (1f / animDuration) : 10f;
            _openAmount = Mathf.MoveTowards(_openAmount, target, Time.deltaTime * speed);
            ApplyDoorTransformAndCollider(_openAmount, null);
        }

        public void Configure(
            string nameKey,
            DoorMotionType motion,
            Transform leaf,
            Collider doorCol,
            Vector3 slideOffset,
            float hingeDeg = 88f,
            bool openByDefault = false,
            bool lockedByDefault = false,
            string lockKey = "",
            Renderer indicator = null)
        {
            doorNameKey = nameKey;
            motionType = motion;
            movingLeaf = leaf;
            blockingCollider = doorCol;
            openLocalOffset = slideOffset;
            openHingeAngle = hingeDeg;
            startsOpen = openByDefault;
            isLocked = lockedByDefault;
            lockedReasonKey = lockKey;
            statusIndicatorRenderer = indicator;
            _poseCaptured = false;
            CaptureClosedPose();
            _targetOpen = openByDefault;
            _openAmount = openByDefault ? 1f : 0f;
            ApplyDoorTransformAndCollider(_openAmount, null);
            RefreshIndicatorColor();
        }

        public void ConfigureBiPartingLeaves(Transform secondLeaf, Vector3 secondSlideOffset, Collider secondCollider = null)
        {
            secondaryLeaf = secondLeaf;
            secondaryOpenLocalOffset = secondSlideOffset;
            secondaryCollider = secondCollider;
            _poseCaptured = false;
            CaptureClosedPose();
            ApplyDoorTransformAndCollider(_openAmount, null);
            RefreshIndicatorColor();
        }

        public string GetPromptText()
        {
            if (isLocked)
                return Localization.Format("door.locked_prompt", DoorDisplayName);
            return Localization.Format(_targetOpen ? "door.close_prompt" : "door.open_prompt", DoorDisplayName);
        }

        public bool TryToggle(AudioManager audio, out string feedbackMessage)
        {
            feedbackMessage = string.Empty;
            if (isLocked)
            {
                audio?.PlayDoorLocked(overrideLockedClip);
                feedbackMessage = string.IsNullOrEmpty(lockedReasonKey)
                    ? Localization.Format("door.locked_prompt", DoorDisplayName)
                    : Localization.Get(lockedReasonKey);
                return false;
            }

            SetOpen(!_targetOpen, audio);
            return true;
        }

        public void SetOpen(bool open, AudioManager audio = null)
        {
            CaptureClosedPose();
            if (_targetOpen == open) return;
            _targetOpen = open;
            if (open) audio?.PlayDoorOpen(overrideOpenClip);
            else audio?.PlayDoorClose(overrideCloseClip);
            RefreshIndicatorColor();
        }

        public void SetOpenImmediate(bool open)
        {
            CaptureClosedPose();
            _targetOpen = open;
            _openAmount = open ? 1f : 0f;
            ApplyDoorTransformAndCollider(_openAmount, null);
            RefreshIndicatorColor();
        }

        public void SetLocked(bool locked, string reasonKey = "")
        {
            isLocked = locked;
            if (!string.IsNullOrEmpty(reasonKey))
                lockedReasonKey = reasonKey;
            RefreshIndicatorColor();
        }

        public void DebugUnlock()
        {
            isLocked = false;
            lockedReasonKey = string.Empty;
            RefreshIndicatorColor();
        }

        private void CaptureClosedPose()
        {
            if (_poseCaptured) return;
            Transform t = movingLeaf != null ? movingLeaf : transform;
            _closedLocalPos = t.localPosition;
            _closedLocalRot = t.localRotation;
            if (secondaryLeaf != null)
            {
                _secondaryClosedLocalPos = secondaryLeaf.localPosition;
                _secondaryClosedLocalRot = secondaryLeaf.localRotation;
            }
            _poseCaptured = true;
        }

        private void ApplyDoorTransformAndCollider(float t, Transform ignoreTransform)
        {
            CaptureClosedPose();
            float s = Mathf.SmoothStep(0f, 1f, t);
            Transform target = movingLeaf != null ? movingLeaf : transform;
            if (motionType == DoorMotionType.SlideLocalOffset)
            {
                target.localPosition = _closedLocalPos + openLocalOffset * s;
                if (secondaryLeaf != null)
                    secondaryLeaf.localPosition = _secondaryClosedLocalPos + secondaryOpenLocalOffset * s;
            }
            else
            {
                target.localRotation = _closedLocalRot * Quaternion.Euler(0f, openHingeAngle * s, 0f);
                if (secondaryLeaf != null)
                    secondaryLeaf.localRotation = _secondaryClosedLocalRot * Quaternion.Euler(0f, -openHingeAngle * s, 0f);
            }

            SyncColliderState(t, ignoreTransform);
        }

        private void SyncColliderState(float openAmt, Transform ignoreTransform)
        {
            bool wantTrigger = openAmt > 0.22f;
            Collider playerCol = null;
            bool lookedUpPlayer = false;

            SyncSingleCollider(blockingCollider, wantTrigger, ref playerCol, ref lookedUpPlayer);
            SyncSingleCollider(secondaryCollider, wantTrigger, ref playerCol, ref lookedUpPlayer);
        }

        private static void SyncSingleCollider(Collider col, bool wantTrigger, ref Collider playerCol, ref bool lookedUpPlayer)
        {
            if (col == null) return;
            if (wantTrigger)
            {
                col.isTrigger = true;
                return;
            }

            if (col.isTrigger)
            {
                if (!lookedUpPlayer)
                {
                    lookedUpPlayer = true;
                    var gate = FindAnyObjectByType<PlayerGate>();
                    playerCol = gate != null && gate.PlayerTransform != null
                        ? gate.PlayerTransform.GetComponentInChildren<Collider>()
                        : null;
                }

                if (playerCol != null)
                {
                    Bounds b = col.bounds;
                    b.Expand(0.18f);
                    if (b.Intersects(playerCol.bounds))
                        return;
                }
                col.isTrigger = false;
            }
        }

        private void RefreshIndicatorColor()
        {
            if (statusIndicatorRenderer == null) return;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            Color col = isLocked
                ? new Color(0.95f, 0.25f, 0.22f, 1f)
                : (_targetOpen ? new Color(0.32f, 0.90f, 0.62f, 1f) : new Color(0.96f, 0.72f, 0.24f, 1f));
            statusIndicatorRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorProp, col);
            _mpb.SetColor(ColorProp, col);
            _mpb.SetColor(EmissionProp, col * 1.4f);
            statusIndicatorRenderer.SetPropertyBlock(_mpb);
        }
    }
}
