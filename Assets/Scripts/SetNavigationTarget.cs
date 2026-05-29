using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public class SetNavigationTarget : MonoBehaviour
{
    // ── GIỐNG HỆT TUTORIAL ───────────────────────────────────────────────────
    [SerializeField] private TMP_Dropdown navigationTargetDropDown;
    [SerializeField] private List<Target> navigationTargetObjects = new List<Target>();
    [SerializeField] private Slider       navigationYOffset;
    [SerializeField] private AudioSource  navigationAudioSource;

    private NavMeshPath path;
    private LineRenderer line;
    private Vector3 targetPosition = Vector3.zero;
    private bool    lineToggle     = false;

    // ── THÊM: audio improvements ─────────────────────────────────────────────
    [Header("Audio Improvements")]
    [SerializeField] private AudioClip arrivalSound;
    [SerializeField] [Range(0.5f, 10f)]  private float arrivalDistance    = 1.5f;
    [SerializeField] [Range(1f,   50f)]  private float pitchRampDistance  = 15f;
    [SerializeField] [Range(35f, 180f)]  private float wrongDirectionAngle = 65f;
    [SerializeField] [Range(1f,   10f)]  private float lookaheadDistance  = 2.5f;

    [Header("Performance")]
    [SerializeField] [Range(0.1f, 1f)]   private float pathRecalcInterval = 0.3f; // giây

    [Header("AR Reference")]
    [SerializeField] private Transform arCamera;

    private bool  hasArrived    = false;
    private float arrivalVolume = 1f;

    // ── Cache — tính 1 lần/frame dùng nhiều chỗ ──────────────────────────────
    private float   pathRecalcTimer    = 0f;
    private float   cachedPathLength   = 0f;
    private float   cachedDistToTarget = 0f;
    private Vector3 cachedLookahead    = Vector3.zero;
    private float   cachedAngle        = 0f;

    // ── Start ─────────────────────────────────────────────────────────────────
    private void Start() {
        path = new NavMeshPath();
        line = transform.GetComponent<LineRenderer>();
        line.enabled = lineToggle;

        if (arCamera == null)
            arCamera = Camera.main != null ? Camera.main.transform : transform;

        if (navigationAudioSource != null) {
            navigationAudioSource.Stop();
            arrivalVolume = navigationAudioSource.volume;
        }
    }

    // ── Update — tính path theo interval, cache kết quả ──────────────────────
    private void Update() {
        if (targetPosition == Vector3.zero) return;

        // Tính lại path theo interval thay vì mỗi frame
        pathRecalcTimer += Time.deltaTime;
        bool pathRecalculated = false;
        if (pathRecalcTimer >= pathRecalcInterval) {
            pathRecalcTimer   = 0f;
            pathRecalculated  = true;
            NavMesh.CalculatePath(transform.position, targetPosition, NavMesh.AllAreas, path);
        }

        // Chỉ tính lại cache khi path mới hoặc player di chuyển đủ xa
        if (pathRecalculated || Vector3.Distance(transform.position, cachedLookahead) > 0.5f) {
            RefreshCache();
        }

        // Line renderer — chỉ update khi bật
        if (lineToggle) {
            line.positionCount = path.corners.Length;
            line.SetPositions(AddLineOffset());
        }

        UpdateAudio();
    }

    // ── Tính tất cả giá trị tốn CPU 1 lần, lưu vào cache ────────────────────
    private void RefreshCache() {
        if (path == null || path.corners.Length < 2) {
            cachedPathLength   = 0f;
            cachedDistToTarget = Vector3.Distance(transform.position, targetPosition);
            cachedLookahead    = targetPosition;
            cachedAngle        = 0f;
            return;
        }

        // 1. Path length — loop corners 1 lần duy nhất
        cachedPathLength = 0f;
        for (int i = 0; i < path.corners.Length - 1; i++)
            cachedPathLength += Vector3.Distance(path.corners[i], path.corners[i + 1]);

        // 2. Distance thẳng đến đích
        cachedDistToTarget = Vector3.Distance(transform.position, targetPosition);

        // 3. Look-ahead point — loop corners 1 lần duy nhất
        cachedLookahead = ComputeLookaheadPoint();

        // 4. Góc hướng
        Vector3 toLookahead   = cachedLookahead - transform.position;
        Vector3 playerForward = arCamera.forward;
        toLookahead.y   = 0f;
        playerForward.y = 0f;
        cachedAngle = Vector3.Angle(playerForward.normalized, toLookahead.normalized);
    }

    private Vector3 ComputeLookaheadPoint() {
        float remaining = lookaheadDistance;
        for (int i = 0; i < path.corners.Length - 1; i++) {
            float segLen = Vector3.Distance(path.corners[i], path.corners[i + 1]);
            if (remaining <= segLen)
                return Vector3.Lerp(path.corners[i], path.corners[i + 1], remaining / segLen);
            remaining -= segLen;
        }
        return targetPosition;
    }

    // ── Audio — dùng cache, không tính lại ───────────────────────────────────
    private void UpdateAudio() {
        if (navigationAudioSource == null) return;

        if (path == null || path.corners.Length < 2) {
            if (navigationAudioSource.isPlaying) navigationAudioSource.Stop();
            return;
        }

        // Đến đích
        if (cachedDistToTarget <= arrivalDistance) {
            if (!hasArrived) {
                hasArrived = true;
                navigationAudioSource.Stop();
                navigationAudioSource.volume = arrivalVolume;
                if (arrivalSound != null)
                    navigationAudioSource.PlayOneShot(arrivalSound);
            }
            return;
        }

        // Di chuyển sound đến look-ahead point
        navigationAudioSource.transform.position = cachedLookahead;

        // Pitch — dùng cachedDistToTarget
        float t = 1f - Mathf.Clamp01(cachedDistToTarget / pitchRampDistance);
        navigationAudioSource.pitch = Mathf.Lerp(1.0f, 1.8f, t);

        // Volume — dùng cachedAngle
        float volumeMultiplier = 1f - Mathf.Clamp01((cachedAngle - 30f) / (wrongDirectionAngle - 30f));
        navigationAudioSource.volume = Mathf.Lerp(0.1f, arrivalVolume, volumeMultiplier);

        if (!navigationAudioSource.isPlaying) navigationAudioSource.Play();
    }

    // ── Hàm public giữ nguyên 100% tutorial ──────────────────────────────────
    public void SetCurrentNavigationTarget(int selectedValue) {
        targetPosition  = Vector3.zero;
        hasArrived      = false;
        pathRecalcTimer = pathRecalcInterval; // tính path ngay frame tiếp

        string selectedText  = navigationTargetDropDown.options[selectedValue].text;
        Target currentTarget = navigationTargetObjects.Find(x => x.Name.Equals(selectedText));
        if (currentTarget != null) {
            if (line.enabled) ToggleVisibility();
            targetPosition = currentTarget.PositionObject.transform.position;

            if (navigationAudioSource != null) {
                navigationAudioSource.pitch  = 1f;
                navigationAudioSource.volume = arrivalVolume;
                navigationAudioSource.Stop();
            }
        }
    }

    public void ToggleVisibility() {
        lineToggle   = !lineToggle;
        line.enabled = lineToggle;
    }

    public void ChangeActiveFloor(int floorNumber) {
        SetNavigationTargetDropdownOptions(floorNumber);
    }

    private Vector3[] AddLineOffset() {
        if (navigationYOffset.value == 0) return path.corners;
        Vector3[] calculatedLine = new Vector3[path.corners.Length];
        for (int i = 0; i < path.corners.Length; i++)
            calculatedLine[i] = path.corners[i] + new Vector3(0, navigationYOffset.value, 0);
        return calculatedLine;
    }

    private void SetNavigationTargetDropdownOptions(int floorNumber) {
        navigationTargetDropDown.ClearOptions();
        navigationTargetDropDown.value = 0;
        if (line.enabled) ToggleVisibility();

        if (floorNumber == 1) {
            navigationTargetDropDown.options.Add(new TMP_Dropdown.OptionData("ClassRoom30"));
            navigationTargetDropDown.options.Add(new TMP_Dropdown.OptionData("RestRoom2"));
            navigationTargetDropDown.options.Add(new TMP_Dropdown.OptionData("Hall1"));
        }
        if (floorNumber == 2) {
            navigationTargetDropDown.options.Add(new TMP_Dropdown.OptionData("SecondHall"));
            navigationTargetDropDown.options.Add(new TMP_Dropdown.OptionData("Rooftop"));
            navigationTargetDropDown.options.Add(new TMP_Dropdown.OptionData("WorshipRoom"));
        }
    }
}