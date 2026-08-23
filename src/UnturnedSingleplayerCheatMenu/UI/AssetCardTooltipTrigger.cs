using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UnturnedSingleplayerCheatMenu.UI
{
    /// <summary>
    /// MonoBehaviour component that triggers tooltip display on pointer enter/exit.
    /// Attached to asset card buttons to show rich hover tooltips.
    /// Shows tooltip after 2-second hover delay as specified in requirements.
    /// </summary>
    internal class AssetCardTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private string _tooltipText;
        private Action<string, RectTransform, Vector2> _onShow;
        private Action _onHide;
        private bool _isHovering;
        private RectTransform _rectTransform;
        private Coroutine _delayCoroutine;
        private Vector2 _lastPointerPosition;

        private const float HoverDelaySeconds = 2f;

        public void Initialize(string tooltipText, Action<string, RectTransform, Vector2> onShow, Action onHide)
        {
            _tooltipText = tooltipText;
            _onShow = onShow;
            _onHide = onHide;
            _rectTransform = GetComponent<RectTransform>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (string.IsNullOrWhiteSpace(_tooltipText) || _onShow == null || _rectTransform == null)
                return;

            // Cancel any existing delay coroutine (from previous card)
            if (_delayCoroutine != null)
            {
                StopCoroutine(_delayCoroutine);
                _delayCoroutine = null;
            }

            _isHovering = true;
            _lastPointerPosition = eventData.position;
            _delayCoroutine = StartCoroutine(ShowTooltipAfterDelay());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Cancel delay and hide immediately on exit
            if (_delayCoroutine != null)
            {
                StopCoroutine(_delayCoroutine);
                _delayCoroutine = null;
            }

            if (_isHovering && _onHide != null)
            {
                _isHovering = false;
                _onHide();
            }
        }

        private IEnumerator ShowTooltipAfterDelay()
        {
            yield return new WaitForSeconds(HoverDelaySeconds);

            if (_isHovering && _onShow != null && _rectTransform != null)
            {
                _onShow(_tooltipText, _rectTransform, _lastPointerPosition);
            }

            _delayCoroutine = null;
        }

        private void OnDisable()
        {
            // Cancel delay and ensure tooltip is hidden when card is disabled/destroyed
            if (_delayCoroutine != null)
            {
                StopCoroutine(_delayCoroutine);
                _delayCoroutine = null;
            }

            if (_isHovering && _onHide != null)
            {
                _isHovering = false;
                _onHide();
            }
        }
    }
}
