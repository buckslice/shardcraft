using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShadowText : MonoBehaviour {

    public Text text;
    public Text shadowText;

    public void SetText(string text) {
        this.text.text = text;
        shadowText.text = text;
    }

}
