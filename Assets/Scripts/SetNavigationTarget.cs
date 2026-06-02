using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public class SetNavigationTarget : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown navigationTargetDropDown;
    [SerializeField] private List<Target> navigationTargetObjects = new List<Target>();
    [SerializeField] private Slider       navigationYOffset;
    [SerializeField] private AudioSource  navigationAudioSource;

    private NavMeshPath path;
    private LineRenderer line;
    private Vector3 targetPosition = Vector3.zero;
    private bool    lineToggle     = false;

    //audio improvements
    [Header("Audio Improvements")]
    [SerializeField] private AudioClip arrivalSound;
    [SerializeField] [Range(0.5f, 10f)]  private float arrivalDistance    = 1.5f;
    [SerializeField] [Range(1f,   50f)]  private float pitchRampDistance  = 15f;
    [SerializeField] [Range(35f, 180f)]  private float wrongDirectionAngle = 65f;
    [SerializeField] [Range(1f,   10f)]  private float lookaheadDistance  = 2.5f;

    [Header("Performance")]
    [SerializeField] [Range(0.1f, 1f)]   private float pathRecalcInterval = 0.3f;
    [Header("AR Reference")]
    [SerializeField] private Transform arCamera;

    private bool  hasArrived    = false;
    private float arrivalVolume = 1f;

    // Cache default values to avoid redundant calculations
    private float   pathRecalcTimer    = 0f;
    private float   cachedPathLength   = 0f;
    private float   cachedDistToTarget = 0f;
    private Vector3 cachedLookahead    = Vector3.zero;
    private float   cachedAngle        = 0f;

    // Start - init NavMeshPath, LineRenderer, and cache audio volume; ensure AR camera reference is set
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

    // Update - recalculate path at intervals, refresh cache when needed, and update line renderer and audio based on cached values; skip processing if no target is set
    private void Update() {
        if (targetPosition == Vector3.zero) return;

        // Recalculate path at defined intervals to optimize performance
        pathRecalcTimer += Time.deltaTime;
        bool pathRecalculated = false;
        if (pathRecalcTimer >= pathRecalcInterval) {
            pathRecalcTimer   = 0f;
            pathRecalculated  = true;
            NavMesh.CalculatePath(transform.position, targetPosition, NavMesh.AllAreas, path);
        }

        // Refresh cache if path is recalculated or if player has moved significantly from the last look-ahead point
        if (pathRecalculated || Vector3.Distance(transform.position, cachedLookahead) > 0.5f) {
            RefreshCache();
        }

        // Update line renderer positions if enabled; use cached path corners with Y offset for better performance
        if (lineToggle) {
            line.positionCount = path.corners.Length;
            line.SetPositions(AddLineOffset());
        }

        UpdateAudio();
    }

    // Cache calculations to avoid redundant processing in Update; only recalculate when path changes or player moves significantly; includes path length, distance to target, look-ahead point, and angle to look-ahead for audio adjustments
    private void RefreshCache() {
        if (path == null || path.corners.Length < 2) {
            cachedPathLength   = 0f;
            cachedDistToTarget = Vector3.Distance(transform.position, targetPosition);
            cachedLookahead    = targetPosition;
            cachedAngle        = 0f;
            return;
        }

        // Path length
        cachedPathLength = 0f;
        for (int i = 0; i < path.corners.Length - 1; i++)
            cachedPathLength += Vector3.Distance(path.corners[i], path.corners[i + 1]);

        // Distance to target
        cachedDistToTarget = Vector3.Distance(transform.position, targetPosition);

        // Look-ahead point
        cachedLookahead = ComputeLookaheadPoint();

        // Angle to look-ahead point for audio adjustments
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

    // Audio updates based on cached distance and angle; handles arrival sound, pitch and volume adjustments, and ensures audio source is positioned at the look-ahead point for better spatial feedback; stops audio when arrived or when no valid path exists
    private void UpdateAudio() {
        if (navigationAudioSource == null) return;

        if (path == null || path.corners.Length < 2) {
            if (navigationAudioSource.isPlaying) navigationAudioSource.Stop();
            return;
        }

        // Arrival handling
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

        // Update audio source position to look-ahead point for better spatial feedback
        navigationAudioSource.transform.position = cachedLookahead;

        // Pitch
        float t = 1f - Mathf.Clamp01(cachedDistToTarget / pitchRampDistance);
        navigationAudioSource.pitch = Mathf.Lerp(1.0f, 1.8f, t);

        // Volume
        float volumeMultiplier = 1f - Mathf.Clamp01((cachedAngle - 30f) / (wrongDirectionAngle - 30f));
        navigationAudioSource.volume = Mathf.Lerp(0.1f, arrivalVolume, volumeMultiplier);

        if (!navigationAudioSource.isPlaying) navigationAudioSource.Play();
    }

    // Set navigation target based on dropdown selection; resets state and forces path recalculation; finds target position from list and updates audio source if needed
    public void SetCurrentNavigationTarget(int selectedValue) {
        targetPosition  = Vector3.zero;
        hasArrived      = false;
        pathRecalcTimer = pathRecalcInterval;

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
            navigationTargetDropDown.options.Add(new TMP_Dropdown.OptionData("MainEntrance"));
            navigationTargetDropDown.options.Add(new TMP_Dropdown.OptionData("LargeHall"));
        }
    }
}