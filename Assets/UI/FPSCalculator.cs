using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FPSCalculator : MonoBehaviour {

    Text fpsText;

    int frameRange = 60;

    int[] fpsBuffer;
    int fpsBufferIndex;

    string[] fpsStrings = new string[100];
    void Start() {

        fpsText = GetComponent<Text>();

        for (int i = 0; i < fpsStrings.Length; ++i) {
            fpsStrings[i] = string.Format("fps:{0}", i);
        }
    }

    void InitializeBuffer() {
        if (frameRange <= 0) {
            frameRange = 1;
        }
        fpsBuffer = new int[frameRange];
        fpsBufferIndex = 0;
    }

    void Update() {
        if (fpsBuffer == null || fpsBuffer.Length != frameRange) {
            InitializeBuffer();
        }
        UpdateBuffer();
        int fps = CalculateFPS();

        fpsText.text = fpsStrings[Mathf.Clamp(fps, 0, fpsStrings.Length - 1)];
    }

    void UpdateBuffer() {
        fpsBuffer[fpsBufferIndex++] = (int)(1f / Time.unscaledDeltaTime);
        if (fpsBufferIndex >= frameRange) {
            fpsBufferIndex = 0;
        }
    }

    int CalculateFPS() {
        int sum = 0;
        for (int i = 0; i < frameRange; ++i) {
            sum += fpsBuffer[i];
        }
        return sum / frameRange;
    }

}
