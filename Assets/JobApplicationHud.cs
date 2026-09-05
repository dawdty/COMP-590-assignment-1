using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>World-space HUD attached to the camera so it renders in both VR eyes.</summary>
public sealed class JobApplicationHud : MonoBehaviour
{
    private Text applicationsLabel;
    private Text interviewsLabel;
    private Text notification;
    private GameObject canvasObject;
    private int applications;
    private int interviews;
    private Coroutine celebration;

    private void Awake()
    {
        canvasObject = new GameObject("Application counters", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localPosition = new Vector3(0, 0, 1f);
        canvasObject.transform.localScale = Vector3.one * 0.001f;
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = GetComponent<Camera>();
        canvas.sortingOrder = 100;
        canvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(800, 800);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        applicationsLabel = CreateText("Applications sent", font, 350, 32);
        applicationsLabel.text = "job applications sent: 0";
        interviewsLabel = CreateText("Interviews", font, 298, 30);
        interviewsLabel.gameObject.SetActive(false);
        notification = CreateText("Next round notification", font, 175, 32);
        notification.text = "you moved on to the next round";
        notification.color = new Color(0.45f, 1f, 0.7f);
        notification.gameObject.SetActive(false);
    }

    private Text CreateText(string name, Font font, float y, int size)
    {
        var labelObject = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
        labelObject.transform.SetParent(canvasObject.transform, false);
        var rect = labelObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(780, 90);
        rect.anchoredPosition = new Vector2(0, y);
        var label = labelObject.GetComponent<Text>();
        label.font = font;
        label.fontSize = size;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        var outline = labelObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.03f, 0.04f, 0.07f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        return label;
    }

    public void RecordApplication()
    {
        applications++;
        applicationsLabel.text = $"job applications sent: {applications}";
    }

    public void RecordInterview()
    {
        interviews++;
        if (celebration != null) StopCoroutine(celebration);
        celebration = StartCoroutine(ShowNextRound());
    }

    private IEnumerator ShowNextRound()
    {
        notification.gameObject.SetActive(true);
        Color color = notification.color;
        color.a = 1;
        notification.color = color;
        yield return new WaitForSecondsRealtime(1.25f);
        const float fadeDuration = 1f;
        for (float elapsed = 0; elapsed < fadeDuration; elapsed += Time.unscaledDeltaTime)
        {
            color.a = 1f - elapsed / fadeDuration;
            notification.color = color;
            yield return null;
        }
        notification.gameObject.SetActive(false);
        interviewsLabel.text = $"interviews: {interviews}";
        interviewsLabel.gameObject.SetActive(true);
        celebration = null;
    }

    private void OnDestroy()
    {
        if (canvasObject != null) Destroy(canvasObject);
    }
}
