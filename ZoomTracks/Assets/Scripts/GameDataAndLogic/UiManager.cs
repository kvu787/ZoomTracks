using System;
using TMPro;
using UnityEngine;

namespace ZoomTracks {
    public class UiManager {
        private TMP_Text PlaceholderLabel { get; }

        public UiManager() {
            this.PlaceholderLabel = GameObject.Find(nameof(this.PlaceholderLabel)).GetComponent<TMP_Text>();
        }

        public void UpdateUi() {
            this.PlaceholderLabel.text = $"{DateTime.Now:dd/MM/yyyy hh:mm:ss tt}";
        }
    }
}
