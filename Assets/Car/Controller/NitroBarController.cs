using Car.Controller;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Car.UI
{
    public class NitroBarController : MonoBehaviour
    {
        [Header("UI refs")]
        [SerializeField] private Image            cooldownFill;
        [SerializeField] private TextMeshProUGUI timerText;

        [Header("Colors")]
        [SerializeField] private Color boostingColor = new Color(1f, 0.6f, 0f);
        [SerializeField] private Color cooldownColor = new Color(0.4f, 0.4f, 0.4f);
        [SerializeField] private Color readyColor    = new Color(1f, 0.9f, 0.2f);

        private NitroService _nitro;

        [Inject]
        public void Construct(NitroService nitro)
        {
            _nitro = nitro;
        }

        private void Update()
        {
            if (_nitro == null) return;

            if (_nitro.IsBoosting)
            {
                SetFill(1f, boostingColor);
                SetTimer(string.Empty);
            }
            else if (_nitro.IsOnCooldown)
            {
                SetFill(1f - _nitro.CooldownProgress, cooldownColor);
                SetTimer(_nitro.CooldownTimeLeft.ToString("0.0") + "s");
            }
            else
            {
                SetFill(1f, readyColor);
                SetTimer(string.Empty);
            }
        }

        private void SetFill(float amount, Color color)
        {
            if (cooldownFill == null) return;
            cooldownFill.fillAmount = amount;
            cooldownFill.color      = color;
        }

        private void SetTimer(string text)
        {
            if (timerText == null) return;
            timerText.text = text;
        }
    }
}