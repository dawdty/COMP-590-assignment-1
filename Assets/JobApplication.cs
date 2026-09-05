using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>A paper target supporting mouse, touch, and Cardboard pointer messages.</summary>
[ExecuteAlways]
[RequireComponent(typeof(BoxCollider))]
public sealed class JobApplication : MonoBehaviour
{
    [Min(0.1f)] public float launchDuration = 0.8f;
    [Min(0.1f)] public float launchHeight = 1.1f;
    [Min(0.1f)] public float resetDelay = 2f;

    private Transform paper;
    private GameObject fragments;
    [SerializeField] private Material white;
    [SerializeField] private Material ink;
    [SerializeField] private AudioClip vineBoom;
    [SerializeField] private AudioClip robloxCongrats;
    private BoxCollider hitbox;
    private bool busy;
    private bool hovered;
    private JobApplicationHud hud;
    private Material[] confettiMaterials;
    private AudioSource audioPlayer;

    private void Start()
    {
        if (Application.isPlaying) EnsureHud();
    }

    private void EnsureHud()
    {
        if (hud != null || Camera.main == null) return;
        hud = Camera.main.GetComponent<JobApplicationHud>();
        if (hud == null) hud = Camera.main.gameObject.AddComponent<JobApplicationHud>();
    }

    private void EnsureAudioPlayer()
    {
        if (audioPlayer != null || !Application.isPlaying) return;
        var player = new GameObject("Job application sound player");
        player.transform.SetParent(transform, false);
        audioPlayer = player.AddComponent<AudioSource>();
        audioPlayer.spatialBlend = 0f;
        audioPlayer.playOnAwake = false;
    }

    private void PlaySound(AudioClip clip)
    {
        EnsureAudioPlayer();
        if (audioPlayer != null && clip != null) audioPlayer.PlayOneShot(clip);
    }

    private void OnEnable()
    {
        hitbox = GetComponent<BoxCollider>();
        hitbox.size = new Vector3(1.6f, 1.05f, 0.08f);
        hitbox.enabled = true;
        busy = false;
        // Serialized materials retain their shader as a dependency in player builds.
        if (white == null || ink == null)
        {
            hitbox.enabled = false;
            if (Application.isPlaying)
                Debug.LogError("Job Application needs its White and Ink materials assigned.", this);
            return;
        }
        paper = new GameObject("Paper visual").transform;
        paper.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        paper.SetParent(transform, false);
        MakePiece("White rectangle", paper, Vector3.zero, new Vector3(1.6f, 1.05f, 0.045f), white);
        var label = new GameObject("job application").AddComponent<TextMesh>();
        label.transform.SetParent(paper, false);
        label.transform.localPosition = new Vector3(0, 0.2f, -0.026f);
        label.text = "job application";
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 20;
        label.characterSize = 0.105f;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.GetComponent<Renderer>().sharedMaterial = label.font.material;
        label.color = ink.color;
        // A few printed lines make the rectangle read as a paper application.
        for (int i = 0; i < 3; i++)
            MakePiece("Printed line", paper, new Vector3(-0.1f, -0.05f - i * 0.12f, -0.028f),
                new Vector3(i == 2 ? 0.7f : 1.1f, 0.012f, 0.005f), ink);
    }

    private static Transform MakePiece(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = name;
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = position;
        piece.transform.localScale = scale;
        Collider collider = piece.GetComponent<Collider>();
        collider.enabled = false;
        Dispose(collider);
        piece.GetComponent<Renderer>().sharedMaterial = material;
        return piece.transform;
    }

    private void Update()
    {
        if (!Application.isPlaying || busy || Camera.main == null) return;
        EnsureHud();
        // In VR, the Cardboard reticle selects the target and sends OnPointerClick.
        // Screen coordinates do not map directly to the distorted stereo image.
        if (Camera.main.stereoEnabled) return;
        Vector2 screenPosition = default;
        bool pressed = false;
#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            pressed = true;
        }
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPosition = Mouse.current.position.ReadValue();
            pressed = true;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            screenPosition = Input.GetTouch(0).position;
            pressed = true;
        }
        else if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            pressed = true;
        }
#endif
        if (pressed && Physics.Raycast(Camera.main.ScreenPointToRay(screenPosition), out RaycastHit hit)
            && hit.collider == hitbox) Launch();
    }

    public void OnPointerEnter()
    {
        hovered = true;
        if (!busy && paper != null) paper.localScale = Vector3.one * 1.04f;
    }

    public void OnPointerExit()
    {
        hovered = false;
        if (!busy && paper != null) paper.localScale = Vector3.one;
    }

    public void OnPointerClick() => Launch();

    public void Launch()
    {
        if (!Application.isPlaying || busy || paper == null) return;
        busy = true;
        hitbox.enabled = false;
        EnsureHud();
        if (hud != null) hud.RecordApplication();
        // Independent 10% chance for each accepted click, not every tenth click.
        bool nextRound = Random.Range(0, 10) == 0;
        if (!nextRound) StartCoroutine(PlayVineBoomBeforeExplosion());
        StartCoroutine(LaunchAndExplode(nextRound));
    }

    private IEnumerator PlayVineBoomBeforeExplosion()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, launchDuration - 0.2f));
        PlaySound(vineBoom);
    }

    private void EnsureConfettiMaterials()
    {
        if (confettiMaterials != null) return;
        Color[] palette = { new Color(1f, 0.25f, 0.45f), new Color(1f, 0.8f, 0.15f),
            new Color(0.2f, 0.85f, 1f), new Color(0.4f, 1f, 0.55f),
            new Color(0.7f, 0.4f, 1f), new Color(1f, 0.55f, 0.15f) };
        confettiMaterials = new Material[palette.Length];
        for (int i = 0; i < palette.Length; i++)
            confettiMaterials[i] = new Material(white) { color = palette[i] };
    }

    private IEnumerator LaunchAndExplode(bool nextRound)
    {
        float duration = Mathf.Max(0.1f, launchDuration);
        float height = Mathf.Max(0.1f, launchHeight);
        Vector3 burstPosition = new Vector3(Random.Range(-0.75f, 0.75f) * height,
            height, Random.Range(-0.2f, 0.2f) * height);
        Vector3 launchTilt = new Vector3(Random.Range(-15f, 15f),
            Random.Range(-20f, 20f), Random.Range(-45f, 45f));
        for (float elapsed = 0; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = elapsed / duration;
            float rise = t * t;
            float drift = rise * t;
            paper.localPosition = new Vector3(burstPosition.x * drift, height * rise,
                burstPosition.z * drift);
            paper.localRotation = Quaternion.Euler(launchTilt * rise);
            paper.localScale = Vector3.one * (1f - 0.12f * t);
            yield return null;
        }

        paper.localPosition = burstPosition;
        paper.gameObject.SetActive(false);
        if (nextRound)
        {
            EnsureConfettiMaterials();
            PlaySound(robloxCongrats);
            if (hud != null) hud.RecordInterview();
        }
        fragments = new GameObject(nextRound ? "Confetti celebration" : "Paper explosion");
        fragments.transform.SetParent(transform, false);
        int count = nextRound ? 84 : 28;
        Vector3 fragmentSize = nextRound ? new Vector3(0.055f, 0.12f, 0.008f)
            : new Vector3(0.18f, 0.2f, 0.018f);
        var pieces = new Transform[count];
        var origins = new Vector3[count];
        var velocities = new Vector3[count];
        var spins = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            origins[i] = burstPosition + new Vector3((i % 7 - 3) * 0.18f, ((i / 7) % 4 - 1.5f) * 0.2f, 0);
            velocities[i] = Random.onUnitSphere * Random.Range(1.2f, 3.2f) + Vector3.up;
            spins[i] = Random.insideUnitSphere * 650f;
            pieces[i] = MakePiece(nextRound ? "Confetti" : "Paper fragment", fragments.transform, origins[i],
                fragmentSize, nextRound ? confettiMaterials[i % confettiMaterials.Length] : white);
        }
        float lifetime = Mathf.Max(0.1f, resetDelay);
        for (float elapsed = 0; elapsed < lifetime; elapsed += Time.deltaTime)
        {
            float shrink = 1f - Mathf.SmoothStep(0f, 1f, elapsed / lifetime);
            for (int i = 0; i < count; i++)
            {
                pieces[i].localPosition = origins[i] + velocities[i] * elapsed
                    + Vector3.down * ((nextRound ? 0.9f : 2f) * elapsed * elapsed);
                pieces[i].localRotation = Quaternion.Euler(spins[i] * elapsed);
                pieces[i].localScale = fragmentSize * shrink;
            }
            yield return null;
        }
        Dispose(fragments);
        fragments = null;
        paper.localPosition = Vector3.zero;
        paper.localRotation = Quaternion.identity;
        paper.localScale = Vector3.one * (hovered ? 1.04f : 1f);
        paper.gameObject.SetActive(true);
        hitbox.enabled = true;
        busy = false;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (paper != null) Dispose(paper.gameObject);
        if (fragments != null) Dispose(fragments);
        if (confettiMaterials != null)
        {
            foreach (Material material in confettiMaterials) Dispose(material);
            confettiMaterials = null;
        }
        busy = false;
    }

    private static void Dispose(Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }
}
