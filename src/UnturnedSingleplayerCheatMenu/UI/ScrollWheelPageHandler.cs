using UnityEngine;
using UnityEngine.EventSystems;
using System;

namespace UnturnedSingleplayerCheatMenu.UI
{
    /// <summary>
    /// Handles mouse scroll wheel input for page navigation.
    /// </summary>
    public class ScrollWheelPageHandler : MonoBehaviour, IScrollHandler
    {
        private int _currentPage;
        private int _totalPages;
        private Action _onScrollUp;
        private Action _onScrollDown;

        public void Initialize(int currentPage, int totalPages, Action onScrollUp, Action onScrollDown)
        {
            _currentPage = currentPage;
            _totalPages = totalPages;
            _onScrollUp = onScrollUp;
            _onScrollDown = onScrollDown;
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (_totalPages <= 1)
                return;

            float scrollDelta = eventData.scrollDelta.y;
            
            // Scroll up (positive delta) -> previous page
            if (scrollDelta > 0 && _currentPage > 0)
            {
                _onScrollUp?.Invoke();
            }
            // Scroll down (negative delta) -> next page
            else if (scrollDelta < 0 && _currentPage < _totalPages - 1)
            {
                _onScrollDown?.Invoke();
            }
        }
    }
}
