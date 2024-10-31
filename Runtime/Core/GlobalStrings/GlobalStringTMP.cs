using UnityEngine;
using TMPro;

namespace PitGL
{
    /// <summary>
    /// Will set the text of the TMP component to the global string value defined by the provided key.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class GlobalStringTMP : MonoBehaviour
    {
        [SerializeField] string key;
        TMP_Text text;

        private void OnEnable()
        {
            text ??= GetComponent<TMP_Text>();
            GlobalString.AddListener(key, OnModifiedString);
        }

        private void OnDisable()
        {
            GlobalString.RemoveListener(key, OnModifiedString);
        }

        private void OnModifiedString(string value)
        {
            text.text = value;
        }
    }
}
