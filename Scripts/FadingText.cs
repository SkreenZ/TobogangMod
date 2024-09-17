using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace TobogangMod.Scripts
{
    public class FadingText : MonoBehaviour
    {
        public float TranslationSpeed { get; set; } = 0.05f;
        public float AlphaSpeed { get; set; } = 0.35f;

        void Start()
        {
            gameObject.GetComponent<TextMeshProUGUI>().font = HUDManager.Instance.newProfitQuotaText.font;
        }

        void Update()
        {
            var pos = gameObject.transform.position;
            gameObject.transform.position = pos + Vector3.up * TranslationSpeed * Time.deltaTime;

            var text = gameObject.GetComponent<TextMeshProUGUI>();
            text.alpha = Math.Max(0f, text.alpha - Time.deltaTime * AlphaSpeed);

            if (text.alpha <= 0f)
            {
                Destroy(gameObject);
            }
        }
    }
}
