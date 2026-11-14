using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ScratchCell : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [SerializeField] private RawImage scratchImage;

    private RectTransform scratchRect;
    Texture2D scratchTexture;

    private int pixelX;
    private int pixelY;

    bool mouseDown;
    private bool[] pixelBools;

    [SerializeField] private int brushRadius = 24;

    [SerializeField] private float scratchPercentage = 60;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        scratchTexture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        scratchTexture.wrapMode = TextureWrapMode.Clamp;
        Color32[] pixels = new Color32[scratchTexture.width * scratchTexture.height];
        for (int i = 0; i < pixels.Length; i++)
        {
            var color = new Color32(255, 255, 255, 255);
            pixels[i] = color;
        }
        
        pixelBools = new bool[scratchTexture.width * scratchTexture.height];
        for (int i = 0; i < pixelBools.Length; i++)
        {
            pixelBools[i] = false;
        }

        scratchTexture.SetPixels32(pixels);
        scratchTexture.Apply();

        scratchImage.texture = scratchTexture;

        scratchRect = scratchImage.rectTransform;
    }

    // Update is called once per frame
    void Update()
    {
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        mouseDown = true;
        EraseRoutine(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        mouseDown = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!mouseDown) return;

        EraseRoutine(eventData);
    }

    private void EraseRoutine(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(scratchRect, eventData.position,
            eventData.pressEventCamera, out Vector2 localPoint);
        var rect = scratchRect.rect;

        var u = (localPoint.x - rect.xMin) / rect.width;
        pixelX = Mathf.Clamp(Mathf.FloorToInt(u * scratchTexture.width), 0, scratchTexture.width - 1);
        var v = (localPoint.y - rect.yMin) / rect.height;
        pixelY = Mathf.Clamp(Mathf.FloorToInt(v * scratchTexture.height), 0, scratchTexture.height - 1);

        EraseCircle();
    }


    private void ErasePixel(int corX, int corY)
    {
        pixelBools[corY * scratchTexture.width + corX] = true;
        
        var scratchColor = new Color32(255, 255, 255, 0);
        scratchTexture.SetPixel(corX, corY, scratchColor);
    }

    private void EraseCircle()
    {
        for (int i = pixelX - brushRadius; i < pixelX + brushRadius; i++)
        {
            for (int j = pixelY - brushRadius; j < pixelY + brushRadius; j++)
            {
                var corX = i - pixelX;
                var corY = j - pixelY;

                if (corX * corX + corY * corY <= brushRadius * brushRadius)
                {
                    var boolX = i >= 0 && i < scratchTexture.width;
                    var boolY = j >= 0 && j < scratchTexture.height;
                    if (boolX && boolY) ErasePixel(i, j);
                }
            }
        }

        scratchTexture.Apply();
        int pixelCounter = pixelBools.Count(pixel => pixel);
        var currentPercentage = ((float)pixelCounter / pixelBools.Length) * 100;
        if(currentPercentage >= scratchPercentage) Complete();
    }

    private void Complete()
    {
        Color32[] pixels = new Color32[scratchTexture.width * scratchTexture.height];
        for (int i = 0; i < pixels.Length; i++)
        {
            var color = new Color32(255, 255, 255, 0);
            pixels[i] = color;
        }
        
        pixelBools = new bool[scratchTexture.width * scratchTexture.height];
        for (int i = 0; i < pixelBools.Length; i++)
        {
            pixelBools[i] = false;
        }

        scratchTexture.SetPixels32(pixels);
        scratchTexture.Apply();
    }
}