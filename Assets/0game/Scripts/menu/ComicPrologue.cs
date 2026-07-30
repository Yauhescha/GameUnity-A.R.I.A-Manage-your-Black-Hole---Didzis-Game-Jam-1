using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[Serializable]
public class ComicFrame
{
    public Sprite image;

    [TextArea(3, 8)]
    public string text;
}

public class ComicPrologue : MonoBehaviour
{
    [Header("Comic")]
    [SerializeField] private List<ComicFrame> frames = new List<ComicFrame>();

    [Header("Scene after prologue")]
    [SerializeField] private string gameSceneName = "Level0";

    [Header("UI from scene")]
    [SerializeField] private Image frameImage;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Button nextButton;
    [SerializeField] private TMP_Text nextButtonText;

    [Tooltip("Необязательно. Если указать кнопку окна текста, клик по нему тоже переключает кадр.")]
    [SerializeField] private Button dialogueWindowButton;

    private int currentFrameIndex;

    private void Start()
    {
        if (frames == null || frames.Count == 0)
        {
            Debug.LogError("ComicPrologue: добавь хотя бы один кадр.", this);
            enabled = false;
            return;
        }

        if (frameImage == null ||
            dialogueText == null ||
            nextButton == null ||
            nextButtonText == null)
        {
            Debug.LogError(
                "ComicPrologue: заполни ссылки на UI в инспекторе.",
                this
            );

            enabled = false;
            return;
        }

        nextButton.onClick.AddListener(Advance);

        if (dialogueWindowButton != null)
            dialogueWindowButton.onClick.AddListener(Advance);

        ShowFrame(0);
    }

    private void OnDestroy()
    {
        if (nextButton != null)
            nextButton.onClick.RemoveListener(Advance);

        if (dialogueWindowButton != null)
            dialogueWindowButton.onClick.RemoveListener(Advance);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.Return))
        {
            Advance();
        }
    }

    private void ShowFrame(int index)
    {
        currentFrameIndex = index;

        ComicFrame frame = frames[currentFrameIndex];

        frameImage.sprite = frame.image;
        frameImage.enabled = frame.image != null;

        dialogueText.text = frame.text;

        bool isLastFrame = currentFrameIndex == frames.Count - 1;

        nextButtonText.text = isLastFrame
            ? "START GAME"
            : "NEXT";
    }

    public void Advance()
    {
        if (currentFrameIndex < frames.Count - 1)
        {
            ShowFrame(currentFrameIndex + 1);
            return;
        }

        if (string.IsNullOrWhiteSpace(gameSceneName))
        {
            Debug.LogError(
                "ComicPrologue: не указано имя следующей сцены.",
                this
            );

            return;
        }

        SceneManager.LoadScene(gameSceneName);
    }
}