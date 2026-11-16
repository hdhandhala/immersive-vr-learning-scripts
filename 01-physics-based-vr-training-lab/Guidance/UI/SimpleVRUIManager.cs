using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class SimpleVRUIManager : MonoBehaviour
{
    [Header("Main Objects")]
    [SerializeField] private GameObject trainingPanel;
    [SerializeField] private GameObject hologramObject;

    [Header("Zone Cards")]
    [SerializeField] private GameObject[] cards;

    [Header("Main Video")]
    [SerializeField] private VideoPlayer tutorialVideo;
    [SerializeField] private float skipSeconds = 10f;

    [Header("Hologram Renderer + Materials")]
    [SerializeField] private Renderer hologramRenderer;
    [SerializeField] private Material idleHologramMaterial;
    [SerializeField] private Material whiteKeyMaterial;
    [SerializeField] private Material greenKeyMaterial;

    [Header("Extra Presentation Panel")]
    [SerializeField] private GameObject extraHologramObject;
    [SerializeField] private VideoPlayer extraVideoPlayer;
    [SerializeField] private bool stopExtraPanelWhenLeavingZone = true;

    [Header("UI Icons")]
    [SerializeField] private Image playPauseIcon;
    [SerializeField] private Sprite playSprite;
    [SerializeField] private Sprite pauseSprite;

    [Header("Done Visual")]
    [SerializeField] private Image doneButtonImage;
    [SerializeField] private Color notDoneColor = Color.white;
    [SerializeField] private Color doneColor = Color.green;

    [Header("End Session")]
    [SerializeField] private GameObject endSessionPanel;
    [SerializeField] private GameObject normalButtonsGroup;
    [SerializeField] private int finalStepIndex = 4;

    [Header("Progress")]
    [SerializeField] private bool[] completedSteps = new bool[5];

    [Header("Behavior")]
    [SerializeField] private bool hideWhenPlayerLeavesZone = false;

    [Header("Debug")]
    [SerializeField] private bool enableLogs = true;

    private SimpleZoneTrigger currentZone;
    private int currentStepIndex = -1;
    private bool endPanelOpen = false;

    private bool mainPrepared = false;
    private bool extraPrepared = false;

    private bool mainPlayRequested = false;
    private bool extraPlayRequested = false;

    private bool extraPanelActiveForCurrentZone = false;
    private bool playRoutineRunning = false;

    private SimpleZoneTrigger.VideoBackgroundMode currentVideoMode =
        SimpleZoneTrigger.VideoBackgroundMode.WhiteKey;

    private void Awake()
    {
        if (tutorialVideo == null && hologramObject != null)
            tutorialVideo = hologramObject.GetComponent<VideoPlayer>();

        if (hologramRenderer == null && hologramObject != null)
            hologramRenderer = hologramObject.GetComponent<Renderer>();

        if (extraVideoPlayer == null && extraHologramObject != null)
            extraVideoPlayer = extraHologramObject.GetComponent<VideoPlayer>();

        SetupMainVideoPlayer();
        SetupExtraVideoPlayer();
    }

    private void OnDestroy()
    {
        UnregisterMainVideoEvents();
        UnregisterExtraVideoEvents();
    }

    private void Start()
    {
        HideAllCards();

        if (trainingPanel != null)
            trainingPanel.SetActive(false);

        if (hologramObject != null)
            hologramObject.SetActive(false);

        if (extraHologramObject != null)
            extraHologramObject.SetActive(false);

        if (endSessionPanel != null)
            endSessionPanel.SetActive(false);

        if (normalButtonsGroup != null)
            normalButtonsGroup.SetActive(true);

        ForceVideoStopped();
        StopExtraPresentationPanel();
        ApplyIdleMaterial();
        UpdatePlayPauseIcon();
        UpdateDoneVisual();
    }

    private void SetupMainVideoPlayer()
    {
        if (tutorialVideo == null)
            return;

        tutorialVideo.playOnAwake = false;
        tutorialVideo.waitForFirstFrame = true;
        tutorialVideo.isLooping = false;
        tutorialVideo.skipOnDrop = true;

        UnregisterMainVideoEvents();
        tutorialVideo.prepareCompleted += OnMainVideoPrepared;
        tutorialVideo.started += OnMainVideoStarted;
        tutorialVideo.loopPointReached += OnMainVideoFinished;
    }

    private void SetupExtraVideoPlayer()
    {
        if (extraVideoPlayer == null)
            return;

        extraVideoPlayer.playOnAwake = false;
        extraVideoPlayer.waitForFirstFrame = true;
        extraVideoPlayer.isLooping = false;
        extraVideoPlayer.skipOnDrop = true;

        UnregisterExtraVideoEvents();
        extraVideoPlayer.prepareCompleted += OnExtraVideoPrepared;
        extraVideoPlayer.started += OnExtraVideoStarted;
        extraVideoPlayer.loopPointReached += OnExtraVideoFinished;
    }

    private void UnregisterMainVideoEvents()
    {
        if (tutorialVideo == null)
            return;

        tutorialVideo.prepareCompleted -= OnMainVideoPrepared;
        tutorialVideo.started -= OnMainVideoStarted;
        tutorialVideo.loopPointReached -= OnMainVideoFinished;
    }

    private void UnregisterExtraVideoEvents()
    {
        if (extraVideoPlayer == null)
            return;

        extraVideoPlayer.prepareCompleted -= OnExtraVideoPrepared;
        extraVideoPlayer.started -= OnExtraVideoStarted;
        extraVideoPlayer.loopPointReached -= OnExtraVideoFinished;
    }

    public void ShowZone(SimpleZoneTrigger zone)
    {
        if (zone == null)
            return;

        currentZone = zone;
        currentStepIndex = zone.stepIndex;
        currentVideoMode = zone.videoBackgroundMode;
        endPanelOpen = false;

        MoveToZoneAnchors(zone);
        HandleExtraPresentationPanel(zone);

        if (trainingPanel != null)
            trainingPanel.SetActive(true);

        if (hologramObject != null)
            hologramObject.SetActive(true);

        if (endSessionPanel != null)
            endSessionPanel.SetActive(false);

        if (normalButtonsGroup != null)
            normalButtonsGroup.SetActive(true);

        ShowOnlyCard(zone.cardToShow);
        LoadMainVideo(zone.videoClip);

        UpdateDoneVisual();
        UpdatePlayPauseIcon();
        ApplyCurrentMaterial();

        if (enableLogs)
            Debug.Log($"[UIManager] ShowZone -> {zone.name} | Step {zone.stepIndex}", this);
    }

    public void HideZone(SimpleZoneTrigger zone)
    {
        if (!hideWhenPlayerLeavesZone)
            return;

        if (zone == null || currentZone != zone)
            return;

        CloseCurrentZoneUIButAllowReopen();
    }

    public void TogglePlayPause()
    {
        if (endPanelOpen)
            return;

        bool hasMainClip = tutorialVideo != null && tutorialVideo.clip != null;
        bool hasExtraClip = extraPanelActiveForCurrentZone && extraVideoPlayer != null && extraVideoPlayer.clip != null;

        if (!hasMainClip && !hasExtraClip)
        {
            if (enableLogs)
                Debug.Log("[UIManager] No active clips to play.", this);
            return;
        }

        bool anythingPlaying =
            (tutorialVideo != null && tutorialVideo.isPlaying) ||
            (extraVideoPlayer != null && extraVideoPlayer.isPlaying);

        if (anythingPlaying)
        {
            PauseAllVideos();
            return;
        }

        RequestPlayAllVideos();
    }

    public void ForwardTutorial()
    {
        if (endPanelOpen || tutorialVideo == null || !tutorialVideo.canSetTime)
            return;

        if (tutorialVideo.clip == null)
            return;

        double newTime = tutorialVideo.time + skipSeconds;
        tutorialVideo.time = Mathf.Min((float)newTime, (float)tutorialVideo.length);

        SyncExtraVideoToMain();
    }

    public void BackwardTutorial()
    {
        if (endPanelOpen || tutorialVideo == null || !tutorialVideo.canSetTime)
            return;

        if (tutorialVideo.clip == null)
            return;

        double newTime = tutorialVideo.time - skipSeconds;
        tutorialVideo.time = Mathf.Max((float)newTime, 0f);

        SyncExtraVideoToMain();
    }

    public void MarkCurrentStepDone()
    {
        if (currentStepIndex < 0 || currentStepIndex >= completedSteps.Length)
        {
            Debug.LogWarning("[UIManager] Invalid current step index.", this);
            return;
        }

        completedSteps[currentStepIndex] = true;
        UpdateDoneVisual();

        if (enableLogs)
            Debug.Log($"[UIManager] Step marked done -> {currentStepIndex}", this);

        if (currentStepIndex == finalStepIndex)
        {
            ShowEndSessionPanel();
            return;
        }

        CloseCurrentZoneUIButAllowReopen();
    }

    public void ResumeObserving()
    {
        HideEndSessionPanel();
        CloseCurrentZoneUIButAllowReopen();

        if (enableLogs)
            Debug.Log("[UIManager] ResumeObserving()", this);
    }

    public void ExitExperience()
    {
        if (enableLogs)
            Debug.Log("[UIManager] ExitExperience()", this);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void ResetSessionLock()
    {
        currentZone = null;
        currentStepIndex = -1;
        endPanelOpen = false;

        HideEndSessionPanel();
        HideAllCards();
        ForceVideoStopped();
        StopExtraPresentationPanel();
        ApplyIdleMaterial();
        UpdatePlayPauseIcon();
        UpdateDoneVisual();
    }

    public void CloseCurrentZoneUIButAllowReopen()
    {
        ForceVideoStopped();

        if (trainingPanel != null)
            trainingPanel.SetActive(false);

        if (hologramObject != null)
            hologramObject.SetActive(false);

        if (stopExtraPanelWhenLeavingZone)
            StopExtraPresentationPanel();

        HideAllCards();
        HideEndSessionPanel();

        currentZone = null;
        currentStepIndex = -1;

        ApplyIdleMaterial();
        UpdatePlayPauseIcon();
        UpdateDoneVisual();

        if (enableLogs)
            Debug.Log("[UIManager] Zone UI closed. Trigger can reopen later.", this);
    }

    private void ShowEndSessionPanel()
    {
        endPanelOpen = true;

        ForceVideoStopped();
        HideAllCards();

        if (trainingPanel != null)
            trainingPanel.SetActive(true);

        if (hologramObject != null)
            hologramObject.SetActive(true);

        if (endSessionPanel != null)
            endSessionPanel.SetActive(true);

        if (normalButtonsGroup != null)
            normalButtonsGroup.SetActive(false);

        ApplyIdleMaterial();

        if (enableLogs)
            Debug.Log("[UIManager] End session panel shown.", this);
    }

    private void HideEndSessionPanel()
    {
        endPanelOpen = false;

        if (endSessionPanel != null)
            endSessionPanel.SetActive(false);

        if (normalButtonsGroup != null)
            normalButtonsGroup.SetActive(true);
    }

    private void MoveToZoneAnchors(SimpleZoneTrigger zone)
    {
        if (zone == null)
            return;

        if (trainingPanel != null && zone.panelAnchor != null)
        {
            trainingPanel.transform.SetPositionAndRotation(
                zone.panelAnchor.position,
                zone.panelAnchor.rotation
            );
        }

        if (hologramObject != null && zone.hologramAnchor != null)
        {
            hologramObject.transform.SetPositionAndRotation(
                zone.hologramAnchor.position,
                zone.hologramAnchor.rotation
            );
        }
    }

    private void HandleExtraPresentationPanel(SimpleZoneTrigger zone)
    {
        extraPanelActiveForCurrentZone = false;

        if (zone == null || extraHologramObject == null)
            return;

        if (!zone.useExtraPresentationPanel)
        {
            StopExtraPresentationPanel();
            return;
        }

        extraPanelActiveForCurrentZone = true;
        extraHologramObject.SetActive(true);

        if (zone.extraHologramAnchor != null)
        {
            extraHologramObject.transform.SetPositionAndRotation(
                zone.extraHologramAnchor.position,
                zone.extraHologramAnchor.rotation
            );
        }

        LoadExtraVideo(zone.extraVideoClip);

        if (enableLogs)
            Debug.Log("[UIManager] Extra presentation panel ready.", this);
    }

    private void LoadMainVideo(VideoClip clip)
    {
        if (tutorialVideo == null)
            return;

        mainPrepared = false;
        mainPlayRequested = false;

        tutorialVideo.Stop();
        tutorialVideo.time = 0;
        tutorialVideo.clip = clip;

        if (clip == null)
        {
            ApplyIdleMaterial();
            UpdatePlayPauseIcon();
            return;
        }

        tutorialVideo.Prepare();

        if (enableLogs)
            Debug.Log("[UIManager] Preparing MAIN video.", this);
    }

    private void LoadExtraVideo(VideoClip clip)
    {
        if (extraVideoPlayer == null)
            return;

        extraPrepared = false;
        extraPlayRequested = false;

        extraVideoPlayer.Stop();
        extraVideoPlayer.time = 0;
        extraVideoPlayer.clip = clip;

        if (clip == null)
        {
            if (enableLogs)
                Debug.LogWarning("[UIManager] Extra presentation clip is NULL.", this);
            return;
        }

        extraVideoPlayer.Prepare();

        if (enableLogs)
            Debug.Log("[UIManager] Preparing EXTRA video.", this);
    }

    private void RequestPlayAllVideos()
    {
        mainPlayRequested = tutorialVideo != null && tutorialVideo.clip != null;
        extraPlayRequested = extraPanelActiveForCurrentZone && extraVideoPlayer != null && extraVideoPlayer.clip != null;

        if (!playRoutineRunning)
            StartCoroutine(PlayWhenReadyCoroutine());

        if (enableLogs)
        {
            Debug.Log($"[UIManager] Play requested. MainPrepared={mainPrepared}, ExtraPrepared={extraPrepared}, ExtraActive={extraPanelActiveForCurrentZone}", this);
        }
    }

    private IEnumerator PlayWhenReadyCoroutine()
    {
        playRoutineRunning = true;

        float timeout = 5f;
        float timer = 0f;

        while (timer < timeout)
        {
            bool mainReady = !mainPlayRequested || mainPrepared;
            bool extraReady = !extraPlayRequested || extraPrepared;

            if (mainReady && extraReady)
                break;

            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        if (mainPlayRequested && tutorialVideo != null && tutorialVideo.clip != null)
        {
            tutorialVideo.time = 0;
            tutorialVideo.Play();
        }

        if (extraPlayRequested && extraVideoPlayer != null && extraVideoPlayer.clip != null)
        {
            extraVideoPlayer.time = 0;
            extraVideoPlayer.Play();
        }

        UpdatePlayPauseIcon();
        playRoutineRunning = false;

        if (enableLogs)
            Debug.Log("[UIManager] PlayWhenReady finished. Both active videos started.", this);
    }

    private void PauseAllVideos()
    {
        if (tutorialVideo != null && tutorialVideo.isPlaying)
            tutorialVideo.Pause();

        if (extraVideoPlayer != null && extraVideoPlayer.isPlaying)
            extraVideoPlayer.Pause();

        mainPlayRequested = false;
        extraPlayRequested = false;

        ApplyCurrentMaterial();
        UpdatePlayPauseIcon();

        if (enableLogs)
            Debug.Log("[UIManager] Paused all active videos.", this);
    }

    private void SyncExtraVideoToMain()
    {
        if (!extraPanelActiveForCurrentZone || tutorialVideo == null || extraVideoPlayer == null)
            return;

        if (tutorialVideo.clip == null || extraVideoPlayer.clip == null)
            return;

        if (!extraVideoPlayer.canSetTime)
            return;

        extraVideoPlayer.time = tutorialVideo.time;

        if (enableLogs)
            Debug.Log($"[UIManager] Synced extra video to main time: {tutorialVideo.time}", this);
    }

    private void StopExtraPresentationPanel()
    {
        extraPanelActiveForCurrentZone = false;
        extraPrepared = false;
        extraPlayRequested = false;

        if (extraVideoPlayer != null)
        {
            extraVideoPlayer.Stop();
            extraVideoPlayer.clip = null;
            extraVideoPlayer.time = 0;
        }

        if (extraHologramObject != null)
            extraHologramObject.SetActive(false);

        if (enableLogs)
            Debug.Log("[UIManager] Extra presentation panel hidden.", this);
    }

    private void ForceVideoStopped()
    {
        mainPrepared = false;
        extraPrepared = false;
        mainPlayRequested = false;
        extraPlayRequested = false;
        playRoutineRunning = false;

        if (tutorialVideo != null)
        {
            if (tutorialVideo.isPlaying) tutorialVideo.Stop();
            else tutorialVideo.Pause();
            tutorialVideo.time = 0;
        }

        if (extraVideoPlayer != null)
        {
            if (extraVideoPlayer.isPlaying) extraVideoPlayer.Stop();
            else extraVideoPlayer.Pause();
            extraVideoPlayer.time = 0;
        }
    }

    private void OnMainVideoPrepared(VideoPlayer vp)
    {
        mainPrepared = true;

        if (enableLogs)
            Debug.Log("[UIManager] MAIN video prepared.", this);
    }

    private void OnMainVideoStarted(VideoPlayer vp)
    {
        ApplyCurrentMaterial();
        UpdatePlayPauseIcon();

        if (enableLogs)
            Debug.Log("[UIManager] MAIN video started.", this);
    }

    private void OnMainVideoFinished(VideoPlayer vp)
    {
        mainPlayRequested = false;

        if (extraVideoPlayer != null && extraVideoPlayer.isPlaying)
            extraVideoPlayer.Stop();

        ApplyIdleMaterial();
        UpdatePlayPauseIcon();

        if (enableLogs)
            Debug.Log("[UIManager] MAIN video finished.", this);
    }

    private void OnExtraVideoPrepared(VideoPlayer vp)
    {
        extraPrepared = true;

        if (enableLogs)
            Debug.Log("[UIManager] EXTRA video prepared.", this);
    }

    private void OnExtraVideoStarted(VideoPlayer vp)
    {
        if (enableLogs)
            Debug.Log("[UIManager] EXTRA video started.", this);
    }

    private void OnExtraVideoFinished(VideoPlayer vp)
    {
        extraPlayRequested = false;

        if (enableLogs)
            Debug.Log("[UIManager] EXTRA video finished.", this);
    }

    private void ApplyCurrentMaterial()
    {
        if (tutorialVideo != null && tutorialVideo.isPlaying)
        {
            if (currentVideoMode == SimpleZoneTrigger.VideoBackgroundMode.GreenKey)
            {
                if (greenKeyMaterial != null && hologramRenderer != null)
                    hologramRenderer.material = greenKeyMaterial;
            }
            else
            {
                if (whiteKeyMaterial != null && hologramRenderer != null)
                    hologramRenderer.material = whiteKeyMaterial;
            }
        }
        else
        {
            ApplyIdleMaterial();
        }
    }

    private void ApplyIdleMaterial()
    {
        if (hologramRenderer != null && idleHologramMaterial != null)
            hologramRenderer.material = idleHologramMaterial;
    }

    private void ShowOnlyCard(GameObject targetCard)
    {
        if (cards == null)
            return;

        foreach (GameObject card in cards)
        {
            if (card != null)
                card.SetActive(card == targetCard);
        }
    }

    private void HideAllCards()
    {
        if (cards == null)
            return;

        foreach (GameObject card in cards)
        {
            if (card != null)
                card.SetActive(false);
        }
    }

    private void UpdatePlayPauseIcon()
    {
        if (playPauseIcon == null)
            return;

        bool showPause =
            (tutorialVideo != null && tutorialVideo.isPlaying) ||
            (extraVideoPlayer != null && extraVideoPlayer.isPlaying);

        playPauseIcon.sprite = showPause ? pauseSprite : playSprite;
    }

    private void UpdateDoneVisual()
    {
        if (doneButtonImage == null)
            return;

        if (currentStepIndex >= 0 && currentStepIndex < completedSteps.Length)
            doneButtonImage.color = completedSteps[currentStepIndex] ? doneColor : notDoneColor;
        else
            doneButtonImage.color = notDoneColor;
    }
}