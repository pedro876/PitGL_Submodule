using UnityEngine;
using System.Collections.Generic;
using System;
using Architecture;

namespace PitGL.IMGUI
{
    public abstract class IMGUI_SubMenu
    {
        public IMGUI_Menu menu;
        private string title;
        private int selectionIndex = 0;
        private IMGUI_ElementTitle elementTitle;
        private IMGUI_ElementFooter elementFooter;
        private List<IMGUI_Element> elements;
        private List<IMGUI_ElementInteractable> interactables;
        private float scroll = 0f;
        private bool shouldRecalculateScroll;
        IMGUI_ElementInteractable selectedInteractable => selectionIndex >= 0 && interactables.Count > 0 ? interactables[selectionIndex] : null;

        #region INITIALIZATION
        public IMGUI_SubMenu(string title = null, string footer = null, bool footerWritesInteractableCount = true)
        {
            if(title == null)
            {
                title = GetType().Name;
                title = title.Substring(title.LastIndexOf('_')+1).ToUpper();
            }
            this.title = title;

            elementTitle = new IMGUI_ElementTitle(this.title) { subMenu = this };
            elementFooter = new IMGUI_ElementFooter(footer, footerWritesInteractableCount) { subMenu = this };
            CreateElements();
        }

        /// <summary>
        /// Creates the elements for the submenu from scratch.
        /// </summary>
        protected void CreateElements()
        {
            elements = new List<IMGUI_Element>();
            IntWrapper indent = 0;
            foreach (IMGUI_Element element in EnumerateElements(indent))
            {
                elements.Add(element);
                element.indent = indent.value;
            }

            interactables = new List<IMGUI_ElementInteractable>();
            foreach (var element in elements)
            {
                if (element is IMGUI_ElementInteractable interactable)
                    interactables.Add(interactable);
            }
        }

        /// <summary>
        /// Override this functions to create the elements for the specific submenu.
        /// If you call CreateElements, this will be called again.
        /// </summary>
        /// <returns></returns>
        protected abstract IEnumerable<IMGUI_Element> EnumerateElements(IntWrapper indent);

        //protected void IndentIncrease()
        //{

        //}

        //protected void IndentDecrease()
        //{

        //}
        #endregion

        #region GUI

        public abstract Type GetParentSubMenu();

        public Rect DrawContent(Rect rect)
        {
            float titleHeight = elementTitle.GetHeight();
            float footerHeight = elementFooter.GetHeight();
            Rect scrollRect = rect;
            scrollRect.y += titleHeight;
            scrollRect.height -= titleHeight;
            scrollRect.height -= footerHeight;

            if (shouldRecalculateScroll)
            {
                RecalculateScroll(scrollRect);
            }

            scrollRect.y -= scroll;

            for (int i = 0; i < elements.Count; i++)
            {
                scrollRect = elements[i].DrawContent(scrollRect);
            }

            elementTitle.DrawContent(rect);
            rect.y = rect.y + rect.height - footerHeight;
            elementFooter.interactableCount = interactables.Count;
            elementFooter.interactableIndex = selectionIndex + 1;
            elementFooter.DrawContent(rect);

            return scrollRect;
        }
        #endregion

        #region LOGIC

        public bool IsInteractableSelected(IMGUI_ElementInteractable interactable) => interactable == selectedInteractable;

        public void Enter(bool shouldResetSelection)
        {
            OnEnter();
            if(shouldResetSelection) selectionIndex = 0;
            elementTitle?.OnBecomeVisible();
            elementFooter?.OnBecomeVisible();
            for(int i = 0; i < elements.Count; i++)
            {
                elements[i].subMenu = this;
                elements[i].OnBecomeVisible();
            }
            selectedInteractable?.OnSelected();
        }

        public void Exit()
        {
            selectedInteractable?.OnUnselected();
            elementTitle?.OnBecomeInvisible();
            elementFooter?.OnBecomeInvisible();
            for (int i = 0; i < elements.Count; i++)
            {
                elements[i].OnBecomeInvisible();
            }
            OnExit();
        }

        public void Destroy()
        {
            OnDestroy();
        }

        protected virtual void OnEnter() { }
        protected virtual void OnExit() { }

        protected virtual void OnDestroy() { }

        public virtual void Update()
        {
            //SCROLL
            IMGUI_ElementInteractable lastInteractable = selectedInteractable;
            int dir = 0;
            if (IMGUI_Input.Down.WasPressed || IMGUI_Input.Down.IsHold)
            {
                dir++;
            }
            if (IMGUI_Input.Up.WasPressed || IMGUI_Input.Up.IsHold)
            {
                dir--;
            }
            if (IMGUI_Input.DownSkip.WasPressed || IMGUI_Input.DownSkip.IsHold) dir += 10;
            if (IMGUI_Input.UpSkip.WasPressed || IMGUI_Input.UpSkip.IsHold) dir -= 10;
            selectionIndex += dir;
            if (selectionIndex < 0) selectionIndex = 0;
            if (selectionIndex >= interactables.Count) selectionIndex = interactables.Count - 1;

            //INTERACTABLE UPDATE
            IMGUI_ElementInteractable newInteractable = selectedInteractable;
            if(lastInteractable != newInteractable)
            {
                lastInteractable?.OnUnselected();
                newInteractable?.OnSelected();
                shouldRecalculateScroll = true;
            }

            if(selectedInteractable != null && selectedInteractable.IsInteractable)
                selectedInteractable.ProcessInputsWhileSelected();
        }

        private void RecalculateScroll(Rect rect)
        {
            shouldRecalculateScroll = false;
            float height = 0f;
            for(int i = 0; i < elements.Count; i++)
            {
                height += elements[i].GetHeight();
                if (elements[i] == selectedInteractable)///is IMGUI_ElementInteractable interactable && IsInteractableSelected(interactable))
                {
                    break;
                }
            }

            float targetY = height;
            if(targetY - scroll > rect.height)
            {
                scroll = targetY - rect.height;
            }
            float selectedHeight = selectedInteractable.GetHeight();
            if(targetY - scroll - selectedHeight < 0)
            {
                scroll = targetY - selectedHeight;
            }
        }

        

        

        #endregion
    }
}
