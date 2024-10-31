using UnityEngine;
using PitGL.TextureCode;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PitGL.IMGUI
{
    public abstract class IMGUI_Element
    {
        public IMGUI_SubMenu subMenu;
        public IMGUI_Menu menu => subMenu.menu;
        public int indent = 0;

        public Rect DrawContent(Rect menuRect)
        {
            menuRect.height = GetHeight();
            OnDrawElement(menuRect);
            menuRect.y += menuRect.height;
            return menuRect;
        }

        /// <summary>
        /// Draws the element in the IMGUI panel.
        /// </summary>
        /// <param name="elementRect"></param>
        /// <returns>A modified rect that can be used by inherited classes to adjust how they draw content.</returns>
        protected virtual Rect OnDrawElement(Rect elementRect)
        {
            float paddingRight = GetPaddingRight();
            float paddingLeft = GetPaddingLeft();
            paddingLeft += indent * IMGUI_Style.STANDARD_ELEMENT_INDENT;
            elementRect.width = elementRect.width - paddingRight - paddingLeft;
            elementRect.x += paddingLeft;
            return elementRect;
        }

        /// <summary>
        /// Draws a background color for the element. If avoidPadding is set to true,
        /// the padding es added again to the provided rect before drawing the background.
        /// </summary>
        /// <param name="elementRect"></param>
        /// <param name="color"></param>
        /// <param name="avoidPadding"></param>
        protected void DrawBackgroundColor(Rect elementRect, Color color, bool avoidPadding)
        {
            if(avoidPadding)
            {
                float leftPadding = GetPaddingLeft();
                float rightPadding = GetPaddingRight();
                elementRect.x -= leftPadding;
                elementRect.width += leftPadding + rightPadding;
            }
            GUI.Box(elementRect, "", IMGUI_Style.GetBoxStyle(color));
        }

        /// <summary>
        /// The height the element will have in the vertical layout of the submenu.
        /// </summary>
        /// <returns></returns>
        public virtual float GetHeight() => 24f;

        /// <summary>
        /// Override this function to specify a right padding to the entire element.
        /// </summary>
        /// <returns></returns>
        protected virtual float GetPaddingRight() => 0f;

        /// <summary>
        /// Override this function to specify a left padding to the entire element.
        /// </summary>
        /// <returns></returns>
        protected virtual float GetPaddingLeft() => 0f;

        /// <summary>
        /// When a submenu is opened, all of its elements become visible.
        /// </summary>
        public virtual void OnBecomeVisible() { }

        /// <summary>
        /// When a submenu is closed, all of its elements become invisible.
        /// </summary>
        public virtual void OnBecomeInvisible() { }
    }

    #region DECORATORS

    public class IMGUI_ElementSpace : IMGUI_Element
    {
        private float height;

        public IMGUI_ElementSpace(float height = -1f)
        {
            this.height = height < 0f ? base.GetHeight() : height;
        }

        public override float GetHeight() => height;
    }

    public class IMGUI_ElementTitle : IMGUI_Element
    {
        private string text;

        public IMGUI_ElementTitle(string text)
        {
            this.text = text;
        }

        public override float GetHeight() => 30f;

        protected override Rect OnDrawElement(Rect elementRect)
        {
            elementRect = base.OnDrawElement(elementRect);
            DrawBackgroundColor(elementRect, IMGUI_Style.ColorFromHex("#2e6bbf"), true);
            //GUI.Box(elementRect, "", IMGUI_Style.GetBoxStyle(IMGUI_Style.ColorFromHex("#2e6bbf")));
            GUI.Label(elementRect, text, IMGUI_Style.GetTextStyle(new(TextAnchor.MiddleCenter, FontStyle.Bold, Color.white, 16)));
            return elementRect;
        }
    }

    public class IMGUI_ElementHeader : IMGUI_Element
    {
        private string text;

        public IMGUI_ElementHeader(string text)
        {
            this.text = text;
        }

        protected override float GetPaddingLeft() => IMGUI_Style.STANDARD_ELEMENT_PADDING;
        protected override float GetPaddingRight() => IMGUI_Style.STANDARD_ELEMENT_PADDING;

        protected override Rect OnDrawElement(Rect elementRect)
        {
            elementRect = base.OnDrawElement(elementRect);
            //GUI.Box(elementRect, "", IMGUI_Style.GetBoxStyle(IMGUI_Style.ColorFromHex("#333c4a")));
            DrawBackgroundColor(elementRect, IMGUI_Style.ColorFromHex("#333c4a"), true);
            GUI.Label(elementRect, text, IMGUI_Style.GetTextStyle(new(TextAnchor.MiddleLeft, FontStyle.Bold, Color.white)));
            return elementRect;
        }
    }

    public class IMGUI_ElementFooter : IMGUI_Element
    {
        private string text;
        private bool writeInteractableCount;
        public int interactableCount;
        public int interactableIndex;

        public IMGUI_ElementFooter(string text = null, bool writeInteractableCount = true)
        {
            this.text = text;
            this.writeInteractableCount = writeInteractableCount;
        }
        public override float GetHeight() => 18f;
        protected override float GetPaddingLeft() => IMGUI_Style.STANDARD_ELEMENT_PADDING;
        protected override float GetPaddingRight() => IMGUI_Style.STANDARD_ELEMENT_PADDING;

        protected override Rect OnDrawElement(Rect elementRect)
        {
            elementRect = base.OnDrawElement(elementRect);

            //GUI.Box(elementRect, "", IMGUI_Style.GetBoxStyle(IMGUI_Style.ColorFromHex("#2e6bbf")));
            DrawBackgroundColor(elementRect, IMGUI_Style.ColorFromHex("#2e6bbf"), true);
            if (text != null)
                GUI.Label(elementRect, text, IMGUI_Style.GetTextStyle(new(TextAnchor.MiddleLeft, FontStyle.Bold, Color.white)));
            if(writeInteractableCount)
                GUI.Label(elementRect, $"{interactableIndex}/{interactableCount}", IMGUI_Style.GetTextStyle(new(TextAnchor.MiddleRight, FontStyle.Bold, Color.white)));


            return elementRect;
        }
    }

    #endregion

    #region INTERACTABLES

    public abstract class IMGUI_ElementInteractable : IMGUI_Element
    {
        private const float SELECTION_WIDTH = 24f;
        private const float ICON_WIDTH = 24f;

        protected bool isBeingInteracted;
        protected Color InteractableTextColor => !IsInteractable ? new Color(0.5f,0.5f,0.5f,1f) :
            isBeingInteracted ? new Color(0.8f,0.8f,0.8f,1f) : Color.white;

        public static HashSet<IMGUI_ElementInteractable> favourites = new HashSet<IMGUI_ElementInteractable>();
        private string uniqueId;
        private bool IsFavourite => PlayerPrefs.HasKey(uniqueId);
        public bool canBeMarkedAsFavourite = true;

        private Texture2D icon = null;
        private Func<bool> isInteractableFunc;
        public bool IsInteractable => isInteractableFunc == null || isInteractableFunc();

        public IMGUI_ElementInteractable(string uniqueId)
        {
            InitFavourite(uniqueId);
        }

        public override void OnBecomeVisible()
        {
            base.OnBecomeVisible();
            isBeingInteracted = false;
        }

        public override void OnBecomeInvisible()
        {
            base.OnBecomeInvisible();
            isBeingInteracted = false;
        }

        /// <summary>
        /// When the element was not selected and the left cursor is set to this element, it is selected.
        /// </summary>
        public virtual void OnSelected()
        {
            isBeingInteracted = false;
        }

        /// <summary>
        /// When the element was selected and the left cursor is set to a different element, it is unselected.
        /// </summary>
        public virtual void OnUnselected()
        {
            isBeingInteracted = false;
        }

        /// <summary>
        /// When the element is selected in the current submenu, it updates every frame.
        /// Use this function to manage interactions.
        /// </summary>
        public virtual void ProcessInputsWhileSelected()
        {
            CheckFavouriteInput();
        }

        protected override float GetPaddingRight() => IMGUI_Style.STANDARD_ELEMENT_PADDING;

        protected override Rect OnDrawElement(Rect elementRect)
        {
            elementRect = base.OnDrawElement(elementRect);

            if (!IsInteractable) isBeingInteracted = false;

            //SELECTION
            Rect selectionRect = elementRect;
            selectionRect.width = SELECTION_WIDTH;
            
            if(selectionRect.width > elementRect.height)
            {
                //This will prevent horizontal stretching of the selection texture by decreasing the width of the selection to fit the vertical size of the element
                selectionRect.width = elementRect.height;
            }

            if(selectionRect.width < elementRect.height)
            {
                //This will prevent vertical stretching of the selection texture by vertically centering the selection
                float diff = elementRect.height - selectionRect.width;
                selectionRect.height -= diff;
                selectionRect.y += (diff * 0.5f);
            }

            if (IsFavourite) DrawFavouriteMark(elementRect);
            if (subMenu.IsInteractableSelected(this))
            {
                GUI.Box(selectionRect, "", IMGUI_Style.GetBoxStyle(TextureCode_SelectionArrow.Tex));
            }

            elementRect.width -= selectionRect.width;
            elementRect.x += selectionRect.width;

            if(icon != null)
            {
                Rect iconRect = elementRect;
                iconRect.width = ICON_WIDTH > elementRect.height ? elementRect.height : ICON_WIDTH;
                GUI.Box(iconRect, "", IMGUI_Style.GetBoxStyle(icon));
                elementRect.width -= iconRect.width;
                elementRect.x += iconRect.width;
            }

            return elementRect;
        }

        public IMGUI_Element SetIcon(Texture2D icon)
        {
            this.icon = icon;
            return this;
        }

        public IMGUI_Element SetInteractableFunc(Func<bool> isInteractableFunc)
        {
            this.isInteractableFunc = isInteractableFunc;
            return this;
        }

        #region Favourites

        private void InitFavourite(string uniqueId)
        {
            uniqueId = $"IMGUI_ElementInteractable_{uniqueId}";
            this.uniqueId = uniqueId;
            if (IsFavourite)
            {
                if (canBeMarkedAsFavourite) favourites.Add(this);
                else PlayerPrefs.DeleteKey(uniqueId);
                
            }
        }

        private void CheckFavouriteInput()
        {
            if (!canBeMarkedAsFavourite) return;
            if (IMGUI_Input.MarkFavourite.WasPressed)
            {
                if (IsFavourite)
                {
                    favourites.Remove(this);
                    PlayerPrefs.DeleteKey(uniqueId);
                }
                else
                {
                    favourites.Add(this);
                    PlayerPrefs.SetInt(uniqueId, 1);
                }
            }
        }

        private void DrawFavouriteMark(Rect rect)
        {
            rect.width = 2f;
            GUI.Box(rect, "", IMGUI_Style.GetBoxStyle(IMGUI_Style.ColorFromHex("#d4af37")));
        }

        #endregion

    }

    public class IMGUI_ElementButton : IMGUI_ElementInteractable
    {
        private string text;
        protected Action action;

        public IMGUI_ElementButton(string text, Action action) : base(text)
        {
            this.text = text;
            this.action = action;
        }

        protected override Rect OnDrawElement(Rect elementRect)
        {
            elementRect = base.OnDrawElement(elementRect);
            GUI.Label(elementRect, text, IMGUI_Style.GetTextStyle(new(TextAnchor.MiddleLeft, FontStyle.Normal, InteractableTextColor)));
            return elementRect;
        }

        public override void ProcessInputsWhileSelected()
        {
            base.ProcessInputsWhileSelected();
            if (IMGUI_Input.Accept.WasPressed && !isBeingInteracted)
            {
                action?.Invoke();
                isBeingInteracted = true;
            }
            if (!IMGUI_Input.Accept.IsPressed) isBeingInteracted = false;
        }
    }

    public class IMGUI_ElementFolder<T> : IMGUI_ElementButton where T : IMGUI_SubMenu
    {
        public IMGUI_ElementFolder(string text, bool shouldResetSelection = true) : base(text, null)
        {
            this.action = () => menu.SetSubMenu<T>(shouldResetSelection);
            this.SetIcon(TextureCode_Folder.Tex);
        }
    }

    public class IMGUI_ElementToggle : IMGUI_ElementInteractable
    {
        private string text;
        private Func<bool> isToggled;
        private Action toggleOn;
        private Action toggleOff;
        private Action toggleOnOff;

        public IMGUI_ElementToggle(string text, Func<bool> isToggled, Action toggleOn, Action toggleOff) : base(text)
        {
            this.text = text;
            this.isToggled = isToggled;
            this.toggleOn = toggleOn;
            this.toggleOff = toggleOff;
            this.toggleOnOff = null;
        }

        public IMGUI_ElementToggle(string text, Func<bool> isToggled, Action toggleOnOff) : base(text)
        {
            this.text = text;
            this.isToggled = isToggled;
            this.toggleOn = null;
            this.toggleOff = null;
            this.toggleOnOff = toggleOnOff;
        }

        protected override Rect OnDrawElement(Rect elementRect)
        {
            elementRect = base.OnDrawElement(elementRect);
            GUI.Label(elementRect, text, IMGUI_Style.GetTextStyle(new(TextAnchor.MiddleLeft, FontStyle.Normal, InteractableTextColor)));
            GUI.Label(elementRect, isToggled() ? "ON" : "OFF", IMGUI_Style.GetTextStyle(new(TextAnchor.MiddleRight, FontStyle.Normal, InteractableTextColor)));
            return elementRect;
        }

        public override void ProcessInputsWhileSelected()
        {
            base.ProcessInputsWhileSelected();
            if(IMGUI_Input.Accept.WasPressed && !isBeingInteracted)
            {
                if(toggleOnOff == null) (isToggled() ? toggleOff : toggleOn).Invoke();
                else toggleOnOff?.Invoke();
                isBeingInteracted = true;
            }
            if (!IMGUI_Input.Accept.IsPressed) isBeingInteracted = false;
        }
    }

    public class IMGUI_ElementEnum<TEnum> : IMGUI_ElementInteractable where TEnum : Enum
    {
        private string text;
        private Type type;
        private List<TEnum> values;
        private Func<TEnum> get;
        private Action<TEnum> set;

        public IMGUI_ElementEnum(string text, Func<TEnum> get, Action<TEnum> set) : base(text)
        {
            this.text = text;
            this.type = typeof(TEnum);
            this.get = get;
            this.set = set;
            var arr = Enum.GetValues(type);
            values = new List<TEnum>(arr.Length);
            for (int i = 0; i < arr.Length; i++)
            {
                values.Add((TEnum)arr.GetValue(i));
            }
            values.Sort((a,b)=> Convert.ToInt32(a).CompareTo(Convert.ToInt32(b)));
        }

        protected override Rect OnDrawElement(Rect elementRect)
        {
            elementRect = base.OnDrawElement(elementRect);
            GUI.Label(elementRect, text, IMGUI_Style.GetTextStyle(new(TextAnchor.MiddleLeft, FontStyle.Normal, InteractableTextColor)));
            GUI.Label(elementRect, $"< {get().ToString()} >", IMGUI_Style.GetTextStyle(new(TextAnchor.MiddleRight, FontStyle.Normal, InteractableTextColor)));
            return elementRect;
        }

        public override void ProcessInputsWhileSelected()
        {
            base.ProcessInputsWhileSelected();
            TEnum current = get();
            if (!isBeingInteracted)
            {
                if (IMGUI_Input.Right.WasPressed)
                {
                    int index = values.IndexOf(current) + 1;
                    if (index >= values.Count) index = values.Count - 1;
                    set(values[index]);
                    isBeingInteracted = true;
                }
                else if (IMGUI_Input.Left.WasPressed)
                {
                    int index = values.IndexOf(current) - 1;
                    if (index < 0) index = 0;
                    set(values[index]);
                    isBeingInteracted = true;
                }
            }

            if (!IMGUI_Input.Left.IsPressed && !IMGUI_Input.Right.IsPressed)
                isBeingInteracted = false;
        }
    }

    public abstract class IMGUI_ElementSlider<T> : IMGUI_ElementInteractable
    {
        const float SLIDER_WIDTH = 60f;
        const float VALUE_TO_SLIDER_WIDTH = 8f;
        const float SLIDER_HEIGHT = 4f;
        const float HANDLE_SIZE = 16f;

        private string text;
        private T min;
        private T max;
        private T step;
        private Func<T> get;
        private Action<T> set;

        public IMGUI_ElementSlider(string text, Func<T> get, Action<T> set, T min, T max, T step) : base(text)
        {
            this.text = text;
            this.get = get;
            this.set = set;
            this.min = min;
            this.max = max;
            this.step = step;
        }

        protected override Rect OnDrawElement(Rect elementRect)
        {
            elementRect = base.OnDrawElement(elementRect);
            GUI.Label(elementRect, $"{text} [{ValueToString(min)}, {ValueToString(max)}]", IMGUI_Style.GetTextStyle(new(TextAnchor.MiddleLeft, FontStyle.Normal, InteractableTextColor)));

            Rect barRect = elementRect;
            barRect.width = SLIDER_WIDTH;
            barRect.x += (elementRect.width - SLIDER_WIDTH);
            barRect.y += (elementRect.height * 0.5f);
            barRect.y -= SLIDER_HEIGHT * 0.5f;
            barRect.height = SLIDER_HEIGHT;
            GUI.Box(barRect, "", IMGUI_Style.GetBoxStyle(Color.grey));
            float t = Mathf.Clamp01(GetNormalizedValue(get(), min, max));
            barRect.width = t * SLIDER_WIDTH;
            GUI.Box(barRect, "", IMGUI_Style.GetBoxStyle(Color.white));
            Rect handleRect = barRect;
            handleRect.x = barRect.x + t * SLIDER_WIDTH - HANDLE_SIZE * 0.5f;
            handleRect.y = barRect.y + barRect.height * 0.5f - HANDLE_SIZE * 0.5f;
            handleRect.height = HANDLE_SIZE;
            handleRect.width = HANDLE_SIZE;
            GUI.Box(handleRect, "", IMGUI_Style.GetBoxStyle(TextureCode_SliderHandle.Tex));

            Rect valueRect = elementRect;
            valueRect.width -= SLIDER_WIDTH;
            valueRect.width -= VALUE_TO_SLIDER_WIDTH;
            GUI.Label(valueRect, ValueToString(get()), IMGUI_Style.GetTextStyle(new(TextAnchor.MiddleRight, FontStyle.Normal, InteractableTextColor)));

            //string maxStr = ValueToString(max);
            //string minStr = ValueToString(min);
            //float  = labelStyle.CalcSize(new GUIContent(maxStr)).x;
            //GUI.Label(elementRect, text, labelStyle);



            //GUI.Label(elementRect, $"< {ValueToString(get())} >", IMGUI_Style.GetTextStyle(new(TextAnchor.MiddleRight, FontStyle.Normal, InteractableTextColor)));
            return elementRect;
        }

        public override void ProcessInputsWhileSelected()
        {
            base.ProcessInputsWhileSelected();
            T current = get();
            bool leftPressed = IMGUI_Input.Left.WasPressed;
            bool rightPressed = IMGUI_Input.Right.WasPressed;
            bool leftHold = IMGUI_Input.Left.IsHold;
            bool rightHold = IMGUI_Input.Right.IsHold;
            if (!isBeingInteracted || leftHold || rightHold)
            {
                if (rightPressed || rightHold)
                {
                    current = Add(current, step);
                    current = Clamp(current, min, max);
                    set(current);
                    isBeingInteracted = true;
                }
                else if (leftPressed || leftHold)
                {
                    current = Subtract(current, step);
                    current = Clamp(current, min, max);
                    set(current);
                    isBeingInteracted = true;
                }
            }

            if (!IMGUI_Input.Left.IsPressed && !IMGUI_Input.Right.IsPressed)
                isBeingInteracted = false;
        }

        protected abstract T Add(T value, T step);
        protected abstract T Subtract(T value, T step);
        protected abstract T Clamp(T value, T min, T max);
        protected abstract float GetNormalizedValue(T value, T min, T max);
        protected abstract string ValueToString(T value);
    }

    public class IMGUI_ElementSliderFloat : IMGUI_ElementSlider<float>
    {
        private string format;
        public IMGUI_ElementSliderFloat(string text, Func<float> get, Action<float> set, float min = 0f, float max = 1f, float step = 0.1f, int decimalCount = 2) 
            : base(text, get, set, min, max, step)
        {
            format = "F" + decimalCount;
        }

        protected override float Add(float value, float step) => value + step;
        protected override float Subtract(float value, float step) => value - step;
        protected override float Clamp(float value, float min, float max) => Mathf.Clamp(value, min, max);
        protected override float GetNormalizedValue(float value, float min, float max) => (value - min) / (max - min);
        protected override string ValueToString(float value) => value.ToString(format, CultureInfo.InvariantCulture);
    }

    public class IMGUI_ElementSliderInt : IMGUI_ElementSlider<int>
    {
        public IMGUI_ElementSliderInt(string text, Func<int> get, Action<int> set, int min = 0, int max = 1, int step = 1)
            : base(text, get, set, min, max, step)
        {

        }

        protected override int Add(int value, int step) => value + step;
        protected override int Subtract(int value, int step) => value - step;
        protected override int Clamp(int value, int min, int max) => Mathf.Clamp(value, min, max);
        protected override float GetNormalizedValue(int value, int min, int max) => ((float)(value - min)) / ((float)(max - min));
        protected override string ValueToString(int value) => value.ToString();
    }

    #endregion
}
